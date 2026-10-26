using Catalogo.Application.Abstracoes;
using Catalogo.Application.Comum;
using Catalogo.Application.Dtos;

namespace Catalogo.Application.CasosDeUso.Metadados;

/// <summary>Caso de uso: todos os grupos, ativos e inativos, para a administração (RN50). Só ADMINISTRADOR.</summary>
public sealed class ListarGruposAdmin(ExecutorAlteracaoMetadados executor, IMetadadosAdminRepository admin)
{
    public async Task<IReadOnlyList<GrupoResumoAdminDto>> ExecutarAsync(CancellationToken cancellationToken)
    {
        await executor.ExigirAdministradorAsync(cancellationToken);
        return (await admin.ListarGruposAsync(cancellationToken))
            .Select(g => new GrupoResumoAdminDto(g.Chave, g.Nome, g.Descricao, g.Ativo, Convert.ToBase64String(g.Versao), g.QuantidadeEntidades, g.QuantidadeRegistros, g.QuantidadeEquipes))
            .ToList();
    }
}
