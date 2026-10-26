using Catalogo.Application.Abstracoes;
using Catalogo.Application.Comum;
using Catalogo.Application.Dtos;
using Catalogo.Application.Mapeamentos;
using Catalogo.Domain.Avaliacoes;
using Catalogo.Domain.Excecoes;
using Catalogo.Domain.Historico;

namespace Catalogo.Application.CasosDeUso.Avaliacoes;

/// <summary>
/// Caso de uso: desativar o modelo ativo ({ "ativo": false }) ou reativar um modelo ({ "ativo": true, "modeloId": "..." }).
/// Desativado, o modelo para de aceitar avaliações, mas as que ele tem continuam visíveis, e a equipe pode
/// criar outro modelo (RN41). Reativar com outro modelo ativo: 409. Gera histórico.
/// </summary>
public sealed class AlterarAtivacaoModeloAvaliacao(
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
        Guid equipeId, string grupoChave, string entidadeChave, AtivacaoModeloEntrada entrada, CancellationToken cancellationToken)
    {
        var usuario = await usuarioAtual.ObterAsync(cancellationToken);
        var entidade = await ConfiguracaoDaEquipe.ValidarAsync(
            usuario, equipeId, grupoChave, entidadeChave, AvaliacaoComum.Funcionalidade, equipes, metadados, cancellationToken);

        var ativo = entrada.Ativo
            ?? throw new ValidacaoRegistroException(new Dictionary<string, string[]> { ["ativo"] = ["Campo obrigatório (true ou false)."] });
        if (ativo && entrada.ModeloId is null)
        {
            throw new ValidacaoRegistroException(new Dictionary<string, string[]>
            {
                ["modeloId"] = ["Obrigatório para reativar: informe qual modelo (veja GET .../avaliacao/modelos)."],
            });
        }

        // Sem modeloId (só na desativação), vale o modelo ativo.
        var modelo = entrada.ModeloId is Guid modeloId
            ? await modelos.ObterParaAlteracaoAsync(modeloId, cancellationToken)
            : await modelos.ObterAtivoParaAlteracaoAsync(entidade.Id, equipeId, cancellationToken);
        if (modelo is null || modelo.EntidadeId != entidade.Id || modelo.EquipeId != equipeId)
        {
            throw new RecursoNaoEncontradoException(entrada.ModeloId is null
                ? $"'{entidade.Nome}' não tem modelo de avaliação ativo nesta equipe."
                : $"Modelo de avaliação {entrada.ModeloId} não encontrado para '{entidade.Nome}' nesta equipe.", CodigosErro.ModeloAvaliacaoNaoEncontrado);
        }

        if (modelo.Ativo != ativo)
        {
            // RN41: só um ativo por entidade e equipe.
            if (ativo && await modelos.ObterAtivoAsync(entidade.Id, equipeId, cancellationToken) is { } outroAtivo)
                throw AvaliacaoComum.ModeloAtivoExistente(entidade, outroAtivo.Id);

            var antes = modelo.ParaJsonHistorico();
            modelo.Ativo = ativo;
            modelo.AtualizadoPorId = usuario.Id;
            modelo.AtualizadoEm = relogio.GetUtcNow().UtcDateTime;
            historico.RegistrarModeloAvaliacao(OperacaoHistorico.Alteracao, modelo, usuario, antes, modelo.ParaJsonHistorico());

            try
            {
                await unidadeDeTrabalho.SalvarAlteracoesAsync(cancellationToken);
            }
            catch (ViolacaoUnicidadeBancoException)
            {
                throw AvaliacaoComum.ModeloAtivoExistente(entidade, null);
            }
        }

        return await montador.MontarAsync(modelo, grupoChave, entidade, cancellationToken);
    }
}
