using Catalogo.Application.Abstracoes;
using Catalogo.Application.Comum;
using Catalogo.Application.Dtos;

namespace Catalogo.Application.CasosDeUso.Aprovacao;

/// <summary>Caso de uso: a configuração de aprovação de uma entidade numa equipe (RN33). Sem linha gravada = desativada.</summary>
public sealed class ObterConfiguracaoAprovacao(
    IUsuarioAtual usuarioAtual, IMetadadosProvider metadados, IEquipeRepository equipes,
    IConfiguracaoRepository configuracoes, IUsuarioRepository usuarios)
{
    public async Task<ConfiguracaoAprovacaoDto> ExecutarAsync(
        Guid equipeId, string grupoChave, string entidadeChave, CancellationToken cancellationToken)
    {
        var usuario = await usuarioAtual.ObterAsync(cancellationToken);
        var entidade = await ConfiguracaoDaEquipe.ValidarAsync(
            usuario, equipeId, grupoChave, entidadeChave, "aprovação", equipes, metadados, cancellationToken);

        var configuracao = await configuracoes.ObterAsync(entidade.Id, equipeId, cancellationToken);
        var autor = configuracao is null
            ? null
            : (await usuarios.ObterResumosAsync([configuracao.AtualizadoPorId], cancellationToken)).GetValueOrDefault(configuracao.AtualizadoPorId);

        return new ConfiguracaoAprovacaoDto(
            equipeId, grupoChave, entidade.Chave, configuracao?.RequerAprovacao ?? false, autor, configuracao?.AtualizadoEm);
    }
}
