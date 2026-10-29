using System.Text.Json.Nodes;
using Catalogo.Application.Dtos;
using Catalogo.Domain.Metadados;

namespace Catalogo.Application.Mapeamentos;

/// <summary>
/// Metadados → DTOs da administração e → objetos do histórico (RN52). Referências a outros metadados saem pela chave.
/// </summary>
public static class MetadadosAdminMapeamentos
{
    public static GrupoAdminDto ParaAdminDto(this Grupo grupo, IReadOnlyDictionary<Guid, int> registrosPorEntidade) => new(
        grupo.Chave, grupo.Nome, grupo.Descricao, grupo.Ativo, Convert.ToBase64String(grupo.Versao),
        grupo.Entidades.Select(e => e.ParaAdminDto(grupo, registrosPorEntidade.GetValueOrDefault(e.Id))).ToList(),
        grupo.EquipesHabilitadas.Count);

    public static EntidadeAdminDto ParaAdminDto(this Entidade entidade, Grupo grupo, int quantidadeRegistros) => new(
        entidade.Chave,
        entidade.Nome,
        entidade.Descricao,
        entidade.Ordem,
        entidade.EscopoEquipe,
        entidade.Ativo,
        Convert.ToBase64String(entidade.Versao),
        entidade.CampoExibicaoId is Guid id ? entidade.ObterCampo(id)?.Chave : null,
        quantidadeRegistros,
        entidade.Campos.Select(c => c.ParaAdminDto(grupo, entidade)).ToList(),
        entidade.RestricoesUnicas.Select(r => new RestricaoUnicaDto(r.Nome, r.Campos.Select(c => c.Chave).ToList())).ToList(),
        entidade.NomePlural,
        entidade.Icone);

    public static CampoAdminDto ParaAdminDto(this Campo campo, Grupo grupo, Entidade entidade)
    {
        var dependencia = entidade.Dependencias.FirstOrDefault(d => d.Campo.Id == campo.Id);
        return new CampoAdminDto(
            campo.Chave,
            campo.Nome,
            TipoDadoConversor.ParaTexto(campo.TipoDado),
            campo.Obrigatorio,
            campo.TamanhoMaximo,
            campo.Precisao,
            campo.Escala,
            campo.TipoDado == TipoDado.Lista ? campo.Opcoes : null,
            campo.EntidadeReferenciadaId is Guid referenciada ? grupo.ObterEntidade(referenciada)?.Chave : null,
            campo.AcaoExclusao is AcaoExclusao acao ? AcaoExclusaoConversor.ParaTexto(acao) : null,
            campo.Ordem,
            campo.Ativo,
            dependencia is null ? null : new DependenciaAdminDto(dependencia.CampoVia?.Chave, dependencia.CampoOrigem.Chave, dependencia.CampoAlvo.Chave));
    }

    // ---- Histórico (RN52): um objeto plano do que mudou, com a entidade dona ----

    public static JsonObject ParaHistorico(this Grupo grupo) => new()
    {
        ["alvo"] = "grupo",
        ["chave"] = grupo.Chave,
        ["nome"] = grupo.Nome,
        ["descricao"] = grupo.Descricao,
        ["ativo"] = grupo.Ativo,
    };

    /// <summary>
    /// As equipes habilitadas na listagem, para o histórico (RN90, RN32): os NOMES, em ordem, num texto só ("Alfa, Beta"), para a
    /// mudança aparecer como um campo "equipes" de/para. Os ids vão em "equipeIds".
    /// </summary>
    public static JsonObject ParaHistoricoEquipes(this Grupo grupo, IReadOnlyDictionary<Guid, string> nomes)
    {
        var ordenadas = grupo.EquipesHabilitadas
            .Select(id => (Id: id, Nome: nomes.GetValueOrDefault(id) ?? id.ToString()))
            .OrderBy(e => e.Nome, StringComparer.CurrentCulture)
            .ToList();
        return new JsonObject
        {
            ["alvo"] = "equipes",
            ["chave"] = grupo.Chave,
            ["equipes"] = string.Join(", ", ordenadas.Select(e => e.Nome)),
            ["equipeIds"] = new JsonArray(ordenadas.Select(e => (JsonNode?)JsonValue.Create(e.Id)).ToArray()),
        };
    }

    public static JsonObject ParaHistorico(this Entidade entidade) => new()
    {
        ["alvo"] = "entidade",
        ["entidade"] = entidade.Chave,
        ["nome"] = entidade.Nome,
        ["descricao"] = entidade.Descricao,
        ["nomePlural"] = entidade.NomePlural,
        ["icone"] = entidade.Icone,
        ["ordem"] = entidade.Ordem,
        ["escopoEquipe"] = entidade.EscopoEquipe,
        ["ativo"] = entidade.Ativo,
        ["campoExibicao"] = entidade.CampoExibicaoId is Guid id ? entidade.ObterCampo(id)?.Chave : null,
    };

    public static JsonObject ParaHistorico(this Campo campo, Grupo grupo, Entidade entidade)
    {
        var dto = campo.ParaAdminDto(grupo, entidade);
        return new JsonObject
        {
            ["alvo"] = "campo",
            ["entidade"] = entidade.Chave,
            ["campo"] = dto.Chave,
            ["nome"] = dto.Nome,
            ["tipoDado"] = dto.TipoDado,
            ["obrigatorio"] = dto.Obrigatorio,
            ["tamanhoMaximo"] = dto.TamanhoMaximo,
            ["precisao"] = dto.Precisao,
            ["escala"] = dto.Escala,
            ["opcoes"] = dto.Opcoes is null ? null : new JsonArray(dto.Opcoes.Select(o => (JsonNode?)JsonValue.Create(o)).ToArray()),
            ["entidadeReferenciada"] = dto.EntidadeReferenciada,
            ["acaoExclusao"] = dto.AcaoExclusao,
            ["ordem"] = dto.Ordem,
            ["ativo"] = dto.Ativo,
        };
    }

    public static JsonObject ParaHistorico(this RestricaoUnica restricao, Entidade entidade) => new()
    {
        ["alvo"] = "restricao",
        ["entidade"] = entidade.Chave,
        ["nome"] = restricao.Nome,
        ["campos"] = new JsonArray(restricao.Campos.Select(c => (JsonNode?)JsonValue.Create(c.Chave)).ToArray()),
    };

    public static JsonObject ParaHistorico(this CampoDependencia dependencia, Entidade entidade) => new()
    {
        ["alvo"] = "dependencia",
        ["entidade"] = entidade.Chave,
        ["campo"] = dependencia.Campo.Chave,
        ["campoVia"] = dependencia.CampoVia?.Chave,
        ["campoOrigem"] = dependencia.CampoOrigem.Chave,
        ["campoAlvo"] = dependencia.CampoAlvo.Chave,
    };

    public static JsonObject OrdemParaHistorico(this Entidade entidade) => new()
    {
        ["alvo"] = "ordem_campos",
        ["entidade"] = entidade.Chave,
        ["campos"] = new JsonArray(entidade.Campos.Select(c => (JsonNode?)JsonValue.Create(c.Chave)).ToArray()),
    };
}
