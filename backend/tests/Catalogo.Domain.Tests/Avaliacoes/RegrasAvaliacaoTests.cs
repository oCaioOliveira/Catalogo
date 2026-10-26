using System.Text.Json.Nodes;
using Catalogo.Domain.Aprovacao;
using Catalogo.Domain.Avaliacoes;
using Catalogo.Domain.Validacao;

namespace Catalogo.Domain.Tests.Avaliacoes;

// Validação das respostas (RN46), aplicação na avaliação (RN43, RN45) e o que pode ser avaliado (RN44).
public class RegrasAvaliacaoTests
{
    // qualidade: ESTRELAS, obrigatório, comentário OBRIGATORIO
    // clareza: ESTRELAS, opcional, sem comentário
    // tempo: ESTRELAS, opcional, comentário OPCIONAL até 10 caracteres
    // observacoes: TEXTO, opcional, até 20 caracteres
    // antigo: ESTRELAS INATIVO
    private readonly ModeloAvaliacao _modelo = new()
    {
        Id = Guid.NewGuid(),
        Nome = "Modelo B",
        Criterios =
        [
            Criterio("qualidade", TipoCriterio.Estrelas, obrigatorio: true, ExigenciaComentario.Obrigatorio, ordem: 1),
            Criterio("clareza", TipoCriterio.Estrelas, obrigatorio: false, ExigenciaComentario.Nenhum, ordem: 2),
            Criterio("tempo", TipoCriterio.Estrelas, obrigatorio: false, ExigenciaComentario.Opcional, ordem: 3, tamanho: 10),
            Criterio("observacoes", TipoCriterio.Texto, obrigatorio: false, ExigenciaComentario.Nenhum, ordem: 4, tamanho: 20),
            Criterio("antigo", TipoCriterio.Estrelas, obrigatorio: true, ExigenciaComentario.Nenhum, ordem: 5, ativo: false),
        ],
    };

    [Fact]
    public void ValidarRespostas_Validas_DevolveNormalizadas()
    {
        // RN46
        var erros = new ErrosPorCampo();

        var respostas = RegrasAvaliacao.ValidarRespostas(_modelo, Json("""
            {
              "qualidade": { "estrelas": 4, "comentario": "  Bom relatório  " },
              "clareza": { "estrelas": 5.0, "comentario": "   " },
              "observacoes": { "texto": " Ok " },
              "tempo": null
            }
            """), erros);

        Assert.False(erros.PossuiErros);
        Assert.Equal(
            [("qualidade", (byte?)4, "Bom relatório"), ("clareza", (byte?)5, null), ("observacoes", null, "Ok")],
            respostas.Select(r => (r.Criterio.Chave, r.Estrelas, r.Texto)));
    }

    public static IEnumerable<object[]> RespostasInvalidas() =>
    [
        // Critério desconhecido ou inativo
        ["""{ "qualidade": { "estrelas": 4, "comentario": "c" }, "inexistente": { "estrelas": 3 } }""", "respostas.inexistente", "desconhecido ou inativo"],
        ["""{ "qualidade": { "estrelas": 4, "comentario": "c" }, "antigo": { "estrelas": 3 } }""", "respostas.antigo", "desconhecido ou inativo"],
        // Obrigatório sem resposta (ausente ou nulo)
        ["""{ "clareza": { "estrelas": 3 } }""", "respostas.qualidade", "Obrigatório"],
        ["""{ "qualidade": null }""", "respostas.qualidade", "Obrigatório"],
        // Estrelas fora de 1 a 5, não inteiras, ausentes ou não numéricas
        ["""{ "qualidade": { "estrelas": 6, "comentario": "c" } }""", "respostas.qualidade.estrelas", "inteiro de 1 a 5"],
        ["""{ "qualidade": { "estrelas": 0, "comentario": "c" } }""", "respostas.qualidade.estrelas", "inteiro de 1 a 5"],
        ["""{ "qualidade": { "estrelas": 4.5, "comentario": "c" } }""", "respostas.qualidade.estrelas", "inteiro de 1 a 5"],
        ["""{ "qualidade": { "estrelas": "4", "comentario": "c" } }""", "respostas.qualidade.estrelas", "inteiro de 1 a 5"],
        ["""{ "qualidade": { "comentario": "c" } }""", "respostas.qualidade.estrelas", "Obrigatório"],
        // Comentário onde não cabe, ausente onde é obrigatório, longo demais ou não-texto
        ["""{ "qualidade": { "estrelas": 4, "comentario": "c" }, "clareza": { "estrelas": 3, "comentario": "x" } }""", "respostas.clareza.comentario", "não aceita comentário"],
        ["""{ "qualidade": { "estrelas": 4 } }""", "respostas.qualidade.comentario", "justifique"],
        ["""{ "qualidade": { "estrelas": 4, "comentario": "c" }, "tempo": { "estrelas": 3, "comentario": "12345678901" } }""", "respostas.tempo.comentario", "no máximo 10"],
        ["""{ "qualidade": { "estrelas": 4, "comentario": 5 } }""", "respostas.qualidade.comentario", "texto"],
        // Texto vazio, só espaços ou acima do máximo
        ["""{ "qualidade": { "estrelas": 4, "comentario": "c" }, "observacoes": { "texto": "" } }""", "respostas.observacoes.texto", "vazio"],
        ["""{ "qualidade": { "estrelas": 4, "comentario": "c" }, "observacoes": { "texto": "   " } }""", "respostas.observacoes.texto", "vazio"],
        ["""{ "qualidade": { "estrelas": 4, "comentario": "c" }, "observacoes": {} }""", "respostas.observacoes.texto", "vazio"],
        ["""{ "qualidade": { "estrelas": 4, "comentario": "c" }, "observacoes": { "texto": "123456789012345678901" } }""", "respostas.observacoes.texto", "no máximo 20"],
        // Formato
        ["""{ "qualidade": 4 }""", "respostas.qualidade", "objeto"],
        ["""{ "qualidade": { "estrelas": 4, "comentario": "c", "nota": 1 } }""", "respostas.qualidade.nota", "desconhecida"],
        ["""{ "qualidade": { "estrelas": 4, "comentario": "c" }, "observacoes": { "texto": "a", "estrelas": 2 } }""", "respostas.observacoes.estrelas", "desconhecida"],
    ];

    [Theory]
    [MemberData(nameof(RespostasInvalidas))]
    public void ValidarRespostas_CadaRegra(string json, string chaveErro, string trecho)
    {
        // RN46: cada caso devolve exatamente o erro esperado.
        var erros = new ErrosPorCampo();

        RegrasAvaliacao.ValidarRespostas(_modelo, Json(json), erros);

        var dicionario = erros.ParaDicionario();
        Assert.Equal([chaveErro], dicionario.Keys);
        Assert.Contains(trecho, Assert.Single(dicionario[chaveErro]));
    }

    [Fact]
    public void ValidarRespostas_SemRespostas_Erro()
    {
        var erros = new ErrosPorCampo();

        RegrasAvaliacao.ValidarRespostas(_modelo, null, erros);

        Assert.True(erros.ParaDicionario().ContainsKey("respostas"));
    }

    [Fact]
    public void ValidarRespostas_ComentarioObrigatorioAusenteSeisEstrelasECriterioInexistente_TresErrosJuntos()
    {
        // RN05 e RN46: o caso do teste de integração, sem banco.
        var erros = new ErrosPorCampo();

        RegrasAvaliacao.ValidarRespostas(_modelo, Json("""{ "qualidade": { "estrelas": 6 }, "inexistente": { "estrelas": 3 } }"""), erros);

        Assert.Equal(
            ["respostas.inexistente", "respostas.qualidade.comentario", "respostas.qualidade.estrelas"],
            erros.ParaDicionario().Keys.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void AplicarRespostas_EditandoAtualizaInclueRemoveEPreservaCriterioInativo()
    {
        // RN43 e RN45: respostas a critérios desativados ficam; critério ativo não respondido sai.
        var avaliacao = new Avaliacao
        {
            Id = Guid.NewGuid(),
            Respostas =
            [
                Resposta("qualidade", 2, "ruim"),
                Resposta("clareza", 3, null),
                Resposta("antigo", 1, null),
            ],
        };
        var erros = new ErrosPorCampo();
        var validadas = RegrasAvaliacao.ValidarRespostas(_modelo, Json("""
            { "qualidade": { "estrelas": 5, "comentario": "melhorou" }, "observacoes": { "texto": "novo" } }
            """), erros);

        var removidas = RegrasAvaliacao.AplicarRespostas(avaliacao, _modelo, validadas);

        Assert.Equal(_modelo.ObterCriterio("clareza")!.Id, Assert.Single(removidas).CriterioAvaliacaoId);
        Assert.Equal(
            [("qualidade", (byte?)5, "melhorou"), ("antigo", (byte?)1, null), ("observacoes", null, "novo")],
            avaliacao.Respostas.Select(r => (Chave(r), r.Estrelas, r.Texto)));
        Assert.Equal(avaliacao.Id, avaliacao.Respostas.Single(r => Chave(r) == "observacoes").AvaliacaoId);   // a nova ganha a FK
    }

    [Theory]
    [InlineData(SituacaoAprovacao.Aprovado, true)]
    [InlineData(SituacaoAprovacao.Pendente, false)]
    [InlineData(SituacaoAprovacao.Rejeitado, false)]
    public void PodeSerAvaliado_SoAprovado(SituacaoAprovacao situacao, bool esperado)
    {
        // RN44
        Assert.Equal(esperado, RegrasAvaliacao.PodeSerAvaliado(situacao));
    }

    [Theory]
    [InlineData(4.0, 4.0)]
    [InlineData(4.25, 4.3)]     // metade para cima
    [InlineData(3.95, 4.0)]
    [InlineData(4.333333, 4.3)]
    [InlineData(null, null)]
    public void ArredondarMedia_UmaCasaDecimal(double? media, double? esperado)
    {
        // RN47
        Assert.Equal(esperado is null ? null : (decimal)esperado, RegrasAvaliacao.ArredondarMedia(media));
    }

    [Fact]
    public void ArredondarMedia_SempreComUmaCasa_NoTexto()
    {
        // RN47: "4.0", e não "4", no JSON.
        Assert.Equal("4.0", RegrasAvaliacao.ArredondarMedia(4.0)!.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    private string Chave(AvaliacaoResposta resposta) => _modelo.Criterios.Single(c => c.Id == resposta.CriterioAvaliacaoId).Chave;

    private AvaliacaoResposta Resposta(string chave, byte? estrelas, string? texto) =>
        new() { CriterioAvaliacaoId = _modelo.ObterCriterio(chave)!.Id, Estrelas = estrelas, Texto = texto };

    private static JsonObject Json(string json) => JsonNode.Parse(json)!.AsObject();

    private static CriterioAvaliacao Criterio(
        string chave, TipoCriterio tipo, bool obrigatorio, ExigenciaComentario comentario, int ordem, int tamanho = 2000, bool ativo = true) => new()
    {
        Id = Guid.NewGuid(),
        Chave = chave,
        Nome = chave,
        Tipo = tipo,
        Obrigatorio = obrigatorio,
        Comentario = comentario,
        TamanhoMaximoTexto = tamanho,
        Ordem = ordem,
        Ativo = ativo,
    };
}
