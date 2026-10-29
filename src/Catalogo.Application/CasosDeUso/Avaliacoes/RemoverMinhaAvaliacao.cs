using Catalogo.Application.Abstracoes;
using Catalogo.Application.Comum;
using Catalogo.Application.Mapeamentos;
using Catalogo.Domain.Acesso;
using Catalogo.Domain.Excecoes;
using Catalogo.Domain.Historico;

namespace Catalogo.Application.CasosDeUso.Avaliacoes;

/// <summary>
/// Caso de uso: o usuário remove a PRÓPRIA avaliação de um item, no modelo ativo (DELETE .../avaliacoes/minha).
/// Mesma permissão de avaliar (RN44). 404 se ele não avaliou. Gera histórico com as respostas removidas.
/// </summary>
public sealed class RemoverMinhaAvaliacao(
    IUsuarioAtual usuarioAtual,
    IMetadadosProvider metadados,
    IRegistroRepository registros,
    ContextoAprovacao contextoAprovacao,
    IModeloAvaliacaoRepository modelos,
    IAvaliacaoRepository avaliacoes,
    IUnidadeDeTrabalho unidadeDeTrabalho,
    ServicoHistorico historico)
{
    public async Task ExecutarAsync(string grupoChave, string entidadeChave, Guid id, CancellationToken cancellationToken)
    {
        var usuario = await usuarioAtual.ObterAsync(cancellationToken);
        (_, var entidade, usuario) = await metadados.ObterEntidadeOuFalharAsync(usuario, grupoChave, entidadeChave, UsoEntidade.Escrita, cancellationToken);

        var registro = await AvaliacaoComum.ObterRegistroAsync(registros, entidade, id, cancellationToken);
        var alvo = await contextoAprovacao.ExistenteAsync(entidade, registro, cancellationToken);
        Permissoes.Exigir(PoliticaPermissao.PodeAvaliar(usuario, alvo), AvaliacaoComum.DescreverRegistro(id, entidade));

        var modelo = await AvaliacaoComum.ObterModeloAtivoOuFalharAsync(modelos, entidade, registro, cancellationToken);
        var avaliacao = await avaliacoes.ObterParaAlteracaoAsync(registro.Id, modelo.Id, usuario.Id, cancellationToken)
            ?? throw new RecursoNaoEncontradoException("Você ainda não avaliou este item.", CodigosErro.AvaliacaoNaoEncontrada);

        historico.RegistrarAvaliacao(OperacaoHistorico.Exclusao, registro, usuario.Id, usuario.Matricula, Guid.NewGuid(),
            avaliacao.ParaJsonHistorico(modelo), null);
        avaliacoes.Remover(avaliacao);
        await unidadeDeTrabalho.SalvarAlteracoesAsync(cancellationToken);
    }
}
