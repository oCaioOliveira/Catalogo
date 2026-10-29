using Catalogo.Application.Abstracoes;
using Catalogo.Application.Comum;
using Catalogo.Application.Dtos;
using Catalogo.Domain.Excecoes;

namespace Catalogo.Application.CasosDeUso.Avaliacoes;

/// <summary>
/// Caso de uso: o modelo de avaliação ATIVO de uma entidade numa equipe, com todos os critérios em ordem
/// (RN40, RN41). 404 se não há modelo ativo. Só GESTOR da equipe ou ADMINISTRADOR.
/// </summary>
public sealed class ObterModeloAvaliacao(
    IUsuarioAtual usuarioAtual, IMetadadosProvider metadados, IEquipeRepository equipes,
    IModeloAvaliacaoRepository modelos, MontadorModeloAvaliacao montador)
{
    public async Task<ModeloAvaliacaoDto> ExecutarAsync(
        Guid equipeId, string grupoChave, string entidadeChave, CancellationToken cancellationToken)
    {
        var usuario = await usuarioAtual.ObterAsync(cancellationToken);
        var entidade = await ConfiguracaoDaEquipe.ValidarAsync(
            usuario, equipeId, grupoChave, entidadeChave, AvaliacaoComum.Funcionalidade, equipes, metadados, cancellationToken);

        var modelo = await modelos.ObterAtivoAsync(entidade.Id, equipeId, cancellationToken)
            ?? throw new RecursoNaoEncontradoException($"'{entidade.Nome}' não tem modelo de avaliação ativo nesta equipe.", CodigosErro.ModeloAvaliacaoNaoEncontrado);

        return await montador.MontarAsync(modelo, grupoChave, entidade, cancellationToken);
    }
}
