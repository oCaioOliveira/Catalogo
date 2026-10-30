using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Catalogo.Api.Autenticacao;

/// <summary>
/// Configuração do JWT: emissor, audiência e validade da seção "Jwt" (fixos no appsettings.json) e a chave de assinatura, que
/// NUNCA fica em arquivo nem em variável: vem do banco, pela <see cref="ChaveAssinaturaJwt"/> (docs/adr/0053, docs/seguranca.md).
/// </summary>
public sealed class OpcoesJwt
{
    public const int TamanhoMinimoChave = 32;

    public required string Emissor { get; init; }
    public required string Audiencia { get; init; }
    public required string ChaveAssinatura { get; init; }
    public required TimeSpan Validade { get; init; }

    public SymmetricSecurityKey Chave => new(Encoding.UTF8.GetBytes(ChaveAssinatura));

    /// <summary>Lê a seção "Jwt" e junta a chave do banco. Falha com mensagem clara se a chave for curta.</summary>
    public static OpcoesJwt Ler(IConfiguration configuracao, string chave)
    {
        var secao = configuracao.GetSection("Jwt");
        if (string.IsNullOrWhiteSpace(chave) || chave.Length < TamanhoMinimoChave)
            throw new InvalidOperationException($"A chave do JWT precisa ter pelo menos {TamanhoMinimoChave} caracteres.");

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
