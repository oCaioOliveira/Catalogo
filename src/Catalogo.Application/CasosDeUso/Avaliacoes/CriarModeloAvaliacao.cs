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
/// Caso de uso: criar o modelo de avaliação de uma entidade numa equipe, com os critérios (RN40 a RN42).
/// Só GESTOR da equipe ou ADMINISTRADOR. 409 se já existe um modelo ativo (RN41). Gera histórico.
/// </summary>
public sealed class CriarModeloAvaliacao(
    IUsuarioAtual usuarioAtual,
    IMetadadosProvider metadados,
    IEquipeRepository equipes,
    IModeloAvaliacaoRepository modelos,
    IUnidadeDeTrabalho unidadeDeTrabalho,
    ServicoHistorico historico,
    MontadorModeloAvaliacao montador)
{
    public async Task<ModeloAvaliacaoDto> ExecutarAsync(
        Guid equipeId, string grupoChave, string entidadeChave, ModeloAvaliacaoEntrada entrada, CancellationToken cancellationToken)
    {
        var usuario = await usuarioAtual.ObterAsync(cancellationToken);
        var entidade = await ConfiguracaoDaEquipe.ValidarAsync(
            usuario, equipeId, grupoChave, entidadeChave, AvaliacaoComum.Funcionalidade, equipes, metadados, cancellationToken);

        // RN41: no máximo um modelo ativo por entidade e equipe.
        if (await modelos.ObterAtivoAsync(entidade.Id, equipeId, cancellationToken) is { } ativo)
            throw AvaliacaoComum.ModeloAtivoExistente(entidade, ativo.Id);

        // RN42: todos os erros da definição juntos.
        var erros = new ErrosPorCampo();
        var definido = RegrasModeloAvaliacao.Validar(AvaliacaoComum.ParaDefinicao(entrada), erros);
        erros.LancarSeHouverErros();

        var modelo = RegrasModeloAvaliacao.Criar(definido!, entidade.Id, equipeId, usuario.Id);
        modelos.Adicionar(modelo);
        historico.RegistrarModeloAvaliacao(OperacaoHistorico.Criacao, modelo, usuario, null, modelo.ParaJsonHistorico());

        try
        {
            await unidadeDeTrabalho.SalvarAlteracoesAsync(cancellationToken);
        }
        catch (ViolacaoUnicidadeBancoException)
        {
            // Outra requisição criou um modelo ativo ao mesmo tempo (índice único filtrado UX_ModeloAvaliacao_Ativo).
            throw AvaliacaoComum.ModeloAtivoExistente(entidade, null);
        }

        return await montador.MontarAsync(modelo, grupoChave, entidade, cancellationToken);
    }
}
