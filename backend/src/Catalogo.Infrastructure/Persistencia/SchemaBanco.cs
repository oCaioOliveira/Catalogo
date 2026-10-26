namespace Catalogo.Infrastructure.Persistencia;

/// <summary>
/// O schema de banco onde ficam TODOS os objetos do Catálogo (V012): o banco de produção é compartilhado com outros sistemas,
/// que têm tabelas de nomes genéricos em dbo (Usuario, Registro...). É o único lugar do código com o nome do schema.
/// </summary>
public static class SchemaBanco
{
    public const string Nome = "catalogo";
}
