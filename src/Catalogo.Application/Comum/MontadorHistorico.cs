using System.Text.Json.Nodes;
using Catalogo.Application.Abstracoes;
using Catalogo.Application.Dtos;
using Catalogo.Application.Mapeamentos;
using Catalogo.Domain.Historico;
using Catalogo.Domain.Metadados;

namespace Catalogo.Application.Comum;

/// <summary>
/// Monta as linhas de histórico para a API (RN32): as mudanças por campo (calculadas no Domain pelo
/// <see cref="ComparadorAlteracoes"/>), o autor { id, matricula, nome } e, em campos REFERENCIA de
/// registro, o rótulo ATUAL do registro apontado. Tudo em lote (uma consulta por tipo de dado).
/// </summary>
public sealed class MontadorHistorico(IRegistroRepository registros, IUsuarioRepository usuarios)
{
    /// <param name="entidade">Para histórico de registro: diz quais campos são referências. Nulo para usuário/equipe.</param>
    public async Task<IReadOnlyList<HistoricoDto>> MontarAsync(
        IReadOnlyList<HistoricoAlteracao> linhas, Entidade? entidade, CancellationToken cancellationToken)
    {
        var comMudancas = linhas
            .Select(l => (Linha: l, Mudancas: ComparadorAlteracoes.Comparar(Ler(l.DadosAntes), Ler(l.DadosDepois))))
            .ToList();

        var autores = await usuarios.ObterResumosAsync(linhas.Select(l => l.UsuarioId).Distinct().ToList(), cancellationToken);

        var camposReferencia = entidade?.Campos.Where(c => c.EhReferencia).Select(c => c.Chave).ToHashSet(StringComparer.Ordinal) ?? [];
        var idsApontados = comMudancas
            .SelectMany(x => x.Mudancas)
            .Where(m => camposReferencia.Contains(m.Campo))
            .SelectMany(m => new[] { ComoGuid(m.De), ComoGuid(m.Para) })
            .OfType<Guid>()
            .Distinct()
            .ToList();
        var rotulos = idsApontados.Count == 0
            ? new Dictionary<Guid, string?>()
            : await registros.ObterRotulosAsync(idsApontados, cancellationToken);

        return comMudancas
            .Select(x => new HistoricoDto(
                x.Linha.Id,
                HistoricoConversor.ParaTexto(x.Linha.Operacao),
                x.Linha.OcorridoEm,
                autores.GetValueOrDefault(x.Linha.UsuarioId) ?? new UsuarioResumoDto(x.Linha.UsuarioId, x.Linha.UsuarioMatricula, ""),
                x.Linha.CorrelacaoId,
                x.Mudancas
                    .Select(m => camposReferencia.Contains(m.Campo)
                        ? new MudancaDto(m.Campo, ComReferencia(m.De, rotulos), ComReferencia(m.Para, rotulos))
                        : new MudancaDto(m.Campo, m.De, m.Para))
                    .ToList(),
                x.Linha.Justificativa,
                Origem: x.Linha.Origem is { } origem ? HistoricoConversor.ParaTexto(origem) : null))
            .ToList();
    }

    private static JsonObject? Ler(string? json) => json is null ? null : JsonNode.Parse(json) as JsonObject;

    private static Guid? ComoGuid(JsonNode? valor) =>
        valor is JsonValue v && v.TryGetValue<string>(out var texto) && Guid.TryParse(texto, out var id) ? id : null;

    /// <summary>GUID gravado → { id, rotulo } com o rótulo atual (nulo se o registro não existe mais).</summary>
    private static JsonNode? ComReferencia(JsonNode? valor, IReadOnlyDictionary<Guid, string?> rotulos) =>
        ComoGuid(valor) is Guid id ? RegistroMapeamentos.ParaReferenciaDto(id, rotulos) : valor;
}
