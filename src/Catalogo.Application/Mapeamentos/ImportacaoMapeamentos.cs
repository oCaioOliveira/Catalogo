using System.Text.Json;
using System.Text.Json.Serialization;
using Catalogo.Application.Dtos;
using Catalogo.Domain.Importacoes;

namespace Catalogo.Application.Mapeamentos;

/// <summary>
/// Conversões da importação: os textos da API (IGNORAR, NOVA, PULAR...) e o JSON guardado nas colunas Opcoes, Mapeamento e Resumo
/// da tabela Importacao.
/// </summary>
public static class ImportacaoMapeamentos
{
    private static readonly JsonSerializerOptions OpcoesJson = new()
    {
        Converters = { new JsonStringEnumConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static string ParaJson<T>(T valor) => JsonSerializer.Serialize(valor, OpcoesJson);

    public static T? DeJson<T>(string? json) where T : class => json is null ? null : JsonSerializer.Deserialize<T>(json, OpcoesJson);

    public static string ParaTexto(TipoDestinoColuna tipo) => tipo switch
    {
        TipoDestinoColuna.Ignorar => "IGNORAR",
        TipoDestinoColuna.Campo => "CAMPO",
        TipoDestinoColuna.CampoId => "CAMPO_ID",
        TipoDestinoColuna.Equipe => "EQUIPE",
        _ => throw new ArgumentOutOfRangeException(nameof(tipo), tipo, null),
    };

    public static TipoDestinoColuna? TipoDestinoDeTexto(string? texto) => texto?.ToUpperInvariant() switch
    {
        "IGNORAR" => TipoDestinoColuna.Ignorar,
        "CAMPO" => TipoDestinoColuna.Campo,
        "CAMPO_ID" => TipoDestinoColuna.CampoId,
        "EQUIPE" => TipoDestinoColuna.Equipe,
        _ => null,
    };

    public static string ParaTexto(SituacaoLinha situacao) => situacao switch
    {
        SituacaoLinha.Nova => "NOVA",
        SituacaoLinha.Atualizacao => "ATUALIZACAO",
        SituacaoLinha.Ignorada => "IGNORADA",
        SituacaoLinha.Erro => "ERRO",
        _ => throw new ArgumentOutOfRangeException(nameof(situacao), situacao, null),
    };

    public static string ParaTexto(AcaoDuplicados acao) => acao == AcaoDuplicados.Atualizar ? "ATUALIZAR" : "PULAR";

    public static AcaoDuplicados? AcaoDuplicadosDeTexto(string? texto) => texto?.ToUpperInvariant() switch
    {
        "PULAR" => AcaoDuplicados.Pular,
        "ATUALIZAR" => AcaoDuplicados.Atualizar,
        _ => null,
    };

    public static string ParaTexto(TratamentoErros tratamento) => tratamento == TratamentoErros.TudoOuNada ? "TUDO_OU_NADA" : "SO_VALIDAS";

    public static TratamentoErros? TratamentoErrosDeTexto(string? texto) => texto?.ToUpperInvariant() switch
    {
        "SO_VALIDAS" => TratamentoErros.SoValidas,
        "TUDO_OU_NADA" => TratamentoErros.TudoOuNada,
        _ => null,
    };

    public static DestinoColunaDto ParaDto(this DestinoColuna destino) => new(ParaTexto(destino.Tipo), destino.CampoChave);

    public static TotaisImportacaoDto ParaDto(this TotaisImportacao totais) =>
        new(totais.Novos, totais.Atualizacoes, totais.Ignorados, totais.ComErro, totais.Total);

    public static OpcoesImportacaoDto ParaDto(this OpcoesImportacao opcoes) =>
        new(ParaTexto(opcoes.Duplicados), ParaTexto(opcoes.LinhasComErro), opcoes.EquipePadraoId);

    public static LinhaImportacaoDto ParaDto(this ResultadoLinha linha, LinhaImportada? original, int colunas) => new(
        linha.Numero,
        ParaTexto(linha.Situacao),
        Enumerable.Range(0, colunas).Select(c => original?.Celula(c).ParaTexto() ?? "").ToList(),
        linha.Erros.Select(e => new ErroLinhaDto(e.Coluna, e.Campo, e.Mensagem)).ToList(),
        linha.Referencias.Select(r => new ReferenciaResolvidaDto(r.Coluna, r.Campo, r.Id, r.Nome)).ToList(),
        linha.RegistroId);

    /// <summary>O texto do resultado de uma linha no relatório (coluna "Resultado"), antes e depois de executar.</summary>
    public static string ResultadoNoRelatorio(SituacaoLinha situacao, bool concluida) => situacao switch
    {
        SituacaoLinha.Nova => concluida ? "Criado" : "Será criado",
        SituacaoLinha.Atualizacao => concluida ? "Atualizado" : "Será atualizado",
        SituacaoLinha.Ignorada => "Ignorado (já existe)",
        SituacaoLinha.Erro => "Erro",
        _ => throw new ArgumentOutOfRangeException(nameof(situacao), situacao, null),
    };
}
