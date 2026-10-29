using Catalogo.Domain.Validacao.Tipos;

namespace Catalogo.Domain.Validacao;

/// <summary>
/// Lista com uma estratégia de validação para cada TipoDado.
/// Ao criar um validador novo, adicione-o aqui (o teste ValidadoresPadraoTests confere
/// que todo TipoDado tem exatamente um validador).
/// </summary>
public static class ValidadoresPadrao
{
    public static IReadOnlyList<ITipoDadoValidador> Todos() =>
    [
        new TextoValidador(),
        new TextoLongoValidador(),
        new InteiroValidador(),
        new DecimalValidador(),
        new BooleanoValidador(),
        new DataValidador(),
        new DataHoraValidador(),
        new ListaValidador(),
        new ReferenciaValidador(),
        new UrlValidador(),
    ];
}
