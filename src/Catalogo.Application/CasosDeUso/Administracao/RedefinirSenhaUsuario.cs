using Catalogo.Application.Abstracoes;
using Catalogo.Application.Comum;
using Catalogo.Application.Dtos;
using Catalogo.Application.Mapeamentos;
using Catalogo.Domain.Acesso;
using Catalogo.Domain.Historico;

namespace Catalogo.Application.CasosDeUso.Administracao;

/// <summary>
/// Caso de uso: gerar uma nova senha PROVISÓRIA para um usuário (só ADMINISTRADOR). Devolvida uma
/// única vez; obriga a troca no próximo acesso (RN25) e desbloqueia o login (RN24). No histórico,
/// aparece só "senha": "redefinida", sem valor (RN32).
/// </summary>
public sealed class RedefinirSenhaUsuario(
    IUsuarioAtual usuarioAtual,
    IUsuarioRepository usuarios,
    IHasherSenha hasher,
    IUnidadeDeTrabalho unidadeDeTrabalho,
    ServicoHistorico historico,
    ISituacaoUsuarios situacao)
{
    public async Task<SenhaProvisoriaDto> ExecutarAsync(Guid id, CancellationToken cancellationToken)
    {
        var autor = await usuarioAtual.ObterAsync(cancellationToken);
        Permissoes.ExigirAdministrador(autor);
        var usuario = await ValidacaoUsuario.ObterOuFalharAsync(usuarios, id, cancellationToken);

        var senhaProvisoria = PoliticaSenha.GerarProvisoria();
        var antes = usuario.ParaJsonHistorico();
        usuario.DefinirSenha(hasher.GerarHash(senhaProvisoria), provisoria: true);

        var depois = usuario.ParaJsonHistorico();
        depois["senha"] = "redefinida";
        historico.RegistrarUsuario(OperacaoHistorico.Alteracao, usuario.Id, autor.Id, autor.Matricula, antes, depois);

        await unidadeDeTrabalho.SalvarAlteracoesAsync(cancellationToken);
        situacao.Invalidar(usuario.Id);
        return new SenhaProvisoriaDto(senhaProvisoria);
    }
}
