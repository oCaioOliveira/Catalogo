using Catalogo.Domain.Exclusao;
using Catalogo.Domain.Metadados;

namespace Catalogo.Domain.Tests.Exclusao;

// RN16 — plano de exclusão. O grafo fica em memória: cada teste descreve "quem aponta para quem".
public class PlanejadorExclusaoTests
{
    private readonly List<ReferenciaEntrante> _grafo = [];

    // Nomes inspirados na listagem de Testes de Desempenho, só para o grafo ficar legível.
    private static readonly Guid Relatorio = Guid.NewGuid();
    private static readonly Guid Cenario1 = Guid.NewGuid();
    private static readonly Guid Cenario2 = Guid.NewGuid();
    private static readonly Guid Detalhe = Guid.NewGuid();
    private static readonly Guid Endpoint = Guid.NewGuid();
    private static readonly Guid Link = Guid.NewGuid();

    [Fact]
    public void Planejar_SemNinguemApontando_ExcluiSoOAlvo()
    {
        var plano = Planejar(Relatorio);

        Assert.True(plano.PodeExcluir);
        Assert.Equal([Relatorio], plano.OrdemExclusao);
    }

    [Fact]
    public void Planejar_CascataEmVariosNiveis_IncluiTodosEExcluiDosFilhosParaOPai()
    {
        Aponta(Cenario1, Relatorio, AcaoExclusao.Cascata);
        Aponta(Cenario2, Relatorio, AcaoExclusao.Cascata);
        Aponta(Detalhe, Cenario1, AcaoExclusao.Cascata);   // neto

        var plano = Planejar(Relatorio);

        Assert.True(plano.PodeExcluir);
        Assert.Equal(4, plano.OrdemExclusao.Count);
        AntesDe(plano, Detalhe, Cenario1);
        AntesDe(plano, Cenario1, Relatorio);
        AntesDe(plano, Cenario2, Relatorio);
        Assert.Equal(Relatorio, plano.OrdemExclusao[^1]);
    }

    [Fact]
    public void Planejar_RestringirNoPrimeiroNivel_Bloqueia()
    {
        Aponta(Endpoint, Relatorio, AcaoExclusao.Restringir);

        var plano = Planejar(Relatorio);

        Assert.False(plano.PodeExcluir);
        Assert.Empty(plano.OrdemExclusao);
        Assert.Equal(Endpoint, Assert.Single(plano.Bloqueios).RegistroOrigemId);
    }

    [Fact]
    public void Planejar_RestringirEmNivelProfundo_BloqueiaTudo()
    {
        // Relatório ← Cenário (cascata) ← Endpoint (restringir): nada pode ser excluído.
        Aponta(Cenario1, Relatorio, AcaoExclusao.Cascata);
        Aponta(Endpoint, Cenario1, AcaoExclusao.Restringir);

        var plano = Planejar(Relatorio);

        Assert.False(plano.PodeExcluir);
        Assert.Empty(plano.OrdemExclusao);
        var bloqueio = Assert.Single(plano.Bloqueios);
        Assert.Equal(Endpoint, bloqueio.RegistroOrigemId);
        Assert.Equal(Cenario1, bloqueio.RegistroDestinoId);
    }

    [Fact]
    public void Planejar_RestringirDeQuemTambemSeraExcluido_NaoBloqueia()
    {
        // O Link aponta para o Relatório (cascata) e para o Cenário (restringir). Excluindo o Relatório,
        // o Link sai junto, então o RESTRINGIR dele não bloqueia.
        Aponta(Cenario1, Relatorio, AcaoExclusao.Cascata);
        Aponta(Link, Relatorio, AcaoExclusao.Cascata);
        Aponta(Link, Cenario1, AcaoExclusao.Restringir);

        var plano = Planejar(Relatorio);

        Assert.True(plano.PodeExcluir);
        Assert.Equal(3, plano.OrdemExclusao.Count);
        AntesDe(plano, Link, Cenario1);
        AntesDe(plano, Cenario1, Relatorio);
    }

    [Fact]
    public void Planejar_RestringirAvaliadoNoFinal_MesmoQueAOrigemEntreNoPlanoDepois()
    {
        // O Cenário2 é encontrado primeiro como RESTRINGIR (aponta para o Cenário1) e só depois entra
        // no plano por CASCATA (aponta para o Relatório). Não pode bloquear.
        Aponta(Cenario1, Relatorio, AcaoExclusao.Cascata);
        Aponta(Cenario2, Cenario1, AcaoExclusao.Restringir);
        Aponta(Cenario2, Detalhe, AcaoExclusao.Cascata);
        Aponta(Detalhe, Relatorio, AcaoExclusao.Cascata);

        var plano = Planejar(Relatorio);

        Assert.True(plano.PodeExcluir);
        Assert.Equal(4, plano.OrdemExclusao.Count);
    }

    [Fact]
    public void Planejar_CicloDeCascata_TerminaEQuebraOCiclo()
    {
        // Cenário1 ↔ Cenário2 em cascata, e Cenário1 aponta para o Relatório.
        Aponta(Cenario1, Relatorio, AcaoExclusao.Cascata);
        Aponta(Cenario2, Cenario1, AcaoExclusao.Cascata);
        Aponta(Cenario1, Cenario2, AcaoExclusao.Cascata);

        var plano = Planejar(Relatorio);

        Assert.True(plano.PodeExcluir);
        Assert.Equal(3, plano.OrdemExclusao.Distinct().Count());
        Assert.Equal(Relatorio, plano.OrdemExclusao[^1]);
        // Uma referência do ciclo é apagada antes, para existir uma ordem válida.
        Assert.Single(plano.ReferenciasARemoverAntes);
    }

    [Fact]
    public void Planejar_RegistroQueApontaParaSiMesmo_NaoTravaNemQuebraCiclo()
    {
        Aponta(Relatorio, Relatorio, AcaoExclusao.Cascata);

        var plano = Planejar(Relatorio);

        Assert.Equal([Relatorio], plano.OrdemExclusao);
        Assert.Empty(plano.ReferenciasARemoverAntes);
    }

    [Fact]
    public void Planejar_CadaRegistroEhConsultadoUmaUnicaVez()
    {
        // Ciclos não fazem o planejador consultar o mesmo registro de novo.
        Aponta(Cenario1, Relatorio, AcaoExclusao.Cascata);
        Aponta(Relatorio, Cenario1, AcaoExclusao.Cascata);
        var consultados = new List<Guid>();

        PlanejadorExclusao.Planejar(Relatorio, ids =>
        {
            consultados.AddRange(ids);
            return _grafo.Where(r => ids.Contains(r.RegistroDestinoId)).ToList();
        });

        Assert.Equal(consultados.Count, consultados.Distinct().Count());
    }

    [Fact]
    public void Concluir_ComPendentes_Falha()
    {
        Aponta(Cenario1, Relatorio, AcaoExclusao.Cascata);
        var planejador = new PlanejadorExclusao(Relatorio);
        planejador.Registrar([Relatorio], _grafo);

        Assert.Throws<InvalidOperationException>(planejador.Concluir);
    }

    private void Aponta(Guid origem, Guid destino, AcaoExclusao acao) =>
        _grafo.Add(new ReferenciaEntrante(origem, Guid.Empty, Guid.NewGuid(), destino, acao));

    private PlanoExclusao Planejar(Guid alvo) =>
        PlanejadorExclusao.Planejar(alvo, ids => _grafo.Where(r => ids.Contains(r.RegistroDestinoId)).ToList());

    private static void AntesDe(PlanoExclusao plano, Guid primeiro, Guid depois) =>
        Assert.True(plano.OrdemExclusao.ToList().IndexOf(primeiro) < plano.OrdemExclusao.ToList().IndexOf(depois),
            "A ordem de exclusão precisa ser dos filhos para o pai.");
}
