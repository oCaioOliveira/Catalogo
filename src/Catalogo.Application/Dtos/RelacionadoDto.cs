namespace Catalogo.Application.Dtos;

/// <summary>
/// Uma relação do item aberto (RN94): os itens de <see cref="Entidade"/> que apontam para ele pelo <see cref="Campo"/>, com a
/// <see cref="Quantidade"/> que o usuário VÊ (a mesma regra da listagem). Quantidade zero também vem: o front decide o que mostra.
/// Os itens em si vêm da listagem filtrada: <c>GET .../entidades/{entidade.chave}/registros?{campo.chave}={id}</c>.
/// </summary>
public sealed record RelacionadoDto(
    EntidadeRelacionadaDto Entidade,
    CampoRelacionadoDto Campo,
    // Campo obrigatório com exclusão CASCATA: a entidade é FILHA do item (editada no detalhe dele).
    bool Filha,
    int Quantidade);

/// <summary>A entidade de origem: chave, nome (singular) e nome no plural (nulo = use o nome).</summary>
public sealed record EntidadeRelacionadaDto(string Chave, string Nome, string? NomePlural);

/// <summary>O campo REFERENCIA que aponta para o item.</summary>
public sealed record CampoRelacionadoDto(string Chave, string Nome);
