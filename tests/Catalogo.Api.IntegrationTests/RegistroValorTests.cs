using System.Text.Json.Nodes;
using Catalogo.Api.IntegrationTests.Apoio;
using Catalogo.Domain.Metadados;
using Catalogo.Domain.Registros;
using Catalogo.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Catalogo.Api.IntegrationTests;

/// <summary>
/// docs/adr/0013: RegistroValor é uma projeção de Registro.Dados e precisa refletir EXATAMENTE os
/// campos simples de cada registro, depois de criar, atualizar e excluir.
/// </summary>
public class RegistroValorTests(BancoDeTestesFixture banco) : TesteDeApi(banco)
{
    [Fact]
    public async Task RegistroValor_DepoisDeCriarAtualizarEExcluir_RefleteOsCamposSimplesDeCadaRegistro()
    {
        // Criar: vários tipos (texto, inteiro, decimal, data e hora, booleano).
        var projeto = await CriarProjeto("A");
        var relatorio = await CriarRelatorio(projeto);
        var endpoint = await CriarEndpoint(projeto, "consulta");
        var cenario = await Criar("cenario_teste", DadosDoCenario(relatorio, endpoint));
        var ambiente = await Criar("ambiente", new JsonObject { ["nome"] = "HML", ["servidor_gerenciador_filas"] = "mq01" });
        await ConferirProjecao();

        // Atualizar: muda um valor e remove um campo opcional.
        var lido = await Ler("ambiente", ambiente);
        await Atualizar("ambiente", ambiente, ComVersao(lido, new JsonObject { ["nome"] = "PRD" }));
        var lidoCenario = await Ler("cenario_teste", cenario);
        await Atualizar("cenario_teste", cenario, ComVersao(lidoCenario, DadosDoCenario(relatorio, endpoint, numero: 7, vazao: 9.5)));
        await ConferirProjecao();

        // Excluir em cascata: o relatório leva o cenário junto.
        await Excluir("relatorio_teste", relatorio);
        await ConferirProjecao();
        Assert.Equal(0, await ContarValores(Guid.Parse(cenario)));
    }

    /// <summary>Para cada registro do banco: as linhas de RegistroValor são exatamente os campos simples do JSON.</summary>
    private async Task ConferirProjecao()
    {
        using var escopo = Banco.Fabrica.Services.CreateScope();
        var contexto = escopo.ServiceProvider.GetRequiredService<CatalogoDbContext>();
        var campos = await contexto.Campos.AsNoTracking().ToDictionaryAsync(c => c.Id);
        var registros = await contexto.Registros.AsNoTracking().Include(r => r.Valores).ToListAsync();

        foreach (var registro in registros)
        {
            var dados = JsonNode.Parse(registro.Dados)!.AsObject();
            var esperado = dados.Select(d => (d.Key, Texto(d.Value!))).OrderBy(x => x.Key).ToList();
            var atual = registro.Valores
                .Select(v => (campos[v.CampoId].Chave, ConversorValorTipado.ParaTexto(campos[v.CampoId].TipoDado, campos[v.CampoId].Escala, v)!))
                .OrderBy(x => x.Chave)
                .ToList();

            Assert.Equal(esperado, atual);
            Assert.All(registro.Valores, v => Assert.NotEqual(TipoDado.Referencia, campos[v.CampoId].TipoDado));
        }
    }

    private async Task<int> ContarValores(Guid registroId)
    {
        using var escopo = Banco.Fabrica.Services.CreateScope();
        var contexto = escopo.ServiceProvider.GetRequiredService<CatalogoDbContext>();
        return await contexto.RegistrosValores.CountAsync(v => v.RegistroId == registroId);
    }

    private static string Texto(JsonNode valor) =>
        valor is JsonValue v && v.TryGetValue<string>(out var texto) ? texto : valor.ToJsonString();
}
