using Catalogo.Application.CasosDeUso.Acesso;
using Catalogo.Domain.Excecoes;

namespace Catalogo.Application.Tests.CasosDeUso;

// Administrador inicial por script (pacote de entrega dos DBAs): sem senha (login pelo Windows, RN23) e RN32 (histórico).
public class GerarSqlAdministradorInicialTests
{
    private readonly GerarSqlAdministradorInicial _caso = new();

    [Fact]
    public void Executar_AdministradorAtivoSemSenha_ComHistoricoEProtecoes()
    {
        var script = _caso.Executar("C123456", "Fulano");

        Assert.Contains("INSERT cat.CATTB008_USUARIO (NU_NSU_USUARIO, CO_MATRICULA, NO_USUARIO, NO_PERFIL, IC_ATIVO_USR)", script);
        Assert.Contains("N'C123456', N'Fulano', N'ADMINISTRADOR', 1);", script);
        Assert.DoesNotContain("SENHA", script.ToUpperInvariant().Replace("SEM AS COLUNAS DE SENHA", "").Replace("SEM SENHA", ""));
        Assert.Contains("INSERT cat.CATTB015_HISTORICO_ALTERACAO", script);
        Assert.Contains("N'USUARIO'", script);
        Assert.Contains("N'CRIACAO'", script);
        Assert.Contains("\"equipes\":[]", script);
        Assert.Contains("WHERE CO_MATRICULA = N'C123456'", script);
        Assert.Contains($"NU_NSU_VERSAO >= {GerarSqlAdministradorInicial.VersaoMinimaEstrutura}", script);
        Assert.Contains("SET NOEXEC ON", script);
        Assert.DoesNotContain("$(", script);
    }

    [Theory]
    [InlineData(null, "Fulano", "matricula")]
    [InlineData("C123456", "", "nome")]
    [InlineData("C 123", "Fulano", "matricula")]
    public void Executar_DadosFaltando_Recusa(string? matricula, string? nome, string campo)
    {
        var erro = Assert.Throws<ValidacaoRegistroException>(() => _caso.Executar(matricula, nome));

        Assert.Contains(campo, erro.Erros.Keys);
    }

    [Fact]
    public void Executar_AspasNoNome_FicamDobradas()
    {
        var script = _caso.Executar("C123456", "D'Ávila");

        Assert.Contains("N'D''Ávila'", script);
    }
}
