namespace Catalogo.Domain.Acesso;

/// <summary>Regra do último administrador (RN30).</summary>
public static class RegrasAdministrador
{
    /// <summary>
    /// Verdadeiro se a mudança deixaria o sistema sem nenhum administrador ativo: o alvo é
    /// administrador ativo, deixaria de ser (rebaixado ou desativado) e é o único.
    /// </summary>
    /// <param name="administradoresAtivos">Quantos administradores ativos existem hoje, contando o alvo.</param>
    public static bool RemoveriaUltimoAdministrador(Usuario alvo, Perfil novoPerfil, bool novoAtivo, int administradoresAtivos) =>
        alvo.EhAdministrador
        && alvo.Ativo
        && (novoPerfil != Perfil.Administrador || !novoAtivo)
        && administradoresAtivos <= 1;
}
