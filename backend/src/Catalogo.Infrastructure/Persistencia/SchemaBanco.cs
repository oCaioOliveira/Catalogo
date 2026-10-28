namespace Catalogo.Infrastructure.Persistencia;

/// <summary>
/// O schema de banco onde ficam TODOS os objetos do Catálogo. O padrão é <see cref="Nome"/> ("cat", V013, padrão da CAIXA);
/// a configuração <see cref="ChaveConfiguracao"/> troca o schema, para os testes de integração usarem um schema próprio
/// (ex.: cat_testes) no mesmo banco (backend/README.md). O banco é compartilhado com outros sistemas, que têm tabelas em dbo.
/// </summary>
public static class SchemaBanco
{
    public const string Nome = "cat";

    public const string ChaveConfiguracao = "Banco:Schema";
}

/// <summary>O schema em uso pelo processo (lido de <see cref="SchemaBanco.ChaveConfiguracao"/>, ou o padrão).</summary>
public sealed record OpcoesBanco(string Schema);
