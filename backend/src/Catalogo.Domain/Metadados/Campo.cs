namespace Catalogo.Domain.Metadados;

/// <summary>
/// Um campo de uma <see cref="Entidade"/> (tabela catalogo.Campo).
/// Modelo de LEITURA e imutável: é montado pela infraestrutura e guardado em cache,
/// então é compartilhado entre requisições e nunca deve ser alterado.
/// </summary>
public sealed class Campo
{
    public required Guid Id { get; init; }
    public required Guid EntidadeId { get; init; }

    /// <summary>Nome da propriedade no JSON enviado pelo cliente (ex.: "nome_servico").</summary>
    public required string Chave { get; init; }

    /// <summary>Rótulo exibido na tela (ex.: "Nome do serviço").</summary>
    public required string Nome { get; init; }

    public required TipoDado TipoDado { get; init; }
    public bool Obrigatorio { get; init; }

    /// <summary>Só para TEXTO: quantidade máxima de caracteres.</summary>
    public int? TamanhoMaximo { get; init; }

    /// <summary>Só para DECIMAL: total de dígitos.</summary>
    public byte? Precisao { get; init; }

    /// <summary>Só para DECIMAL: dígitos depois da vírgula.</summary>
    public byte? Escala { get; init; }

    /// <summary>Só para LISTA: valores aceitos. Vazia para os demais tipos.</summary>
    public IReadOnlyList<string> Opcoes { get; init; } = [];

    /// <summary>Só para REFERENCIA: entidade dos registros que podem ser apontados.</summary>
    public Guid? EntidadeReferenciadaId { get; init; }

    /// <summary>Só para REFERENCIA: o que fazer quando o registro apontado for excluído.</summary>
    public AcaoExclusao? AcaoExclusao { get; init; }

    public int Ordem { get; init; }

    /// <summary>Campo inativo continua no banco, mas não pode mais ser preenchido (RN02).</summary>
    public bool Ativo { get; init; } = true;

    public bool EhReferencia => TipoDado == TipoDado.Referencia;
}
