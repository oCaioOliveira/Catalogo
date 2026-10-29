using Catalogo.Application.Abstracoes;
using Catalogo.Application.Comum;
using Catalogo.Application.Dtos;
using Catalogo.Application.Mapeamentos;
using Catalogo.Domain.Acesso;
using Catalogo.Domain.Avaliacoes;
using Catalogo.Domain.Consultas;
using Catalogo.Domain.Validacao;

namespace Catalogo.Application.CasosDeUso.Avaliacoes;

/// <summary>
/// Caso de uso: as avaliações de um item (GET .../avaliacoes, RN47).
///   - Resumo do modelo ATIVO (médias com uma casa, quantidades, distribuição e a avaliação do próprio usuário)
///     e, separados, os resumos dos modelos INATIVOS que avaliaram o item. Médias e contagens vêm do banco.
///   - Avaliações individuais (autor e textos), paginadas, das mais recentes para as mais antigas: só para
///     INTERNO, GESTOR e ADMINISTRADOR. Para o EXTERNO, "avaliacoes" vem nulo.
/// Quem vê o item vê o resumo (404 para os demais, RN28 e RN37).
/// </summary>
public sealed class ListarAvaliacoesRegistro(
    IUsuarioAtual usuarioAtual,
    IMetadadosProvider metadados,
    IRegistroRepository registros,
    IModeloAvaliacaoRepository modelos,
    IAvaliacaoRepository avaliacoes,
    IUsuarioRepository usuarios)
{
    public async Task<AvaliacoesDoRegistroDto> ExecutarAsync(
        string grupoChave, string entidadeChave, Guid id, IEnumerable<KeyValuePair<string, string>> parametros,
        CancellationToken cancellationToken)
    {
        var usuario = await usuarioAtual.ObterAsync(cancellationToken);
        (_, var entidade, usuario) = await metadados.ObterEntidadeOuFalharAsync(usuario, grupoChave, entidadeChave, UsoEntidade.Leitura, cancellationToken);

        var registro = await AvaliacaoComum.ObterRegistroAsync(registros, entidade, id, cancellationToken);
        Permissoes.Exigir(
            PoliticaPermissao.Decidir(usuario, OperacaoRegistro.Ver, RegistroAlvo.De(entidade, registro, false)),
            AvaliacaoComum.DescreverRegistro(id, entidade));

        var erros = new ErrosPorCampo();
        var paginacao = InterpretadorConsulta.InterpretarPaginacao(parametros.ToList(), erros);
        erros.LancarSeHouverErros();

        // O modelo ativo (mesmo sem avaliações) e os inativos que avaliaram este item.
        var ativo = registro.EquipeId is Guid equipe && !entidade.EhGlobal
            ? await modelos.ObterAtivoAsync(entidade.Id, equipe, cancellationToken)
            : null;
        var idsAvaliados = await avaliacoes.ModelosAvaliadosAsync(registro.Id, cancellationToken);
        var anteriores = await modelos.ObterPorIdsAsync(idsAvaliados.Where(m => m != ativo?.Id).ToList(), cancellationToken);
        var todos = new[] { ativo }.OfType<ModeloAvaliacao>().Concat(anteriores).ToDictionary(m => m.Id);

        var individuais = PoliticaPermissao.PodeVerAvaliacoesIndividuais(usuario);
        var minhas = individuais
            ? await avaliacoes.ListarDoUsuarioAsync(registro.Id, usuario.Id, cancellationToken)
            : [];
        var pagina = individuais
            ? await avaliacoes.ListarDoRegistroAsync(registro.Id, paginacao, cancellationToken)
            : null;

        var autores = await usuarios.ObterResumosAsync(
            minhas.Concat(pagina?.Itens ?? []).Select(a => a.UsuarioId).Distinct().ToList(), cancellationToken);

        AvaliacaoDto? Minha(ModeloAvaliacao modelo) =>
            minhas.FirstOrDefault(a => a.ModeloAvaliacaoId == modelo.Id)?.ParaDto(modelo, autores);

        var resumo = ativo is null ? null : await ResumirAsync(registro.Id, ativo, Minha(ativo), cancellationToken);
        var resumosAnteriores = new List<ResumoAvaliacaoDto>();
        foreach (var modelo in anteriores.OrderByDescending(m => m.CriadoEm))
            resumosAnteriores.Add(await ResumirAsync(registro.Id, modelo, Minha(modelo), cancellationToken));

        var itens = pagina?.Itens
            .Where(a => todos.ContainsKey(a.ModeloAvaliacaoId))
            .Select(a => a.ParaDto(todos[a.ModeloAvaliacaoId], autores))
            .ToList();

        return new AvaliacoesDoRegistroDto(
            resumo,
            resumosAnteriores,
            pagina is null
                ? null
                : new PaginaDto<AvaliacaoDto>(itens!, paginacao.Pagina, paginacao.Tamanho, pagina.Total, paginacao.TotalPaginas(pagina.Total)));
    }

    /// <summary>RN47: por critério ESTRELAS ativo, média com uma casa, quantidade e distribuição de 1 a 5.</summary>
    private async Task<ResumoAvaliacaoDto> ResumirAsync(
        Guid registroId, ModeloAvaliacao modelo, AvaliacaoDto? minha, CancellationToken cancellationToken)
    {
        var notas = await avaliacoes.ResumirAsync(registroId, modelo.Id, cancellationToken);
        var criterios = modelo.CriteriosAtivos
            .Where(c => c.Tipo == TipoCriterio.Estrelas)
            .Select(c =>
            {
                var doCriterio = notas.Criterios.FirstOrDefault(n => n.CriterioId == c.Id);
                var distribuicao = Enumerable.Range(1, 5)
                    .ToDictionary(e => e.ToString(), e => doCriterio?.Distribuicao.GetValueOrDefault(e) ?? 0);
                return new ResumoCriterioDto(
                    c.Chave, c.Nome, RegrasAvaliacao.ArredondarMedia(doCriterio?.Media), doCriterio?.Quantidade ?? 0, distribuicao);
            })
            .ToList();

        return new ResumoAvaliacaoDto(modelo.ParaResumoDto(), notas.Avaliadores, criterios, minha);
    }
}
