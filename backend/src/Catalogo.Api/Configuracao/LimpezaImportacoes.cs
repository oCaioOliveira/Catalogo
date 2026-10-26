using Catalogo.Application.CasosDeUso.Importacoes;

namespace Catalogo.Api.Configuracao;

/// <summary>
/// Serviço em segundo plano da limpeza das importações (RN104): a cada "Importacao:IntervaloLimpezaMinutos" (padrão 60; 0 desliga),
/// chama o caso de uso <see cref="LimparImportacoes"/>, que apaga os arquivos com mais de 7 dias e marca como EXPIRADA as importações
/// nunca executadas. Com vários servidores, todos rodam a limpeza; ela é idempotente (um DELETE e um UPDATE com a mesma condição).
/// </summary>
internal sealed class LimpezaImportacoes(
    IServiceScopeFactory escopos, IConfiguration configuracao, ILogger<LimpezaImportacoes> logger) : BackgroundService
{
    public const int IntervaloPadraoMinutos = 60;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var minutos = configuracao.GetValue("Importacao:IntervaloLimpezaMinutos", IntervaloPadraoMinutos);
        if (minutos <= 0)
            return;

        using var relogio = new PeriodicTimer(TimeSpan.FromMinutes(minutos));
        do
        {
            try
            {
                await using var escopo = escopos.CreateAsyncScope();
                var (arquivos, expiradas) = await escopo.ServiceProvider.GetRequiredService<LimparImportacoes>().ExecutarAsync(stoppingToken);
                if (arquivos + expiradas > 0)
                    logger.LogInformation("Limpeza das importações: {Arquivos} arquivos apagados, {Expiradas} importações expiradas", arquivos, expiradas);
            }
            catch (Exception erro) when (erro is not OperationCanceledException)
            {
                // Um banco fora do ar não derruba a API: tenta de novo no próximo intervalo.
                logger.LogWarning(erro, "A limpeza das importações falhou; tenta de novo em {Minutos} minutos", minutos);
            }
        }
        while (await relogio.WaitForNextTickAsync(stoppingToken));
    }
}
