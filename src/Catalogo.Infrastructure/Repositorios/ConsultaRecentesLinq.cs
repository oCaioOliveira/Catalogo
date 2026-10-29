using Catalogo.Application.Abstracoes;
using Catalogo.Domain.Historico;
using Catalogo.Infrastructure.Persistencia;

namespace Catalogo.Infrastructure.Repositorios;

/// <summary>
/// RN96: os registros que o usuário criou ou alterou por último, num SQL só. Parte do histórico dele (índice
/// IX_HistoricoAlteracao_Usuario, V010), só com os registros que ele VÊ hoje: os visíveis de cada entidade vêm de
/// <see cref="ConsultaRegistrosLinq.Filtrar"/> com o escopo da listagem, juntados com UNION ALL dentro de um IN (como em
/// <see cref="ConsultaRelacionadosLinq"/>). Agrupa por registro, pega a data mais recente e corta no limite, tudo no banco.
/// </summary>
internal static class ConsultaRecentesLinq
{
    public static IQueryable<ItemRecente> Montar(
        CatalogoDbContext contexto, Guid usuarioId, IReadOnlyList<EscopoDaEntidade> escopos, int limite)
    {
        var entidades = escopos.Select(e => (Guid?)e.EntidadeId).Distinct().ToList();

        IQueryable<Guid>? visiveis = null;
        foreach (var escopo in escopos)
        {
            var daEntidade = ConsultaRegistrosLinq.Filtrar(contexto, escopo.EntidadeId, escopo.Escopo).Select(r => r.Id);
            visiveis = visiveis is null ? daEntidade : visiveis.Concat(daEntidade);
        }

        return contexto.HistoricosAlteracao
            .Where(h => h.UsuarioId == usuarioId
                && h.AlvoTipo == AlvoHistorico.Registro
                && (h.Operacao == OperacaoHistorico.Criacao || h.Operacao == OperacaoHistorico.Alteracao)
                && entidades.Contains(h.EntidadeId))
            .Where(h => visiveis!.Contains(h.AlvoId))
            .GroupBy(h => new { h.AlvoId, h.EntidadeId })
            .Select(g => new { g.Key.AlvoId, g.Key.EntidadeId, Ultima = g.Max(h => h.OcorridoEm) })
            .OrderByDescending(x => x.Ultima)
            .ThenBy(x => x.AlvoId)
            .Take(limite)
            .Select(x => new ItemRecente(x.AlvoId, x.EntidadeId!.Value, x.Ultima));
    }
}
