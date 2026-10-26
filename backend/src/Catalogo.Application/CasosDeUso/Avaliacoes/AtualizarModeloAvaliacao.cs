using Catalogo.Application.Abstracoes;
using Catalogo.Application.Comum;
using Catalogo.Application.Dtos;
using Catalogo.Application.Mapeamentos;
using Catalogo.Domain.Avaliacoes;
using Catalogo.Domain.Excecoes;
using Catalogo.Domain.Historico;
using Catalogo.Domain.Validacao;

namespace Catalogo.Application.CasosDeUso.Avaliacoes;

/// <summary>
/// Caso de uso: alterar o modelo ATIVO (nome, descrição e a lista inteira de critérios), com a "versao" lida
/// no GET (RN12). Critérios são identificados pela chave. Com avaliações já feitas, um critério com
/// respostas não pode ser excluído nem mudar de tipo: deve ser desativado (RN43). Gera histórico.
/// </summary>
public sealed class AtualizarModeloAvaliacao(
    IUsuarioAtual usuarioAtual,
    IMetadadosProvider metadados,
    IEquipeRepository equipes,
    IModeloAvaliacaoRepository modelos,
    IUnidadeDeTrabalho unidadeDeTrabalho,
    ServicoHistorico historico,
    MontadorModeloAvaliacao montador,
    TimeProvider relogio)
{
    public async Task<ModeloAvaliacaoDto> ExecutarAsync(
        Guid equipeId, string grupoChave, string entidadeChave, ModeloAvaliacaoEntrada entrada, CancellationToken cancellationToken)
    {
        var usuario = await usuarioAtual.ObterAsync(cancellationToken);
        var entidade = await ConfiguracaoDaEquipe.ValidarAsync(
            usuario, equipeId, grupoChave, entidadeChave, AvaliacaoComum.Funcionalidade, equipes, metadados, cancellationToken);

        var modelo = await modelos.ObterAtivoParaAlteracaoAsync(entidade.Id, equipeId, cancellationToken)
            ?? throw new RecursoNaoEncontradoException($"'{entidade.Nome}' não tem modelo de avaliação ativo nesta equipe.", CodigosErro.ModeloAvaliacaoNaoEncontrado);

        // Versão e definição: todos os erros juntos (RN05).
        var erros = new ErrosPorCampo();
        var versao = AvaliacaoComum.LerVersao(entrada.Versao, "pelo GET do modelo", erros);
        var definido = RegrasModeloAvaliacao.Validar(AvaliacaoComum.ParaDefinicao(entrada), erros);
        erros.LancarSeHouverErros();

        // RN12: alguém alterou o modelo depois que o cliente o leu?
        if (!versao!.AsSpan().SequenceEqual(modelo.Versao))
            throw new ConflitoConcorrenciaException("O modelo foi alterado por outra pessoa depois que você o leu. Recarregue e tente de novo.");
        modelos.DefinirVersaoEsperada(modelo, versao!);

        // RN43: não corromper as avaliações já feitas.
        var comRespostas = await modelos.CriteriosComRespostasAsync(modelo.Id, cancellationToken);
        RegrasModeloAvaliacao.ValidarAlteracao(modelo, definido!, comRespostas, erros);
        erros.LancarSeHouverErros();

        var antes = modelo.ParaJsonHistorico();
        var removidos = RegrasModeloAvaliacao.Aplicar(modelo, definido!, usuario.Id, relogio.GetUtcNow().UtcDateTime);
        modelos.RemoverCriterios(removidos);
        historico.RegistrarModeloAvaliacao(OperacaoHistorico.Alteracao, modelo, usuario, antes, modelo.ParaJsonHistorico());

        await unidadeDeTrabalho.SalvarAlteracoesAsync(cancellationToken);
        return await montador.MontarAsync(modelo, grupoChave, entidade, cancellationToken);
    }
}
