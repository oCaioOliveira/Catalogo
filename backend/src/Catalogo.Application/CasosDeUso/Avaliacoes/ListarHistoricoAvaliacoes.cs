using Catalogo.Application.Abstracoes;
using Catalogo.Application.Comum;
using Catalogo.Application.Dtos;
using Catalogo.Domain.Acesso;
using Catalogo.Domain.Consultas;
using Catalogo.Domain.Excecoes;
using Catalogo.Domain.Historico;
using Catalogo.Domain.Validacao;

namespace Catalogo.Application.CasosDeUso.Avaliacoes;

/// <summary>
/// Caso de uso: histórico das avaliações de um item (alvo AVALIACAO), do mais recente para o mais antigo
/// (RN32, RN45): criação, edição e remoção, com as respostas antes e depois. Como mostra autores e textos,
/// segue a visibilidade das avaliações individuais (RN47): o EXTERNO recebe 403. Funciona para item já excluído.
/// </summary>
public sealed class ListarHistoricoAvaliacoes(
    IUsuarioAtual usuarioAtual,
    IMetadadosProvider metadados,
    IRegistroRepository registros,
    IHistoricoRepository historico,
    MontadorHistorico montador)
{
    public async Task<PaginaDto<HistoricoDto>> ExecutarAsync(
        string grupoChave, string entidadeChave, Guid id, IEnumerable<KeyValuePair<string, string>> parametros,
        CancellationToken cancellationToken)
    {
        var usuario = await usuarioAtual.ObterAsync(cancellationToken);
        (_, var entidade, usuario) = await metadados.ObterEntidadeOuFalharAsync(usuario, grupoChave, entidadeChave, UsoEntidade.Leitura, cancellationToken);
        var descricao = $"Registro {id} em '{entidade.Nome}'";

        if (!PoliticaPermissao.PodeVerAvaliacoesIndividuais(usuario))
            throw new AcessoNegadoException("Seu perfil vê só o resumo das avaliações.");

        // Equipe e situação do próprio registro ou, se ele já foi excluído, a equipe gravada no histórico.
        RegistroAlvo alvo;
        var registro = await registros.ObterPorIdAsync(id, cancellationToken);
        if (registro is not null)
        {
            if (registro.EntidadeId != entidade.Id)
                throw new RecursoNaoEncontradoException($"{descricao} não encontrado.", CodigosErro.RegistroNaoEncontrado);
            alvo = RegistroAlvo.De(entidade, registro, false);
        }
        else
        {
            var ultima = await historico.ObterMaisRecenteAsync(AlvoHistorico.Avaliacao, id, cancellationToken);
            if (ultima?.EntidadeId != entidade.Id)
                throw new RecursoNaoEncontradoException($"{descricao} não encontrado.", CodigosErro.RegistroNaoEncontrado);
            alvo = new RegistroAlvo(entidade.EhGlobal, ultima.EquipeId);
        }

        Permissoes.Exigir(PoliticaPermissao.Decidir(usuario, OperacaoRegistro.Ver, alvo), descricao);

        var erros = new ErrosPorCampo();
        var paginacao = InterpretadorConsulta.InterpretarPaginacao(parametros.ToList(), erros);
        erros.LancarSeHouverErros();

        var pagina = await historico.ListarAsync(AlvoHistorico.Avaliacao, id, paginacao, cancellationToken);
        var itens = await montador.MontarAsync(pagina.Itens, entidade: null, cancellationToken);
        return new PaginaDto<HistoricoDto>(itens, paginacao.Pagina, paginacao.Tamanho, pagina.Total, paginacao.TotalPaginas(pagina.Total));
    }
}
