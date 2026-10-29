using Catalogo.Application.Dtos;
using Catalogo.Domain.Metadados;

namespace Catalogo.Application.Mapeamentos;

/// <summary>Mapeamento manual dos metadados para DTOs (sem AutoMapper, ver docs/adr/0004).</summary>
public static class EntidadeMapeamentos
{
    /// <param name="grupo">Usado para trocar o id da entidade referenciada pela sua chave.</param>
    /// <param name="requerAprovacao">Aprovação ativa para a equipe consultada (nulo se nenhuma equipe foi informada).</param>
    /// <param name="modeloAvaliacao">Formulário de avaliação ativo na equipe consultada (RN40), se houver.</param>
    /// <param name="equipesComAprovacao">Equipes visíveis ao usuário com aprovação ativa para a entidade.</param>
    /// <param name="criteriosAvaliacao">Critérios ESTRELAS ativos dos modelos ativos nas equipes visíveis.</param>
    public static EntidadeDto ParaDto(
        this Entidade entidade, Grupo grupo, bool? requerAprovacao = null, FormularioAvaliacaoDto? modeloAvaliacao = null,
        IReadOnlyList<Guid>? equipesComAprovacao = null, IReadOnlyList<CriterioListagemDto>? criteriosAvaliacao = null) => new(
        entidade.Id,
        entidade.Chave,
        entidade.Nome,
        entidade.Descricao,
        entidade.CampoExibicaoId is Guid campoExibicaoId ? entidade.ObterCampo(campoExibicaoId)?.Chave : null,
        entidade.EscopoEquipe,
        grupo.Ativo && entidade.Ativo,
        entidade.EhGlobal ? false : requerAprovacao,
        modeloAvaliacao,
        entidade.Campos.Where(c => c.Ativo).Select(c => c.ParaDto(entidade, grupo)).ToList(),
        entidade.RestricoesUnicas
            .Select(r => new RestricaoUnicaDto(r.Nome, r.Campos.Select(c => c.Chave).ToList()))
            .ToList(),
        entidade.Ordem,
        entidade.NomePlural,
        entidade.EhGlobal ? [] : equipesComAprovacao ?? [],
        entidade.EhGlobal ? [] : criteriosAvaliacao ?? [],
        entidade.Icone);

    public static CampoDto ParaDto(this Campo campo, Entidade entidade, Grupo grupo)
    {
        var dependencia = entidade.Dependencias.FirstOrDefault(d => d.Campo.Id == campo.Id);

        return new CampoDto(
            campo.Id,
            campo.Chave,
            campo.Nome,
            TipoDadoConversor.ParaTexto(campo.TipoDado),
            campo.Obrigatorio,
            campo.Ordem,
            campo.TamanhoMaximo,
            campo.Precisao,
            campo.Escala,
            campo.TipoDado == TipoDado.Lista ? campo.Opcoes : null,
            campo.EntidadeReferenciadaId is Guid referenciadaId ? grupo.ObterEntidade(referenciadaId)?.Chave : null,
            campo.AcaoExclusao is AcaoExclusao acao ? AcaoExclusaoConversor.ParaTexto(acao) : null,
            dependencia is null
                ? null
                : new DependenciaDto(dependencia.CampoVia?.Chave, dependencia.CampoOrigem.Chave, dependencia.CampoAlvo.Chave),
            // Com CampoVia, o front manda o registro escolhido no campo via; sem, o valor da origem no próprio formulário.
            dependencia is null ? null : (dependencia.CampoVia ?? dependencia.CampoOrigem).Chave,
            campo.Ativo);
    }
}
