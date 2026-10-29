using Catalogo.Domain.Acesso;
using Catalogo.Domain.Aprovacao;
using Catalogo.Domain.ChavesUnicas;
using Catalogo.Domain.Exclusao;
using Catalogo.Domain.Metadados;

namespace Catalogo.Domain.Importacoes;

/// <summary>
/// Sugestão de mapeamento coluna → campo (RN98): compara o cabeçalho com o NOME do campo ou com a CHAVE, sem diferenciar maiúsculas
/// nem acentos e ignorando o "*" que o modelo põe nos obrigatórios. "&lt;campo&gt; (ID)" vai para o identificador do item de uma
/// referência e "Equipe", para a equipe (só em tipos de item por equipe). Cada campo recebe no máximo uma coluna; o resto é ignorado.
/// </summary>
public static class SugestaoMapeamento
{
    public const string SufixoId = "(ID)";
    public const string CabecalhoEquipe = "Equipe";

    public static MapeamentoImportacao Sugerir(Entidade entidade, IReadOnlyList<string> cabecalhos)
    {
        var usados = new HashSet<(TipoDestinoColuna, string?)>();
        var colunas = new List<ColunaMapeada>();

        for (var indice = 0; indice < cabecalhos.Count; indice++)
        {
            var destino = Destino(entidade, cabecalhos[indice]);
            if (destino.Tipo != TipoDestinoColuna.Ignorar && !usados.Add((destino.Tipo, destino.CampoChave)))
                destino = DestinoColuna.Ignorar;
            colunas.Add(new ColunaMapeada(indice, cabecalhos[indice], destino));
        }

        return new MapeamentoImportacao(colunas);
    }

    private static DestinoColuna Destino(Entidade entidade, string cabecalho)
    {
        var texto = cabecalho.Trim().TrimEnd('*').Trim();
        var ativos = entidade.Campos.Where(c => c.Ativo).ToList();

        if (texto.EndsWith(SufixoId, StringComparison.OrdinalIgnoreCase))
        {
            var semSufixo = texto[..^SufixoId.Length];
            var referencia = Encontrar(ativos.Where(c => c.EhReferencia), semSufixo);
            return referencia is null ? DestinoColuna.Ignorar : new DestinoColuna(TipoDestinoColuna.CampoId, referencia.Chave);
        }

        var campo = Encontrar(ativos, texto);
        if (campo is not null)
            return new DestinoColuna(TipoDestinoColuna.Campo, campo.Chave);

        return !entidade.EhGlobal && Igual(texto, CabecalhoEquipe) ? DestinoColuna.Equipe : DestinoColuna.Ignorar;
    }

    private static Campo? Encontrar(IEnumerable<Campo> campos, string texto) =>
        campos.FirstOrDefault(c => Igual(texto, c.Nome)) ?? campos.FirstOrDefault(c => Igual(texto, c.Chave));

    private static bool Igual(string a, string b) =>
        CalculadoraChaveUnica.NormalizarTexto(a) == CalculadoraChaveUnica.NormalizarTexto(b);
}

/// <summary>Um item que pode ser o apontado por uma referência digitada pelo nome (já dentro do que o usuário vê).</summary>
public sealed record CandidatoReferencia(Guid Id, string Nome, Guid? EquipeId, bool Global, SituacaoAprovacao Situacao, Guid? CriadoPorId);

/// <summary>Resultado da resolução de uma referência: o item encontrado ou a mensagem de erro.</summary>
public sealed record ResolucaoReferencia(CandidatoReferencia? Item, string? Erro);

/// <summary>
/// Referências pelo nome (RN99). Os candidatos já vêm da consulta com o escopo de visibilidade do usuário (equipe e aprovação, RN26
/// e RN37) e com o mesmo nome (sem diferenciar maiúsculas e acentos). Aqui ficam só os que a LINHA pode referenciar: mesma equipe ou
/// globais (RN29) e aprovados, salvo os do próprio autor (RN38). Nenhum: "não encontrado"; mais de um: pede a coluna (ID).
/// </summary>
public static class ResolvedorReferencias
{
    /// <summary>O campo de exibição é texto: o nome é comparado sem diferenciar maiúsculas nem acentos.</summary>
    public static bool ExibicaoDeTexto(Campo campoExibicao) =>
        campoExibicao.TipoDado is TipoDado.Texto or TipoDado.TextoLongo or TipoDado.Lista or TipoDado.Url;

    /// <summary>O campo de exibição é número: o "nome" é comparado pelo valor (ex.: "1" = "1,0").</summary>
    public static bool ExibicaoNumerica(Campo campoExibicao) => campoExibicao.TipoDado is TipoDado.Inteiro or TipoDado.Decimal;

    /// <summary>Um tipo de item pode ser referenciado pelo nome se tem campo de exibição de texto ou número.</summary>
    public static bool AceitaNome(Campo? campoExibicao) =>
        campoExibicao is not null && (ExibicaoDeTexto(campoExibicao) || ExibicaoNumerica(campoExibicao));

    /// <summary>
    /// O nome como ele é comparado: texto sem acentos e em maiúsculas (RN09); número pelo valor, sem zeros à direita. Nulo se o
    /// texto não é um número num campo de exibição numérico.
    /// </summary>
    public static string? ChaveDoNome(Campo campoExibicao, string nome)
    {
        if (ExibicaoNumerica(campoExibicao))
        {
            return ConversorCelulas.TentarNumero(CelulaImportada.DeTexto(nome), out var numero)
                ? numero.ToString("0.############################", System.Globalization.CultureInfo.InvariantCulture)
                : null;
        }

        return CalculadoraChaveUnica.NormalizarTexto(nome);
    }

    public static ResolucaoReferencia Resolver(
        string nomeDigitado, IEnumerable<CandidatoReferencia> candidatos, Guid? equipeDaLinha, Guid autor, Campo campo, string nomeEntidade,
        Func<CandidatoReferencia, bool>? atendeDependencia = null)
    {
        var usaveis = candidatos
            .Where(c => PoliticaPermissao.PodeReferenciar(equipeDaLinha, c.Global ? null : c.EquipeId)
                        && PoliticaPermissao.PodeReferenciarSituacao(c.Situacao, c.CriadoPorId, autor))
            .ToList();

        // RN08 antes da ambiguidade: entre os de mesmo nome, valem os que atendem à dependência do campo (ex.: o endpoint do projeto
        // do relatório). Se nenhum atende, o erro da dependência aparece depois, na validação da linha.
        if (usaveis.Count > 1 && atendeDependencia is not null)
        {
            var filtrados = usaveis.Where(atendeDependencia).ToList();
            if (filtrados.Count > 0)
                usaveis = filtrados;
        }

        return usaveis.Count switch
        {
            1 => new ResolucaoReferencia(usaveis[0], null),
            0 => new ResolucaoReferencia(null,
                $"Nenhum item de '{nomeEntidade}' com o nome '{nomeDigitado}' que possa ser usado aqui (confira o nome, a equipe e se ele já foi aprovado)."),
            _ => new ResolucaoReferencia(null,
                $"Há {usaveis.Count} itens de '{nomeEntidade}' com o nome '{nomeDigitado}'. Use a coluna '{campo.Nome} {SugestaoMapeamento.SufixoId}' para indicar qual."),
        };
    }
}

/// <summary>
/// Duplicados DENTRO do próprio arquivo (RN102): duas linhas com o mesmo valor numa restrição única (a mesma chave da
/// <see cref="CalculadoraChaveUnica"/>, inclusive a equipe) são erro nas DUAS.
/// </summary>
public static class DuplicadosNoArquivo
{
    /// <returns>Para cada linha repetida, as outras linhas com a mesma chave e o nome da restrição.</returns>
    public static IReadOnlyDictionary<int, (string Restricao, IReadOnlyList<int> Outras)> Encontrar(
        IEnumerable<(int Linha, IReadOnlyList<ChaveUnicaCalculada> Chaves)> linhas)
    {
        var porChave = new Dictionary<(Guid, string), List<int>>();
        var nomes = new Dictionary<Guid, string>();
        foreach (var (linha, chaves) in linhas)
        {
            foreach (var chave in chaves)
            {
                var id = (chave.Restricao.Id, Convert.ToHexString(chave.ValorHash));
                if (!porChave.TryGetValue(id, out var lista))
                    porChave[id] = lista = [];
                lista.Add(linha);
                nomes[chave.Restricao.Id] = chave.Restricao.Nome;
            }
        }

        var resultado = new Dictionary<int, (string, IReadOnlyList<int>)>();
        foreach (var ((restricaoId, _), linhasDaChave) in porChave.Where(p => p.Value.Count > 1))
        {
            foreach (var linha in linhasDaChave)
                resultado.TryAdd(linha, (nomes[restricaoId], linhasDaChave.Where(l => l != linha).ToList()));
        }

        return resultado;
    }
}

/// <summary>Por que uma importação não pode ser desfeita, e os itens que impedem (RN103).</summary>
public sealed record ImpedimentoDesfazer(string Motivo, IReadOnlyList<Guid> Itens);

/// <summary>
/// Desfazer uma importação (RN103) remove só os itens que ela CRIOU (as atualizações não voltam atrás), e só se:
///   - nenhum deles foi alterado depois (outra linha de histórico que não seja a da importação);
///   - nenhum é referenciado por um item que não veio desta importação, nem por RESTRINGIR nem por CASCATA: o plano de exclusão
///     (o mesmo da exclusão manual, RN16) não pode bloquear nem levar junto itens de fora.
/// </summary>
public static class RegrasDesfazer
{
    public static IReadOnlyList<ImpedimentoDesfazer> Avaliar(
        IReadOnlySet<Guid> criados, IReadOnlySet<Guid> alteradosDepois, PlanoExclusao plano)
    {
        var impedimentos = new List<ImpedimentoDesfazer>();

        var alterados = criados.Where(alteradosDepois.Contains).ToList();
        if (alterados.Count > 0)
            impedimentos.Add(new ImpedimentoDesfazer("Itens alterados depois da importação.", alterados));

        // RESTRINGIR de fora bloqueia o plano; CASCATA levaria junto itens de fora (que entram no plano). Os dois impedem.
        var deFora = plano.Bloqueios.Select(b => b.RegistroOrigemId)
            .Concat(plano.OrdemExclusao.Where(id => !criados.Contains(id)))
            .Distinct()
            .ToList();
        if (deFora.Count > 0)
            impedimentos.Add(new ImpedimentoDesfazer("Itens que não vieram desta importação apontam para itens criados por ela.", deFora));

        return impedimentos;
    }
}
