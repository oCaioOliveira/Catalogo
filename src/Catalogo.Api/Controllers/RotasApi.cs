namespace Catalogo.Api.Controllers;

/// <summary>
/// Prefixo de TODAS as rotas da API (docs/adr/0022). Os controllers usam <c>[Route(RotasApi.V1 + "/...")]</c>.
/// Mudança que quebra o contrato (remover ou renomear campo, mudar tipo ou status HTTP) só numa v2;
/// acrescentar campos ou endpoints pode ser feito na v1. O /health fica fora da versão.
/// </summary>
public static class RotasApi
{
    public const string V1 = "api/v1";

    /// <summary>Administração de metadados (RN50): separada da API de uso, só para o ADMINISTRADOR.</summary>
    public const string AdminMetadados = V1 + "/admin/metadados";
}
