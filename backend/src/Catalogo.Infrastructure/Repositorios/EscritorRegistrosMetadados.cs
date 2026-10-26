using Catalogo.Application.Abstracoes;
using Catalogo.Domain.ChavesUnicas;
using Catalogo.Domain.Metadados;
using Catalogo.Domain.Metadados.Administracao;
using Catalogo.Domain.Registros;
using Catalogo.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace Catalogo.Infrastructure.Repositorios;

/// <summary>
/// As escritas em massa das alterações de metadados (RN58), em LOTES de <see cref="TamanhoLote"/> registros, na transação da
/// alteração: cada lote é carregado, alterado pelo Domain (SincronizadorRegistro, CalculadoraChaveUnica, para Dados,
/// RegistroValor e RegistroChaveUnica andarem juntos) e gravado; depois o rastreamento é limpo, para a memória não crescer.
/// A Versao (rowversion) de cada registro alterado muda no UPDATE: quem estava editando um deles recebe 409 (RN12).
/// </summary>
internal sealed class EscritorRegistrosMetadados(
    CatalogoDbContext contexto, IUnidadeDeTrabalho unidadeDeTrabalho, CalculadoraChaveUnica calculadora) : IEscritorRegistrosMetadados
{
    public const int TamanhoLote = 500;

    public Task<int> AplicarAsync(EscritaRegistros escrita, Entidade entidade, CancellationToken cancellationToken) => escrita switch
    {
        PreencherValorPadrao preencher => PreencherAsync(preencher, entidade, cancellationToken),
        RecalcularChavesUnicas recalcular => RecalcularAsync(recalcular, entidade, cancellationToken),
        _ => throw new ArgumentOutOfRangeException(nameof(escrita), escrita, "Escrita sem implementação."),
    };

    /// <summary>
    /// RN60 e RN61: grava o valor padrão nos registros SEM valor no campo (num campo novo, todos). Cada lote preenchido deixa de
    /// passar no filtro, por isso a consulta é sempre a primeira página.
    /// </summary>
    private async Task<int> PreencherAsync(PreencherValorPadrao escrita, Entidade entidade, CancellationToken cancellationToken)
    {
        var campo = entidade.ObterCampo(escrita.ChaveCampo)
            ?? throw new InvalidOperationException($"Campo '{escrita.ChaveCampo}' não encontrado para o valor padrão.");
        var total = 0;

        while (true)
        {
            var lote = await contexto.Registros
                .Include(r => r.Valores)
                .Where(r => r.EntidadeId == entidade.Id
                            && !contexto.RegistrosValores.Any(v => v.RegistroId == r.Id && v.CampoId == campo.Id))
                .OrderBy(r => r.Id)
                .Take(TamanhoLote)
                .ToListAsync(cancellationToken);
            if (lote.Count == 0)
                return total;

            foreach (var registro in lote)
                SincronizadorRegistro.DefinirValor(registro, campo, escrita.Valor);

            await GravarLoteAsync(cancellationToken);
            total += lote.Count;
        }
    }

    /// <summary>
    /// RN71 e RN09: (re)gera as chaves das restrições em todos os registros, com a estrutura nova (restrição nova, ordem dos
    /// campos ou escala). As chaves antigas saem ANTES: atualizar no lugar poderia colidir no UNIQUE no meio do lote (o hash
    /// novo de um registro igual ao antigo de outro, numa ordem de campos trocada).
    /// </summary>
    private async Task<int> RecalcularAsync(RecalcularChavesUnicas escrita, Entidade entidade, CancellationToken cancellationToken)
    {
        var restricoes = entidade.RestricoesUnicas.Where(r => escrita.Restricoes.Contains(r.Nome, StringComparer.OrdinalIgnoreCase)).ToList();
        var ids = restricoes.Select(r => r.Id).ToList();
        await contexto.RegistrosChavesUnicas.Where(c => ids.Contains(c.RestricaoUnicaId)).ExecuteDeleteAsync(cancellationToken);

        var total = 0;
        for (var pagina = 0; ; pagina++)
        {
            var lote = await contexto.Registros.AsNoTracking()
                .Include(r => r.Referencias)
                .Where(r => r.EntidadeId == entidade.Id)
                .OrderBy(r => r.Id)
                .Skip(pagina * TamanhoLote)
                .Take(TamanhoLote)
                .ToListAsync(cancellationToken);

            foreach (var registro in lote)
            {
                var leitor = SincronizadorRegistro.LeitorDeValores(registro);
                var gerou = false;
                foreach (var restricao in restricoes)
                {
                    // RN10: com algum valor vazio, não há chave.
                    if (calculadora.CalcularHash(restricao, leitor, entidade.EhGlobal ? null : registro.EquipeId) is not { } hash)
                        continue;

                    contexto.RegistrosChavesUnicas.Add(new RegistroChaveUnica
                    {
                        RestricaoUnicaId = restricao.Id,
                        EntidadeId = entidade.Id,
                        RegistroId = registro.Id,
                        ValorHash = hash,
                    });
                    gerou = true;
                }

                if (gerou)
                    total++;
            }

            await GravarLoteAsync(cancellationToken);
            if (lote.Count < TamanhoLote)
                return total;
        }
    }

    private async Task GravarLoteAsync(CancellationToken cancellationToken)
    {
        await unidadeDeTrabalho.SalvarAlteracoesAsync(cancellationToken);
        contexto.ChangeTracker.Clear();
    }
}
