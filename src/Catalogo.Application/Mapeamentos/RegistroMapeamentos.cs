using System.Text.Json.Nodes;
using Catalogo.Application.Dtos;
using Catalogo.Domain.Aprovacao;
using Catalogo.Domain.Metadados;
using Catalogo.Domain.Registros;

namespace Catalogo.Application.Mapeamentos;

/// <summary>Mapeamento manual Registro → RegistroDto (sem AutoMapper, ver docs/adr/0004).</summary>
public static class RegistroMapeamentos
{
    /// <param name="rotulos">Rótulo de cada registro apontado (RN20). Use <c>MontadorRegistroDto</c> para buscá-los em lote.</param>
    /// <param name="usuarios">Resumo de quem criou e alterou, por id.</param>
    /// <param name="avaliacao">Médias das avaliações (RN48), nulo sem modelo ativo.</param>
    public static RegistroDto ParaDto(
        this Registro registro,
        Entidade entidade,
        IReadOnlyDictionary<Guid, string?> rotulos,
        IReadOnlyDictionary<Guid, UsuarioResumoDto> usuarios,
        AvaliacaoRegistroDto? avaliacao = null)
    {
        var dados = registro.ParaJsonPlano(entidade);

        // RN69: campo inativo some da resposta (e dos formulários, da listagem e da exportação), mas o valor continua
        // gravado e volta quando o campo é reativado. O histórico (ParaJsonPlano) continua com todos os valores.
        foreach (var inativo in entidade.Campos.Where(c => !c.Ativo))
            dados.Remove(inativo.Chave);

        // Na saída, cada referência vira { id, rotulo } (RN20).
        foreach (var campo in entidade.Campos.Where(c => c.EhReferencia))
        {
            if (dados[campo.Chave] is JsonValue valor && valor.TryGetValue<string>(out var texto) && Guid.TryParse(texto, out var id))
                dados[campo.Chave] = ParaReferenciaDto(id, rotulos);
        }

        return new RegistroDto(
            registro.Id,
            registro.EquipeId,
            registro.CriadoEm,
            registro.AtualizadoEm,
            Convert.ToBase64String(registro.Versao),
            Resumo(registro.CriadoPorId, usuarios),
            Resumo(registro.AtualizadoPorId, usuarios),
            AprovacaoConversor.ParaTexto(registro.SituacaoAprovacao),
            UltimaDecisao(registro, usuarios),
            avaliacao,
            dados);
    }

    /// <summary>
    /// Objeto PLANO do registro: campos simples e referências (como GUID) juntos, na ordem dos campos,
    /// no mesmo formato do corpo do POST. É o que vai para o histórico (RN32).
    /// </summary>
    public static JsonObject ParaJsonPlano(this Registro registro, Entidade entidade)
    {
        var armazenados = JsonNode.Parse(registro.Dados) as JsonObject ?? [];
        var dados = new JsonObject();

        // Junta campos simples (do JSON) e referências (da tabela) num objeto só, na ordem dos campos.
        foreach (var campo in entidade.Campos)
        {
            if (campo.EhReferencia)
            {
                var referencia = registro.Referencias.FirstOrDefault(r => r.CampoId == campo.Id);
                if (referencia is not null)
                    dados[campo.Chave] = referencia.RegistroDestinoId.ToString("D");
            }
            else if (armazenados.TryGetPropertyValue(campo.Chave, out var valor))
            {
                dados[campo.Chave] = valor?.DeepClone();
            }
        }

        // Valores gravados cujo campo não existe mais nos metadados: devolvidos no fim, para não sumirem.
        foreach (var (chave, valor) in armazenados)
        {
            if (!dados.ContainsKey(chave))
                dados[chave] = valor?.DeepClone();
        }

        return dados;
    }

    public static JsonObject ParaReferenciaDto(Guid id, IReadOnlyDictionary<Guid, string?> rotulos) => new()
    {
        ["id"] = id.ToString("D"),
        ["rotulo"] = rotulos.GetValueOrDefault(id),
    };

    private static UltimaDecisaoDto? UltimaDecisao(Registro registro, IReadOnlyDictionary<Guid, UsuarioResumoDto> usuarios) =>
        FluxoAprovacao.UltimaDecisao(registro) is DecisaoAprovacao decisao && registro.DecididoEm is DateTime decididoEm
            ? new UltimaDecisaoDto(AprovacaoConversor.ParaTexto(decisao), registro.JustificativaDecisao,
                Resumo(registro.DecididoPorId, usuarios), decididoEm)
            : null;

    private static UsuarioResumoDto? Resumo(Guid? usuarioId, IReadOnlyDictionary<Guid, UsuarioResumoDto> usuarios) =>
        usuarioId is Guid id ? usuarios.GetValueOrDefault(id) : null;
}
