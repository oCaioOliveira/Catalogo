using System.Text.Json;
using System.Text.Json.Nodes;
using Catalogo.Domain.Metadados;

namespace Catalogo.Domain.Validacao;

/// <summary>
/// Valida o JSON enviado pelo cliente contra os metadados da entidade.
/// Recebe um objeto PLANO com todos os campos (simples e referências misturados) e devolve
/// TODOS os erros de uma vez, por campo (RN05). Regras aplicadas:
///   RN01 campo desconhecido, RN02 campo inativo, RN03 obrigatoriedade, RN04 tipo.
/// Não acessa banco: existência dos registros referenciados (RN06) e dependências (RN08)
/// são conferidas depois, pelo caso de uso.
/// </summary>
public sealed class ValidadorRegistro
{
    private readonly Dictionary<TipoDado, ITipoDadoValidador> _validadoresPorTipo = [];

    public ValidadorRegistro(IEnumerable<ITipoDadoValidador> validadores)
    {
        foreach (var validador in validadores)
        {
            if (!_validadoresPorTipo.TryAdd(validador.Tipo, validador))
                throw new InvalidOperationException($"Há mais de um validador para o tipo {validador.Tipo}.");
        }

        // Falha logo na inicialização se alguém criou um TipoDado e esqueceu o validador.
        foreach (var tipo in Enum.GetValues<TipoDado>())
        {
            if (!_validadoresPorTipo.ContainsKey(tipo))
                throw new InvalidOperationException($"Não há validador para o tipo {tipo}.");
        }
    }

    public ResultadoValidacaoRegistro Validar(Entidade entidade, JsonObject dados)
    {
        var erros = new ErrosPorCampo();
        // Chaves recusadas por RN01/RN02: quem enviou usa metadados desatualizados (o front recarrega e pede revisão).
        var desatualizados = new List<string>();

        // RN01 e RN02: toda chave enviada precisa ser um campo ativo da entidade.
        foreach (var (chave, _) in dados)
        {
            // RN14: id, versao, criadoEm e atualizadoEm são colunas de sistema, não campos.
            // São ignoradas aqui (o PUT lê a "versao" antes de validar).
            if (ChavesReservadas.DoRegistro.Contains(chave))
                continue;

            var campo = entidade.ObterCampo(chave);
            if (campo is null)
            {
                erros.Adicionar(chave, $"Campo desconhecido na entidade '{entidade.Nome}'.");
                desatualizados.Add(chave);
            }
            else if (!campo.Ativo)
            {
                erros.Adicionar(chave, "Campo inativo: não pode mais ser preenchido.");
                desatualizados.Add(chave);
            }
        }

        var dadosSimples = new JsonObject();
        var referencias = new List<ReferenciaValidada>();

        foreach (var campo in entidade.Campos.Where(c => c.Ativo))
        {
            dados.TryGetPropertyValue(campo.Chave, out var valorEnviado);

            // RN03: nulo, ausente ou texto em branco contam como "não preenchido".
            if (EstaVazio(valorEnviado))
            {
                if (campo.Obrigatorio)
                    erros.Adicionar(campo.Chave, "Campo obrigatório.");
                continue;
            }

            // RN04: cada tipo tem sua estratégia de validação e normalização.
            var resultado = _validadoresPorTipo[campo.TipoDado]
                .Validar(campo, JsonSerializer.SerializeToElement(valorEnviado));

            if (!resultado.Valido)
            {
                erros.Adicionar(campo.Chave, resultado.Erro!);
                continue;
            }

            if (campo.EhReferencia)
                referencias.Add(new ReferenciaValidada(campo, Guid.Parse(resultado.ValorNormalizado!.GetValue<string>())));
            else
                dadosSimples[campo.Chave] = resultado.ValorNormalizado;
        }

        return erros.PossuiErros
            ? ResultadoValidacaoRegistro.ComErros(erros.ParaDicionario(), desatualizados)
            : ResultadoValidacaoRegistro.Sucesso(new RegistroValidado(dadosSimples, referencias));
    }

    /// <summary>
    /// RN04 para UM valor (ex.: o valor padrão de um campo, RN60): valida e normaliza com a estratégia do tipo do campo,
    /// a mesma usada nos registros. Vazio é erro: um valor padrão vazio não preencheria nada.
    /// </summary>
    public ResultadoValidacaoValor ValidarValor(Campo campo, JsonNode? valor) =>
        EstaVazio(valor)
            ? ResultadoValidacaoValor.Falha("Obrigatório: o valor que vai preencher os registros.")
            : _validadoresPorTipo[campo.TipoDado].Validar(campo, JsonSerializer.SerializeToElement(valor));

    private static bool EstaVazio(JsonNode? valor) =>
        valor is null
        || (valor is JsonValue valorSimples
            && valorSimples.TryGetValue<string>(out var texto)
            && string.IsNullOrWhiteSpace(texto));
}

/// <summary>Resultado do <see cref="ValidadorRegistro"/>: o registro validado OU os erros.</summary>
public sealed class ResultadoValidacaoRegistro
{
    private ResultadoValidacaoRegistro(
        RegistroValidado? registro, IReadOnlyDictionary<string, string[]> erros, IReadOnlyList<string> camposDesatualizados)
    {
        Registro = registro;
        Erros = erros;
        CamposDesatualizados = camposDesatualizados;
    }

    /// <summary>Chaves recusadas como desconhecidas ou inativas (RN01, RN02): sinal de metadados desatualizados em quem enviou.</summary>
    public IReadOnlyList<string> CamposDesatualizados { get; }

    /// <summary>Preenchido só quando <see cref="Valido"/>.</summary>
    public RegistroValidado? Registro { get; }

    /// <summary>Chave do campo → mensagens. Vazio quando <see cref="Valido"/>.</summary>
    public IReadOnlyDictionary<string, string[]> Erros { get; }

    public bool Valido => Registro is not null;

    public static ResultadoValidacaoRegistro Sucesso(RegistroValidado registro) =>
        new(registro, new Dictionary<string, string[]>(), []);

    public static ResultadoValidacaoRegistro ComErros(
        IReadOnlyDictionary<string, string[]> erros, IReadOnlyList<string>? camposDesatualizados = null) =>
        new(null, erros, camposDesatualizados ?? []);
}
