using Catalogo.Application.Abstracoes;
using Catalogo.Application.Comum;
using Catalogo.Application.Dtos;
using Catalogo.Domain.Consultas;
using Catalogo.Domain.Historico;
using Catalogo.Domain.Validacao;

namespace Catalogo.Application.CasosDeUso.Avaliacoes;

/// <summary>
/// Caso de uso: histórico dos modelos de avaliação de uma entidade numa equipe (RN32, RN40), do mais
/// recente para o mais antigo: criação, alterações, ativação e desativação. Mesma permissão de configurar.
/// </summary>
public sealed class ListarHistoricoModeloAvaliacao(
    IUsuarioAtual usuarioAtual, IMetadadosProvider metadados, IEquipeRepository equipes,
    IHistoricoRepository historico, MontadorHistorico montador)
{
    public async Task<PaginaDto<HistoricoDto>> ExecutarAsync(
        Guid equipeId, string grupoChave, string entidadeChave, IEnumerable<KeyValuePair<string, string>> parametros,
        CancellationToken cancellationToken)
    {
        var usuario = await usuarioAtual.ObterAsync(cancellationToken);
        var entidade = await ConfiguracaoDaEquipe.ValidarAsync(
            usuario, equipeId, grupoChave, entidadeChave, AvaliacaoComum.Funcionalidade, equipes, metadados, cancellationToken);

        var erros = new ErrosPorCampo();
        var paginacao = InterpretadorConsulta.InterpretarPaginacao(parametros.ToList(), erros);
        erros.LancarSeHouverErros();

        var pagina = await historico.ListarAsync(AlvoHistorico.ModeloAvaliacao, equipeId, paginacao, cancellationToken, entidade.Id);
        var itens = await montador.MontarAsync(pagina.Itens, entidade: null, cancellationToken);
        return new PaginaDto<HistoricoDto>(itens, paginacao.Pagina, paginacao.Tamanho, pagina.Total, paginacao.TotalPaginas(pagina.Total));
    }
}
