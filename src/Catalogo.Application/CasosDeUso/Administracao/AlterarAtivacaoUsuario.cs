using Catalogo.Application.Abstracoes;
using Catalogo.Application.Comum;
using Catalogo.Application.Dtos;
using Catalogo.Application.Mapeamentos;
using Catalogo.Domain.Historico;
using Catalogo.Domain.Excecoes;

namespace Catalogo.Application.CasosDeUso.Administracao;

/// <summary>
/// Caso de uso: ativar ou desativar um usuário (só ADMINISTRADOR). Usuário nunca é excluído (RN31).
/// Não desativa o último administrador (RN30). Desativado, o usuário perde o acesso na hora.
/// </summary>
public sealed class AlterarAtivacaoUsuario(
    IUsuarioAtual usuarioAtual,
    IUsuarioRepository usuarios,
    IEquipeRepository equipes,
    IUnidadeDeTrabalho unidadeDeTrabalho,
    ServicoHistorico historico,
    ISituacaoUsuarios situacao)
{
    public async Task<UsuarioDto> ExecutarAsync(Guid id, AtivacaoUsuarioEntrada entrada, CancellationToken cancellationToken)
    {
        var autor = await usuarioAtual.ObterAsync(cancellationToken);
        Permissoes.ExigirAdministrador(autor);
        var usuario = await ValidacaoUsuario.ObterOuFalharAsync(usuarios, id, cancellationToken);

        var ativo = entrada.Ativo
            ?? throw new ValidacaoRegistroException(new Dictionary<string, string[]> { ["ativo"] = ["Campo obrigatório (true ou false)."] });

        await ValidacaoUsuario.ProtegerUltimoAdministradorAsync(usuarios, usuario, usuario.Perfil, ativo, cancellationToken);

        if (usuario.Ativo != ativo)
        {
            var antes = usuario.ParaJsonHistorico();
            usuario.Ativo = ativo;
            historico.RegistrarUsuario(OperacaoHistorico.Alteracao, usuario.Id, autor.Id, autor.Matricula, antes, usuario.ParaJsonHistorico());
            await unidadeDeTrabalho.SalvarAlteracoesAsync(cancellationToken);
            situacao.Invalidar(usuario.Id);
        }

        return usuario.ParaDto(await ValidacaoUsuario.EquipesDosUsuariosAsync(equipes, [usuario], cancellationToken));
    }
}
