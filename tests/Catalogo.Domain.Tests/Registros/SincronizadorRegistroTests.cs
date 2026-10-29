using System.Text.Json.Nodes;
using Catalogo.Domain.ChavesUnicas;
using Catalogo.Domain.Metadados;
using Catalogo.Domain.Registros;
using Catalogo.Domain.Tests.Apoio;
using Catalogo.Domain.Validacao;

namespace Catalogo.Domain.Tests.Registros;

// docs/adr/0013: Registro.Dados, RegistroValor, RegistroReferencia e RegistroChaveUnica andam juntos.
public class SincronizadorRegistroTests
{
    private static readonly Guid ProjetoEntidadeId = Guid.NewGuid();

    private readonly Campo _nome = Metadado.Campo("nome", TipoDado.Texto, tamanhoMaximo: 50, ordem: 1);
    private readonly Campo _quantidade = Metadado.Campo("quantidade", TipoDado.Inteiro, ordem: 2);
    private readonly Campo _taxa = Metadado.Campo("taxa", TipoDado.Decimal, precisao: 5, escala: 2, ordem: 3);
    private readonly Campo _data = Metadado.Campo("data", TipoDado.Data, ordem: 4);
    private readonly Campo _execucao = Metadado.Campo("execucao", TipoDado.DataHora, ordem: 5);
    private readonly Campo _ativo = Metadado.Campo("ativo", TipoDado.Booleano, ordem: 6);
    private readonly Campo _projeto = Metadado.Campo("projeto", TipoDado.Referencia, entidadeReferenciadaId: ProjetoEntidadeId, ordem: 7);

    private readonly ValidadorRegistro _validador = new(ValidadoresPadrao.Todos());
    private readonly CalculadoraChaveUnica _calculadora = new();

    private readonly Entidade _entidade;

    public SincronizadorRegistroTests()
    {
        // Montada uma vez: a restrição única precisa ter o mesmo Id em todas as aplicações do teste.
        _entidade = Metadado.Entidade(
            [_nome, _quantidade, _taxa, _data, _execucao, _ativo, _projeto],
            [Metadado.Restricao("uq_nome", _nome)]);
    }

    [Fact]
    public void Aplicar_RegistroNovo_PreencheDadosValoresTipadosReferenciasEChaves()
    {
        var projeto = Guid.NewGuid();
        var registro = new Registro();

        Aplicar(registro, new JsonObject
        {
            ["nome"] = "Ana", ["quantidade"] = 7, ["taxa"] = 1.5, ["data"] = "2026-10-21",
            ["execucao"] = "2026-10-21T14:30:00-03:00", ["ativo"] = true, ["projeto"] = projeto.ToString(),
        });

        Assert.Equal(Metadado.EntidadeId, registro.EntidadeId);
        Assert.Equal("Ana", Valor(registro, _nome).ValorTexto);
        Assert.Equal(7m, Valor(registro, _quantidade).ValorNumero);
        Assert.Equal(1.50m, Valor(registro, _taxa).ValorNumero);
        Assert.Equal(new DateTime(2026, 10, 21), Valor(registro, _data).ValorData);
        Assert.Equal(new DateTime(2026, 10, 21, 17, 30, 0), Valor(registro, _execucao).ValorData);
        Assert.Equal(true, Valor(registro, _ativo).ValorBooleano);
        // REFERENCIA não vira RegistroValor (fica em RegistroReferencia).
        Assert.Equal(6, registro.Valores.Count);
        Assert.Equal(projeto, Assert.Single(registro.Referencias).RegistroDestinoId);
        Assert.Single(registro.ChavesUnicas);
    }

    [Fact]
    public void Aplicar_CampoRemovido_SaiDoJsonDosValoresDaReferenciaEDaChave()
    {
        // RN15 (ausente = removido) e RN10 (nulo não gera chave).
        var registro = new Registro();
        Aplicar(registro, new JsonObject { ["nome"] = "Ana", ["quantidade"] = 7, ["projeto"] = Guid.NewGuid().ToString() });

        Aplicar(registro, new JsonObject { ["quantidade"] = 7 });

        Assert.Equal("{\"quantidade\":7}", registro.Dados);
        Assert.Equal(_quantidade.Id, Assert.Single(registro.Valores).CampoId);
        Assert.Empty(registro.Referencias);
        Assert.Empty(registro.ChavesUnicas);
    }

    [Fact]
    public void Aplicar_ValorAlterado_AlteraOsMesmosObjetos()
    {
        // O EF transforma alteração no próprio objeto em UPDATE (e não DELETE + INSERT).
        var registro = new Registro();
        Aplicar(registro, new JsonObject { ["nome"] = "Ana", ["projeto"] = Guid.NewGuid().ToString() });
        var valorAntes = Valor(registro, _nome);
        var referenciaAntes = registro.Referencias[0];
        var chaveAntes = registro.ChavesUnicas[0];
        var hashAntes = chaveAntes.ValorHash;
        var novoProjeto = Guid.NewGuid();

        Aplicar(registro, new JsonObject { ["nome"] = "Bia", ["projeto"] = novoProjeto.ToString() });

        Assert.Same(valorAntes, Valor(registro, _nome));
        Assert.Equal("Bia", valorAntes.ValorTexto);
        Assert.Same(referenciaAntes, registro.Referencias[0]);
        Assert.Equal(novoProjeto, referenciaAntes.RegistroDestinoId);
        Assert.Same(chaveAntes, registro.ChavesUnicas[0]);
        Assert.NotEqual(hashAntes, chaveAntes.ValorHash);
    }

    [Fact]
    public void Aplicar_CampoInativo_PreservaOValorEmDadosValoresEReferencias()
    {
        // RN69: desativar um campo, editar o registro e reativar o campo devolve o valor original.
        var projeto = Guid.NewGuid();
        var registro = new Registro { Id = Guid.NewGuid() };
        Aplicar(registro, new JsonObject { ["nome"] = "Ana", ["quantidade"] = 7, ["projeto"] = projeto.ToString() });
        var comInativos = Metadado.Entidade(
            _entidade.Campos.Select(c => c == _quantidade || c == _projeto ? Inativo(c) : c).ToList(), _entidade.RestricoesUnicas);

        // A edição não traz os campos inativos (eles nem aparecem no formulário).
        var validado = _validador.Validar(comInativos, new JsonObject { ["nome"] = "Bia" }).Registro!;
        SincronizadorRegistro.Aplicar(registro, comInativos, validado, _calculadora.Calcular(comInativos, validado));

        var dados = JsonNode.Parse(registro.Dados)!;
        Assert.Equal(("Bia", 7), (dados["nome"]!.GetValue<string>(), dados["quantidade"]!.GetValue<int>()));
        Assert.Equal(7m, Valor(registro, _quantidade).ValorNumero);
        Assert.Equal(projeto, Assert.Single(registro.Referencias).RegistroDestinoId);
    }

    [Fact]
    public void Aplicar_RegistroDeOutraEntidade_Falha()
    {
        var registro = new Registro { EntidadeId = Guid.NewGuid() };

        Assert.Throws<InvalidOperationException>(() => Aplicar(registro, new JsonObject { ["nome"] = "Ana" }));
    }

    [Theory]
    [InlineData(TipoDado.Decimal, 35.5, "35.50")]
    [InlineData(TipoDado.Inteiro, 100, "100")]
    public void ConversorValorTipado_ParaTexto_UsaOFormatoDoJson(TipoDado tipo, double numero, string esperado)
    {
        // RN20: o rótulo tem o mesmo formato do valor no JSON.
        var texto = ConversorValorTipado.ParaTexto(tipo, 2, new RegistroValor { ValorNumero = (decimal)numero });

        Assert.Equal(esperado, texto);
    }

    [Fact]
    public void ConversorValorTipado_ParaTexto_DataHoraEmUtcEBooleano()
    {
        Assert.Equal("2026-10-21T17:30:00Z",
            ConversorValorTipado.ParaTexto(TipoDado.DataHora, null, new RegistroValor { ValorData = new DateTime(2026, 10, 21, 17, 30, 0) }));
        Assert.Equal("2026-10-21",
            ConversorValorTipado.ParaTexto(TipoDado.Data, null, new RegistroValor { ValorData = new DateTime(2026, 10, 21) }));
        Assert.Equal("false", ConversorValorTipado.ParaTexto(TipoDado.Booleano, null, new RegistroValor { ValorBooleano = false }));
        Assert.Null(ConversorValorTipado.ParaTexto(TipoDado.Referencia, null, new RegistroValor()));
    }

    private void Aplicar(Registro registro, JsonObject dados)
    {
        var entidade = _entidade;
        var validado = _validador.Validar(entidade, dados).Registro!;
        SincronizadorRegistro.Aplicar(registro, entidade, validado, _calculadora.Calcular(entidade, validado));
    }

    private static Campo Inativo(Campo c) => new()
    {
        Id = c.Id, EntidadeId = c.EntidadeId, Chave = c.Chave, Nome = c.Nome, TipoDado = c.TipoDado, TamanhoMaximo = c.TamanhoMaximo,
        Precisao = c.Precisao, Escala = c.Escala, EntidadeReferenciadaId = c.EntidadeReferenciadaId, AcaoExclusao = c.AcaoExclusao,
        Ordem = c.Ordem, Ativo = false,
    };

    private static RegistroValor Valor(Registro registro, Campo campo) => registro.Valores.Single(v => v.CampoId == campo.Id);
}
