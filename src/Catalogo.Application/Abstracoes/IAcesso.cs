using Catalogo.Domain.Acesso;

namespace Catalogo.Application.Abstracoes;

/// <summary>
/// Quem está fazendo a requisição. Implementado na Api (lê o "sub" do token) usando
/// <see cref="ISituacaoUsuarios"/>, para refletir a situação ATUAL do usuário (RN23).
/// </summary>
public interface IUsuarioAtual
{
    /// <exception cref="Domain.Excecoes.NaoAutenticadoException">Sem token válido ou usuário desativado.</exception>
    Task<UsuarioAtual> ObterAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Perfil, equipes ativas e situação (ativo) de cada usuário, com cache curto.
/// Quem altera usuários ou equipes chama <see cref="Invalidar"/>/<see cref="InvalidarTodos"/> para
/// a mudança valer na hora; sem isso, vale em até o tempo do cache.
/// </summary>
public interface ISituacaoUsuarios
{
    /// <summary>Nulo se o usuário não existe.</summary>
    Task<UsuarioAtual?> ObterAsync(Guid usuarioId, CancellationToken cancellationToken);

    void Invalidar(Guid usuarioId);

    /// <summary>Descarta o cache de todos (ex.: uma equipe foi desativada).</summary>
    void InvalidarTodos();
}

/// <summary>Emite o token de acesso (JWT) de um usuário autenticado pelo Windows. Implementado na Api.</summary>
public interface IGeradorToken
{
    TokenAcesso Gerar(Usuario usuario);
}

/// <summary>Token emitido e quando ele expira (UTC).</summary>
public sealed record TokenAcesso(string Token, DateTime ExpiraEm);
