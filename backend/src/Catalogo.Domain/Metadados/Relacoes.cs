namespace Catalogo.Domain.Metadados;

/// <summary>
/// Uma relação "itens de <see cref="Origem"/> apontam para a entidade" por um campo REFERENCIA (RN94).
/// <see cref="Filha"/>: o campo é obrigatório e CASCATA, a mesma regra de "entidade filha" do front (as filhas são editadas no
/// detalhe do pai).
/// </summary>
public sealed record Relacao(Entidade Origem, Campo Campo)
{
    public bool Filha => Campo.Obrigatorio && Campo.AcaoExclusao == AcaoExclusao.Cascata;
}

/// <summary>
/// RN94: os itens relacionados a um item são DESCOBERTOS pelos metadados (ADR 0047), sem configuração por tipo de item: toda
/// entidade ATIVA do grupo com um campo REFERENCIA ATIVO que aponta para a entidade do item. Vale para qualquer listagem, inclusive as
/// criadas pela administração. A ordem é a dos metadados: a da entidade de origem e, dentro dela, a dos campos.
/// </summary>
public static class Relacoes
{
    public static IReadOnlyList<Relacao> QueApontamPara(Grupo grupo, Entidade destino) =>
        grupo.Entidades
            .Where(origem => origem.Ativo)
            .OrderBy(origem => origem.Ordem)
            .ThenBy(origem => origem.Chave, StringComparer.Ordinal)
            .SelectMany(origem => origem.Campos
                .Where(c => c.Ativo && c.EhReferencia && c.EntidadeReferenciadaId == destino.Id)
                .OrderBy(c => c.Ordem)
                .ThenBy(c => c.Chave, StringComparer.Ordinal)
                .Select(c => new Relacao(origem, c)))
            .ToList();
}
