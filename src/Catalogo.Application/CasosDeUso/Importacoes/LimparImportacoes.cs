using Catalogo.Application.Abstracoes;
using Catalogo.Domain.Importacoes;

namespace Catalogo.Application.CasosDeUso.Importacoes;

/// <summary>
/// Caso de uso da limpeza automática (RN104), chamado por um serviço em segundo plano, sem usuário: apaga os arquivos enviados há mais
/// de <see cref="LimitesImportacao.DiasRetencaoArquivo"/> dias (o registro da importação fica, com o resumo) e marca como EXPIRADA as
/// importações que nunca foram executadas.
/// </summary>
public sealed class LimparImportacoes(IImportacaoRepository importacoes, TimeProvider relogio)
{
    public Task<(int ArquivosApagados, int Expiradas)> ExecutarAsync(CancellationToken cancellationToken) =>
        importacoes.LimparAntigasAsync(relogio.GetUtcNow().UtcDateTime.AddDays(-LimitesImportacao.DiasRetencaoArquivo), cancellationToken);
}
