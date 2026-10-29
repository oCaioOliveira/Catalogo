using Catalogo.Application.Abstracoes;
using Catalogo.Domain.Acesso;
using Microsoft.AspNetCore.Identity;

namespace Catalogo.Infrastructure.Acesso;

/// <summary>
/// Hash de senha com o PasswordHasher do .NET (PBKDF2 com HMAC-SHA512, sal aleatório e 100 mil
/// iterações na versão 8). Só o hasher: SEM o ASP.NET Core Identity completo, que exigiria as próprias
/// tabelas e migrations (docs/adr/0016). O hash já inclui o sal e a versão do algoritmo.
/// </summary>
internal sealed class HasherSenha : IHasherSenha
{
    private readonly PasswordHasher<Usuario> _hasher = new();

    // O PasswordHasher não usa o usuário no cálculo; o parâmetro existe só por compatibilidade.
    private static readonly Usuario Ninguem = new();

    public string GerarHash(string senha) => _hasher.HashPassword(Ninguem, senha);

    public bool Verificar(string senhaHash, string senha)
    {
        // SuccessRehashNeeded = senha certa num formato antigo: ainda é sucesso.
        var resultado = _hasher.VerifyHashedPassword(Ninguem, senhaHash, senha);
        return resultado is PasswordVerificationResult.Success or PasswordVerificationResult.SuccessRehashNeeded;
    }
}
