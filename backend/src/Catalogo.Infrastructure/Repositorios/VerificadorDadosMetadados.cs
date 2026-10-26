using Catalogo.Application.Abstracoes;
using Catalogo.Domain.ChavesUnicas;
using Catalogo.Domain.Metadados;
using Catalogo.Domain.Metadados.Administracao;
using Catalogo.Domain.Registros;
using Catalogo.Domain.Validacao.Tipos;
using Catalogo.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace Catalogo.Infrastructure.Repositorios;

/// <summary>
/// Responde às verificações contra os dados das alterações de metadados (RN58), em LINQ sobre Registro, RegistroValor e
/// RegistroReferencia (docs/adr/0011). Roda dentro da transação Serializable da alteração: o que foi lido fica travado
/// até a gravação, então um registro inserido no meio não escapa.
/// Cada verificação devolve a quantidade e até 10 ids de exemplo (os rótulos são buscados pelo caso de uso).
/// </summary>
internal sealed class VerificadorDadosMetadados(CatalogoDbContext contexto, CalculadoraChaveUnica calculadora) : IVerificadorDadosMetadados
{
    private const int TamanhoLote = 500;

    public Task<ResultadoVerificacao> VerificarAsync(VerificacaoDados verificacao, CancellationToken cancellationToken) => verificacao switch
    {
        RegistrosDaEntidade v => ContarAsync(contexto.Registros.Where(r => r.EntidadeId == v.EntidadeId).Select(r => r.Id), cancellationToken),
        RegistrosSemValor v => ContarAsync(SemValor(v.Campo), cancellationToken),
        RegistrosComValor v => ContarAsync(ComValor(v.Campo), cancellationToken),
        TextosAcimaDoTamanho v => ContarAsync(TextosAcimaDoTamanho(v), cancellationToken),
        NumerosForaDaPrecisao v => ContarAsync(NumerosForaDaPrecisao(v), cancellationToken),
        ValoresComOpcoes v => ContarAsync(ValoresComOpcoes(v), cancellationToken),
        ViolacoesDaDependencia v => ContarAsync(Violacoes(v.Dependencia), cancellationToken),
        DuplicatasNaRestricao v => DuplicatasAsync(v, cancellationToken),
        ValoresQueNaoSaoUrl v => ValoresQueNaoSaoUrlAsync(v, cancellationToken),
        HistoricoDaEntidade v => ContarAsync(
            contexto.HistoricosAlteracao.Where(h => h.EntidadeId == v.EntidadeId).Select(h => h.AlvoId).Distinct(), cancellationToken),
        ConfiguracoesDaEntidade v => ConfiguracoesAsync(v.EntidadeId, cancellationToken),
        RegistrosDaEquipeNoGrupo v => ContarAsync(
            contexto.Registros
                .Where(r => r.EquipeId == v.EquipeId && contexto.Entidades.Any(e => e.Id == r.EntidadeId && e.GrupoId == v.GrupoId))
                .Select(r => r.Id),
            cancellationToken),
        _ => throw new ArgumentOutOfRangeException(nameof(verificacao), verificacao, "Verificação sem implementação."),
    };

    /// <summary>RN60 e RN61: registros da entidade sem valor no campo (campo novo: todos).</summary>
    private IQueryable<Guid> SemValor(Campo campo) => campo.EhReferencia
        ? contexto.Registros
            .Where(r => r.EntidadeId == campo.EntidadeId
                        && !contexto.RegistrosReferencias.Any(x => x.RegistroOrigemId == r.Id && x.CampoId == campo.Id))
            .Select(r => r.Id)
        : contexto.Registros
            .Where(r => r.EntidadeId == campo.EntidadeId
                        && !contexto.RegistrosValores.Any(x => x.RegistroId == r.Id && x.CampoId == campo.Id))
            .Select(r => r.Id);

    /// <summary>RN70: registros com valor no campo.</summary>
    private IQueryable<Guid> ComValor(Campo campo) => campo.EhReferencia
        ? contexto.RegistrosReferencias.Where(x => x.CampoId == campo.Id).Select(x => x.RegistroOrigemId)
        : contexto.RegistrosValores.Where(x => x.CampoId == campo.Id).Select(x => x.RegistroId);

    /// <summary>RN63: textos com mais caracteres que o novo limite (LEN no SQL Server; os textos já são gravados sem espaços nas pontas).</summary>
    private IQueryable<Guid> TextosAcimaDoTamanho(TextosAcimaDoTamanho v) =>
        contexto.RegistrosValores
            .Where(x => x.CampoId == v.Campo.Id && x.ValorTexto != null && x.ValorTexto.Length > v.Tamanho)
            .Select(x => x.RegistroId);

    /// <summary>
    /// RN65: números que não cabem em DECIMAL(precisão, escala): mais casas decimais que a escala (ROUND diferente do valor) ou
    /// parte inteira com mais de (precisão − escala) dígitos.
    /// </summary>
    private IQueryable<Guid> NumerosForaDaPrecisao(NumerosForaDaPrecisao v)
    {
        var escala = v.Escala;
        var limiteInteiro = Potencia10(v.Precisao - v.Escala);
        return contexto.RegistrosValores
            .Where(x => x.CampoId == v.Campo.Id && x.ValorNumero != null
                        && (Math.Round(x.ValorNumero.Value, escala) != x.ValorNumero.Value || Math.Abs(x.ValorNumero.Value) >= limiteInteiro))
            .Select(x => x.RegistroId);
    }

    /// <summary>RN68: registros que usam uma das opções removidas da LISTA.</summary>
    private IQueryable<Guid> ValoresComOpcoes(ValoresComOpcoes v)
    {
        var opcoes = v.Opcoes.ToList();
        return contexto.RegistrosValores
            .Where(x => x.CampoId == v.Campo.Id && x.ValorTexto != null && opcoes.Contains(x.ValorTexto))
            .Select(x => x.RegistroId);
    }

    /// <summary>
    /// RN73, com a semântica da RN08: no registro, o campo dependente aponta para um registro cujo CampoAlvo é diferente (ou
    /// vazio) do valor de CampoOrigem, lido no próprio registro (sem via) ou no registro apontado pelo via. Origem vazia: aceita.
    /// </summary>
    private IQueryable<Guid> Violacoes(CampoDependencia dependencia)
    {
        var campoId = dependencia.Campo.Id;
        var origemId = dependencia.CampoOrigem.Id;
        var alvoId = dependencia.CampoAlvo.Id;
        var referencias = contexto.RegistrosReferencias;

        if (dependencia.CampoVia is null)
        {
            return from d in referencias
                   where d.CampoId == campoId
                   let origem = referencias.Where(o => o.RegistroOrigemId == d.RegistroOrigemId && o.CampoId == origemId)
                       .Select(o => (Guid?)o.RegistroDestinoId).FirstOrDefault()
                   let alvo = referencias.Where(a => a.RegistroOrigemId == d.RegistroDestinoId && a.CampoId == alvoId)
                       .Select(a => (Guid?)a.RegistroDestinoId).FirstOrDefault()
                   where origem != null && (alvo == null || alvo != origem)
                   select d.RegistroOrigemId;
        }

        var viaId = dependencia.CampoVia.Id;
        return from d in referencias
               where d.CampoId == campoId
               let origem = (from v in referencias
                             where v.RegistroOrigemId == d.RegistroOrigemId && v.CampoId == viaId
                             from o in referencias
                             where o.RegistroOrigemId == v.RegistroDestinoId && o.CampoId == origemId
                             select (Guid?)o.RegistroDestinoId).FirstOrDefault()
               let alvo = referencias.Where(a => a.RegistroOrigemId == d.RegistroDestinoId && a.CampoId == alvoId)
                   .Select(a => (Guid?)a.RegistroDestinoId).FirstOrDefault()
               where origem != null && (alvo == null || alvo != origem)
               select d.RegistroOrigemId;
    }

    /// <summary>
    /// RN71: registros que já repetem os valores da restrição nova. A unicidade é definida pelo HASH da
    /// <see cref="CalculadoraChaveUnica"/> (normalização de acentos, maiúsculas, escala, equipe, RN09): os registros são lidos
    /// em lotes (LINQ) e o hash é calculado com a MESMA calculadora da gravação, para a verificação ser idêntica à regra.
    /// </summary>
    private async Task<ResultadoVerificacao> DuplicatasAsync(DuplicatasNaRestricao v, CancellationToken cancellationToken)
    {
        var porHash = new Dictionary<string, List<Guid>>();
        for (var pagina = 0; ; pagina++)
        {
            var lote = await contexto.Registros.AsNoTracking()
                .Include(r => r.Referencias)
                .Where(r => r.EntidadeId == v.Entidade.Id)
                .OrderBy(r => r.Id)
                .Skip(pagina * TamanhoLote)
                .Take(TamanhoLote)
                .ToListAsync(cancellationToken);

            foreach (var registro in lote)
            {
                var hash = calculadora.CalcularHash(
                    v.Restricao, SincronizadorRegistro.LeitorDeValores(registro), v.Entidade.EhGlobal ? null : registro.EquipeId);
                if (hash is null)
                    continue;   // RN10: valor vazio não gera chave nem conflita.

                var chave = Convert.ToHexString(hash);
                if (!porHash.TryGetValue(chave, out var ids))
                    porHash[chave] = ids = [];
                ids.Add(registro.Id);
            }

            if (lote.Count < TamanhoLote)
                break;
        }

        var grupos = porHash.Values.Where(ids => ids.Count > 1).ToList();
        return new ResultadoVerificacao(
            grupos.Sum(g => g.Count),
            grupos.SelectMany(g => g).Take(ImpactoDados.MaximoExemplos).ToList(),
            grupos.Take(ImpactoDados.MaximoExemplos).Select(g => (IReadOnlyList<Guid>)g.Take(ImpactoDados.MaximoExemplos).ToList()).ToList());
    }

    /// <summary>
    /// RN66 (para URL, ADR 0042): valores do campo que não são URL válida. A regra (esquema http/https, host, sem espaços, até
    /// 2000 caracteres) é a do <see cref="UrlValidador.Conferir"/>, a MESMA da gravação: os valores são lidos em lotes (LINQ) e
    /// conferidos em C#, porque o SQL não tem como reproduzir o Uri.TryCreate.
    /// </summary>
    private async Task<ResultadoVerificacao> ValoresQueNaoSaoUrlAsync(ValoresQueNaoSaoUrl v, CancellationToken cancellationToken)
    {
        var invalidos = new List<Guid>();
        for (var pagina = 0; ; pagina++)
        {
            var lote = await contexto.RegistrosValores.AsNoTracking()
                .Where(x => x.CampoId == v.Campo.Id && x.ValorTexto != null)
                .OrderBy(x => x.RegistroId)
                .Skip(pagina * TamanhoLote)
                .Take(TamanhoLote)
                .Select(x => new { x.RegistroId, x.ValorTexto })
                .ToListAsync(cancellationToken);

            invalidos.AddRange(lote.Where(x => UrlValidador.Conferir(x.ValorTexto!.Trim()) is not null).Select(x => x.RegistroId));
            if (lote.Count < TamanhoLote)
                break;
        }

        return invalidos.Count == 0
            ? ResultadoVerificacao.Nenhum
            : new ResultadoVerificacao(invalidos.Count, invalidos.Take(ImpactoDados.MaximoExemplos).ToList());
    }

    /// <summary>RN74 e RN76: configuração de aprovação ou modelo de avaliação da entidade, em qualquer equipe.</summary>
    private async Task<ResultadoVerificacao> ConfiguracoesAsync(Guid entidadeId, CancellationToken cancellationToken)
    {
        var quantidade = await contexto.ConfiguracoesEntidadesEquipes.CountAsync(c => c.EntidadeId == entidadeId, cancellationToken)
                         + await contexto.ModelosAvaliacao.CountAsync(m => m.EntidadeId == entidadeId, cancellationToken);
        return new ResultadoVerificacao(quantidade, []);
    }

    private static async Task<ResultadoVerificacao> ContarAsync(IQueryable<Guid> ids, CancellationToken cancellationToken)
    {
        var quantidade = await ids.CountAsync(cancellationToken);
        if (quantidade == 0)
            return ResultadoVerificacao.Nenhum;

        var exemplos = await ids.OrderBy(id => id).Take(ImpactoDados.MaximoExemplos).ToListAsync(cancellationToken);
        return new ResultadoVerificacao(quantidade, exemplos);
    }

    private static decimal Potencia10(int expoente)
    {
        var resultado = 1m;
        for (var i = 0; i < expoente; i++)
            resultado *= 10;
        return resultado;
    }
}
