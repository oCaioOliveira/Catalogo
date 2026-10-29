using Catalogo.Application.Abstracoes;
using Catalogo.Application.Comum;
using Catalogo.Application.Dtos;
using Catalogo.Application.Mapeamentos;
using Catalogo.Domain.Historico;
using Catalogo.Domain.Metadados.Administracao;

namespace Catalogo.Application.CasosDeUso.Metadados;

/// <summary>Caso de uso: excluir um grupo SEM entidades (RN77). Com entidades, 409 METADADO_EM_USO: desative-o.</summary>
public sealed class ExcluirGrupo(ExecutorAlteracaoMetadados executor, IMetadadosAdminRepository admin)
{
    public async Task<ResultadoAdministracaoDto> ExecutarAsync(
        string grupoChave, string? versaoTexto, bool simular, CancellationToken cancellationToken)
    {
        var usuario = await executor.ExigirAdministradorAsync(cancellationToken);
        var grupo = await executor.ObterGrupoAsync(grupoChave, cancellationToken);
        var versao = ExecutorAlteracaoMetadados.ConferirVersao(versaoTexto, grupo.Versao, "do grupo");

        return await executor.ExecutarAsync(usuario, new AlteracaoMetadados(
            grupo.Chave, grupo.Id, OperacaoHistorico.Exclusao, PlanejadorAlteracoes.ExcluirGrupo(grupo),
            ct => admin.ExcluirGrupoAsync(grupo.Id, versao, ct),
            grupo.ParaHistorico(),
            _ => null), simular, cancellationToken);
    }
}
