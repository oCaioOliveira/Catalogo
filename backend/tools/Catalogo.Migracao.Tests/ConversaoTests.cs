using Catalogo.Migracao.Conversao;

namespace Catalogo.Migracao.Tests;

public class RegrasConversaoTests
{
    [Theory]
    [InlineData("https://gid.caixa:9443/ccm/web/projects/Qualidade%20de%20TI#action=com.ibm.team.workitem.viewWorkItem&id=25528904", "25528904")]
    [InlineData("https://gid.caixa:9443/ccm/resource/itemName/com.ibm.team.workitem.WorkItem/21463723", "21463723")]
    public void Rtc_WorkItem_ExtraiONumero(string valor, string numero)
    {
        var rtc = RegrasConversao.Rtc(valor)!;

        Assert.True(rtc.EhWorkItem);
        Assert.Equal(numero, rtc.NumeroWorkItem);
        Assert.Equal(valor, rtc.Url);
    }

    [Fact]
    public void Rtc_LinkDeCommitDoGit_NaoEhWorkItem()
    {
        var rtc = RegrasConversao.Rtc("https://devops.caixa/projetos/Caixa/_git/GECEQ-certificacao-teste/commit/8d20974e6d?refName=refs/heads/SIFUG-desempenho")!;

        Assert.False(rtc.EhWorkItem);
        Assert.Equal("Script no Azure DevOps (commit)", RegrasConversao.TextoDoLink(rtc.Url));
    }

    [Fact]
    public void Rtc_Vazio_Nulo() => Assert.Null(RegrasConversao.Rtc("  "));

    [Theory]
    [InlineData("/teams/O365GRP-GECEQ03/Lists/Relatrios/DispForm.aspx?ID=68", "https://caixa.sharepoint.com/teams/O365GRP-GECEQ03/Lists/Relatrios/DispForm.aspx?ID=68", true)]
    [InlineData("/:li:/t/O365GRP-GECEQ03/E1nKnZ26?e=JnfXqe", "https://caixa.sharepoint.com/:li:/t/O365GRP-GECEQ03/E1nKnZ26?e=JnfXqe", true)]
    [InlineData("https://devops.caixa/x", "https://devops.caixa/x", false)]
    public void LinkAbsoluto_RelativosCompletadosComABase(string valor, string url, bool completado)
    {
        Assert.Equal((url, completado), RegrasConversao.LinkAbsoluto(valor, RegrasConversao.BaseSharePointPadrao));
    }

    [Fact]
    public void Comunidade_TrimEEspacosColapsados()
    {
        Assert.Equal("Box Dados, CRM e Marketing Digital", RegrasConversao.NomeComunidade("\t Box Dados,  CRM e Marketing Digital "));
    }

    [Fact]
    public void Ambiente_ErroDeDigitacaoCorrigido()
    {
        Assert.Equal("Contingência PRD", RegrasConversao.NomeAmbiente("Contigência PRD"));
        Assert.Equal("Piloto PRD", RegrasConversao.NomeAmbiente(" Piloto  PRD"));
    }

    [Fact]
    public void BateriaTituloEExecutadoPor()
    {
        Assert.Equal(23, RegrasConversao.NumeroBateria("23ª"));
        Assert.Equal("API Consulta Campanha — 2ª bateria", RegrasConversao.Titulo("​API Consulta  Campanha ", 2));
        Assert.Equal("Andre Rodrigues Paschoal; Leandro Batista Martins", RegrasConversao.ExecutadoPor("Andre Rodrigues Paschoal;Leandro Batista Martins"));
    }

    [Fact]
    public void Infraestrutura_GrafiaMaisFrequenteEPreferidaNoEmpate()
    {
        var ocorrencias = new[] { "Oracle", "Oracle", "ORACLE", "Broker", "BROKER", "cosmosDB", "CosmosDB", "CosmosDB", "Java" };
        var grafias = RegrasConversao.GrafiasInfraestrutura(ocorrencias);

        Assert.Equal("Java, Oracle, Broker, CosmosDB", RegrasConversao.Infraestrutura("[\"Java\",\"ORACLE\",\"BROKER\",\"cosmosDB\",\"oracle\"]", grafias));
    }

    [Fact]
    public void ItensJson_ArrayDoSharePoint()
    {
        Assert.Equal(["Fila MQ"], RegrasConversao.ItensJson("[\"Fila MQ\"]"));
        Assert.Equal(["texto solto"], RegrasConversao.ItensJson("texto solto"));
    }
}
