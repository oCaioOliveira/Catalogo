using Catalogo.Application.Comum;
using Catalogo.Application.Abstracoes;
using Catalogo.Application.Mapeamentos;
using Catalogo.Domain.Importacoes;
using Catalogo.Domain.Metadados;

namespace Catalogo.Application.CasosDeUso.Importacoes;

/// <summary>
/// Caso de uso: o relatório de uma importação (GET .../importacoes/{id}/relatorio): um XLSX com as linhas ORIGINAIS do arquivo e duas
/// colunas a mais, "Resultado" (criado, atualizado, ignorado, erro; antes de executar, "será criado"...) e "Mensagens" (os erros, com a
/// coluna de cada um). Quem vê a importação baixa o relatório (RN97), enquanto o arquivo estiver guardado (RN104).
/// </summary>
public sealed class BaixarRelatorioImportacao(AcessoImportacao acesso, IArquivosImportacao arquivos)
{
    public const string TipoConteudo = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    public async Task<ArquivoExportado> ExecutarAsync(string grupoChave, string entidadeChave, Guid id, CancellationToken cancellationToken)
    {
        var (_, entidade, usuario) = await acesso.EntrarParaLerAsync(grupoChave, entidadeChave, UsoEntidade.Leitura, cancellationToken);
        var importacao = await acesso.ObterAsync(usuario, entidade, id, somenteAutor: false, cancellationToken);
        var planilha = await acesso.LerArquivoAsync(importacao, cancellationToken);

        var resumo = ImportacaoMapeamentos.DeJson<ResumoImportacao>(importacao.Resumo);
        var concluida = importacao.Situacao is SituacaoImportacao.Concluida or SituacaoImportacao.Desfeita;
        var resultados = (resumo?.Linhas ?? []).ToDictionary(
            l => l.Numero,
            l => (ImportacaoMapeamentos.ResultadoNoRelatorio(l.Situacao, concluida), Mensagens(l, planilha, entidade)));

        var conteudo = arquivos.GerarRelatorio(planilha, resultados);
        var nome = $"relatorio_{Path.GetFileNameWithoutExtension(importacao.NomeArquivo)}.xlsx";
        return new ArquivoExportado(nome, TipoConteudo, (destino, ct) => destino.WriteAsync(conteudo, ct).AsTask());
    }

    /// <summary>Uma mensagem por erro, com a coluna (o cabeçalho do arquivo) ou o campo, uma por linha da célula.</summary>
    private static string Mensagens(ResultadoLinha linha, PlanilhaImportada planilha, Entidade entidade) => string.Join("\n",
        linha.Erros.Select(e =>
        {
            var onde = e.Coluna is int coluna && coluna < planilha.Cabecalhos.Count
                ? planilha.Cabecalhos[coluna]
                : e.Campo is string chave ? entidade.ObterCampo(chave)?.Nome : null;
            return onde is null ? e.Mensagem : $"{onde}: {e.Mensagem}";
        }));
}
