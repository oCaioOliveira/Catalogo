using Catalogo.Application.Abstracoes;
using Catalogo.Application.Comum;
using Catalogo.Application.Dtos;
using Catalogo.Application.Mapeamentos;
using Catalogo.Domain.Historico;
using Catalogo.Domain.Metadados.Administracao;

namespace Catalogo.Application.CasosDeUso.Metadados;

/// <summary>
/// Caso de uso: excluir um campo (RN70). Só se NENHUM registro tem valor nele e ele não é usado em restrição, dependência ou
/// exibição. Caso contrário, a orientação é desativar (RN69): os valores ficam guardados.
/// </summary>
public sealed class ExcluirCampo(ExecutorAlteracaoMetadados executor, IMetadadosAdminRepository admin)
{
    public async Task<ResultadoAdministracaoDto> ExecutarAsync(
        string grupoChave, string entidadeChave, string campoChave, string? versaoTexto, bool simular, CancellationToken cancellationToken)
    {
        var usuario = await executor.ExigirAdministradorAsync(cancellationToken);
        var grupo = await executor.ObterGrupoAsync(grupoChave, cancellationToken);
        var entidade = ExecutorAlteracaoMetadados.ObterEntidade(grupo, entidadeChave);
        var campo = ExecutorAlteracaoMetadados.ObterCampo(entidade, campoChave);
        var versao = ExecutorAlteracaoMetadados.ConferirVersao(versaoTexto, entidade.Versao, "da entidade");

        return await executor.ExecutarAsync(usuario, new AlteracaoMetadados(
            grupo.Chave, grupo.Id, OperacaoHistorico.Exclusao, PlanejadorAlteracoes.ExcluirCampo(grupo, entidade, campo),
            async ct =>
            {
                await admin.TocarEntidadeAsync(entidade.Id, versao, executor.AgoraUtc, ct);
                await admin.ExcluirCampoAsync(campo.Id, ct);
            },
            campo.ParaHistorico(grupo, entidade),
            _ => null), simular, cancellationToken);
    }
}
