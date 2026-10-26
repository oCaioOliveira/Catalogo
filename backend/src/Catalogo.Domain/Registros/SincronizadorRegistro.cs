using System.Text.Json.Nodes;
using Catalogo.Domain.ChavesUnicas;
using Catalogo.Domain.Metadados;
using Catalogo.Domain.Validacao;

namespace Catalogo.Domain.Registros;

/// <summary>
/// Copia um <see cref="RegistroValidado"/> para um <see cref="Registro"/>, deixando Dados,
/// Referencias, Valores e ChavesUnicas coerentes entre si. É o MESMO código para criar
/// (registro novo, listas vazias) e para atualizar (registro carregado do banco):
///   - o que não existe mais é removido da lista;
///   - o que mudou é alterado no próprio objeto;
///   - o que é novo é adicionado.
/// Assim Registro.Dados e RegistroValor nunca ficam fora de sincronia (docs/adr/0013).
///
/// Campos INATIVOS não passam pela validação, mas os valores deles são PRESERVADOS (RN69): em Dados, RegistroValor e
/// RegistroReferencia. Assim, desativar um campo, editar o registro e reativar o campo devolve o valor original.
/// </summary>
public static class SincronizadorRegistro
{
    public static void Aplicar(
        Registro registro,
        Entidade entidade,
        RegistroValidado validado,
        IReadOnlyList<ChaveUnicaCalculada> chavesUnicas)
    {
        // A entidade de um registro nunca muda (faz parte das FKs compostas).
        if (registro.EntidadeId == Guid.Empty)
            registro.EntidadeId = entidade.Id;
        else if (registro.EntidadeId != entidade.Id)
            throw new InvalidOperationException("O registro é de outra entidade.");

        var inativos = entidade.Campos.Where(c => !c.Ativo).ToList();
        var idsInativos = inativos.Select(c => c.Id).ToHashSet();

        registro.Dados = validado.SerializarDadosSimples(ValoresPreservados(registro, inativos));
        SincronizarReferencias(registro, entidade, validado, idsInativos);
        SincronizarValores(registro, entidade, validado, idsInativos);
        SincronizarChavesUnicas(registro, entidade, chavesUnicas);
    }

    /// <summary>
    /// RN60 e RN61: grava o valor (já validado e normalizado) de UM campo simples num registro existente, em Dados e em
    /// RegistroValor juntos. Usado pela administração de metadados para preencher o valor padrão em lotes.
    /// </summary>
    public static void DefinirValor(Registro registro, Campo campo, JsonNode valorNormalizado)
    {
        if (campo.EhReferencia)
            throw new InvalidOperationException("REFERENCIA não recebe valor padrão (RN60).");

        var dados = JsonNode.Parse(registro.Dados) as JsonObject ?? [];
        dados[campo.Chave] = valorNormalizado.DeepClone();
        registro.Dados = RegistroValidado.Serializar(dados);

        var novo = ConversorValorTipado.ParaValor(campo, valorNormalizado)!;
        var existente = registro.Valores.FirstOrDefault(v => v.CampoId == campo.Id);
        if (existente is null)
        {
            novo.RegistroId = registro.Id;
            registro.Valores.Add(novo);
        }
        else
        {
            existente.ValorTexto = novo.ValorTexto;
            existente.ValorNumero = novo.ValorNumero;
            existente.ValorData = novo.ValorData;
            existente.ValorBooleano = novo.ValorBooleano;
        }
    }

    /// <summary>
    /// O valor ARMAZENADO de um campo num registro, no formato normalizado da validação: o JSON de Dados para campos simples e
    /// o GUID ("D") para referências. Com isto se recalcula o hash da chave única de registros já gravados (RN09, RN71).
    /// </summary>
    public static Func<Campo, JsonNode?> LeitorDeValores(Registro registro)
    {
        var dados = JsonNode.Parse(registro.Dados) as JsonObject ?? [];
        return campo => campo.EhReferencia
            ? registro.Referencias.FirstOrDefault(r => r.CampoId == campo.Id) is { } referencia
                ? JsonValue.Create(referencia.RegistroDestinoId.ToString("D"))
                : null
            : dados[campo.Chave]?.DeepClone();
    }

    /// <summary>Valores gravados (em Dados) dos campos simples inativos.</summary>
    private static IEnumerable<KeyValuePair<string, JsonNode?>> ValoresPreservados(Registro registro, IReadOnlyList<Campo> inativos)
    {
        if (inativos.Count == 0 || string.IsNullOrEmpty(registro.Dados))
            return [];

        var armazenados = JsonNode.Parse(registro.Dados) as JsonObject ?? [];
        return inativos
            .Where(c => !c.EhReferencia && armazenados.ContainsKey(c.Chave))
            .Select(c => KeyValuePair.Create(c.Chave, armazenados[c.Chave]))
            .ToList();
    }

    private static void SincronizarReferencias(Registro registro, Entidade entidade, RegistroValidado validado, IReadOnlySet<Guid> inativos)
    {
        var desejadas = validado.Referencias.ToDictionary(r => r.Campo.Id);

        // Campo REFERENCIA que ficou vazio (RN15: ausente no PUT = removido). Os de campos inativos ficam (RN69).
        registro.Referencias.RemoveAll(r => !desejadas.ContainsKey(r.CampoId) && !inativos.Contains(r.CampoId));

        foreach (var desejada in desejadas.Values)
        {
            var existente = registro.Referencias.FirstOrDefault(r => r.CampoId == desejada.Campo.Id);
            if (existente is null)
            {
                registro.Referencias.Add(new RegistroReferencia
                {
                    RegistroOrigemId = registro.Id,
                    EntidadeOrigemId = entidade.Id,
                    CampoId = desejada.Campo.Id,
                    RegistroDestinoId = desejada.RegistroDestinoId,
                    EntidadeDestinoId = desejada.Campo.EntidadeReferenciadaId!.Value,
                });
            }
            else if (existente.RegistroDestinoId != desejada.RegistroDestinoId)
            {
                existente.RegistroDestinoId = desejada.RegistroDestinoId;
            }
        }
    }

    private static void SincronizarValores(Registro registro, Entidade entidade, RegistroValidado validado, IReadOnlySet<Guid> inativos)
    {
        var desejados = new Dictionary<Guid, RegistroValor>();
        foreach (var (chave, valor) in validado.DadosSimples)
        {
            var campo = entidade.ObterCampo(chave);
            if (campo is null || valor is null)
                continue;

            var registroValor = ConversorValorTipado.ParaValor(campo, valor);
            if (registroValor is not null)
                desejados[campo.Id] = registroValor;
        }

        registro.Valores.RemoveAll(v => !desejados.ContainsKey(v.CampoId) && !inativos.Contains(v.CampoId));

        foreach (var desejado in desejados.Values)
        {
            var existente = registro.Valores.FirstOrDefault(v => v.CampoId == desejado.CampoId);
            if (existente is null)
            {
                desejado.RegistroId = registro.Id;
                registro.Valores.Add(desejado);
            }
            else
            {
                existente.ValorTexto = desejado.ValorTexto;
                existente.ValorNumero = desejado.ValorNumero;
                existente.ValorData = desejado.ValorData;
                existente.ValorBooleano = desejado.ValorBooleano;
            }
        }
    }

    private static void SincronizarChavesUnicas(Registro registro, Entidade entidade, IReadOnlyList<ChaveUnicaCalculada> chavesUnicas)
    {
        var desejadas = chavesUnicas.ToDictionary(c => c.Restricao.Id);

        // Restrição que ficou com algum valor vazio deixa de ter chave (RN10).
        registro.ChavesUnicas.RemoveAll(c => !desejadas.ContainsKey(c.RestricaoUnicaId));

        foreach (var desejada in desejadas.Values)
        {
            var existente = registro.ChavesUnicas.FirstOrDefault(c => c.RestricaoUnicaId == desejada.Restricao.Id);
            if (existente is null)
            {
                registro.ChavesUnicas.Add(new RegistroChaveUnica
                {
                    RestricaoUnicaId = desejada.Restricao.Id,
                    EntidadeId = entidade.Id,
                    RegistroId = registro.Id,
                    ValorHash = desejada.ValorHash,
                });
            }
            else if (!existente.ValorHash.AsSpan().SequenceEqual(desejada.ValorHash))
            {
                existente.ValorHash = desejada.ValorHash;
            }
        }
    }
}
