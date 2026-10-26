using Catalogo.Application.Abstracoes;
using Catalogo.Application.Dtos;
using Catalogo.Application.Mapeamentos;
using Catalogo.Domain.Acesso;
using Catalogo.Domain.Avaliacoes;
using Catalogo.Domain.Excecoes;

namespace Catalogo.Application.CasosDeUso;

/// <summary>
/// Caso de uso: metadados das entidades de um grupo, para o front montar formulários. Sempre traz, por entidade, as equipes
/// visíveis com aprovação ativa e os critérios de estrelas dos modelos ativos nelas (ADR 0045): duas consultas para o grupo
/// inteiro, não uma por equipe.
/// Lança <see cref="RecursoNaoEncontradoException"/>.
/// </summary>
public sealed class ListarEntidades(
    IUsuarioAtual usuarioAtual, IMetadadosProvider metadados, IConfiguracaoRepository configuracoes, IModeloAvaliacaoRepository modelos)
{
    /// <param name="equipeIdInformado">
    /// Opcional: com ele, cada entidade informa <c>requerAprovacao</c> nessa equipe (RN33), para o front
    /// saber quando mostrar o fluxo de aprovação, e o formulário de avaliação ativo (<c>modeloAvaliacao</c>, RN40).
    /// O não-administrador só consulta as suas equipes (403).
    /// </param>
    public async Task<IReadOnlyList<EntidadeDto>> ExecutarAsync(string grupoChave, string? equipeIdInformado, CancellationToken cancellationToken)
    {
        var usuario = await usuarioAtual.ObterAsync(cancellationToken);
        (var grupo, usuario) = await metadados.ObterGrupoOuFalharAsync(usuario, grupoChave, cancellationToken);

        IReadOnlySet<Guid>? comAprovacao = null;
        var formularios = new Dictionary<Guid, FormularioAvaliacaoDto>();
        if (!string.IsNullOrWhiteSpace(equipeIdInformado))
        {
            if (!Guid.TryParseExact(equipeIdInformado.Trim(), "D", out var equipeId))
                throw new ValidacaoRegistroException(new Dictionary<string, string[]> { ["equipeId"] = ["Deve ser o id (GUID) de uma equipe."] });

            var visiveis = PoliticaPermissao.EquipesVisiveis(usuario);
            if (visiveis is not null && !visiveis.Contains(equipeId))
                throw new AcessoNegadoException("Você não pertence a essa equipe.");

            comAprovacao = await configuracoes.EntidadesComAprovacaoAsync(equipeId, cancellationToken);
            foreach (var modelo in await modelos.ListarAtivosAsync(null, [equipeId], cancellationToken))
                formularios[modelo.EntidadeId] = modelo.ParaFormularioDto();
        }

        var equipesDoUsuario = PoliticaPermissao.EquipesVisiveis(usuario)?.ToList();
        var aprovacoes = (await configuracoes.AprovacoesAtivasAsync(equipesDoUsuario, cancellationToken))
            .GroupBy(a => a.EntidadeId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<Guid>)g.Select(a => a.EquipeId).Distinct().ToList());
        var criterios = (await modelos.ListarAtivosAsync(null, equipesDoUsuario, cancellationToken))
            .GroupBy(m => m.EntidadeId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<CriterioListagemDto>)g
                .SelectMany(m => m.CriteriosAtivos)
                .Where(c => c.Tipo == TipoCriterio.Estrelas)
                .OrderBy(c => c.Ordem)
                .DistinctBy(c => c.Chave)
                .Select(c => new CriterioListagemDto(c.Chave, c.Nome))
                .ToList());

        // RN75: entidade desativada não aparece para o não-administrador; o administrador a vê, com "ativo": false.
        // Na ordem do menu (Entidade.Ordem), definida na administração de metadados.
        return grupo.Entidades
            .Where(e => PoliticaPermissao.PodeUsarEntidade(usuario, grupo, e, escrita: false) == DecisaoPermissao.Permitido)
            .OrderBy(e => e.Ordem).ThenBy(e => e.Nome, StringComparer.CurrentCulture)
            .Select(e => e.ParaDto(grupo, comAprovacao?.Contains(e.Id), formularios.GetValueOrDefault(e.Id),
                aprovacoes.GetValueOrDefault(e.Id), criterios.GetValueOrDefault(e.Id)))
            .ToList();
    }
}
