using Catalogo.Application.Abstracoes;
using Catalogo.Application.Comum;
using Catalogo.Application.Dtos;
using Catalogo.Application.Mapeamentos;
using Catalogo.Domain.Historico;
using Catalogo.Domain.Metadados.Administracao;
using Catalogo.Domain.Validacao;

namespace Catalogo.Application.CasosDeUso.Metadados;

/// <summary>
/// Caso de uso: alterar nome, descrição, ordem, campo de exibição e escopo de uma entidade, com a versão dela (RN52).
/// A chave não muda (RN51). Mudar o escopo só numa entidade sem registros nem configuração de equipe (RN74).
/// </summary>
public sealed class AtualizarEntidade(ExecutorAlteracaoMetadados executor, IMetadadosAdminRepository admin)
{
    public async Task<ResultadoAdministracaoDto> ExecutarAsync(
        string grupoChave, string entidadeChave, EntidadeEntrada entrada, bool simular, CancellationToken cancellationToken)
    {
        var usuario = await executor.ExigirAdministradorAsync(cancellationToken);
        var grupo = await executor.ObterGrupoAsync(grupoChave, cancellationToken);
        var entidade = ExecutorAlteracaoMetadados.ObterEntidade(grupo, entidadeChave);
        var versao = ExecutorAlteracaoMetadados.ConferirVersao(entrada.Versao, entidade.Versao, "da entidade");

        var erros = new ErrosPorCampo();
        var definida = RegrasMetadados.ValidarEntidade(grupo, entidade, CriarEntidade.Definicao(entrada), erros);
        erros.LancarSeHouverErros();

        var plano = definida!.EscopoEquipe != entidade.EscopoEquipe ? PlanejadorAlteracoes.MudarEscopo(entidade) : PlanoAlteracao.Vazio;

        return await executor.ExecutarAsync(usuario, new AlteracaoMetadados(
            grupo.Chave, grupo.Id, OperacaoHistorico.Alteracao, plano,
            async ct =>
            {
                await admin.TocarEntidadeAsync(entidade.Id, versao, executor.AgoraUtc, ct);
                await admin.AlterarEntidadeAsync(entidade.Id, definida, entidade.Ativo, ct);
            },
            entidade.ParaHistorico(),
            depois => depois?.ObterEntidade(entidade.Id)?.ParaHistorico()), simular, cancellationToken);
    }
}
