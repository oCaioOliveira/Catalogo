using Catalogo.Application.Abstracoes;
using Catalogo.Application.Comum;
using Catalogo.Application.Dtos;
using Catalogo.Application.Mapeamentos;
using Catalogo.Domain.Historico;
using Catalogo.Domain.Metadados.Administracao;
using Catalogo.Domain.Validacao;

namespace Catalogo.Application.CasosDeUso.Metadados;

/// <summary>
/// Caso de uso: reordenar TODOS os campos de uma entidade de uma vez (RN53), com a versão dela. Como a ordem dos campos é a
/// ordem do hash das restrições compostas (RN09), as chaves dessas restrições são recalculadas na mesma transação.
/// </summary>
public sealed class ReordenarCampos(ExecutorAlteracaoMetadados executor, IMetadadosAdminRepository admin)
{
    public async Task<ResultadoAdministracaoDto> ExecutarAsync(
        string grupoChave, string entidadeChave, OrdemCamposEntrada entrada, bool simular, CancellationToken cancellationToken)
    {
        var usuario = await executor.ExigirAdministradorAsync(cancellationToken);
        var grupo = await executor.ObterGrupoAsync(grupoChave, cancellationToken);
        var entidade = ExecutorAlteracaoMetadados.ObterEntidade(grupo, entidadeChave);
        var versao = ExecutorAlteracaoMetadados.ConferirVersao(entrada.Versao, entidade.Versao, "da entidade");

        var erros = new ErrosPorCampo();
        var ordem = RegrasMetadados.ValidarOrdem(entidade, entrada.Campos, erros);
        erros.LancarSeHouverErros();

        return await executor.ExecutarAsync(usuario, new AlteracaoMetadados(
            grupo.Chave, grupo.Id, OperacaoHistorico.Alteracao, PlanejadorAlteracoes.ReordenarCampos(entidade, ordem!),
            async ct =>
            {
                await admin.TocarEntidadeAsync(entidade.Id, versao, executor.AgoraUtc, ct);
                await admin.ReordenarCamposAsync(ordem!, ct);
            },
            entidade.OrdemParaHistorico(),
            depois => depois?.ObterEntidade(entidade.Id)?.OrdemParaHistorico()), simular, cancellationToken);
    }
}
