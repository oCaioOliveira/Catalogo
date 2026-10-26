using Catalogo.Application.Abstracoes;
using Catalogo.Application.Comum;
using Catalogo.Application.Dtos;
using Catalogo.Domain.Consultas;
using Catalogo.Domain.Excecoes;

namespace Catalogo.Application.CasosDeUso;

/// <summary>
/// Caso de uso: listar registros com filtros, ordenação e paginação (RN17 a RN19).
/// Os parâmetros da URL são interpretados no Domain (<see cref="InterpretadorConsulta"/>) e a
/// consulta é feita em LINQ pela Infrastructure, SEMPRE com o escopo de equipes do usuário (RN26),
/// montado pelo <see cref="PreparadorConsultaRegistros"/> (o mesmo da exportação).
/// Lança <see cref="RecursoNaoEncontradoException"/>, <see cref="ValidacaoRegistroException"/> ou
/// <see cref="AcessoNegadoException"/>.
/// </summary>
public sealed class ListarRegistros(
    PreparadorConsultaRegistros preparador,
    IRegistroRepository registros,
    MontadorRegistroDto montador)
{
    /// <param name="parametros">Todos os parâmetros da URL (um par por valor, inclusive repetidos).</param>
    public async Task<PaginaDto<RegistroDto>> ExecutarAsync(
        string grupoChave, string entidadeChave, IEnumerable<KeyValuePair<string, string>> parametros, CancellationToken cancellationToken)
    {
        var (_, entidade, consulta) = await preparador.PrepararAsync(grupoChave, entidadeChave, parametros.ToList(), cancellationToken);

        var pagina = await registros.ListarIdsAsync(entidade.Id, consulta, cancellationToken);

        // Carrega os registros da página e devolve na ordem da consulta.
        var itens = await CarregarNaOrdemAsync(registros, montador, entidade, pagina.Ids, cancellationToken);

        var paginacao = consulta.Paginacao;
        return new PaginaDto<RegistroDto>(itens, paginacao.Pagina, paginacao.Tamanho, pagina.Total, paginacao.TotalPaginas(pagina.Total));
    }

    /// <summary>Registros dos ids informados, já como DTO, na ordem dos ids (usado também pela exportação).</summary>
    internal static async Task<IReadOnlyList<RegistroDto>> CarregarNaOrdemAsync(
        IRegistroRepository registros, MontadorRegistroDto montador, Domain.Metadados.Entidade entidade,
        IReadOnlyList<Guid> ids, CancellationToken cancellationToken)
    {
        var porId = (await registros.ObterPorIdsAsync(ids, cancellationToken)).ToDictionary(r => r.Id);
        var ordenados = ids.Where(porId.ContainsKey).Select(id => porId[id]).ToList();
        return await montador.MontarAsync(ordenados, entidade, cancellationToken);
    }
}
