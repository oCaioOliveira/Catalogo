using Catalogo.Domain.Acesso;
using Catalogo.Domain.Aprovacao;
using Catalogo.Domain.Excecoes;
using Catalogo.Domain.Registros;

namespace Catalogo.Domain.Tests.Aprovacao;

// Transições de situação (RN34 a RN36).
public class FluxoAprovacaoTests
{
    private static readonly DateTime Agora = new(2026, 10, 21, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Gestor = Guid.NewGuid();
    private const string JustificativaValida = "Dados conferidos com o relatório original.";

    [Theory]
    [InlineData(Perfil.Externo, true, SituacaoAprovacao.Pendente)]
    [InlineData(Perfil.Externo, false, SituacaoAprovacao.Aprovado)]   // sem aprovação nem chega a criar, mas a regra é essa
    [InlineData(Perfil.Interno, true, SituacaoAprovacao.Aprovado)]
    [InlineData(Perfil.Gestor, true, SituacaoAprovacao.Aprovado)]
    [InlineData(Perfil.Administrador, true, SituacaoAprovacao.Aprovado)]
    public void SituacaoInicial_PorPerfil(Perfil perfil, bool requerAprovacao, SituacaoAprovacao esperada)
    {
        // RN34
        Assert.Equal(esperada, FluxoAprovacao.SituacaoInicial(Usuario(perfil), requerAprovacao));
    }

    [Theory]
    [InlineData(DecisaoAprovacao.Aprovar, SituacaoAprovacao.Aprovado)]
    [InlineData(DecisaoAprovacao.Rejeitar, SituacaoAprovacao.Rejeitado)]
    public void Decidir_Pendente_GravaSituacaoEUltimaDecisao(DecisaoAprovacao decisao, SituacaoAprovacao esperada)
    {
        // RN36
        var registro = new Registro { SituacaoAprovacao = SituacaoAprovacao.Pendente };

        FluxoAprovacao.Decidir(registro, decisao, "  " + JustificativaValida + "  ", Gestor, Agora);

        Assert.Equal(esperada, registro.SituacaoAprovacao);
        Assert.Equal(Gestor, registro.DecididoPorId);
        Assert.Equal(Agora, registro.DecididoEm);
        Assert.Equal(JustificativaValida, registro.JustificativaDecisao);
        Assert.Equal(decisao, FluxoAprovacao.UltimaDecisao(registro));
    }

    [Theory]
    [InlineData(SituacaoAprovacao.Aprovado)]
    [InlineData(SituacaoAprovacao.Rejeitado)]
    public void Decidir_RegistroNaoPendente_EhRegraDeNegocio(SituacaoAprovacao situacao)
    {
        // RN36: 409
        var registro = new Registro { SituacaoAprovacao = situacao };

        var erro = Assert.Throws<RegraDeNegocioException>(() =>
            FluxoAprovacao.Decidir(registro, DecisaoAprovacao.Aprovar, JustificativaValida, Gestor, Agora));

        Assert.Equal("REGISTRO_NAO_PENDENTE", erro.Codigo);
        Assert.Equal(situacao, registro.SituacaoAprovacao);
    }

    [Theory]
    [InlineData(null, "Obrigatória")]
    [InlineData("   ", "Obrigatória")]
    [InlineData("123456789", "pelo menos 10")]   // 9 caracteres
    public void ValidarJustificativa_AusenteOuCurta(string? justificativa, string trecho)
    {
        Assert.Contains(trecho, Assert.Single(FluxoAprovacao.ValidarJustificativa(justificativa)));
    }

    [Fact]
    public void ValidarJustificativa_Limites()
    {
        Assert.Empty(FluxoAprovacao.ValidarJustificativa(new string('x', 10)));
        Assert.Empty(FluxoAprovacao.ValidarJustificativa(new string('x', 1000)));
        Assert.Contains("no máximo 1000", Assert.Single(FluxoAprovacao.ValidarJustificativa(new string('x', 1001))));
    }

    [Fact]
    public void Decidir_SemJustificativa_NaoMudaNada()
    {
        var registro = new Registro { SituacaoAprovacao = SituacaoAprovacao.Pendente };

        var erro = Assert.Throws<ValidacaoRegistroException>(() =>
            FluxoAprovacao.Decidir(registro, DecisaoAprovacao.Rejeitar, "curta", Gestor, Agora));

        Assert.True(erro.Erros.ContainsKey("justificativa"));
        Assert.Equal(SituacaoAprovacao.Pendente, registro.SituacaoAprovacao);
        Assert.Null(registro.DecididoEm);
    }

    [Fact]
    public void SituacaoAposAlteracao_ExternoAutorDeRejeitado_ReenviaParaPendente()
    {
        // RN35
        var externo = Usuario(Perfil.Externo);
        var registro = new Registro { SituacaoAprovacao = SituacaoAprovacao.Rejeitado, CriadoPorId = externo.Id };

        Assert.Equal(SituacaoAprovacao.Pendente, FluxoAprovacao.SituacaoAposAlteracao(externo, registro));
    }

    [Theory]
    [InlineData(Perfil.Interno, SituacaoAprovacao.Rejeitado)]   // outro perfil alterando um rejeitado: continua rejeitado
    [InlineData(Perfil.Gestor, SituacaoAprovacao.Pendente)]     // alterar um pendente não muda a situação
    [InlineData(Perfil.Externo, SituacaoAprovacao.Pendente)]
    [InlineData(Perfil.Interno, SituacaoAprovacao.Aprovado)]
    public void SituacaoAposAlteracao_DemaisCasos_NaoMuda(Perfil perfil, SituacaoAprovacao situacao)
    {
        var usuario = Usuario(perfil);
        var registro = new Registro { SituacaoAprovacao = situacao, CriadoPorId = usuario.Id };

        Assert.Equal(situacao, FluxoAprovacao.SituacaoAposAlteracao(usuario, registro));
    }

    [Fact]
    public void UltimaDecisao_ReenviadoContinuaMostrandoARejeicao()
    {
        // Depois do reenvio, a última decisão (a rejeição) continua no registro até a próxima decisão.
        var registro = new Registro { SituacaoAprovacao = SituacaoAprovacao.Pendente, DecididoEm = Agora };

        Assert.Equal(DecisaoAprovacao.Rejeitar, FluxoAprovacao.UltimaDecisao(registro));
        Assert.Null(FluxoAprovacao.UltimaDecisao(new Registro { SituacaoAprovacao = SituacaoAprovacao.Pendente }));
    }

    [Theory]
    [InlineData("aprovar", true)]
    [InlineData("REJEITAR", true)]
    [InlineData("APROVADO", false)]
    [InlineData(null, false)]
    public void AprovacaoConversor_TentarConverterDecisao(string? texto, bool valido)
    {
        Assert.Equal(valido, AprovacaoConversor.TentarConverterDecisao(texto, out _));
    }

    private static UsuarioAtual Usuario(Perfil perfil) => new()
    {
        Id = Guid.NewGuid(),
        Matricula = "m",
        Nome = "n",
        Perfil = perfil,
        Equipes = new HashSet<Guid>(),
    };
}
