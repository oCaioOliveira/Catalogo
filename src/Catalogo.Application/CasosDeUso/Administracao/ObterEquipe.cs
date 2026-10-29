using Catalogo.Application.Abstracoes;
using Catalogo.Application.Comum;
using Catalogo.Application.Dtos;
using Catalogo.Application.Mapeamentos;
using Catalogo.Domain.Acesso;
using Catalogo.Domain.Excecoes;

namespace Catalogo.Application.CasosDeUso.Administracao;

/// <summary>Caso de uso: uma equipe pelo id (só ADMINISTRADOR), com as listagens em que ela está habilitada (RN90).</summary>
public sealed class ObterEquipe(IUsuarioAtual usuarioAtual, IEquipeRepository equipes)
{
    public async Task<EquipeDto> ExecutarAsync(Guid id, CancellationToken cancellationToken)
    {
        Permissoes.ExigirAdministrador(await usuarioAtual.ObterAsync(cancellationToken));
        return await ParaDtoAsync(equipes, await ObterOuFalharAsync(equipes, id, cancellationToken), cancellationToken);
    }

    /// <summary>O DTO com a quantidade de membros ativos (contada no banco) e as listagens em que a equipe está habilitada (RN90).</summary>
    public static async Task<EquipeDto> ParaDtoAsync(IEquipeRepository equipes, Equipe equipe, CancellationToken cancellationToken) =>
        equipe.ParaDto(
            (await equipes.ContarMembrosAtivosAsync([equipe.Id], cancellationToken)).GetValueOrDefault(equipe.Id),
            (await equipes.ListarListagensAsync([equipe.Id], cancellationToken)).GetValueOrDefault(equipe.Id));

    public static async Task<Equipe> ObterOuFalharAsync(IEquipeRepository equipes, Guid id, CancellationToken cancellationToken) =>
        await equipes.ObterPorIdAsync(id, cancellationToken)
        ?? throw new RecursoNaoEncontradoException($"Equipe {id} não encontrada.", CodigosErro.EquipeNaoEncontrada);
}
