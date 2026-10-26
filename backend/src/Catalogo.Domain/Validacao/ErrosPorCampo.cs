using Catalogo.Domain.Excecoes;

namespace Catalogo.Domain.Validacao;

/// <summary>
/// Junta os erros de validação por campo, para devolver TODOS de uma vez (RN05)
/// em vez de parar no primeiro.
/// </summary>
public sealed class ErrosPorCampo
{
    private readonly Dictionary<string, List<string>> _erros = new(StringComparer.Ordinal);

    public bool PossuiErros => _erros.Count > 0;

    public void Adicionar(string chaveCampo, string mensagem)
    {
        if (!_erros.TryGetValue(chaveCampo, out var mensagens))
        {
            mensagens = [];
            _erros[chaveCampo] = mensagens;
        }

        mensagens.Add(mensagem);
    }

    public IReadOnlyDictionary<string, string[]> ParaDicionario() =>
        _erros.ToDictionary(par => par.Key, par => par.Value.ToArray(), StringComparer.Ordinal);

    /// <summary>Lança <see cref="ValidacaoRegistroException"/> se algum erro foi adicionado.</summary>
    public void LancarSeHouverErros()
    {
        if (PossuiErros)
            throw new ValidacaoRegistroException(ParaDicionario());
    }
}
