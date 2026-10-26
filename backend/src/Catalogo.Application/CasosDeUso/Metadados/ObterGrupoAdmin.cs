using Catalogo.Application.Comum;
using Catalogo.Application.Dtos;

namespace Catalogo.Application.CasosDeUso.Metadados;

/// <summary>
/// Caso de uso: a estrutura completa de um grupo para edição (RN50): entidades e campos ATIVOS E INATIVOS, restrições,
/// dependências, versões e quantidade de registros. Lê do banco, sem o cache de metadados.
/// </summary>
public sealed class ObterGrupoAdmin(ExecutorAlteracaoMetadados executor)
{
    public async Task<GrupoAdminDto> ExecutarAsync(string grupoChave, CancellationToken cancellationToken)
    {
        await executor.ExigirAdministradorAsync(cancellationToken);
        return await executor.MontarAsync(await executor.ObterGrupoAsync(grupoChave, cancellationToken), cancellationToken);
    }
}
