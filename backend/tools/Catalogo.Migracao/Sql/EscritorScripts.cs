using System.Globalization;
using System.Text;
using Catalogo.Domain.Historico;
using Catalogo.Domain.Metadados;
using Catalogo.Domain.Registros;
using Catalogo.Infrastructure.Persistencia;
using Catalogo.Migracao.Conversao;
using Catalogo.Migracao.Montagem;
using Catalogo.Migracao.Texto;

namespace Catalogo.Migracao.Sql;

/// <summary>A configuração da área no banco local (impressão digital), que o banco de destino precisa ter antes da parte 01.</summary>
public sealed record AreaEsperada(int Entidades, int Campos, int Restricoes, int RestricaoCampos, int Dependencias, string Impressao);

/// <summary>Um arquivo gerado.</summary>
public sealed record ArquivoGerado(string Nome, long Bytes, int Relatorios, int Itens);

/// <summary>
/// Escreve os scripts da migração para o SSMS ou o sqlcmd (SQL Server 2014): a parte 01 (tabela de controle, origem do histórico,
/// equipe, equipe habilitada na área e os cadastros de apoio), as partes seguintes (relatórios completos, com cenários e links, até
/// ~5 MB por arquivo) e a conferência (03). Cada parte numa transação, registrada em cat.CATTB024_MIGRACAO_CONTROLE; rodar de novo não
/// faz nada e rodar fora de ordem para. Os nomes das tabelas e colunas vêm do <see cref="NomesBanco"/>.
/// </summary>
public sealed class EscritorScripts(ResultadoMigracao resultado, Grupo grupo, OpcoesMigracao opcoes, AreaEsperada area, string leituraArea, string trechoImpressao)
{
    public const string CodigoMigracao = "sharepoint-testes-desempenho";
    public const string TabelaControle = "cat.CATTB024_MIGRACAO_CONTROLE";
    public const int LimiteBytes = 5_000_000;
    public const int LinhasPorInsert = 1000;
    public static readonly Guid Correlacao = FabricaRegistros.GuidDe("correlacao:migracao-sharepoint");

    private static readonly string Origem = HistoricoConversor.ParaTexto(OrigemHistorico.MigracaoSharePoint);

    public IReadOnlyList<ArquivoGerado> Gerar(string pasta)
    {
        Directory.CreateDirectory(pasta);
        var blocos = resultado.Relatorios.Where(r => r.Relatorio is not null).Select(r => (Relatorio: r, Linhas: new LinhasSql(r.Itens.ToList()))).ToList();

        // Partes 02 em diante: relatórios completos, até ~5 MB (o tamanho estimado pelas linhas de INSERT).
        var partes = new List<List<(RelatorioMigrado Relatorio, LinhasSql Linhas)>>();
        var atual = new List<(RelatorioMigrado, LinhasSql)>();
        long tamanho = 0;
        foreach (var bloco in blocos)
        {
            var bytes = bloco.Linhas.Bytes;
            if (atual.Count > 0 && tamanho + bytes > LimiteBytes - 200_000)
            {
                partes.Add(atual);
                atual = [];
                tamanho = 0;
            }

            atual.Add(bloco);
            tamanho += bytes;
        }

        if (atual.Count > 0)
            partes.Add(atual);

        var total = partes.Count + 1;
        var arquivos = new List<ArquivoGerado>();
        var apoio = new LinhasSql(resultado.Apoio);
        arquivos.Add(Escrever(pasta, "02_migracao_parte_01.sql", ParteUm(total, apoio), 0, resultado.Apoio.Count));
        for (var i = 0; i < partes.Count; i++)
        {
            var numero = i + 2;
            var linhas = LinhasSql.Juntar(partes[i].Select(p => p.Linhas));
            var nome = $"02_migracao_parte_{numero:D2}.sql";
            arquivos.Add(Escrever(pasta, nome, ParteRelatorios(numero, total, nome, partes[i].Select(p => p.Relatorio).ToList(), linhas), partes[i].Count, linhas.Itens));
        }

        arquivos.Add(Escrever(pasta, "03_conferencia.sql", Conferencia(total), 0, 0));
        return arquivos;
    }

    private static ArquivoGerado Escrever(string pasta, string nome, string conteudo, int relatorios, int itens)
    {
        var caminho = Path.Combine(pasta, nome);
        var normalizado = conteudo.Replace("\r\n", "\n");
        File.WriteAllText(caminho, normalizado, new UTF8Encoding(true));
        return new ArquivoGerado(nome, new FileInfo(caminho).Length, relatorios, itens);
    }

    // ------------------------------------------------------------------ Parte 01

    private string ParteUm(int totalPartes, LinhasSql apoio)
    {
        var s = new StringBuilder();
        var prefixo = $"parte 01/{totalPartes:D2}";
        Cabecalho(s, 1, totalPartes,
            $"""
               Cria a tabela de controle da migração ({TabelaControle}), passa a aceitar a origem MIGRACAO_SHAREPOINT no histórico,
               cria a equipe {opcoes.NomeEquipe}, a habilita na área testes_desempenho e cria os cadastros de apoio e os itens que os
               relatórios referenciam: {Contagem(resultado.Apoio)}.
               Pré-requisitos: o 01_reconfigurar_area_testes_desempenho.sql já executado (a configuração da área é conferida pela impressão
               digital), a área sem nenhum item e o usuário {opcoes.MatriculaAutor} cadastrado.
            """);

        s.AppendLine($"PRINT '{prefixo}: verificando pré-requisitos...';");
        PrerequisitosComuns(s, prefixo, 1);
        s.AppendLine($"""
            ELSE IF @ParteAplicada = 1
            BEGIN
                PRINT '{prefixo}: já aplicada. Nada a fazer.';
                SET NOEXEC ON;
            END
            ELSE IF EXISTS (SELECT 1 FROM {T("Equipe")} WHERE {C("Equipe", "Nome")} = {LiteralSql.Texto(opcoes.NomeEquipe)} AND {C("Equipe", "Id")} <> {LiteralSql.Guid(resultado.EquipeId)})
            BEGIN
                RAISERROR (N'{prefixo}: já existe uma equipe {opcoes.NomeEquipe} criada fora da migração. Os itens precisam da equipe com o identificador {resultado.EquipeId:D}: fale com a equipe do Catálogo. Nada foi alterado.', 16, 1);
                SET NOEXEC ON;
            END
            ELSE IF EXISTS (SELECT 1 FROM {T("Registro")} r JOIN {T("Entidade")} e ON e.{C("Entidade", "Id")} = r.{C("Registro", "EntidadeId")}
                            WHERE e.{C("Entidade", "GrupoId")} = {LiteralSql.Guid(grupo.Id)})
            BEGIN
                RAISERROR (N'{prefixo}: a área testes_desempenho já tem itens. A migração só roda com a área vazia. Nada foi alterado.', 16, 1);
                SET NOEXEC ON;
            END;
            GO

            PRINT '{prefixo}: conferindo a configuração da área (impressão digital)...';
            DECLARE @GrupoChave varchar(100) = 'testes_desempenho';
            {leituraArea}
            {trechoImpressao}
            IF (SELECT COUNT(*) FROM @Canonico WHERE Secao = 2) <> {area.Entidades}
               OR (SELECT COUNT(*) FROM @Canonico WHERE Secao = 3) <> {area.Campos}
               OR (SELECT COUNT(*) FROM @Canonico WHERE Secao = 4) <> {area.Restricoes}
               OR (SELECT COUNT(*) FROM @Canonico WHERE Secao = 5) <> {area.RestricaoCampos}
               OR (SELECT COUNT(*) FROM @Canonico WHERE Secao = 6) <> {area.Dependencias}
               OR @Impressao <> {area.Impressao}
            BEGIN
                RAISERROR (N'{prefixo}: a configuração da área testes_desempenho não é a esperada. Execute antes o 01_reconfigurar_area_testes_desempenho.sql. Nada foi alterado.', 16, 1);
                SET NOEXEC ON;
            END;
            GO

            PRINT '{prefixo}: tabela de controle e origem do histórico...';
            IF OBJECT_ID(N'{TabelaControle}', N'U') IS NULL
            BEGIN
                CREATE TABLE {TabelaControle} (
                    NU_NSU_MIGRACAO_CONTROLE uniqueidentifier NOT NULL CONSTRAINT DF_MigracaoControle_Id DEFAULT NEWID(),
                    CO_MIGRACAO              varchar(50)      NOT NULL,
                    NU_PARTE                 int              NOT NULL,
                    NU_TOTAL_PARTES_MC       int              NOT NULL,
                    DE_ARQUIVO_MC            nvarchar(260)    COLLATE Latin1_General_CI_AI NOT NULL,
                    NU_ITENS_MC              int              NOT NULL,
                    NU_CORRELACAO_MC         uniqueidentifier NOT NULL,
                    DH_APLICADO_MC           datetime2        NOT NULL CONSTRAINT DF_MigracaoControle_AplicadoEm DEFAULT SYSUTCDATETIME(),
                    CONSTRAINT PK_MigracaoControle PRIMARY KEY (NU_NSU_MIGRACAO_CONTROLE),
                    CONSTRAINT UQ_MigracaoControle_Parte UNIQUE (CO_MIGRACAO, NU_PARTE)
                );
                PRINT '{prefixo}: tabela {TabelaControle} criada.';
            END;
            IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_HistoricoAlteracao_Origem'
                           AND parent_object_id = OBJECT_ID(N'{T("HistoricoAlteracao")}') AND definition LIKE N'%{Origem}%')
            BEGIN
                IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_HistoricoAlteracao_Origem' AND parent_object_id = OBJECT_ID(N'{T("HistoricoAlteracao")}'))
                    ALTER TABLE {T("HistoricoAlteracao")} DROP CONSTRAINT CK_HistoricoAlteracao_Origem;
                ALTER TABLE {T("HistoricoAlteracao")} WITH CHECK ADD CONSTRAINT CK_HistoricoAlteracao_Origem
                    CHECK ({C("HistoricoAlteracao", "Origem")} IS NULL OR {C("HistoricoAlteracao", "Origem")} IN ('IMPORTACAO', '{Origem}'));
                PRINT '{prefixo}: o histórico passa a aceitar a origem {Origem} (como o V017 do projeto).';
            END;
            GO

            BEGIN TRANSACTION;
            """);
        DeclararVariaveis(s);
        var equipe = LiteralSql.Guid(resultado.EquipeId);
        var grupoId = LiteralSql.Guid(grupo.Id);
        var criadaEm = LiteralSql.DataHora(resultado.EquipeCriadaEm);
        s.AppendLine($"""

            PRINT '{prefixo}: 1/3 equipe {opcoes.NomeEquipe} e a área testes_desempenho habilitada para ela...';
            IF NOT EXISTS (SELECT 1 FROM {T("Equipe")} WHERE {C("Equipe", "Id")} = {equipe})
            BEGIN
                INSERT {T("Equipe")} ({C("Equipe", "Id")}, {C("Equipe", "Nome")}, {C("Equipe", "Ativa")}, {C("Equipe", "CriadoEm")})
                VALUES ({equipe}, {LiteralSql.Texto(opcoes.NomeEquipe)}, 1, {criadaEm});
                INSERT {T("HistoricoAlteracao")} ({Colunas("HistoricoAlteracao", ColunasHistorico)})
                VALUES ('{HistoricoConversor.ParaTexto(AlvoHistorico.Equipe)}', {equipe}, NULL, {equipe}, '{HistoricoConversor.ParaTexto(OperacaoHistorico.Criacao)}', @Usuario, @Matricula,
                        {criadaEm}, @Correlacao, NULL, {LiteralSql.Texto($"{{\"nome\":\"{opcoes.NomeEquipe}\",\"ativa\":true}}")}, NULL, '{Origem}');
            END;

            IF NOT EXISTS (SELECT 1 FROM {T("GrupoEquipe")} WHERE {C("GrupoEquipe", "GrupoId")} = {grupoId} AND {C("GrupoEquipe", "EquipeId")} = {equipe})
            BEGIN
                DECLARE @EquipesAntes nvarchar(max) = {JsonEquipes(grupoId)};
                INSERT {T("GrupoEquipe")} ({C("GrupoEquipe", "GrupoId")}, {C("GrupoEquipe", "EquipeId")}, {C("GrupoEquipe", "CriadoPorId")}, {C("GrupoEquipe", "CriadoEm")})
                VALUES ({grupoId}, {equipe}, @Usuario, {criadaEm});
                -- Histórico de metadados (RN52), no formato da tela "Equipes habilitadas": as equipes antes e depois, pelo nome.
                INSERT {T("HistoricoAlteracao")} ({Colunas("HistoricoAlteracao", ColunasHistorico)})
                VALUES ('{HistoricoConversor.ParaTexto(AlvoHistorico.Metadado)}', {grupoId}, NULL, NULL, '{HistoricoConversor.ParaTexto(OperacaoHistorico.Alteracao)}', @Usuario, @Matricula,
                        {criadaEm}, @Correlacao, @EquipesAntes, {JsonEquipes(grupoId)}, NULL, '{Origem}');
            END;

            PRINT '{prefixo}: 2/3 cadastros de apoio, projetos, endpoints e RTCs ({resultado.Apoio.Count} itens)...';
            """);
        apoio.Escrever(s);
        s.AppendLine($"\nPRINT '{prefixo}: 3/3 registrando a parte...';");
        Registrar(s, 1, totalPartes, "02_migracao_parte_01.sql", resultado.Apoio.Count);
        Fim(s, prefixo, $"concluída ({resultado.Apoio.Count} itens de apoio; equipe {opcoes.NomeEquipe} habilitada)");
        return s.ToString();
    }

    // ------------------------------------------------------------------ Partes de relatórios

    private string ParteRelatorios(int numero, int totalPartes, string nome, IReadOnlyList<RelatorioMigrado> relatorios, LinhasSql linhas)
    {
        var s = new StringBuilder();
        var prefixo = $"parte {numero:D2}/{totalPartes:D2}";
        var de = relatorios.Min(r => r.DataUtc);
        var ate = relatorios.Max(r => r.DataUtc);
        Cabecalho(s, numero, totalPartes,
            $"""
               Cria {relatorios.Count} relatório(s) completos, com os seus cenários e links: {Contagem(relatorios.SelectMany(r => r.Itens))}.
               Relatórios executados de {de:dd/MM/yyyy} a {ate:dd/MM/yyyy} (linhas do CSV: {string.Join(", ", Faixas(relatorios.Select(r => r.Linha.Numero)))}).
               Pré-requisito: a parte {numero - 1:D2} já aplicada.
            """);
        s.AppendLine($"PRINT '{prefixo}: verificando pré-requisitos...';");
        PrerequisitosComuns(s, prefixo, numero);
        s.AppendLine($"""
            ELSE IF @ControleExiste = 0
            BEGIN
                RAISERROR (N'{prefixo}: a parte 01 ainda não foi aplicada (não existe {TabelaControle}). Rode as partes em ordem. Nada foi alterado.', 16, 1);
                SET NOEXEC ON;
            END
            ELSE IF @ParteAplicada = 1
            BEGIN
                PRINT '{prefixo}: já aplicada. Nada a fazer.';
                SET NOEXEC ON;
            END
            ELSE IF @AnteriorAplicada = 0
            BEGIN
                RAISERROR (N'{prefixo}: fora de ordem: a parte {numero - 1:D2} ainda não foi aplicada. Rode as partes em ordem. Nada foi alterado.', 16, 1);
                SET NOEXEC ON;
            END;
            GO

            BEGIN TRANSACTION;
            """);
        DeclararVariaveis(s);
        s.AppendLine($"\nPRINT '{prefixo}: 1/2 {relatorios.Count} relatório(s), {relatorios.Sum(r => r.Cenarios.Count)} cenário(s) e {relatorios.Sum(r => r.Links.Count)} link(s)...';");
        linhas.Escrever(s);
        s.AppendLine($"\nPRINT '{prefixo}: 2/2 registrando a parte...';");
        Registrar(s, numero, totalPartes, nome, linhas.Itens);
        Fim(s, prefixo, $"concluída ({linhas.Itens} itens)");
        return s.ToString();
    }

    private void Cabecalho(StringBuilder s, int numero, int total, string descricao)
    {
        s.AppendLine($"""
            /* =====================================================================
               CATÁLOGO — Migração dos relatórios de testes de desempenho (SharePoint) — parte {numero:D2} de {total:D2}
               ---------------------------------------------------------------------
            {descricao}

               GERADO por backend/tools/Catalogo.Migracao em {DateTime.Now:yyyy-MM-dd HH:mm}, a partir do CSV exportado do SharePoint e da
               configuração da área no banco local, com o código da aplicação (validadores, Dados, valores tipados, referências, chaves
               únicas e histórico). Não edite à mão: gere de novo (docs/migracao-sharepoint.md).
               Autor de todos os itens: o usuário de matrícula {opcoes.MatriculaAutor} (procurado no banco; o script para se ele não existir).
               Histórico: uma linha de criação por item, origem {Origem}, correlação {Correlacao:D}.

               Execução: SSMS, ou sqlcmd -i (recomendado se o SSMS travar: sqlcmd -S <servidor> -d <banco> -E -i <arquivo> -f 65001).
               Sem variáveis de SQLCMD e sem USE. Compatível com SQL Server 2014 (nível 120).
               Uma transação só: em caso de erro, nada desta parte fica gravado. Ao terminar, a parte é registrada em {TabelaControle}:
               rodar de novo não faz nada; rodar fora de ordem para com uma mensagem.
               ===================================================================== */

            SET NOCOUNT ON;
            SET XACT_ABORT ON;
            SET ANSI_NULLS ON;
            SET QUOTED_IDENTIFIER ON;
            SET ANSI_PADDING ON;
            SET ANSI_WARNINGS ON;
            SET CONCAT_NULL_YIELDS_NULL ON;
            GO

            """);
    }

    /// <summary>
    /// O começo da verificação de uma parte: a situação no controle vai para variáveis por SQL dinâmico, porque a tabela de controle
    /// pode ainda não existir (o SQL Server não garante a ordem de avaliação de "OBJECT_ID(...) IS NOT NULL AND EXISTS (...)": a
    /// referência à tabela inexistente abortaria o lote, e o sqlcmd seguiria para o próximo sem as verificações).
    /// </summary>
    private void PrerequisitosComuns(StringBuilder s, string prefixo, int numero) =>
        s.AppendLine($"""
            DECLARE @ControleExiste bit = CASE WHEN OBJECT_ID(N'{TabelaControle}', N'U') IS NULL THEN 0 ELSE 1 END;
            DECLARE @ParteAplicada bit = 0, @AnteriorAplicada bit = 0;
            IF @ControleExiste = 1
                EXEC sys.sp_executesql
                    N'SELECT @aplicada = CASE WHEN EXISTS (SELECT 1 FROM {TabelaControle} WHERE CO_MIGRACAO = ''{CodigoMigracao}'' AND NU_PARTE = @parte) THEN 1 ELSE 0 END,
                             @anterior = CASE WHEN EXISTS (SELECT 1 FROM {TabelaControle} WHERE CO_MIGRACAO = ''{CodigoMigracao}'' AND NU_PARTE = @parte - 1) THEN 1 ELSE 0 END;',
                    N'@parte int, @aplicada bit OUTPUT, @anterior bit OUTPUT', @parte = {numero}, @aplicada = @ParteAplicada OUTPUT, @anterior = @AnteriorAplicada OUTPUT;

            IF OBJECT_ID(N'{T("Registro")}', N'U') IS NULL OR OBJECT_ID(N'cat.CATTB023_VERSAO_SCHEMA', N'U') IS NULL
            BEGIN
                RAISERROR (N'{prefixo}: a estrutura do Catálogo não está instalada no schema cat. Nada foi alterado.', 16, 1);
                SET NOEXEC ON;
            END
            ELSE IF NOT EXISTS (SELECT 1 FROM {T("Usuario")} WHERE {C("Usuario", "Matricula")} = {LiteralSql.Texto(opcoes.MatriculaAutor)})
            BEGIN
                RAISERROR (N'{prefixo}: o usuário {opcoes.MatriculaAutor} (autor da migração) não existe neste banco. Nada foi alterado.', 16, 1);
                SET NOEXEC ON;
            END
            ELSE IF NOT EXISTS (SELECT 1 FROM {T("Grupo")} WHERE {C("Grupo", "Id")} = {LiteralSql.Guid(grupo.Id)} AND {C("Grupo", "Chave")} = 'testes_desempenho')
            BEGIN
                RAISERROR (N'{prefixo}: a área testes_desempenho (identificador {grupo.Id:D}) não existe neste banco. Nada foi alterado.', 16, 1);
                SET NOEXEC ON;
            END
            """);

    private void DeclararVariaveis(StringBuilder s) =>
        s.AppendLine($"""
            DECLARE @Usuario uniqueidentifier = (SELECT {C("Usuario", "Id")} FROM {T("Usuario")} WHERE {C("Usuario", "Matricula")} = {LiteralSql.Texto(opcoes.MatriculaAutor)});
            DECLARE @Matricula varchar(20) = (SELECT {C("Usuario", "Matricula")} FROM {T("Usuario")} WHERE {C("Usuario", "Id")} = @Usuario);
            DECLARE @Correlacao uniqueidentifier = {LiteralSql.Guid(Correlacao)};
            """);

    private static void Registrar(StringBuilder s, int numero, int total, string arquivo, int itens) =>
        s.AppendLine($"""
            INSERT {TabelaControle} (CO_MIGRACAO, NU_PARTE, NU_TOTAL_PARTES_MC, DE_ARQUIVO_MC, NU_ITENS_MC, NU_CORRELACAO_MC)
            VALUES ('{CodigoMigracao}', {numero}, {total}, N'{arquivo}', {itens}, @Correlacao);
            """);

    private static void Fim(StringBuilder s, string prefixo, string mensagem) =>
        s.AppendLine($"""

            COMMIT TRANSACTION;
            PRINT '{prefixo}: {mensagem}.';
            GO

            SET NOEXEC OFF;
            GO
            """);

    /// <summary>JSON das equipes habilitadas na área, no formato de ParaHistoricoEquipes (nomes por ordem alfabética e os ids).</summary>
    private string JsonEquipes(string grupoId)
    {
        var de = $"FROM {T("GrupoEquipe")} ge JOIN {T("Equipe")} e ON e.{C("Equipe", "Id")} = ge.{C("GrupoEquipe", "EquipeId")} WHERE ge.{C("GrupoEquipe", "GrupoId")} = {grupoId} ORDER BY e.{C("Equipe", "Nome")}";
        var nome = $"REPLACE(REPLACE(e.{C("Equipe", "Nome")}, N'\\', N'\\\\'), N'\"', N'\\\"')";
        return $"N'{{\"alvo\":\"equipes\",\"chave\":\"testes_desempenho\",\"equipes\":\"' + ISNULL(STUFF((SELECT N', ' + {nome} {de} FOR XML PATH(''), TYPE).value('.', 'nvarchar(max)'), 1, 2, N''), N'')"
            + $" + N'\",\"equipeIds\":[' + ISNULL(STUFF((SELECT N',\"' + LOWER(CONVERT(nvarchar(36), e.{C("Equipe", "Id")})) + N'\"' {de} FOR XML PATH(''), TYPE).value('.', 'nvarchar(max)'), 1, 1, N''), N'') + N']}}'";
    }

    // ------------------------------------------------------------------ Conferência

    private string Conferencia(int totalPartes)
    {
        var migrados = resultado.Relatorios.Where(r => r.Relatorio is not null).ToList();
        var esperados = resultado.Apoio.Concat(migrados.SelectMany(r => r.Itens))
            .GroupBy(i => i.Entidade.Chave).ToDictionary(g => g.Key, g => g.Count());
        var relatorio = grupo.ObterEntidade("relatorio_teste")!;
        var campoStatus = relatorio.ObterCampo("status_resultado")!;
        var campoAmbiente = relatorio.ObterCampo("ambiente")!;
        var campoData = relatorio.ObterCampo("data_execucao")!;
        var descricaoStatus = grupo.ObterEntidade("status_resultado")!.ObterCampo("descricao")!;
        var nomeAmbiente = grupo.ObterEntidade("ambiente")!.ObterCampo("nome")!;
        var linhas = resultado.Relatorios.Select(r => r.Linha).ToList();

        var s = new StringBuilder();
        s.AppendLine($"""
            /* =====================================================================
               CATÁLOGO — Migração dos relatórios de testes de desempenho (SharePoint) — 03: conferência
               ---------------------------------------------------------------------
               SÓ CONSULTAS: não altera nada. Compara o banco com o CSV do SharePoint ({linhas.Count} relatórios):
                 1. as {totalPartes} partes registradas em {TabelaControle};
                 2. a quantidade de itens por tipo de item da área testes_desempenho;
                 3. os relatórios por resultado, por ambiente e por ano de execução.
               Cada linha mostra "OK" ou "DIVERGÊNCIA". A última consulta resume: a quantidade de divergências (o esperado é 0).
               GERADO por backend/tools/Catalogo.Migracao em {DateTime.Now:yyyy-MM-dd HH:mm}. Não edite à mão.
               ===================================================================== */

            SET NOCOUNT ON;
            DECLARE @Conferencia TABLE (Ordem int IDENTITY(1,1), Grupo nvarchar(60), Item nvarchar(200), Esperado int, Encontrado int);
            DECLARE @Grupo uniqueidentifier = {LiteralSql.Guid(grupo.Id)};

            INSERT @Conferencia (Grupo, Item, Esperado, Encontrado)
            SELECT N'Partes aplicadas', N'partes de 01 a {totalPartes:D2}', {totalPartes},
                   CASE WHEN OBJECT_ID(N'{TabelaControle}', N'U') IS NULL THEN 0
                        ELSE (SELECT COUNT(*) FROM {TabelaControle} WHERE CO_MIGRACAO = '{CodigoMigracao}' AND NU_PARTE BETWEEN 1 AND {totalPartes}) END;

            INSERT @Conferencia (Grupo, Item, Esperado, Encontrado)
            SELECT N'Itens por tipo', e.Nome, e.Esperado,
                   (SELECT COUNT(*) FROM {T("Registro")} r WHERE r.{C("Registro", "EntidadeId")} = e.Id)
            FROM (VALUES
            """);
        var tipos = grupo.Entidades.OrderBy(e => e.Ordem).ToList();
        s.AppendLine(string.Join(",\n", tipos.Select(e => $"    ({LiteralSql.Guid(e.Id)}, {LiteralSql.Texto(e.NomePlural ?? e.Nome)}, {esperados.GetValueOrDefault(e.Chave)})")));
        s.AppendLine(") e (Id, Nome, Esperado);\n");

        void PorDimensao(string titulo, IEnumerable<(string Valor, int Quantidade)> doCsv, string consultaValor)
        {
            s.AppendLine($"""
                INSERT @Conferencia (Grupo, Item, Esperado, Encontrado)
                SELECT N'{titulo}', v.Valor, v.Esperado, ISNULL(b.Encontrado, 0)
                FROM (VALUES
                """);
            s.AppendLine(string.Join(",\n", doCsv.Select(d => $"    ({LiteralSql.Texto(d.Valor)}, {d.Quantidade})")));
            s.AppendLine($"""
                ) v (Valor, Esperado)
                LEFT JOIN (
                    SELECT x.Valor, COUNT(*) AS Encontrado
                    FROM ({consultaValor}) x
                    GROUP BY x.Valor
                ) b ON b.Valor = v.Valor;

                """);
        }

        PorDimensao("Relatórios por resultado",
            linhas.GroupBy(l => LimpezaTexto.NormalizarLinha(l.Resultado)).OrderBy(g => g.Key, StringComparer.Ordinal).Select(g => (g.Key, g.Count())),
            ConsultaPorReferencia(relatorio, campoStatus, descricaoStatus));
        PorDimensao("Relatórios por ambiente",
            linhas.GroupBy(l => RegrasConversao.NomeAmbiente(l.Ambiente)).OrderBy(g => g.Key, StringComparer.Ordinal).Select(g => (g.Key, g.Count())),
            ConsultaPorReferencia(relatorio, campoAmbiente, nomeAmbiente));
        PorDimensao("Relatórios por ano de execução",
            linhas.GroupBy(l => l.DataExecucao.Trim().Substring(6, 4)).OrderBy(g => g.Key, StringComparer.Ordinal).Select(g => (g.Key, g.Count())),
            $"SELECT CONVERT(nvarchar(4), YEAR(DATEADD(HOUR, -3, v.{C("RegistroValor", "ValorData")}))) AS Valor FROM {T("Registro")} r " +
            $"JOIN {T("RegistroValor")} v ON v.{C("RegistroValor", "RegistroId")} = r.{C("Registro", "Id")} AND v.{C("RegistroValor", "CampoId")} = {LiteralSql.Guid(campoData.Id)} " +
            $"WHERE r.{C("Registro", "EntidadeId")} = {LiteralSql.Guid(relatorio.Id)}");

        s.AppendLine($"""
            SELECT Grupo, Item, Esperado, Encontrado, CASE WHEN Esperado = Encontrado THEN 'OK' ELSE 'DIVERGÊNCIA' END AS Situacao
            FROM @Conferencia ORDER BY Ordem;

            SELECT COUNT(*) AS Verificacoes, SUM(CASE WHEN Esperado = Encontrado THEN 0 ELSE 1 END) AS Divergencias,
                   CASE WHEN SUM(CASE WHEN Esperado = Encontrado THEN 0 ELSE 1 END) = 0 THEN 'OK: o banco confere com o CSV.' ELSE 'DIVERGÊNCIA: veja as linhas acima.' END AS Resultado
            FROM @Conferencia;
            """);
        if (migrados.Count < linhas.Count)
            s.AppendLine($"-- Atenção: {linhas.Count - migrados.Count} relatório(s) do CSV não foram migrados (ver anomalias.md): as contagens do CSV os incluem.");
        return s.ToString();
    }

    private static string ConsultaPorReferencia(Entidade relatorio, Campo referencia, Campo nome) =>
        $"SELECT v.{C("RegistroValor", "ValorTexto")} AS Valor FROM {T("Registro")} r " +
        $"JOIN {T("RegistroReferencia")} rr ON rr.{C("RegistroReferencia", "RegistroOrigemId")} = r.{C("Registro", "Id")} AND rr.{C("RegistroReferencia", "CampoId")} = {LiteralSql.Guid(referencia.Id)} " +
        $"JOIN {T("RegistroValor")} v ON v.{C("RegistroValor", "RegistroId")} = rr.{C("RegistroReferencia", "RegistroDestinoId")} AND v.{C("RegistroValor", "CampoId")} = {LiteralSql.Guid(nome.Id)} " +
        $"WHERE r.{C("Registro", "EntidadeId")} = {LiteralSql.Guid(relatorio.Id)}";

    // ------------------------------------------------------------------ Apoio

    internal static readonly string[] ColunasHistorico =
        ["AlvoTipo", "AlvoId", "EntidadeId", "EquipeId", "Operacao", "UsuarioId", "UsuarioMatricula", "OcorridoEm", "CorrelacaoId", "DadosAntes", "DadosDepois", "Justificativa", "Origem"];

    internal static string T(string tabelaLogica) => SchemaBanco.Nome + "." + NomesBanco.Tabela(tabelaLogica);

    internal static string C(string tabelaLogica, string colunaLogica) => NomesBanco.Coluna(tabelaLogica, colunaLogica);

    internal static string Colunas(string tabelaLogica, IEnumerable<string> colunas) => string.Join(", ", colunas.Select(c => C(tabelaLogica, c)));

    private static string Contagem(IEnumerable<ItemMigrado> itens) =>
        string.Join(", ", itens.GroupBy(i => i.Entidade.NomePlural ?? i.Entidade.Nome).Select(g => $"{g.Count()} {g.Key.ToLower(CultureInfo.GetCultureInfo("pt-BR"))}"));

    private static IEnumerable<string> Faixas(IEnumerable<int> numeros)
    {
        var ordenados = numeros.Order().ToList();
        for (var i = 0; i < ordenados.Count;)
        {
            var j = i;
            while (j + 1 < ordenados.Count && ordenados[j + 1] == ordenados[j] + 1)
                j++;
            yield return i == j ? $"{ordenados[i]}" : $"{ordenados[i]}–{ordenados[j]}";
            i = j + 1;
        }
    }
}

/// <summary>
/// As linhas de INSERT de um conjunto de itens, por tabela, na ordem das FKs: registros, referências, chaves únicas, valores e
/// histórico. Escreve no máximo <see cref="EscritorScripts.LinhasPorInsert"/> linhas por comando.
/// </summary>
internal sealed class LinhasSql
{
    private readonly List<string> _registros = [];
    private readonly List<string> _referencias = [];
    private readonly List<string> _chaves = [];
    private readonly List<string> _valores = [];
    private readonly List<string> _historico = [];

    public LinhasSql(IReadOnlyList<ItemMigrado> itens)
    {
        Itens = itens.Count;
        foreach (var item in itens)
            Adicionar(item);
    }

    private LinhasSql()
    {
    }

    public int Itens { get; private set; }

    public long Bytes => new[] { _registros, _referencias, _chaves, _valores, _historico }.Sum(l => l.Sum(x => (long)Encoding.UTF8.GetByteCount(x) + 4));

    public static LinhasSql Juntar(IEnumerable<LinhasSql> partes)
    {
        var junto = new LinhasSql();
        foreach (var parte in partes)
        {
            junto.Itens += parte.Itens;
            junto._registros.AddRange(parte._registros);
            junto._referencias.AddRange(parte._referencias);
            junto._chaves.AddRange(parte._chaves);
            junto._valores.AddRange(parte._valores);
            junto._historico.AddRange(parte._historico);
        }

        return junto;
    }

    private void Adicionar(ItemMigrado item)
    {
        var r = item.Registro;
        _registros.Add($"({LiteralSql.Guid(r.Id)}, {LiteralSql.Guid(r.EntidadeId)}, {LiteralSql.Texto(r.Dados)}, {LiteralSql.DataHora(r.CriadoEm)}, " +
                       $"{LiteralSql.DataHora(r.AtualizadoEm)}, {LiteralSql.Guid(r.EquipeId)}, @Usuario, @Usuario, 'APROVADO')");
        foreach (var referencia in r.Referencias)
        {
            _referencias.Add($"({LiteralSql.Guid(referencia.RegistroOrigemId)}, {LiteralSql.Guid(referencia.EntidadeOrigemId)}, {LiteralSql.Guid(referencia.CampoId)}, " +
                             $"{LiteralSql.Guid(referencia.RegistroDestinoId)}, {LiteralSql.Guid(referencia.EntidadeDestinoId)})");
        }

        foreach (var chave in r.ChavesUnicas)
            _chaves.Add($"({LiteralSql.Guid(chave.RestricaoUnicaId)}, {LiteralSql.Guid(chave.EntidadeId)}, {LiteralSql.Guid(chave.RegistroId)}, {LiteralSql.Binario(chave.ValorHash)})");

        foreach (var valor in r.Valores)
        {
            _valores.Add($"({LiteralSql.Guid(valor.RegistroId)}, {LiteralSql.Guid(valor.EntidadeId)}, {LiteralSql.Guid(valor.CampoId)}, {LiteralSql.Texto(valor.ValorTexto)}, " +
                         $"{LiteralSql.Numero(valor.ValorNumero)}, {LiteralSql.DataHora(valor.ValorData)}, {LiteralSql.Bit(valor.ValorBooleano)})");
        }

        _historico.Add($"('{HistoricoConversor.ParaTexto(AlvoHistorico.Registro)}', {LiteralSql.Guid(r.Id)}, {LiteralSql.Guid(r.EntidadeId)}, {LiteralSql.Guid(r.EquipeId)}, " +
                       $"'{HistoricoConversor.ParaTexto(OperacaoHistorico.Criacao)}', @Usuario, @Matricula, {LiteralSql.DataHora(r.CriadoEm)}, @Correlacao, NULL, " +
                       $"{LiteralSql.Texto(item.HistoricoDepois)}, NULL, '{HistoricoConversor.ParaTexto(OrigemHistorico.MigracaoSharePoint)}')");
    }

    public void Escrever(StringBuilder s)
    {
        Insert(s, "Registro", ["Id", "EntidadeId", "Dados", "CriadoEm", "AtualizadoEm", "EquipeId", "CriadoPorId", "AtualizadoPorId", "SituacaoAprovacao"], _registros);
        Insert(s, "RegistroReferencia", ["RegistroOrigemId", "EntidadeOrigemId", "CampoId", "RegistroDestinoId", "EntidadeDestinoId"], _referencias);
        Insert(s, "RegistroChaveUnica", ["RestricaoUnicaId", "EntidadeId", "RegistroId", "ValorHash"], _chaves);
        Insert(s, "RegistroValor", ["RegistroId", "EntidadeId", "CampoId", "ValorTexto", "ValorNumero", "ValorData", "ValorBooleano"], _valores);
        Insert(s, "HistoricoAlteracao", EscritorScripts.ColunasHistorico, _historico);
    }

    private static void Insert(StringBuilder s, string tabela, IReadOnlyList<string> colunas, List<string> linhas)
    {
        for (var inicio = 0; inicio < linhas.Count; inicio += EscritorScripts.LinhasPorInsert)
        {
            var lote = linhas.Skip(inicio).Take(EscritorScripts.LinhasPorInsert).ToList();
            s.Append("INSERT ").Append(EscritorScripts.T(tabela)).Append(" (").Append(EscritorScripts.Colunas(tabela, colunas)).AppendLine(") VALUES");
            for (var i = 0; i < lote.Count; i++)
                s.Append("    ").Append(lote[i]).AppendLine(i == lote.Count - 1 ? ";" : ",");
        }
    }
}
