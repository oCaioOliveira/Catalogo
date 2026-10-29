namespace Catalogo.Domain.Acesso;

/// <summary>
/// Quem está fazendo a requisição, com a situação ATUAL lida do banco (com cache curto), e não a
/// do token: assim desativar, trocar o perfil ou mudar as equipes tem efeito rápido (RN23).
/// Imutável: pode ser guardado em cache e compartilhado.
/// </summary>
public sealed class UsuarioAtual
{
    public required Guid Id { get; init; }
    public required string Matricula { get; init; }
    public required string Nome { get; init; }
    public required Perfil Perfil { get; init; }

    /// <summary>
    /// Equipes ATIVAS às quais o usuário pertence ("suas equipes"). Dentro de uma listagem (<see cref="PoliticaPermissao.NaListagem"/>),
    /// só as que estão habilitadas nela: "as equipes do usuário nesta listagem" (RN91).
    /// </summary>
    public required IReadOnlySet<Guid> Equipes { get; init; }

    /// <summary>
    /// As equipes habilitadas na listagem em que o usuário está agindo (RN90). Nulo = fora de uma listagem (ex.: a fila de
    /// aprovação, que cruza listagens). Limita também o ADMINISTRADOR: ele só cria e configura nas equipes habilitadas (RN91).
    /// </summary>
    public IReadOnlySet<Guid>? EquipesHabilitadasNaListagem { get; init; }

    public bool Ativo { get; init; } = true;
    public bool DeveTrocarSenha { get; init; }

    public bool EhAdministrador => Perfil == Perfil.Administrador;
}
