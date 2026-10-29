using Catalogo.Domain.Excecoes;
using Catalogo.Domain.Painel;
using Catalogo.Domain.Validacao;

namespace Catalogo.Domain.Tests.Painel;

// RN80: "de" e "ate" são datas no fuso de negócio; os limites viram UTC com o deslocamento FIXO do fuso.
public class PeriodoPainelTests
{
    private static readonly TimeZoneInfo SaoPaulo = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");

    private static PeriodoPainel Interpretar(params (string Chave, string Valor)[] parametros)
    {
        var erros = new ErrosPorCampo();
        var periodo = PeriodoPainel.Interpretar(parametros.Select(p => new KeyValuePair<string, string>(p.Chave, p.Valor)).ToList(), SaoPaulo, erros);
        erros.LancarSeHouverErros();
        return periodo;
    }

    [Fact]
    public void SemDatas_SemLimites()
    {
        var periodo = Interpretar();

        Assert.Null(periodo.InicioUtc);
        Assert.Null(periodo.FimUtc);
        Assert.Equal(TimeSpan.FromHours(-3), periodo.Deslocamento);
    }

    [Fact]
    public void Datas_ViramLimitesUtcComODeslocamentoDoFuso()
    {
        var periodo = Interpretar(("de", "2026-03-01"), ("ate", "2026-03-31"));

        // 00:00 em São Paulo = 03:00 UTC; o fim é EXCLUSIVO: 00:00 do dia seguinte a "ate".
        Assert.Equal(new DateTime(2026, 3, 1, 3, 0, 0, DateTimeKind.Utc), periodo.InicioUtc);
        Assert.Equal(new DateTime(2026, 4, 1, 3, 0, 0, DateTimeKind.Utc), periodo.FimUtc);
        Assert.Equal(DateTimeKind.Utc, periodo.InicioUtc!.Value.Kind);
    }

    [Fact]
    public void RegistroDas01hUtcDoDia1_FicaNoMesAnterior()
    {
        // 2026-04-01 01:00 UTC = 2026-03-31 22:00 em São Paulo: está em março, não em abril.
        var marco = Interpretar(("de", "2026-03-01"), ("ate", "2026-03-31"));
        var instante = new DateTime(2026, 4, 1, 1, 0, 0, DateTimeKind.Utc);

        Assert.True(instante >= marco.InicioUtc && instante < marco.FimUtc);
    }

    [Theory]
    [InlineData("de", "01/03/2026")]
    [InlineData("ate", "2026-02-30")]
    [InlineData("de", "2026-03-01T00:00:00")]
    public void DataInvalida_DevolveErroNoParametro(string parametro, string valor)
    {
        var erro = Assert.Throws<ValidacaoRegistroException>(() => Interpretar((parametro, valor)));

        Assert.Contains("aaaa-mm-dd", Assert.Single(erro.Erros[parametro]));
    }

    [Fact]
    public void DeDepoisDeAte_Recusa()
    {
        var erro = Assert.Throws<ValidacaoRegistroException>(() => Interpretar(("de", "2026-04-01"), ("ate", "2026-03-31")));

        Assert.Contains("posterior", Assert.Single(erro.Erros["ate"]));
    }

    [Fact]
    public void MesmoDia_Aceita()
    {
        var periodo = Interpretar(("de", "2026-03-15"), ("ate", "2026-03-15"));

        Assert.Equal(TimeSpan.FromDays(1), periodo.FimUtc - periodo.InicioUtc);
    }
}
