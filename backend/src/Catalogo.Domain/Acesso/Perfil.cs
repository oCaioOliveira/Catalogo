namespace Catalogo.Domain.Acesso;

/// <summary>
/// Perfil de acesso de um usuário (coluna Usuario.Perfil). É ÚNICO por usuário e vale em todas
/// as equipes dele (docs/adr/0014). O que cada perfil pode fazer está em <see cref="PoliticaPermissao"/> (RN26).
/// </summary>
public enum Perfil
{
    Externo,
    Interno,
    Gestor,
    Administrador,
}

/// <summary>Converte <see cref="Perfil"/> de e para o texto do banco ("EXTERNO", "ADMINISTRADOR"...).</summary>
public static class PerfilConversor
{
    public static string ParaTexto(Perfil perfil) => perfil switch
    {
        Perfil.Externo => "EXTERNO",
        Perfil.Interno => "INTERNO",
        Perfil.Gestor => "GESTOR",
        Perfil.Administrador => "ADMINISTRADOR",
        _ => throw new ArgumentOutOfRangeException(nameof(perfil), perfil, "Perfil sem texto correspondente."),
    };

    public static Perfil DeTexto(string texto) => texto switch
    {
        "EXTERNO" => Perfil.Externo,
        "INTERNO" => Perfil.Interno,
        "GESTOR" => Perfil.Gestor,
        "ADMINISTRADOR" => Perfil.Administrador,
        _ => throw new ArgumentOutOfRangeException(nameof(texto), texto, "Texto de Perfil desconhecido."),
    };

    /// <summary>Converte texto vindo da API (sem diferenciar maiúsculas). Falso se não for um perfil.</summary>
    public static bool TentarConverter(string? texto, out Perfil perfil)
    {
        perfil = default;
        if (string.IsNullOrWhiteSpace(texto))
            return false;

        try
        {
            perfil = DeTexto(texto.Trim().ToUpperInvariant());
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
    }
}
