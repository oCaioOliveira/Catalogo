using System.Text.Json.Nodes;
using Catalogo.Api.IntegrationTests.Apoio;
using Catalogo.Application.Abstracoes;
using Catalogo.Domain.Registros;
using Catalogo.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Catalogo.Api.IntegrationTests;

/// <summary>Tradução dos erros reais do SQL Server feita pela Infrastructure.</summary>
public class UnidadeDeTrabalhoTests(BancoDeTestesFixture banco) : TesteDeApi(banco)
{
    [Fact]
    public async Task Salvar_HashRepetidoNaMesmaRestricao_LancaViolacaoComIdDaRestricao()
    {
        // RN09: simula a corrida pulando a verificação prévia e gravando direto um hash que já existe.
        await Criar("comunidade", new JsonObject { ["nome"] = "Comunidade Ágil" });

        using var escopo = Banco.Fabrica.Services.CreateScope();
        var contexto = escopo.ServiceProvider.GetRequiredService<CatalogoDbContext>();
        var existente = await contexto.RegistrosChavesUnicas.AsNoTracking().SingleAsync();

        var duplicado = new Registro { EntidadeId = existente.EntidadeId, Dados = "{\"nome\":\"comunidade agil\"}" };
        duplicado.ChavesUnicas.Add(new RegistroChaveUnica
        {
            RestricaoUnicaId = existente.RestricaoUnicaId,
            EntidadeId = existente.EntidadeId,
            ValorHash = existente.ValorHash,
        });
        escopo.ServiceProvider.GetRequiredService<IRegistroRepository>().Adicionar(duplicado);

        var erro = await Assert.ThrowsAsync<ViolacaoUnicidadeBancoException>(() =>
            escopo.ServiceProvider.GetRequiredService<IUnidadeDeTrabalho>().SalvarAlteracoesAsync(CancellationToken.None));

        Assert.Equal(existente.RestricaoUnicaId, erro.RestricaoUnicaId);
        Assert.Equal(1, await contexto.Registros.CountAsync());  // nada foi gravado: a transação é única
    }
}
