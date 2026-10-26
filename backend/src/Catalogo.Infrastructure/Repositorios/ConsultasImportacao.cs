using Catalogo.Application.Abstracoes;
using Catalogo.Domain.Consultas;
using Catalogo.Domain.Importacoes;
using Catalogo.Domain.Metadados;
using Catalogo.Domain.Registros;
using Catalogo.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace Catalogo.Infrastructure.Repositorios;

/// <summary>
/// As consultas em lote da importação (RN99, RN102, RN103): uma por tipo de item referenciado, nunca uma por linha. Todas em LINQ
/// (docs/adr/0011); o escopo de visibilidade é o mesmo da listagem (<see cref="ConsultaRegistrosLinq.Filtrar"/>), no WHERE.
/// </summary>
internal sealed class ConsultasImportacao(CatalogoDbContext contexto) : IConsultasImportacao
{
    /// <summary>
    /// Quantos valores por consulta nas listas com Contains. Os hashes (varbinary) vão como constantes (EF.Constant), porque a lista
    /// de bytes não vira parâmetro OPENJSON; em lotes, para o SQL não ficar enorme.
    /// </summary>
    private const int TamanhoLote = 1000;

    public async Task<IReadOnlyList<ItemComNome>> BuscarPorNomesAsync(
        Guid entidadeId, Campo campoExibicao, IReadOnlyCollection<string> nomes, ConsultaRegistros escopo, CancellationToken cancellationToken)
    {
        if (nomes.Count == 0)
            return [];

        var visiveis = ConsultaRegistrosLinq.Filtrar(contexto, entidadeId, escopo);
        var valores = contexto.RegistrosValores.Where(v => v.CampoId == campoExibicao.Id);

        if (ResolvedorReferencias.ExibicaoDeTexto(campoExibicao))
        {
            // Igualdade de texto no banco: a collation Latin1_General_CI_AI não diferencia maiúsculas nem acentos.
            var textos = nomes.Select(n => n.Trim()).Distinct().ToList();
            valores = valores.Where(v => v.ValorTexto != null && textos.Contains(v.ValorTexto));
        }
        else if (ResolvedorReferencias.ExibicaoNumerica(campoExibicao))
        {
            var numeros = nomes
                .Select(n => ConversorCelulas.TentarNumero(CelulaImportada.DeTexto(n), out var numero) ? numero : (decimal?)null)
                .OfType<decimal>()
                .Distinct()
                .ToList();
            if (numeros.Count == 0)
                return [];
            valores = valores.Where(v => v.ValorNumero != null && numeros.Contains(v.ValorNumero.Value));
        }
        else
        {
            return [];
        }

        var linhas = await (
                from registro in visiveis
                join valor in valores on registro.Id equals valor.RegistroId
                select new { registro.Id, registro.EquipeId, registro.SituacaoAprovacao, registro.CriadoPorId, Valor = valor })
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return linhas
            .Select(l => new ItemComNome(
                l.Id,
                ConversorValorTipado.ParaTexto(campoExibicao.TipoDado, campoExibicao.Escala, l.Valor) ?? "",
                l.EquipeId,
                l.SituacaoAprovacao,
                l.CriadoPorId))
            .ToList();
    }

    public async Task<IReadOnlyList<string>> ListarNomesAsync(
        Guid entidadeId, Campo campoExibicao, ConsultaRegistros escopo, int limite, CancellationToken cancellationToken)
    {
        if (!ResolvedorReferencias.ExibicaoDeTexto(campoExibicao))
            return [];

        var visiveis = ConsultaRegistrosLinq.Filtrar(contexto, entidadeId, escopo);
        return await (
                from registro in visiveis
                join valor in contexto.RegistrosValores on registro.Id equals valor.RegistroId
                where valor.CampoId == campoExibicao.Id && valor.ValorTexto != null
                select valor.ValorTexto!)
            .Distinct()
            .OrderBy(nome => nome)
            .Take(limite)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<(Guid RestricaoId, string Hash), Guid>> ObterDonosDasChavesAsync(
        Guid entidadeId, IReadOnlyCollection<(Guid RestricaoId, byte[] Hash)> chaves, CancellationToken cancellationToken)
    {
        var donos = new Dictionary<(Guid, string), Guid>();
        var procuradas = chaves.Select(c => (c.RestricaoId, Hash: Convert.ToHexString(c.Hash))).ToHashSet();

        foreach (var lote in chaves.Select(c => c.Hash).DistinctBy(Convert.ToHexString).Chunk(TamanhoLote))
        {
            var hashes = lote.ToList();
            var encontradas = await contexto.RegistrosChavesUnicas.AsNoTracking()
                .Where(c => c.EntidadeId == entidadeId && EF.Constant(hashes).Contains(c.ValorHash))
                .Select(c => new { c.RestricaoUnicaId, c.ValorHash, c.RegistroId })
                .ToListAsync(cancellationToken);

            foreach (var chave in encontradas)
            {
                var id = (chave.RestricaoUnicaId, Convert.ToHexString(chave.ValorHash));
                if (procuradas.Contains(id))
                    donos[id] = chave.RegistroId;
            }
        }

        return donos;
    }

    public async Task<IReadOnlyList<Registro>> ObterParaAlteracaoAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken)
    {
        if (ids.Count == 0)
            return [];

        // Rastreados, como o PUT os carrega (RegistroRepository.ObterParaAlteracaoAsync), mas todos de uma vez.
        return await contexto.Registros
            .Include(r => r.Referencias)
            .Include(r => r.ChavesUnicas)
            .Include(r => r.Valores)
            .AsSplitQuery()
            .Where(r => ids.Contains(r.Id))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlySet<Guid>> AlteradosForaDaCorrelacaoAsync(
        IReadOnlyCollection<Guid> ids, Guid correlacaoId, CancellationToken cancellationToken)
    {
        if (ids.Count == 0)
            return new HashSet<Guid>();

        // Qualquer linha de histórico do item (alteração, decisão de aprovação, avaliação) que não seja da própria importação.
        return (await contexto.HistoricosAlteracao.AsNoTracking()
                .Where(h => ids.Contains(h.AlvoId) && h.CorrelacaoId != correlacaoId)
                .Select(h => h.AlvoId)
                .Distinct()
                .ToListAsync(cancellationToken))
            .ToHashSet();
    }
}
