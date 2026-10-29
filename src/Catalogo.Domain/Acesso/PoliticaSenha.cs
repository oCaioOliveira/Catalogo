using System.Security.Cryptography;

namespace Catalogo.Domain.Acesso;

/// <summary>Regras de senha (RN25). O hash é feito na Infrastructure (docs/adr/0016).</summary>
public static class PoliticaSenha
{
    public const int TamanhoMinimo = 10;

    /// <summary>
    /// Erros da senha nova (lista vazia = válida): mínimo de 10 caracteres, com letra e número,
    /// e diferente da atual quando <paramref name="senhaAtual"/> é informada.
    /// </summary>
    public static IReadOnlyList<string> Validar(string? novaSenha, string? senhaAtual = null)
    {
        var erros = new List<string>();
        var senha = novaSenha ?? "";

        if (senha.Length < TamanhoMinimo)
            erros.Add($"Deve ter pelo menos {TamanhoMinimo} caracteres.");
        if (!senha.Any(char.IsLetter))
            erros.Add("Deve ter pelo menos uma letra.");
        if (!senha.Any(char.IsDigit))
            erros.Add("Deve ter pelo menos um número.");
        if (senhaAtual is not null && senha == senhaAtual)
            erros.Add("Deve ser diferente da senha atual.");

        return erros;
    }

    /// <summary>
    /// Senha provisória gerada pelo sistema (16 caracteres, criptograficamente aleatória).
    /// Sem caracteres parecidos (0/O, 1/l/I), para ser fácil de digitar. Sempre atende a política.
    /// </summary>
    public static string GerarProvisoria()
    {
        const string letras = "abcdefghijkmnpqrstuvwxyzABCDEFGHJKLMNPQRSTUVWXYZ";
        const string numeros = "23456789";
        const string todos = letras + numeros;

        // Garante pelo menos uma letra e um número; o resto é sorteado do conjunto todo.
        var caracteres = new List<char>
        {
            letras[RandomNumberGenerator.GetInt32(letras.Length)],
            numeros[RandomNumberGenerator.GetInt32(numeros.Length)],
        };
        caracteres.AddRange(RandomNumberGenerator.GetItems<char>(todos, 14));

        // Embaralha, para a letra e o número garantidos não ficarem sempre no começo.
        var resultado = caracteres.ToArray();
        RandomNumberGenerator.Shuffle<char>(resultado);
        return new string(resultado);
    }
}

/// <summary>Regra do último administrador (RN30).</summary>
public static class RegrasAdministrador
{
    /// <summary>
    /// Verdadeiro se a mudança deixaria o sistema sem nenhum administrador ativo: o alvo é
    /// administrador ativo, deixaria de ser (rebaixado ou desativado) e é o único.
    /// </summary>
    /// <param name="administradoresAtivos">Quantos administradores ativos existem hoje, contando o alvo.</param>
    public static bool RemoveriaUltimoAdministrador(Usuario alvo, Perfil novoPerfil, bool novoAtivo, int administradoresAtivos) =>
        alvo.EhAdministrador
        && alvo.Ativo
        && (novoPerfil != Perfil.Administrador || !novoAtivo)
        && administradoresAtivos <= 1;
}
