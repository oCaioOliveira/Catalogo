using System.Net;
using Catalogo.Api.Autenticacao;
using Catalogo.Api.IntegrationTests.Apoio;
using Catalogo.Infrastructure;
using Catalogo.Infrastructure.Persistencia;
using Catalogo.Infrastructure.Persistencia.Tabelas;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Catalogo.Api.IntegrationTests;

// Chave de assinatura do JWT no banco, cat.CATTB025_CHAVE_JWT (docs/adr/0053): gerada na primeira subida, lida nas próximas.
public class ChaveJwtApiTests(BancoDeTestesFixture banco) : TesteDeApi(banco)
{
    [Fact]
    public async Task Subida_UsaAChaveGravadaNoBanco()
    {
        var gravada = await LerChaveAsync();

        Assert.NotNull(gravada);
        Assert.True(gravada.Length >= OpcoesJwt.TamanhoMinimoChave);
        Assert.Equal(gravada, Banco.Fabrica.Services.GetRequiredService<ChaveAssinaturaJwt>().Valor);
    }

    [Fact]
    public async Task TokenDeUmaInstancia_EhAceitoPorOutraQueSubiuDepois()
    {
        // Outra instância da API (outro processo, no servidor) lê a mesma chave do banco: o token emitido por uma vale na outra.
        var outraInstancia = FabricaCom(new Dictionary<string, string>());

        var resposta = await ClienteDe(Administrador, outraInstancia).GetAsync("/api/v1/grupos");

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
    }

    [Fact]
    public async Task ObterOuCriar_ComChaveNoBanco_DevolveAMesmaSemGerarOutra()
    {
        var gravada = await LerChaveAsync();

        var lida = await ConfiguracaoInfraestrutura.ObterOuCriarChaveJwtAsync(
            Banco.Fabrica.Services, () => throw new InvalidOperationException("Não deveria gerar outra chave."));

        Assert.Equal(gravada, lida);
    }

    [Fact]
    public async Task ObterOuCriar_SemChaveNoBanco_GeraGravaEDepoisLeAMesma()
    {
        var original = await LerChaveAsync();
        try
        {
            await ApagarChaveAsync();
            var nova = ChaveAssinaturaJwt.GerarNova();

            var primeira = await ConfiguracaoInfraestrutura.ObterOuCriarChaveJwtAsync(Banco.Fabrica.Services, () => nova);
            var segunda = await ConfiguracaoInfraestrutura.ObterOuCriarChaveJwtAsync(Banco.Fabrica.Services, ChaveAssinaturaJwt.GerarNova);

            Assert.Equal(nova, primeira);
            Assert.Equal(nova, segunda);
            Assert.Equal(nova, await LerChaveAsync());
            Assert.Equal(88, nova.Length);
        }
        finally
        {
            // Volta a chave da coleção: as outras instâncias criadas pelos testes leem a do banco.
            await ApagarChaveAsync();
            await ConfiguracaoInfraestrutura.ObterOuCriarChaveJwtAsync(Banco.Fabrica.Services, () => original!);
        }
    }

    private async Task<string?> LerChaveAsync()
    {
        using var escopo = Banco.Fabrica.Services.CreateScope();
        var contexto = escopo.ServiceProvider.GetRequiredService<CatalogoDbContext>();
        return await contexto.ChavesJwt.Where(c => c.Id == ChaveJwtTabela.IdUnico).Select(c => c.Chave).SingleOrDefaultAsync();
    }

    private async Task ApagarChaveAsync()
    {
        using var escopo = Banco.Fabrica.Services.CreateScope();
        await escopo.ServiceProvider.GetRequiredService<CatalogoDbContext>().ChavesJwt.ExecuteDeleteAsync();
    }
}
