using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Catalogo.Api.Autenticacao;

/// <summary>
/// Configuração do JWT (seção "Jwt"). A chave de assinatura NUNCA fica em arquivo versionado:
/// user-secrets em desenvolvimento, variável de ambiente Jwt__ChaveAssinatura nos servidores (docs/seguranca.md).
/// </summary>
public sealed class OpcoesJwt
{
    public const int TamanhoMinimoChave = 32;

    public required string Emissor { get; init; }
    public required string Audiencia { get; init; }
    public required string ChaveAssinatura { get; init; }
    public required TimeSpan Validade { get; init; }

    public SymmetricSecurityKey Chave => new(Encoding.UTF8.GetBytes(ChaveAssinatura));

    /// <summary>Lê e valida a seção "Jwt". Falha com mensagem clara se a chave faltar ou for curta.</summary>
    public static OpcoesJwt Ler(IConfiguration configuracao)
    {
        var secao = configuracao.GetSection("Jwt");
        var chave = secao["ChaveAssinatura"];
        if (string.IsNullOrWhiteSpace(chave) || chave.Length < TamanhoMinimoChave)
        {
            throw new InvalidOperationException(
                $"Configure 'Jwt:ChaveAssinatura' com pelo menos {TamanhoMinimoChave} caracteres aleatórios. " +
                "Em desenvolvimento use o user-secrets; nos servidores, a variável de ambiente Jwt__ChaveAssinatura. Ver docs/seguranca.md.");
        }

        var horas = double.TryParse(secao["ValidadeHoras"], System.Globalization.CultureInfo.InvariantCulture, out var valor) && valor > 0
            ? valor
            : 8;

        return new OpcoesJwt
        {
            Emissor = string.IsNullOrWhiteSpace(secao["Emissor"]) ? "catalogo-api" : secao["Emissor"]!,
            Audiencia = string.IsNullOrWhiteSpace(secao["Audiencia"]) ? "catalogo" : secao["Audiencia"]!,
            ChaveAssinatura = chave,
            Validade = TimeSpan.FromHours(horas),
        };
    }
}
