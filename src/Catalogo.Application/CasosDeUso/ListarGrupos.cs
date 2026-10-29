using Catalogo.Application.Abstracoes;
using Catalogo.Application.Dtos;
using Catalogo.Application.Mapeamentos;
using Catalogo.Domain.Acesso;

namespace Catalogo.Application.CasosDeUso;

/// <summary>
/// Caso de uso: os grupos (listagens) que o usuário vê, para o front montar o seletor e o menu (RN90): o ADMINISTRADOR vê
/// todos (os desativados com "ativo": false, RN77); os demais, só os ativos em que alguma equipe deles está habilitada.
/// Cada grupo traz "minhasEquipes" (RN91) e "possuiEquipes" (RN93), para o front montar selects e avisos sem outras chamadas.
/// </summary>
public sealed class ListarGrupos(IUsuarioAtual usuarioAtual, IMetadadosProvider metadados, IEquipeRepository equipes)
{
    public async Task<IReadOnlyList<GrupoDto>> ExecutarAsync(CancellationToken cancellationToken)
    {
        var usuario = await usuarioAtual.ObterAsync(cancellationToken);

        var visiveis = (await metadados.ListarGruposAsync(cancellationToken))
            .Where(g => PoliticaPermissao.PodeVerListagem(usuario, g.Ativo, g.EquipesHabilitadas))
            .Select(g => (Grupo: g, Equipes: PoliticaPermissao.EquipesNaListagem(usuario, g.EquipesHabilitadas)))
            .ToList();

        // Nomes das equipes de uma vez (sem N+1). Só as ativas: equipe inativa não é escolhida em lugar nenhum.
        var ids = visiveis.SelectMany(v => v.Equipes).Distinct().ToList();
        var ativas = (await equipes.ObterPorIdsAsync(ids, cancellationToken)).Where(e => e.Ativa).ToDictionary(e => e.Id);

        return visiveis
            .Select(v => new GrupoDto(
                v.Grupo.Chave, v.Grupo.Nome, v.Grupo.Descricao, v.Grupo.Ativo,
                v.Equipes
                    .Where(ativas.ContainsKey)
                    .Select(id => ativas[id].ParaResumoDto())
                    .OrderBy(e => e.Nome, StringComparer.CurrentCulture)
                    .ToList(),
                v.Grupo.EquipesHabilitadas.Count > 0))
            .ToList();
    }
}
