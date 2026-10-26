using Catalogo.Application.Abstracoes;
using Catalogo.Application.Comum;
using Catalogo.Application.Dtos;

namespace Catalogo.Application.CasosDeUso.Avaliacoes;

/// <summary>
/// Caso de uso: todos os modelos de avaliação (ativo e inativos) de uma entidade numa equipe, mais recentes
/// primeiro. Serve para achar o id de um modelo inativo a reativar (RN41). Só GESTOR da equipe ou ADMINISTRADOR.
/// </summary>
public sealed class ListarModelosAvaliacao(
    IUsuarioAtual usuarioAtual, IMetadadosProvider metadados, IEquipeRepository equipes,
    IModeloAvaliacaoRepository modelos, MontadorModeloAvaliacao montador)
{
    public async Task<IReadOnlyList<ModeloAvaliacaoDto>> ExecutarAsync(
        Guid equipeId, string grupoChave, string entidadeChave, CancellationToken cancellationToken)
    {
        var usuario = await usuarioAtual.ObterAsync(cancellationToken);
        var entidade = await ConfiguracaoDaEquipe.ValidarAsync(
            usuario, equipeId, grupoChave, entidadeChave, AvaliacaoComum.Funcionalidade, equipes, metadados, cancellationToken);

        var dtos = new List<ModeloAvaliacaoDto>();
        foreach (var modelo in await modelos.ListarAsync(entidade.Id, equipeId, cancellationToken))
            dtos.Add(await montador.MontarAsync(modelo, grupoChave, entidade, cancellationToken));

        return dtos;
    }
}
