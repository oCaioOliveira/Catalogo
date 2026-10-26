using Catalogo.Application.Abstracoes;
using Catalogo.Domain.Acesso;
using Catalogo.Domain.Excecoes;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Catalogo.Api.Autenticacao;

/// <summary>
/// Usuário da requisição: o "sub" do token + a situação ATUAL do banco (<see cref="ISituacaoUsuarios"/>, com cache).
/// </summary>
internal sealed class UsuarioAtualHttp(IHttpContextAccessor acessor, ISituacaoUsuarios situacao) : IUsuarioAtual
{
    private UsuarioAtual? _usuario;

    public async Task<UsuarioAtual> ObterAsync(CancellationToken cancellationToken)
    {
        if (_usuario is not null)
            return _usuario;

        var id = ObterId(acessor.HttpContext)
            ?? throw new NaoAutenticadoException("Faça login para continuar.");

        var usuario = await situacao.ObterAsync(id, cancellationToken);
        if (usuario is null || !usuario.Ativo)
            throw new NaoAutenticadoException("Sessão inválida: usuário inexistente ou desativado.");

        return _usuario = usuario;
    }

    /// <summary>Id do usuário no claim "sub" do token, se houver.</summary>
    public static Guid? ObterId(HttpContext? http) =>
        Guid.TryParse(http?.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var id) ? id : null;
}
