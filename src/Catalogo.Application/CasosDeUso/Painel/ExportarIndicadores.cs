using System.Runtime.CompilerServices;
using Catalogo.Application.Abstracoes;
using Catalogo.Application.Comum;
using Catalogo.Application.Dtos;
using Catalogo.Domain.Excecoes;
using Catalogo.Domain.Metadados;
using Catalogo.Domain.Painel;
using Catalogo.Domain.Validacao;

namespace Catalogo.Application.CasosDeUso.Painel;

/// <summary>
/// Caso de uso: exportar o resultado da agregação (GET .../indicadores/exportar?formato=csv|xlsx), RN88.
///   - Os MESMOS parâmetros e o MESMO resultado de <see cref="CalcularIndicadores"/> (inclusive o limite de linhas).
///   - Os mesmos geradores da exportação da listagem (<see cref="IGeradorArquivoExportacao"/>), com a mesma proteção contra
///     injeção de fórmula (<see cref="ProtecaoFormula"/>).
///   - Colunas: as dimensões (REFERENCIA e equipe pelo rótulo), a contagem e as métricas, com os nomes da resposta JSON.
/// </summary>
public sealed class ExportarIndicadores(
    CalcularIndicadores calcular,
    IEnumerable<IGeradorArquivoExportacao> geradores,
    OpcoesPainel opcoes,
    TimeProvider relogio)
{
    public async Task<ArquivoExportado> ExecutarAsync(
        string grupoChave, string entidadeChave, IEnumerable<KeyValuePair<string, string>> parametros, CancellationToken cancellationToken)
    {
        var lista = parametros.ToList();

        // O erro do "formato" sai junto com os dos outros parâmetros (RN05).
        var erros = new ErrosPorCampo();
        IGeradorArquivoExportacao? gerador = null;
        try
        {
            gerador = ExportarRegistros.EscolherGerador(geradores, lista);
        }
        catch (ValidacaoRegistroException ex)
        {
            CalcularIndicadores.Juntar(erros, ex);
        }

        var (entidade, indicadores) = await calcular.CalcularAsync(
            grupoChave, entidadeChave, lista.Where(p => p.Key != ChavesReservadas.Formato).ToList(), erros, cancellationToken);

        var colunas = indicadores.Dimensoes.Select(d => d.Nome)
            .Append("Contagem")
            .Concat(indicadores.Metricas.Where(m => m.Chave != ConsultaIndicadores.Contagem).Select(m => m.Nome))
            .ToList();

        var hoje = TimeZoneInfo.ConvertTime(relogio.GetUtcNow(), opcoes.Fuso);
        return new ArquivoExportado(
            $"{entidade.Chave}_indicadores_{hoje:yyyy-MM-dd}.{gerador!.Formato}",
            gerador.TipoConteudo,
            (destino, ct) => gerador.EscreverAsync(
                new PlanilhaExportacao($"{entidade.Nome} - indicadores", colunas, LinhasAsync(indicadores, ct)), destino, ct));
    }

    private static async IAsyncEnumerable<IReadOnlyList<object?>> LinhasAsync(
        IndicadoresDto indicadores, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var chaves = indicadores.Dimensoes.Select(d => d.Chave)
            .Append(ConsultaIndicadores.Contagem)
            .Concat(indicadores.Metricas.Select(m => m.Chave).Where(c => c != ConsultaIndicadores.Contagem))
            .ToList();

        foreach (var linha in indicadores.Linhas)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return chaves.Select(c => ParaPlanilha(linha.GetValueOrDefault(c))).ToList();
        }

        await Task.CompletedTask;
    }

    /// <summary>Os tipos que o gerador entende: texto (protegido), número (decimal) ou vazio.</summary>
    private static object? ParaPlanilha(object? valor) => valor switch
    {
        null => null,
        ValorReferenciaDto referencia => ProtecaoFormula.Neutralizar(referencia.Rotulo ?? referencia.Id.ToString()),
        bool booleano => booleano ? "Sim" : "Não",
        int inteiro => (decimal)inteiro,
        decimal numero => numero,
        string texto => ProtecaoFormula.Neutralizar(texto),
        var outro => ProtecaoFormula.Neutralizar(outro.ToString() ?? ""),
    };
}
