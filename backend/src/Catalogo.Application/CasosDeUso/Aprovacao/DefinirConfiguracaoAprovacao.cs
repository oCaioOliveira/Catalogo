using System.Text.Json.Nodes;
using Catalogo.Application.Abstracoes;
using Catalogo.Application.Comum;
using Catalogo.Application.Dtos;
using Catalogo.Domain.Aprovacao;
using Catalogo.Domain.Excecoes;
using Catalogo.Domain.Historico;

namespace Catalogo.Application.CasosDeUso.Aprovacao;

/// <summary>
/// Caso de uso: ativar ou desativar a aprovação de uma entidade numa equipe (RN33).
/// Só GESTOR da equipe ou ADMINISTRADOR. Desativar com registros PENDENTES é recusado (409) até o gestor
/// decidir os pendentes. Toda mudança gera histórico (alvo CONFIGURACAO).
/// </summary>
public sealed class DefinirConfiguracaoAprovacao(
    IUsuarioAtual usuarioAtual,
    IMetadadosProvider metadados,
    IEquipeRepository equipes,
    IConfiguracaoRepository configuracoes,
    IRegistroRepository registros,
    IUnidadeDeTrabalho unidadeDeTrabalho,
    ServicoHistorico historico,
    TimeProvider relogio,
    ObterConfiguracaoAprovacao obterConfiguracao)
{
    public async Task<ConfiguracaoAprovacaoDto> ExecutarAsync(
        Guid equipeId, string grupoChave, string entidadeChave, ConfiguracaoAprovacaoEntrada entrada, CancellationToken cancellationToken)
    {
        var usuario = await usuarioAtual.ObterAsync(cancellationToken);
        var entidade = await ConfiguracaoDaEquipe.ValidarAsync(
            usuario, equipeId, grupoChave, entidadeChave, "aprovação", equipes, metadados, cancellationToken);

        var ativa = entrada.Ativa
            ?? throw new ValidacaoRegistroException(new Dictionary<string, string[]> { ["ativa"] = ["Campo obrigatório (true ou false)."] });

        var configuracao = await configuracoes.ObterAsync(entidade.Id, equipeId, cancellationToken);
        var atual = configuracao?.RequerAprovacao ?? false;

        if (atual != ativa)
        {
            if (!ativa)
            {
                var pendentes = await registros.ContarPendentesAsync(entidade.Id, equipeId, cancellationToken);
                if (pendentes > 0)
                {
                    throw new RegraDeNegocioException(
                        $"Há {pendentes} registro(s) pendente(s) de aprovação em '{entidade.Nome}' nesta equipe. Decida-os antes de desativar.",
                        CodigosErro.AprovacoesPendentes,
                        new Dictionary<string, object?> { ["quantidade"] = pendentes });
                }
            }

            var agora = relogio.GetUtcNow().UtcDateTime;
            var operacao = configuracao is null ? OperacaoHistorico.Criacao : OperacaoHistorico.Alteracao;
            if (configuracao is null)
            {
                configuracao = new ConfiguracaoEntidadeEquipe { EntidadeId = entidade.Id, EquipeId = equipeId };
                configuracoes.Adicionar(configuracao);
            }

            configuracao.RequerAprovacao = ativa;
            configuracao.AtualizadoPorId = usuario.Id;
            configuracao.AtualizadoEm = agora;

            historico.RegistrarConfiguracao(operacao, entidade.Id, equipeId, usuario,
                operacao == OperacaoHistorico.Criacao ? null : Json(atual), Json(ativa));
            await unidadeDeTrabalho.SalvarAlteracoesAsync(cancellationToken);
        }

        return await obterConfiguracao.ExecutarAsync(equipeId, grupoChave, entidadeChave, cancellationToken);
    }

    private static JsonObject Json(bool requerAprovacao) => new() { ["requerAprovacao"] = requerAprovacao };
}
