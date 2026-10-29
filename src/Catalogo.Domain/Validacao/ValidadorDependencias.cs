using System.Text.Json.Nodes;
using Catalogo.Domain.Metadados;
using Catalogo.Domain.Registros;

namespace Catalogo.Domain.Validacao;

/// <summary>
/// Confere os campos dependentes (RN08) de um registro novo.
/// Exemplo: em cenario_teste, o endpoint_servico escolhido precisa ter o mesmo projeto
/// do relatorio_teste escolhido. Ver <see cref="CampoDependencia"/> para o significado de cada campo.
/// </summary>
public static class ValidadorDependencias
{
    /// <param name="entidade">Entidade do registro novo (com suas dependências).</param>
    /// <param name="registro">Registro novo, já validado.</param>
    /// <param name="registrosReferenciados">
    /// Registros apontados pelas referências do registro novo, por Id, carregados com as
    /// suas Referencias. Deve conter só registros que existem e são da entidade certa (RN06).
    /// </param>
    /// <param name="erros">Onde os erros são adicionados (chave = campo dependente).</param>
    public static void Validar(
        Entidade entidade,
        RegistroValidado registro,
        IReadOnlyDictionary<Guid, Registro> registrosReferenciados,
        ErrosPorCampo erros)
    {
        foreach (var dependencia in entidade.Dependencias)
        {
            // Campo dependente vazio: nada a conferir (obrigatoriedade é outra regra).
            var escolhido = registro.ObterReferencia(dependencia.Campo);
            if (escolhido is null || !registrosReferenciados.TryGetValue(escolhido.RegistroDestinoId, out var registroEscolhido))
                continue;

            string? valorOrigem;
            if (dependencia.CampoVia is null)
            {
                // Origem no próprio registro novo.
                valorOrigem = ValorComparavel(dependencia.CampoOrigem, registro.ObterValor(dependencia.CampoOrigem));
            }
            else
            {
                // Origem no registro apontado pelo CampoVia (um salto).
                var via = registro.ObterReferencia(dependencia.CampoVia);
                if (via is null || !registrosReferenciados.TryGetValue(via.RegistroDestinoId, out var registroVia))
                    continue;

                valorOrigem = LerValor(registroVia, dependencia.CampoOrigem);
            }

            // Sem valor de origem não há filtro a respeitar.
            if (valorOrigem is null)
                continue;

            var valorAlvo = LerValor(registroEscolhido, dependencia.CampoAlvo);
            if (!string.Equals(valorAlvo, valorOrigem, StringComparison.Ordinal))
                erros.Adicionar(dependencia.Campo.Chave, Mensagem(dependencia));
        }
    }

    private static string Mensagem(CampoDependencia dependencia) => dependencia.CampoVia is null
        ? $"O registro escolhido em '{dependencia.Campo.Nome}' precisa ter '{dependencia.CampoAlvo.Nome}' " +
          $"igual ao campo '{dependencia.CampoOrigem.Nome}' deste registro."
        : $"O registro escolhido em '{dependencia.Campo.Nome}' precisa ter '{dependencia.CampoAlvo.Nome}' " +
          $"igual ao '{dependencia.CampoOrigem.Nome}' do registro escolhido em '{dependencia.CampoVia.Nome}'.";

    /// <summary>
    /// O registro <paramref name="candidato"/> atende à dependência, com o valor de origem já lido? Usado pela importação para
    /// escolher, entre itens de mesmo nome, o que respeita a dependência (RN08 e RN99). A regra é a mesma do <see cref="Validar"/>.
    /// </summary>
    public static bool Atende(CampoDependencia dependencia, string? valorOrigem, Registro candidato) =>
        valorOrigem is null || string.Equals(LerValor(candidato, dependencia.CampoAlvo), valorOrigem, StringComparison.Ordinal);

    /// <summary>Lê o valor de um campo num registro já gravado, no mesmo formato de <see cref="ValorComparavel"/>.</summary>
    public static string? LerValor(Registro registro, Campo campo)
    {
        if (campo.EhReferencia)
            return registro.Referencias.FirstOrDefault(r => r.CampoId == campo.Id)?.RegistroDestinoId.ToString("D");

        var dados = JsonNode.Parse(registro.Dados) as JsonObject;
        return ValorComparavel(campo, dados?[campo.Chave]);
    }

    /// <summary>
    /// Transforma um valor em texto comparável. Os valores gravados já estão normalizados (RN04),
    /// então o mesmo valor sempre gera o mesmo texto.
    /// </summary>
    public static string? ValorComparavel(Campo campo, JsonNode? valor)
    {
        if (valor is null)
            return null;

        return campo.EhReferencia ? valor.GetValue<string>() : valor.ToJsonString();
    }
}
