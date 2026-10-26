using Catalogo.Application.CasosDeUso.Acesso;
using Catalogo.Application.Tests.Dubles;
using Catalogo.Domain.Excecoes;

namespace Catalogo.Application.Tests.CasosDeUso;

// Administrador inicial por script (pacote de entrega dos DBAs): RN25 (troca obrigatória) e RN32 (histórico, sem senha).
public class GerarSqlAdministradorInicialTests
{
    private readonly GerarSqlAdministradorInicial _caso = new(new HasherSenhaFalso());

    [Fact]
    public void Executar_GravaSoOHash_AdministradorAtivoComTrocaObrigatoria()
    {
        var script = _caso.Executar("C123456", "Fulano", "SenhaForte123", aceitarSenhaForaDaPolitica: false);

        Assert.Contains("N'hash:SenhaForte123'", script);
        Assert.DoesNotContain("'SenhaForte123'", script);
        Assert.Contains("N'C123456', N'Fulano', N'hash:SenhaForte123',\n", script.ReplaceLineEndings("\n"));
        Assert.Contains("N'ADMINISTRADOR', 1, 1);", script);   // ativo e DeveTrocarSenha
        Assert.Contains("INSERT catalogo.HistoricoAlteracao", script);
        Assert.Contains("N'USUARIO'", script);
        Assert.Contains("N'CRIACAO'", script);
        Assert.Contains("\"equipes\":[]", script);
        Assert.Contains("WHERE Matricula = N'C123456'", script);
        Assert.Contains("SET NOEXEC ON", script);
        Assert.DoesNotContain("$(", script);
    }

    [Fact]
    public void Executar_SenhaForaDaPolitica_RecusaPorPadrao()
    {
        var erro = Assert.Throws<ValidacaoRegistroException>(() =>
            _caso.Executar("C123456", "Fulano", "Curta#1a", aceitarSenhaForaDaPolitica: false));

        Assert.Contains("senha", erro.Erros.Keys);
    }

    [Fact]
    public void Executar_SenhaForaDaPolitica_SoComAExcecaoExplicita()
    {
        var script = _caso.Executar("C123456", "Fulano", "Curta#1a", aceitarSenhaForaDaPolitica: true);

        Assert.Contains("N'hash:Curta#1a'", script);
        Assert.Contains("N'ADMINISTRADOR', 1, 1);", script);
    }

    [Theory]
    [InlineData(null, "Fulano", "SenhaForte123", "matricula")]
    [InlineData("C123456", "", "SenhaForte123", "nome")]
    [InlineData("C123456", "Fulano", "", "senha")]
    public void Executar_DadosFaltando_Recusa(string? matricula, string? nome, string? senha, string campo)
    {
        var erro = Assert.Throws<ValidacaoRegistroException>(() => _caso.Executar(matricula, nome, senha, aceitarSenhaForaDaPolitica: true));

        Assert.Contains(campo, erro.Erros.Keys);
    }

    [Fact]
    public void Executar_AspasNoNome_FicamDobradas()
    {
        var script = _caso.Executar("C123456", "D'Ávila", "SenhaForte123", aceitarSenhaForaDaPolitica: false);

        Assert.Contains("N'D''Ávila'", script);
    }
}
