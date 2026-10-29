using System.Text.Json.Nodes;
using Catalogo.Domain.Metadados;
using Catalogo.Domain.Registros;
using Catalogo.Domain.Tests.Apoio;
using Catalogo.Domain.Validacao;

namespace Catalogo.Domain.Tests.Validacao;

// RN08 — mesmo desenho do seed: o endpoint do cenário precisa ser do projeto do relatório.
public class ValidadorDependenciasTests
{
    private static readonly Guid RelatorioEntidadeId = Guid.NewGuid();
    private static readonly Guid EndpointEntidadeId = Guid.NewGuid();
    private static readonly Guid ProjetoEntidadeId = Guid.NewGuid();

    private readonly Campo _relatorioProjeto = Metadado.Campo("projeto", TipoDado.Referencia,
        entidadeReferenciadaId: ProjetoEntidadeId, entidadeId: RelatorioEntidadeId);
    private readonly Campo _endpointProjeto = Metadado.Campo("projeto", TipoDado.Referencia,
        entidadeReferenciadaId: ProjetoEntidadeId, entidadeId: EndpointEntidadeId);
    private readonly Campo _cenarioRelatorio = Metadado.Campo("relatorio_teste", TipoDado.Referencia,
        entidadeReferenciadaId: RelatorioEntidadeId, ordem: 1);
    private readonly Campo _cenarioEndpoint = Metadado.Campo("endpoint_servico", TipoDado.Referencia,
        entidadeReferenciadaId: EndpointEntidadeId, ordem: 2);

    private Entidade Cenario() => Metadado.Entidade(
        [_cenarioRelatorio, _cenarioEndpoint],
        dependencias:
        [
            new CampoDependencia
            {
                Campo = _cenarioEndpoint,
                CampoVia = _cenarioRelatorio,
                CampoOrigem = _relatorioProjeto,
                CampoAlvo = _endpointProjeto,
            },
        ]);

    [Fact]
    public void Validar_EndpointDoMesmoProjetoDoRelatorio_NaoGeraErro()
    {
        var projeto = Guid.NewGuid();
        var (registro, referenciados) = Montar(projetoDoRelatorio: projeto, projetoDoEndpoint: projeto);
        var erros = new ErrosPorCampo();

        ValidadorDependencias.Validar(Cenario(), registro, referenciados, erros);

        Assert.False(erros.PossuiErros);
    }

    [Fact]
    public void Validar_EndpointDeOutroProjeto_GeraErroNoCampoDependente()
    {
        var (registro, referenciados) = Montar(projetoDoRelatorio: Guid.NewGuid(), projetoDoEndpoint: Guid.NewGuid());
        var erros = new ErrosPorCampo();

        ValidadorDependencias.Validar(Cenario(), registro, referenciados, erros);

        var mensagem = Assert.Single(erros.ParaDicionario()["endpoint_servico"]);
        Assert.Contains("relatorio_teste", mensagem);
    }

    [Fact]
    public void Validar_RelatorioSemProjeto_NaoHaFiltroARespeitar()
    {
        var (registro, referenciados) = Montar(projetoDoRelatorio: null, projetoDoEndpoint: Guid.NewGuid());
        var erros = new ErrosPorCampo();

        ValidadorDependencias.Validar(Cenario(), registro, referenciados, erros);

        Assert.False(erros.PossuiErros);
    }

    [Fact]
    public void Validar_CampoViaNulo_ComparaComCampoDoProprioRegistro()
    {
        // Variante sem salto: o próprio registro tem o campo "projeto".
        var proprioProjeto = Metadado.Campo("projeto", TipoDado.Referencia, entidadeReferenciadaId: ProjetoEntidadeId, ordem: 1);
        var endpoint = Metadado.Campo("endpoint_servico", TipoDado.Referencia, entidadeReferenciadaId: EndpointEntidadeId, ordem: 2);
        var entidade = Metadado.Entidade([proprioProjeto, endpoint], dependencias:
            [new CampoDependencia { Campo = endpoint, CampoOrigem = proprioProjeto, CampoAlvo = _endpointProjeto }]);

        var registroEndpoint = RegistroCom(EndpointEntidadeId, (_endpointProjeto, Guid.NewGuid()));
        var registro = new RegistroValidado([],
            [new ReferenciaValidada(proprioProjeto, Guid.NewGuid()), new ReferenciaValidada(endpoint, registroEndpoint.Id)]);
        var erros = new ErrosPorCampo();

        ValidadorDependencias.Validar(entidade, registro, new Dictionary<Guid, Registro> { [registroEndpoint.Id] = registroEndpoint }, erros);

        Assert.Contains("deste registro", Assert.Single(erros.ParaDicionario()["endpoint_servico"]));
    }

    private (RegistroValidado Registro, Dictionary<Guid, Registro> Referenciados) Montar(Guid? projetoDoRelatorio, Guid projetoDoEndpoint)
    {
        var relatorio = projetoDoRelatorio is Guid projeto
            ? RegistroCom(RelatorioEntidadeId, (_relatorioProjeto, projeto))
            : RegistroCom(RelatorioEntidadeId);
        var endpoint = RegistroCom(EndpointEntidadeId, (_endpointProjeto, projetoDoEndpoint));

        var registro = new RegistroValidado(new JsonObject(),
        [
            new ReferenciaValidada(_cenarioRelatorio, relatorio.Id),
            new ReferenciaValidada(_cenarioEndpoint, endpoint.Id),
        ]);

        return (registro, new Dictionary<Guid, Registro> { [relatorio.Id] = relatorio, [endpoint.Id] = endpoint });
    }

    private static Registro RegistroCom(Guid entidadeId, params (Campo Campo, Guid Destino)[] referencias)
    {
        var registro = new Registro { Id = Guid.NewGuid(), EntidadeId = entidadeId };
        foreach (var (campo, destino) in referencias)
        {
            registro.Referencias.Add(new RegistroReferencia
            {
                RegistroOrigemId = registro.Id,
                EntidadeOrigemId = entidadeId,
                CampoId = campo.Id,
                RegistroDestinoId = destino,
                EntidadeDestinoId = ProjetoEntidadeId,
            });
        }
        return registro;
    }
}
