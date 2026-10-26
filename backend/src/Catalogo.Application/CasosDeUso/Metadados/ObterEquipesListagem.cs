using Catalogo.Application.Abstracoes;
using Catalogo.Application.Comum;
using Catalogo.Application.Dtos;

namespace Catalogo.Application.CasosDeUso.Metadados;

/// <summary>
/// Caso de uso: a aba "Equipes" de uma listagem (RN90): TODAS as equipes, ativas e inativas, cada uma dizendo se está habilitada
/// e quantos itens tem na listagem, e a versão do grupo para o PUT. Só o ADMINISTRADOR (RN50). Lê do banco, sem cache.
/// </summary>
public sealed class ObterEquipesListagem(ExecutorAlteracaoMetadados executor, IMetadadosAdminRepository admin, IEquipeRepository equipes)
{
    public async Task<EquipesListagemDto> ExecutarAsync(string grupoChave, CancellationToken cancellationToken)
    {
        await executor.ExigirAdministradorAsync(cancellationToken);
        var grupo = await executor.ObterGrupoAsync(grupoChave, cancellationToken);

        var todas = await equipes.ListarTodasAsync(cancellationToken);
        var itens = await admin.ContarRegistrosPorEquipeAsync(grupo.Id, cancellationToken);

        return new EquipesListagemDto(
            Convert.ToBase64String(grupo.Versao),
            todas
                .Select(e => new EquipeNaListagemDto(
                    e.Id, e.Nome, e.Ativa, grupo.EquipesHabilitadas.Contains(e.Id), itens.GetValueOrDefault(e.Id)))
                .ToList());
    }
}
