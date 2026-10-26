using Catalogo.Application.Abstracoes;
using Catalogo.Application.Comum;
using Catalogo.Application.Dtos;
using Catalogo.Domain.Acesso;
using Catalogo.Domain.Consultas;
using Catalogo.Domain.Excecoes;
using Catalogo.Domain.Historico;
using Catalogo.Domain.Validacao;

namespace Catalogo.Application.CasosDeUso.Historico;

/// <summary>
/// Caso de uso: histórico de um registro, do mais recente para o mais antigo (RN32). Quem pode VER
/// o registro pode ver o histórico. Para registro já excluído, vale a equipe gravada no histórico.
/// Lança <see cref="RecursoNaoEncontradoException"/> (também para registro de outra equipe, RN28).
/// </summary>
public sealed class ListarHistoricoRegistro(
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

        // Equipe e entidade do registro: do próprio registro ou, se ele já foi excluído, do histórico.
        // Equipe e situação: do próprio registro (RN28 e RN37) ou, se ele já foi excluído, a equipe do histórico.
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
            var ultima = await historico.ObterMaisRecenteAsync(AlvoHistorico.Registro, id, cancellationToken);
            if (ultima?.EntidadeId != entidade.Id)
                throw new RecursoNaoEncontradoException($"{descricao} não encontrado.", CodigosErro.RegistroNaoEncontrado);
            alvo = new RegistroAlvo(entidade.EhGlobal, ultima.EquipeId);
        }

        Permissoes.Exigir(PoliticaPermissao.Decidir(usuario, OperacaoRegistro.Ver, alvo), descricao);

        var erros = new ErrosPorCampo();
        var paginacao = InterpretadorConsulta.InterpretarPaginacao(parametros.ToList(), erros);
        erros.LancarSeHouverErros();

        var pagina = await historico.ListarAsync(AlvoHistorico.Registro, id, paginacao, cancellationToken);
        var itens = await montador.MontarAsync(pagina.Itens, entidade, cancellationToken);
        return new PaginaDto<HistoricoDto>(itens, paginacao.Pagina, paginacao.Tamanho, pagina.Total, paginacao.TotalPaginas(pagina.Total));
    }
}
