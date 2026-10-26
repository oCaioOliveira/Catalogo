using System.Globalization;
using Catalogo.Application.Abstracoes;
using Catalogo.Application.Comum;
using Catalogo.Domain.Acesso;
using Catalogo.Domain.Importacoes;
using Catalogo.Domain.Metadados;

namespace Catalogo.Application.CasosDeUso.Importacoes;

/// <summary>
/// Caso de uso: o modelo XLSX de um tipo de item (GET .../importacao/modelo, RN98), gerado a partir dos metadados:
///   - aba de dados com o NOME dos campos ativos, na ordem do formulário, e "*" nos obrigatórios; "Equipe" no fim, se o tipo de item é
///     por equipe e o usuário tem mais de uma equipe nesta área de gestão;
///   - lista suspensa em LISTA e BOOLEANO, e nas referências com até 1.000 itens visíveis (numa aba oculta);
///   - formato de célula para datas e números (e texto nos demais, para "007" não virar 7);
///   - aba "Instruções": cada coluna (tipo, obrigatoriedade, tamanho, opções, referência e como ela é resolvida, dependências e
///     unicidade) e as regras gerais.
/// Só quem pode importar baixa o modelo.
/// </summary>
public sealed class ObterModeloImportacao(
    AcessoImportacao acesso,
    IConsultasImportacao consultas,
    IEquipeRepository equipes,
    IArquivosImportacao arquivos,
    OpcoesExportacao opcoesExportacao)
{
    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("pt-BR");

    public async Task<ArquivoExportado> ExecutarAsync(string grupoChave, string entidadeChave, CancellationToken cancellationToken)
    {
        var (grupo, entidade, usuario) = await acesso.EntrarParaImportarAsync(grupoChave, entidadeChave, cancellationToken);

        var colunas = new List<ColunaModelo>();
        foreach (var campo in entidade.Campos.Where(c => c.Ativo))
            colunas.Add(await ColunaAsync(grupo, entidade, usuario, campo, cancellationToken));

        if (!entidade.EhGlobal && usuario.Equipes.Count > 1)
        {
            var nomes = (await equipes.ObterPorIdsAsync(usuario.Equipes.ToList(), cancellationToken))
                .Where(e => e.Ativa).Select(e => e.Nome).Order(StringComparer.Create(Cultura, true)).ToList();
            colunas.Add(new ColunaModelo(SugestaoMapeamento.CabecalhoEquipe, null, null, nomes, true, "Equipe (pelo nome)", false,
                $"O nome de uma das suas equipes nesta área de gestão: {string.Join(", ", nomes)}. Linha sem equipe: vale a equipe padrão " +
                "escolhida na importação."));
        }

        var modelo = new ModeloPlanilha(entidade.NomePlural ?? entidade.Nome, colunas, Regras(entidade));
        var conteudo = arquivos.GerarModelo(modelo);
        return new ArquivoExportado($"modelo_{entidade.Chave}.xlsx", BaixarRelatorioImportacao.TipoConteudo,
            (destino, ct) => destino.WriteAsync(conteudo, ct).AsTask());
    }

    private async Task<ColunaModelo> ColunaAsync(
        Grupo grupo, Entidade entidade, UsuarioAtual usuario, Campo campo, CancellationToken cancellationToken)
    {
        var cabecalho = campo.Obrigatorio ? $"{campo.Nome} *" : campo.Nome;
        var unicidade = Unicidade(entidade, campo);

        if (campo.EhReferencia)
        {
            var referenciada = grupo.ObterEntidade(campo.EntidadeReferenciadaId!.Value);
            var exibicao = referenciada?.CampoExibicaoId is Guid exibicaoId ? referenciada.ObterCampo(exibicaoId) : null;
            IReadOnlyList<string>? lista = null;
            if (referenciada is not null && exibicao is not null && ResolvedorReferencias.ExibicaoDeTexto(exibicao))
            {
                var escopo = PreparadorConsultaRegistros.Escopo(usuario, referenciada, null);
                var nomes = await consultas.ListarNomesAsync(referenciada.Id, exibicao, escopo,
                    LimitesImportacao.MaximoOpcoesListaNoModelo + 1, cancellationToken);
                lista = nomes.Count is > 0 and <= LimitesImportacao.MaximoOpcoesListaNoModelo ? nomes : null;
            }

            var nomeReferenciada = referenciada?.Nome ?? campo.Nome;
            var como = ResolvedorReferencias.AceitaNome(exibicao)
                ? $"O nome do item de '{nomeReferenciada}' (o valor de '{exibicao!.Nome}'), como aparece no sistema. Maiúsculas e acentos " +
                  "não importam. Só valem os itens que você vê" +
                  (entidade.EhGlobal || referenciada!.EhGlobal ? "" : ", da mesma equipe da linha") +
                  ", e aprovados (ou criados por você). Se houver dois itens com o mesmo nome, acrescente a coluna " +
                  $"'{campo.Nome} {SugestaoMapeamento.SufixoId}' com o identificador do item." +
                  (lista is null ? "" : " A lista suspensa mostra os itens que você vê hoje.")
                : $"'{nomeReferenciada}' não tem um nome para identificar os itens: acrescente a coluna '{campo.Nome} {SugestaoMapeamento.SufixoId}' " +
                  "com o identificador do item.";
            return new ColunaModelo(cabecalho, campo.TipoDado, null, lista, false, $"{nomeReferenciada} (pelo nome)", campo.Obrigatorio,
                Juntar(como, Dependencia(entidade, campo), unicidade));
        }

        var (descricao, comoPreencher, opcoes) = campo.TipoDado switch
        {
            TipoDado.Texto => ($"Texto (até {campo.TamanhoMaximo} caracteres)", $"Qualquer texto, com até {campo.TamanhoMaximo} caracteres.", null),
            TipoDado.TextoLongo => ("Texto longo", "Texto livre, sem limite de tamanho.", null),
            TipoDado.Inteiro => ("Número inteiro", "Só algarismos, sem casas decimais (ex.: 1500).", null),
            TipoDado.Decimal => ($"Número com até {campo.Escala} casas decimais",
                $"Com vírgula ou ponto decimal (ex.: 1.234,56 ou 1234.56), com até {campo.Escala} casas depois da vírgula.", null),
            TipoDado.Data => ("Data", "dd/mm/aaaa (ex.: 21/10/2026), ou uma data do Excel.", null),
            TipoDado.DataHora => ("Data e hora",
                $"dd/mm/aaaa hh:mm (ex.: 21/10/2026 14:30), no horário local ({opcoesExportacao.Fuso.Id}), ou uma data e hora do Excel.", null),
            TipoDado.Booleano => ("Sim ou Não", "Sim ou Não (também vale Verdadeiro/Falso ou 1/0).", (IReadOnlyList<string>)["Sim", "Não"]),
            TipoDado.Lista => ("Lista de opções", $"Uma destas opções: {string.Join(", ", campo.Opcoes)}. Maiúsculas e acentos não importam.",
                campo.Opcoes),
            TipoDado.Url => ("Endereço (URL)", "O endereço completo, começando com http:// ou https:// (até 2000 caracteres).", null),
            _ => (campo.TipoDado.ToString(), "", null),
        };
        return new ColunaModelo(cabecalho, campo.TipoDado, campo.Escala, opcoes, true, descricao, campo.Obrigatorio,
            Juntar(comoPreencher, Dependencia(entidade, campo), unicidade));
    }

    private static string? Dependencia(Entidade entidade, Campo campo) =>
        entidade.Dependencias.FirstOrDefault(d => d.Campo.Id == campo.Id) is not { } dependencia
            ? null
            : dependencia.CampoVia is { } via
                ? $"Precisa ter '{dependencia.CampoAlvo.Nome}' igual ao '{dependencia.CampoOrigem.Nome}' do item escolhido em '{via.Nome}'. " +
                  "Entre itens com o mesmo nome, vale o que atende a esta regra."
                : $"Precisa ter '{dependencia.CampoAlvo.Nome}' igual ao campo '{dependencia.CampoOrigem.Nome}' desta linha.";

    private static string? Unicidade(Entidade entidade, Campo campo)
    {
        var restricoes = entidade.RestricoesUnicas.Where(r => r.Campos.Any(c => c.Id == campo.Id)).ToList();
        return restricoes.Count == 0
            ? null
            : string.Join(" ", restricoes.Select(r => r.Campos.Count == 1
                ? $"Não pode se repetir{(entidade.EhGlobal ? "" : " na mesma equipe")}."
                : $"A combinação {string.Join(" + ", r.Campos.Select(c => c.Nome))} não pode se repetir{(entidade.EhGlobal ? "" : " na mesma equipe")}."));
    }

    private static string Juntar(params string?[] partes) => string.Join(" ", partes.Where(p => !string.IsNullOrWhiteSpace(p)));

    private static IReadOnlyList<string> Regras(Entidade entidade)
    {
        var regras = new List<string>
        {
            $"Preencha um item de '{entidade.Nome}' por linha, a partir da linha 2 da primeira aba. Não mude os nomes das colunas: a ordem " +
            "pode mudar, e colunas a mais são ignoradas (você confere a ligação de cada coluna antes de importar).",
            "As colunas com * são obrigatórias. Célula vazia = campo não informado.",
            $"Até {LimitesImportacao.MaximoLinhas.ToString("N0", Cultura)} linhas, {LimitesImportacao.MaximoColunas} colunas e " +
            $"{LimitesImportacao.TamanhoMaximoBytes / 1024 / 1024} MB por arquivo. Também é possível enviar um CSV em UTF-8, separado por ; ou ,.",
        };

        if (entidade.Campos.Any(c => c.Ativo && c.EhReferencia))
        {
            regras.Add("Referências a outros tipos de item são preenchidas pelo NOME do item. Se houver nomes repetidos, acrescente a coluna " +
                       "'<campo> (ID)' com o identificador do item (o código que aparece no endereço da página dele).");
        }

        foreach (var restricao in entidade.RestricoesUnicas)
        {
            regras.Add($"Unicidade: não pode haver dois itens com o mesmo {string.Join(" + ", restricao.Campos.Select(c => c.Nome))}" +
                       $"{(entidade.EhGlobal ? "" : " na mesma equipe")}. Linhas repetidas na planilha são recusadas; um item que já existe é " +
                       "pulado ou atualizado, como você escolher na importação.");
        }

        regras.Add("Antes de gravar, o sistema mostra uma prévia linha a linha, com os erros em cada célula. Nada é gravado sem a sua confirmação, " +
                   "e uma importação pode ser desfeita enquanto os itens criados não forem alterados.");
        return regras;
    }
}
