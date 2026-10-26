namespace Catalogo.Domain.Metadados.Administracao;

/// <summary>
/// Os metadados são imutáveis (ficam em cache, compartilhados entre requisições): para descrever "como ficaria" uma
/// alteração, cria-se uma CÓPIA com o que muda. Usado pela administração para planejar e validar antes de gravar.
/// </summary>
public static class CopiasMetadados
{
    public static Campo Campo(Campo c, bool? ativo = null, int? ordem = null) => new()
    {
        Id = c.Id,
        EntidadeId = c.EntidadeId,
        Chave = c.Chave,
        Nome = c.Nome,
        TipoDado = c.TipoDado,
        Obrigatorio = c.Obrigatorio,
        TamanhoMaximo = c.TamanhoMaximo,
        Precisao = c.Precisao,
        Escala = c.Escala,
        Opcoes = c.Opcoes,
        EntidadeReferenciadaId = c.EntidadeReferenciadaId,
        AcaoExclusao = c.AcaoExclusao,
        Ordem = ordem ?? c.Ordem,
        Ativo = ativo ?? c.Ativo,
    };

    public static Entidade Entidade(Entidade e, IReadOnlyList<RestricaoUnica>? restricoes = null) => new()
    {
        Id = e.Id,
        GrupoId = e.GrupoId,
        Chave = e.Chave,
        Nome = e.Nome,
        Descricao = e.Descricao,
        NomePlural = e.NomePlural,
        Icone = e.Icone,
        EscopoEquipe = e.EscopoEquipe,
        CampoExibicaoId = e.CampoExibicaoId,
        Ativo = e.Ativo,
        Ordem = e.Ordem,
        Versao = e.Versao,
        Campos = e.Campos,
        RestricoesUnicas = restricoes ?? e.RestricoesUnicas,
        Dependencias = e.Dependencias,
    };

    /// <summary>A entidade como está, na forma de definição (para alterar só a ativação).</summary>
    public static EntidadeDefinida Definicao(Entidade e) =>
        new(e.Chave, e.Nome, e.Descricao, e.Ordem, e.EscopoEquipe, e.CampoExibicaoId, e.NomePlural, e.Icone);
}
