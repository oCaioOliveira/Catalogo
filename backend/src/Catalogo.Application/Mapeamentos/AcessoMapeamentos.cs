using System.Text.Json.Nodes;
using Catalogo.Application.Abstracoes;
using Catalogo.Application.Dtos;
using Catalogo.Domain.Acesso;

namespace Catalogo.Application.Mapeamentos;

/// <summary>Mapeamento manual de usuários e equipes. NENHUM método aqui expõe SenhaHash.</summary>
public static class AcessoMapeamentos
{
    /// <param name="equipes">Equipes do sistema por id (para nome e situação dos vínculos).</param>
    public static UsuarioDto ParaDto(this Usuario usuario, IReadOnlyDictionary<Guid, Equipe> equipes) => new(
        usuario.Id,
        usuario.Matricula,
        usuario.Nome,
        PerfilConversor.ParaTexto(usuario.Perfil),
        usuario.Ativo,
        usuario.DeveTrocarSenha,
        usuario.BloqueadoAte,
        usuario.UltimoAcessoEm,
        usuario.CriadoEm,
        usuario.Equipes
            .Where(v => equipes.ContainsKey(v.EquipeId))
            .Select(v => equipes[v.EquipeId].ParaResumoDto())
            .OrderBy(e => e.Nome, StringComparer.CurrentCulture)
            .ToList());

    public static EquipeDto ParaDto(this Equipe equipe, int membrosAtivos, IReadOnlyList<ListagemHabilitada>? listagens) =>
        new(equipe.Id, equipe.Nome, equipe.Ativa, equipe.CriadoEm, membrosAtivos,
            (listagens ?? []).Select(l => new ListagemDaEquipeDto(l.Chave, l.Nome, l.Ativo)).ToList());

    public static EquipeResumoDto ParaResumoDto(this Equipe equipe) => new(equipe.Id, equipe.Nome, equipe.Ativa);

    /// <summary>
    /// Objeto plano do usuário para o histórico (RN32): matrícula, nome, perfil, ativo e a lista de
    /// equipes (ids ordenados). SEM senha e SEM hash.
    /// </summary>
    public static JsonObject ParaJsonHistorico(this Usuario usuario) => new()
    {
        ["matricula"] = usuario.Matricula,
        ["nome"] = usuario.Nome,
        ["perfil"] = PerfilConversor.ParaTexto(usuario.Perfil),
        ["ativo"] = usuario.Ativo,
        ["equipes"] = new JsonArray(usuario.Equipes
            .Select(v => v.EquipeId)
            .Order()
            .Select(id => (JsonNode)JsonValue.Create(id.ToString("D")))
            .ToArray()),
    };

    /// <summary>Objeto plano da equipe para o histórico (RN32).</summary>
    public static JsonObject ParaJsonHistorico(this Equipe equipe) => new()
    {
        ["nome"] = equipe.Nome,
        ["ativa"] = equipe.Ativa,
    };
}
