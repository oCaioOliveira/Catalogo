using Catalogo.Application.Abstracoes;
using Catalogo.Domain.Acesso;
using Catalogo.Domain.Excecoes;
using Catalogo.Domain.Validacao;

namespace Catalogo.Application.CasosDeUso.Administracao;

/// <summary>Validações de usuário usadas na criação e na alteração (sem duplicar regra).</summary>
internal static class ValidacaoUsuario
{
    /// <summary>Perfil informado como texto ("INTERNO"...). Adiciona erro e devolve nulo se inválido.</summary>
    public static Perfil? LerPerfil(string? texto, ErrosPorCampo erros)
    {
        if (PerfilConversor.TentarConverter(texto, out var perfil))
            return perfil;

        erros.Adicionar("perfil", "Deve ser EXTERNO, INTERNO, GESTOR ou ADMINISTRADOR.");
        return null;
    }

    /// <summary>
    /// As equipes precisam existir, e um não-administrador precisa de pelo menos uma ATIVA (RN31).
    /// Devolve os ids sem repetição.
    /// </summary>
    public static async Task<IReadOnlyList<Guid>> ValidarEquipesAsync(
        IEquipeRepository equipes, Perfil? perfil, IReadOnlyList<Guid>? informadas, ErrosPorCampo erros, CancellationToken cancellationToken)
    {
        var ids = (informadas ?? []).Distinct().ToList();
        var encontradas = await equipes.ObterPorIdsAsync(ids, cancellationToken);

        foreach (var faltando in ids.Except(encontradas.Select(e => e.Id)))
            erros.Adicionar("equipes", $"Equipe {faltando} não encontrada.");

        if (perfil is not null and not Perfil.Administrador && !encontradas.Any(e => e.Ativa))
            erros.Adicionar("equipes", "Usuário não-administrador precisa de pelo menos uma equipe ativa.");

        return ids;
    }

    /// <summary>RN30: impede rebaixar ou desativar o último administrador ativo.</summary>
    public static async Task ProtegerUltimoAdministradorAsync(
        IUsuarioRepository usuarios, Usuario alvo, Perfil novoPerfil, bool novoAtivo, CancellationToken cancellationToken)
    {
        if (!alvo.EhAdministrador || !alvo.Ativo)
            return;

        var administradores = await usuarios.ContarAdministradoresAtivosAsync(cancellationToken);
        if (RegrasAdministrador.RemoveriaUltimoAdministrador(alvo, novoPerfil, novoAtivo, administradores))
        {
            throw new RegraDeNegocioException(
                "Não é possível desativar ou rebaixar o último administrador ativo.", CodigosErro.UltimoAdministrador);
        }
    }

    /// <summary>Troca os vínculos de equipe do usuário pelos informados (remove os que saíram, adiciona os novos).</summary>
    public static void SincronizarEquipes(Usuario usuario, IReadOnlyList<Guid> equipeIds)
    {
        usuario.Equipes.RemoveAll(v => !equipeIds.Contains(v.EquipeId));
        foreach (var id in equipeIds.Where(id => usuario.Equipes.All(v => v.EquipeId != id)))
            usuario.Equipes.Add(new UsuarioEquipe { UsuarioId = usuario.Id, EquipeId = id });
    }

    /// <summary>Usuário pelo id, ou 404.</summary>
    public static async Task<Usuario> ObterOuFalharAsync(IUsuarioRepository usuarios, Guid id, CancellationToken cancellationToken) =>
        await usuarios.ObterPorIdAsync(id, cancellationToken)
        ?? throw new RecursoNaoEncontradoException($"Usuário {id} não encontrado.", CodigosErro.UsuarioNaoEncontrado);

    /// <summary>Equipes do sistema por id, para montar o DTO de um ou mais usuários.</summary>
    public static async Task<IReadOnlyDictionary<Guid, Equipe>> EquipesDosUsuariosAsync(
        IEquipeRepository equipes, IEnumerable<Usuario> lista, CancellationToken cancellationToken)
    {
        var ids = lista.SelectMany(u => u.Equipes).Select(v => v.EquipeId).Distinct().ToList();
        return (await equipes.ObterPorIdsAsync(ids, cancellationToken)).ToDictionary(e => e.Id);
    }
}
