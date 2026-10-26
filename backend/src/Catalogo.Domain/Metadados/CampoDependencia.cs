namespace Catalogo.Domain.Metadados;

/// <summary>
/// Campo dependente (tabela catalogo.CampoDependencia). Ver RN08 em docs/regras-de-negocio.md.
///
/// Leitura: "em <see cref="Campo"/> só vale escolher registros cujo <see cref="CampoAlvo"/>
/// seja igual ao valor de <see cref="CampoOrigem"/>".
///   - <see cref="CampoVia"/> nulo: CampoOrigem é um campo do próprio registro.
///   - <see cref="CampoVia"/> preenchido: segue essa referência e lê CampoOrigem no registro apontado.
///
/// Exemplo (cenario_teste):
///   Campo       = cenario_teste.endpoint_servico
///   CampoVia    = cenario_teste.relatorio_teste
///   CampoOrigem = relatorio_teste.projeto
///   CampoAlvo   = endpoint_servico.projeto
///   → o endpoint escolhido precisa ser do mesmo projeto do relatório escolhido.
/// </summary>
public sealed class CampoDependencia
{
    /// <summary>O campo REFERENCIA que tem as opções filtradas.</summary>
    public required Campo Campo { get; init; }

    /// <summary>Referência do próprio registro usada para chegar ao CampoOrigem (opcional).</summary>
    public Campo? CampoVia { get; init; }

    /// <summary>Campo de onde vem o valor de comparação.</summary>
    public required Campo CampoOrigem { get; init; }

    /// <summary>Campo, no registro escolhido em <see cref="Campo"/>, que precisa ter o mesmo valor.</summary>
    public required Campo CampoAlvo { get; init; }
}
