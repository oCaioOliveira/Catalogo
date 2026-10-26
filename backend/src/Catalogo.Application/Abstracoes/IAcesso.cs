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
/// Perfil, equipes ativas e situação (ativo, troca de senha) de cada usuário, com cache curto.
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

/// <summary>
/// Fornecedor de identidade: confere as credenciais (docs/adr/0015). Hoje é matrícula e senha
/// local (<c>ProvedorIdentidadeLocal</c>); no futuro pode ser um SSO corporativo, sem mexer nos casos de uso.
/// </summary>
public interface IProvedorIdentidade
{
    /// <summary>
    /// O usuário autenticado, ou nulo se as credenciais forem inválidas, o usuário estiver inativo
    /// ou bloqueado. O motivo NÃO é informado, para a mensagem ser sempre genérica (RN23).
    /// </summary>
    Task<Usuario?> AutenticarAsync(string matricula, string senha, CancellationToken cancellationToken);
}

/// <summary>Hash de senha (docs/adr/0016). Implementado na Infrastructure com PasswordHasher (PBKDF2).</summary>
public interface IHasherSenha
{
    string GerarHash(string senha);

    bool Verificar(string senhaHash, string senha);
}

/// <summary>Emite o token de acesso (JWT) de um usuário autenticado. Implementado na Api.</summary>
public interface IGeradorToken
{
    TokenAcesso Gerar(Usuario usuario);
}

/// <summary>Token emitido e quando ele expira (UTC).</summary>
public sealed record TokenAcesso(string Token, DateTime ExpiraEm);
