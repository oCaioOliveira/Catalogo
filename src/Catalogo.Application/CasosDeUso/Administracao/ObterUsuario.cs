using Catalogo.Application.Abstracoes;
using Catalogo.Application.Comum;
using Catalogo.Application.Dtos;
using Catalogo.Application.Mapeamentos;

namespace Catalogo.Application.CasosDeUso.Administracao;

/// <summary>Caso de uso: um usuário pelo id (só ADMINISTRADOR).</summary>
public sealed class ObterUsuario(IUsuarioAtual usuarioAtual, IUsuarioRepository usuarios, IEquipeRepository equipes)
{
    public async Task<UsuarioDto> ExecutarAsync(Guid id, CancellationToken cancellationToken)
    {
        Permissoes.ExigirAdministrador(await usuarioAtual.ObterAsync(cancellationToken));

        var usuario = await ValidacaoUsuario.ObterOuFalharAsync(usuarios, id, cancellationToken);
        return usuario.ParaDto(await ValidacaoUsuario.EquipesDosUsuariosAsync(equipes, [usuario], cancellationToken));
    }
}
