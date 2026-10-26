using Catalogo.Domain.Metadados;

namespace Catalogo.Domain.Exclusao;

/// <summary>
/// Uma linha de RegistroReferencia vista "de trás para frente": o registro de origem aponta,
/// pelo campo, para o registro de destino, que é quem está sendo avaliado para exclusão.
/// <see cref="Acao"/> é a AcaoExclusao do campo.
/// </summary>
public sealed record ReferenciaEntrante(
    Guid RegistroOrigemId,
    Guid EntidadeOrigemId,
    Guid CampoId,
    Guid RegistroDestinoId,
    AcaoExclusao Acao);

/// <summary>
/// Monta o plano de exclusão de um registro (RN11 e RN16). Código puro, sem banco: quem usa
/// busca as referências e entrega aos poucos, num laço:
/// <code>
/// var planejador = new PlanejadorExclusao(alvoId);
/// for (var pendentes = planejador.ObterPendentes(); pendentes.Count &gt; 0; pendentes = planejador.ObterPendentes())
///     planejador.Registrar(pendentes, await BuscarReferenciasQueApontamPara(pendentes));
/// var plano = planejador.Concluir();
/// </code>
/// Regras:
///   - CASCATA: o registro de origem entra no plano e também é expandido (recursivo).
///   - RESTRINGIR: bloqueia, A MENOS QUE o registro de origem também esteja no plano.
///   - Ciclos: cada registro é expandido uma única vez.
///   - Qualquer bloqueio, em qualquer nível, impede a exclusão de tudo.
/// </summary>
public sealed class PlanejadorExclusao
{
    private readonly Guid _alvoId;
    // Ordem em que os registros entraram no plano (o alvo é o primeiro).
    private readonly List<Guid> _noPlano = [];
    private readonly HashSet<Guid> _noPlanoConjunto = [];
    private readonly HashSet<Guid> _expandidos = [];
    private readonly List<ReferenciaEntrante> _referencias = [];

    public PlanejadorExclusao(Guid registroAlvoId)
        : this([registroAlvoId])
    {
    }

    /// <summary>
    /// Plano para excluir VÁRIOS registros juntos (ex.: desfazer uma importação, RN103). As mesmas regras: uma referência RESTRINGIR
    /// entre dois alvos não bloqueia, porque os dois saem. O primeiro alvo é o <see cref="PlanoExclusao.RegistroAlvoId"/>.
    /// </summary>
    public PlanejadorExclusao(IReadOnlyCollection<Guid> registrosAlvo)
    {
        if (registrosAlvo.Count == 0)
            throw new ArgumentException("Informe pelo menos um registro.", nameof(registrosAlvo));
        _alvoId = registrosAlvo.First();
        foreach (var id in registrosAlvo)
            Incluir(id);
    }

    /// <summary>Atalho para quando todas as referências já estão em memória (ex.: testes).</summary>
    public static PlanoExclusao Planejar(
        Guid registroAlvoId, Func<IReadOnlyCollection<Guid>, IEnumerable<ReferenciaEntrante>> buscarReferenciasQueApontamPara)
    {
        var planejador = new PlanejadorExclusao(registroAlvoId);
        for (var pendentes = planejador.ObterPendentes(); pendentes.Count > 0; pendentes = planejador.ObterPendentes())
            planejador.Registrar(pendentes, buscarReferenciasQueApontamPara(pendentes));

        return planejador.Concluir();
    }

    /// <summary>Registros do plano cujas referências entrantes ainda não foram buscadas.</summary>
    public IReadOnlyCollection<Guid> ObterPendentes() => _noPlano.Where(id => !_expandidos.Contains(id)).ToList();

    /// <param name="expandidos">Os ids que foram consultados (normalmente o retorno de <see cref="ObterPendentes"/>).</param>
    /// <param name="referenciasQueApontamParaEles">Todas as referências cujo destino está em <paramref name="expandidos"/>.</param>
    public void Registrar(IReadOnlyCollection<Guid> expandidos, IEnumerable<ReferenciaEntrante> referenciasQueApontamParaEles)
    {
        foreach (var id in expandidos)
            _expandidos.Add(id);

        foreach (var referencia in referenciasQueApontamParaEles)
        {
            _referencias.Add(referencia);

            // CASCATA: quem aponta também será excluído (e será expandido na próxima volta).
            if (referencia.Acao == AcaoExclusao.Cascata)
                Incluir(referencia.RegistroOrigemId);
        }
    }

    public PlanoExclusao Concluir()
    {
        if (ObterPendentes().Count > 0)
            throw new InvalidOperationException("Ainda há registros do plano sem as referências buscadas.");

        // RESTRINGIR só bloqueia se quem aponta NÃO vai ser excluído junto. Como um registro pode
        // entrar no plano por outro caminho depois, a avaliação é feita só no final.
        var bloqueios = _referencias
            .Where(r => r.Acao == AcaoExclusao.Restringir && !_noPlanoConjunto.Contains(r.RegistroOrigemId))
            .Distinct()
            .ToList();

        if (bloqueios.Count > 0)
            return new PlanoExclusao(_alvoId, [], [], bloqueios);

        var (ordem, quebrasDeCiclo) = OrdenarFilhosAntesDosPais();
        return new PlanoExclusao(_alvoId, ordem, quebrasDeCiclo, []);
    }

    private void Incluir(Guid registroId)
    {
        if (_noPlanoConjunto.Add(registroId))
            _noPlano.Add(registroId);
    }

    /// <summary>
    /// Ordem de exclusão: um registro só sai depois de todos os registros do plano que apontam
    /// para ele (a FK de destino não tem CASCADE). Referência de um registro para ele mesmo não
    /// conta: o próprio DELETE apaga as referências de origem.
    /// Se sobrar um ciclo (A → B → A), as referências que o fecham são devolvidas para serem
    /// apagadas antes, e o ciclo vira uma sequência comum.
    /// </summary>
    private (List<Guid> Ordem, List<ReferenciaEntrante> QuebrasDeCiclo) OrdenarFilhosAntesDosPais()
    {
        // Referências entre registros do plano (todas, já que nenhuma bloqueia).
        var internas = _referencias
            .Where(r => r.RegistroOrigemId != r.RegistroDestinoId && _noPlanoConjunto.Contains(r.RegistroOrigemId))
            .Distinct()
            .ToList();

        var restantes = new List<Guid>(_noPlano);
        var ordem = new List<Guid>();
        var quebras = new List<ReferenciaEntrante>();
        var referenciasAtivas = new List<ReferenciaEntrante>(internas);

        while (restantes.Count > 0)
        {
            // Pode sair quem não é mais apontado por ninguém que ainda vai ser excluído.
            // Entre os livres, sai primeiro o que entrou no plano por último (o "mais filho").
            var indiceLivre = restantes.FindLastIndex(id => !referenciasAtivas.Any(r => r.RegistroDestinoId == id));
            Guid livre;

            if (indiceLivre >= 0)
            {
                livre = restantes[indiceLivre];
            }
            else
            {
                // Ciclo: escolhe o que entrou por último e apaga antes as referências para ele.
                livre = restantes[^1];
                var fechamCiclo = referenciasAtivas.Where(r => r.RegistroDestinoId == livre).ToList();
                quebras.AddRange(fechamCiclo);
                referenciasAtivas.RemoveAll(r => r.RegistroDestinoId == livre);
            }

            ordem.Add(livre);
            restantes.Remove(livre);
            // Ao excluir "livre", somem as referências em que ele é a origem.
            referenciasAtivas.RemoveAll(r => r.RegistroOrigemId == livre);
        }

        return (ordem, quebras);
    }
}

/// <summary>Resultado do <see cref="PlanejadorExclusao"/>.</summary>
public sealed class PlanoExclusao(
    Guid registroAlvoId,
    IReadOnlyList<Guid> ordemExclusao,
    IReadOnlyList<ReferenciaEntrante> referenciasARemoverAntes,
    IReadOnlyList<ReferenciaEntrante> bloqueios)
{
    public Guid RegistroAlvoId { get; } = registroAlvoId;

    /// <summary>Registros a excluir, dos filhos para o pai. Vazio quando há bloqueios.</summary>
    public IReadOnlyList<Guid> OrdemExclusao { get; } = ordemExclusao;

    /// <summary>Referências que fecham ciclos: precisam ser apagadas antes dos registros.</summary>
    public IReadOnlyList<ReferenciaEntrante> ReferenciasARemoverAntes { get; } = referenciasARemoverAntes;

    /// <summary>Referências RESTRINGIR de registros que NÃO seriam excluídos.</summary>
    public IReadOnlyList<ReferenciaEntrante> Bloqueios { get; } = bloqueios;

    public bool PodeExcluir => Bloqueios.Count == 0;
}
