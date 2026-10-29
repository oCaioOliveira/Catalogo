using Catalogo.Application.Abstracoes;
using Catalogo.Application.Comum;
using Catalogo.Application.Dtos;
using Catalogo.Domain.Consultas;
using Catalogo.Domain.Historico;
using Catalogo.Domain.Validacao;

namespace Catalogo.Application.CasosDeUso.Aprovacao;

/// <summary>
/// Caso de uso: histórico da configuração de uma entidade numa equipe (RN32, RN33), do mais recente
/// para o mais antigo. Mesma permissão de configurar.
/// </summary>
public sealed class ListarHistoricoConfiguracao(
    IUsuarioAtual usuarioAtual, IMetadadosProvider metadados, IEquipeRepository equipes,
    IHistoricoRepository historico, MontadorHistorico montador)
{
    public async Task<PaginaDto<HistoricoDto>> ExecutarAsync(
        Guid equipeId, string grupoChave, string entidadeChave, IEnumerable<KeyValuePair<string, string>> parametros,
        CancellationToken cancellationToken)
    {
        var usuario = await usuarioAtual.ObterAsync(cancellationToken);
        var entidade = await ConfiguracaoDaEquipe.ValidarAsync(
            usuario, equipeId, grupoChave, entidadeChave, "aprovação", equipes, metadados, cancellationToken);

        var erros = new ErrosPorCampo();
        var paginacao = InterpretadorConsulta.InterpretarPaginacao(parametros.ToList(), erros);
        erros.LancarSeHouverErros();

        var pagina = await historico.ListarAsync(AlvoHistorico.Configuracao, equipeId, paginacao, cancellationToken, entidade.Id);
        var itens = await montador.MontarAsync(pagina.Itens, entidade: null, cancellationToken);
        return new PaginaDto<HistoricoDto>(itens, paginacao.Pagina, paginacao.Tamanho, pagina.Total, paginacao.TotalPaginas(pagina.Total));
    }
}
