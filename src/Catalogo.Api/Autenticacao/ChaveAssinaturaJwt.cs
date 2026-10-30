using System.Security.Cryptography;

namespace Catalogo.Api.Autenticacao;

/// <summary>
/// A chave de assinatura do JWT em uso pelo processo. Ela fica no banco (cat.CATTB025_CHAVE_JWT, docs/adr/0053): o Program.cs a lê
/// (ou gera e grava, na primeira subida) antes de a API começar a atender, e a guarda aqui. Sem ela, nenhum token é emitido nem aceito.
/// </summary>
public sealed class ChaveAssinaturaJwt
{
    private string? _valor;

    public string Valor => _valor
        ?? throw new InvalidOperationException("A chave do JWT ainda não foi carregada do banco (Program.cs, antes do app.Run).");

    public void Definir(string valor)
    {
        if (string.IsNullOrWhiteSpace(valor) || valor.Length < OpcoesJwt.TamanhoMinimoChave)
        {
            throw new InvalidOperationException(
                $"A chave do JWT guardada no banco tem menos de {OpcoesJwt.TamanhoMinimoChave} caracteres. Apague a linha de " +
                "cat.CATTB025_CHAVE_JWT para a API gerar outra na próxima subida (os usuários logados precisarão entrar de novo).");
        }

        _valor = valor;
    }

    /// <summary>Chave nova para gravar no banco: 64 bytes aleatórios em Base64 (88 caracteres).</summary>
    public static string GerarNova() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
}
