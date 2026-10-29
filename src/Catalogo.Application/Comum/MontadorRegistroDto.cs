using Catalogo.Application.Abstracoes;
using Catalogo.Application.Dtos;
using Catalogo.Application.Mapeamentos;
using Catalogo.Domain.Avaliacoes;
using Catalogo.Domain.Metadados;
using Catalogo.Domain.Registros;

namespace Catalogo.Application.Comum;

/// <summary>
/// Monta os <see cref="RegistroDto"/> buscando EM LOTE os rótulos das referências (RN20), os
/// autores (criadoPor/atualizadoPor/quem decidiu) e as médias das avaliações (RN48): uma consulta
/// para todos, nunca uma por linha (N+1).
/// </summary>
public sealed class MontadorRegistroDto(
    IRegistroRepository registros,
    IUsuarioRepository usuarios,
    IModeloAvaliacaoRepository modelos,
    IAvaliacaoRepository avaliacoes,
    IUsuarioAtual usuarioAtual)
{
    public async Task<RegistroDto> MontarAsync(Registro registro, Entidade entidade, CancellationToken cancellationToken) =>
        (await MontarAsync([registro], entidade, cancellationToken))[0];

    /// <summary>Mantém a ordem de <paramref name="lista"/>.</summary>
    public async Task<IReadOnlyList<RegistroDto>> MontarAsync(
        IReadOnlyList<Registro> lista, Entidade entidade, CancellationToken cancellationToken)
    {
        var idsApontados = lista
            .SelectMany(r => r.Referencias)
            .Select(r => r.RegistroDestinoId)
            .Distinct()
            .ToList();

        var rotulos = idsApontados.Count == 0
            ? new Dictionary<Guid, string?>()
            : await registros.ObterRotulosAsync(idsApontados, cancellationToken);

        var idsAutores = lista
            .SelectMany(r => new[] { r.CriadoPorId, r.AtualizadoPorId, r.DecididoPorId })
            .OfType<Guid>()
            .Distinct()
            .ToList();

        var autores = idsAutores.Count == 0
            ? new Dictionary<Guid, UsuarioResumoDto>()
            : await usuarios.ObterResumosAsync(idsAutores, cancellationToken);

        var avaliacoesPorRegistro = await MontarAvaliacoesAsync(lista, entidade, cancellationToken);

        return lista.Select(r => r.ParaDto(entidade, rotulos, autores, avaliacoesPorRegistro.GetValueOrDefault(r.Id))).ToList();
    }

    /// <summary>
    /// RN48: médias por critério ESTRELAS ativo, quantidade e "minha", só para registros cuja equipe tem
    /// modelo ATIVO para a entidade. Sem modelo, o registro fica sem "avaliacao" (nada muda).
    /// </summary>
    private async Task<IReadOnlyDictionary<Guid, AvaliacaoRegistroDto>> MontarAvaliacoesAsync(
        IReadOnlyList<Registro> lista, Entidade entidade, CancellationToken cancellationToken)
    {
        var equipes = lista.Select(r => r.EquipeId).OfType<Guid>().Distinct().ToList();
        if (entidade.EhGlobal || equipes.Count == 0)
            return new Dictionary<Guid, AvaliacaoRegistroDto>();

        var ativos = await modelos.ListarAtivosAsync(entidade.Id, equipes, cancellationToken);
        if (ativos.Count == 0)
            return new Dictionary<Guid, AvaliacaoRegistroDto>();

        var modeloPorEquipe = ativos.ToDictionary(m => m.EquipeId);
        var comModelo = lista.Where(r => r.EquipeId is Guid equipe && modeloPorEquipe.ContainsKey(equipe)).ToList();
        var usuario = await usuarioAtual.ObterAsync(cancellationToken);
        var medias = await avaliacoes.MediasAsync(
            comModelo.Select(r => r.Id).ToList(), ativos.Select(m => m.Id).ToList(), usuario.Id, cancellationToken);

        return comModelo.ToDictionary(
            r => r.Id,
            r =>
            {
                var modelo = modeloPorEquipe[r.EquipeId!.Value];
                var porCriterio = modelo.CriteriosAtivos
                    .Where(c => c.Tipo == TipoCriterio.Estrelas)
                    .ToDictionary(
                        c => c.Chave,
                        c => RegrasAvaliacao.ArredondarMedia(medias.Medias.TryGetValue((r.Id, c.Id), out var media) ? media : null));
                return new AvaliacaoRegistroDto(porCriterio, medias.Avaliadores.GetValueOrDefault(r.Id), medias.AvaliadosPeloUsuario.Contains(r.Id));
            });
    }
}
