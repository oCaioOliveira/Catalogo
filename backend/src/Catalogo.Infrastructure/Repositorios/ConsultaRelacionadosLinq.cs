using Catalogo.Application.Abstracoes;
using Catalogo.Infrastructure.Persistencia;

namespace Catalogo.Infrastructure.Repositorios;

/// <summary>Quantos registros visíveis apontam para o registro por um campo (RN94).</summary>
internal sealed record QuantidadePorCampo(Guid CampoId, int Quantidade);

/// <summary>
/// RN94 (ADR 0047): a contagem dos itens relacionados num SQL só. Agrupa <c>RegistroReferencia</c> por campo, só com as referências
/// cujo registro de ORIGEM é visível para o usuário: os registros visíveis de cada entidade de origem vêm de
/// <see cref="ConsultaRegistrosLinq.Filtrar"/> com o escopo da listagem (equipes nesta listagem, aprovação, globais), juntados com
/// UNION ALL dentro de um IN. Nada é lido para a memória antes do GROUP BY.
/// </summary>
internal static class ConsultaRelacionadosLinq
{
    public static IQueryable<QuantidadePorCampo> Montar(
        CatalogoDbContext contexto, Guid registroId, IReadOnlyList<ContagemPorCampo> contagens)
    {
        var campos = contagens.Select(c => c.CampoId).Distinct().ToList();

        // O escopo é por entidade de origem (dois campos da mesma entidade usam o mesmo).
        IQueryable<Guid>? visiveis = null;
        foreach (var origem in contagens.GroupBy(c => c.EntidadeOrigemId))
        {
            var daOrigem = ConsultaRegistrosLinq.Filtrar(contexto, origem.Key, origem.First().Escopo).Select(r => r.Id);
            visiveis = visiveis is null ? daOrigem : visiveis.Concat(daOrigem);
        }

        return contexto.RegistrosReferencias
            .Where(rr => rr.RegistroDestinoId == registroId && campos.Contains(rr.CampoId))
            .Where(rr => visiveis!.Contains(rr.RegistroOrigemId))
            .GroupBy(rr => rr.CampoId)
            .Select(g => new QuantidadePorCampo(g.Key, g.Count()));
    }
}
