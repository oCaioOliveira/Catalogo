using Catalogo.Application.Abstracoes;
using Catalogo.Domain.Acesso;

namespace Catalogo.Application.Comum;

/// <summary>
/// Fornecedor de identidade de hoje: matrícula e senha guardadas no próprio banco (docs/adr/0015).
/// Aplica o bloqueio por tentativas (RN24) e nunca diz o motivo da recusa (RN23).
/// Para trocar por um SSO, crie outra implementação de <see cref="IProvedorIdentidade"/> e troque o
/// registro na injeção de dependência: os casos de uso não mudam.
/// </summary>
public sealed class ProvedorIdentidadeLocal(
    IUsuarioRepository usuarios,
    IHasherSenha hasher,
    IUnidadeDeTrabalho unidadeDeTrabalho,
    TimeProvider relogio) : IProvedorIdentidade
{
    // Hash de uma senha qualquer, usado quando a matrícula não existe: a resposta demora o mesmo
    // tempo que uma senha errada, para não dar pistas de quais matrículas existem (RN23).
    private static string? _hashFicticio;

    public async Task<Usuario?> AutenticarAsync(string matricula, string senha, CancellationToken cancellationToken)
    {
        var agora = relogio.GetUtcNow().UtcDateTime;
        var usuario = await usuarios.ObterPorMatriculaAsync(matricula.Trim(), cancellationToken);

        if (usuario is null)
        {
            _hashFicticio ??= hasher.GerarHash(Guid.NewGuid().ToString());
            hasher.Verificar(_hashFicticio, senha);
            return null;
        }

        // Inativo ou bloqueado: recusa sem conferir nem contar tentativa (RN23, RN24).
        if (!usuario.Ativo || usuario.EstaBloqueado(agora))
            return null;

        if (!hasher.Verificar(usuario.SenhaHash, senha))
        {
            usuario.RegistrarFalhaDeLogin(agora);
            await unidadeDeTrabalho.SalvarAlteracoesAsync(cancellationToken);
            return null;
        }

        usuario.RegistrarLoginComSucesso(agora);
        await unidadeDeTrabalho.SalvarAlteracoesAsync(cancellationToken);
        return usuario;
    }
}
