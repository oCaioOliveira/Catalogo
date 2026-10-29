using System.Security.Claims;
using Catalogo.Application.Abstracoes;
using Catalogo.Domain.Acesso;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Catalogo.Api.Autenticacao;

/// <summary>
/// Emite o JWT do login (RN23) com "sub" (id do usuário), "matricula" e "perfil". As equipes NÃO
/// vão no token: são lidas a cada requisição (com cache curto), para mudanças valerem rápido.
/// </summary>
internal sealed class GeradorTokenJwt(IConfiguration configuracao, TimeProvider relogio) : IGeradorToken
{
    public const string ClaimMatricula = "matricula";
    public const string ClaimPerfil = "perfil";

    public TokenAcesso Gerar(Usuario usuario)
    {
        var opcoes = OpcoesJwt.Ler(configuracao);
        var agora = relogio.GetUtcNow().UtcDateTime;
        var expiraEm = agora + opcoes.Validade;

        var token = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = opcoes.Emissor,
            Audience = opcoes.Audiencia,
            IssuedAt = agora,
            NotBefore = agora,
            Expires = expiraEm,
            Subject = new ClaimsIdentity(
            [
                new Claim(JwtRegisteredClaimNames.Sub, usuario.Id.ToString("D")),
                new Claim(ClaimMatricula, usuario.Matricula),
                new Claim(ClaimPerfil, PerfilConversor.ParaTexto(usuario.Perfil)),
            ]),
            SigningCredentials = new SigningCredentials(opcoes.Chave, SecurityAlgorithms.HmacSha256),
        });

        return new TokenAcesso(token, expiraEm);
    }
}
