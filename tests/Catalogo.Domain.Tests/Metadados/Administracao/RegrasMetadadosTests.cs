using Catalogo.Domain.Metadados;
using Catalogo.Domain.Metadados.Administracao;
using Catalogo.Domain.Validacao;

namespace Catalogo.Domain.Tests.Metadados.Administracao;

// Regras de configuração dos metadados, sem dados (RN51, RN53 a RN55).
public class RegrasMetadadosTests
{
    private readonly GrupoInventario _inv = new();

    // ---- Chaves (RN51, RN53) ----

    [Fact]
    public void ValidarNovoGrupo_VariosErros_VoltamTodosJuntos()
    {
        // RN05 e RN53
        var erros = new ErrosPorCampo();

        var grupo = RegrasMetadados.ValidarNovoGrupo(new DefinicaoGrupo("Inventário", "", new string('x', 501)), ["outro"], erros);

        Assert.Null(grupo);
        Assert.Equal(["chave", "descricao", "nome"], erros.ParaDicionario().Keys.Order());
    }

    [Theory]
    [InlineData("inventario", "Já existe")]       // repetida
    [InlineData("com espaço", "letras minúsculas")]
    [InlineData("", "Obrigatória")]
    public void ValidarNovoGrupo_ChaveInvalida_Erro(string chave, string trecho)
    {
        // RN53
        var erros = new ErrosPorCampo();

        RegrasMetadados.ValidarNovoGrupo(new DefinicaoGrupo(chave, "Inventário", null), ["inventario"], erros);

        Assert.Contains(trecho, Assert.Single(erros.ParaDicionario()["chave"]));
    }

    [Theory]
    [InlineData("versao")]
    [InlineData("equipeId")]
    [InlineData("avaliacao")]
    [InlineData("formato")]
    [InlineData("ordem")]   // rota de reordenação dos campos
    public void ValidarCampo_ChaveReservada_Erro(string chave)
    {
        // RN14 e RN53
        var erros = ValidarCampoNovo(new DefinicaoCampo(chave, "X", "TEXTO", TamanhoMaximo: 10));

        Assert.True(erros.ContainsKey("chave"), string.Join(", ", erros.Keys));
    }

    [Fact]
    public void ValidarCampo_ChaveRepetidaNaEntidade_Erro()
    {
        // RN53: única na entidade.
        var erros = ValidarCampoNovo(new DefinicaoCampo("nome", "Outro nome", "TEXTO", TamanhoMaximo: 10));

        Assert.Contains("Já existe", Assert.Single(erros["chave"]));
    }

    [Fact]
    public void ValidarCampo_MudarAChave_Erro()
    {
        // RN51: a chave é imutável; o nome pode mudar.
        var erros = ValidarAlteracao(_inv.Nome, new DefinicaoCampo("nome_novo", "Nome novo", null, TamanhoMaximo: 100));

        Assert.Equal(["chave"], erros.Keys.ToList());
        Assert.Contains("não muda", erros["chave"][0]);
    }

    [Fact]
    public void ValidarEntidadeEGrupo_MudarAChave_Erro()
    {
        // RN51
        var erros = new ErrosPorCampo();

        RegrasMetadados.ValidarEntidade(_inv.Grupo, _inv.Local, new DefinicaoEntidade("lugar", "Lugar", null, null, null, null), erros);
        RegrasMetadados.ValidarAlteracaoGrupo(_inv.Grupo, new DefinicaoGrupo("estoque", "Estoque", null), erros);

        Assert.Equal(2, erros.ParaDicionario()["chave"].Length);
    }

    [Fact]
    public void ValidarCampo_AlterarNomeSemMandarAChave_MantemAChave()
    {
        // RN51
        var erros = new ErrosPorCampo();

        var campo = RegrasMetadados.ValidarCampo(_inv.Grupo, _inv.Equipamento, _inv.Nome,
            new DefinicaoCampo(null, "Nome do equipamento", null, Obrigatorio: true, TamanhoMaximo: 100), erros);

        Assert.False(erros.PossuiErros);
        Assert.Equal(("nome", "Nome do equipamento", _inv.Nome.Id), (campo!.Chave, campo.Nome, campo.Id));
    }

    // ---- Tipos (RN53, CHECKs de Campo) ----

    public static IEnumerable<object[]> DefinicoesInvalidas() =>
    [
        [new DefinicaoCampo("c", "C", "TEXTO"), "tamanhoMaximo"],
        [new DefinicaoCampo("c", "C", "TEXTO", TamanhoMaximo: 0), "tamanhoMaximo"],
        [new DefinicaoCampo("c", "C", "TEXTO", TamanhoMaximo: 4001), "tamanhoMaximo"],
        [new DefinicaoCampo("c", "C", "INTEIRO", TamanhoMaximo: 10), "tamanhoMaximo"],
        [new DefinicaoCampo("c", "C", "DECIMAL", Precisao: 10), "escala"],
        [new DefinicaoCampo("c", "C", "DECIMAL", Precisao: 39, Escala: 2), "precisao"],
        [new DefinicaoCampo("c", "C", "DECIMAL", Precisao: 20, Escala: 11), "escala"],       // escala até 10
        [new DefinicaoCampo("c", "C", "DECIMAL", Precisao: 38, Escala: 9), "precisao"],      // parte inteira de 29 dígitos
        [new DefinicaoCampo("c", "C", "DECIMAL", Precisao: 4, Escala: 5), "escala"],         // escala maior que a precisão
        [new DefinicaoCampo("c", "C", "TEXTO", TamanhoMaximo: 10, Precisao: 5, Escala: 2), "precisao"],
        [new DefinicaoCampo("c", "C", "LISTA"), "opcoes"],
        [new DefinicaoCampo("c", "C", "LISTA", Opcoes: ["A", " "]), "opcoes[1]"],
        [new DefinicaoCampo("c", "C", "LISTA", Opcoes: ["A", "B", "A"]), "opcoes[2]"],
        [new DefinicaoCampo("c", "C", "BOOLEANO", Opcoes: ["A"]), "opcoes"],
        [new DefinicaoCampo("c", "C", "REFERENCIA", AcaoExclusao: "RESTRINGIR"), "entidadeReferenciada"],
        [new DefinicaoCampo("c", "C", "REFERENCIA", EntidadeReferenciada: "nao_existe", AcaoExclusao: "RESTRINGIR"), "entidadeReferenciada"],
        [new DefinicaoCampo("c", "C", "REFERENCIA", EntidadeReferenciada: "local"), "acaoExclusao"],
        [new DefinicaoCampo("c", "C", "REFERENCIA", EntidadeReferenciada: "local", AcaoExclusao: "APAGAR"), "acaoExclusao"],
        [new DefinicaoCampo("c", "C", "TEXTO", TamanhoMaximo: 10, EntidadeReferenciada: "local"), "entidadeReferenciada"],
        [new DefinicaoCampo("c", "C", "MOEDA"), "tipoDado"],
        [new DefinicaoCampo("c", "C", null), "tipoDado"],
    ];

    [Theory]
    [MemberData(nameof(DefinicoesInvalidas))]
    public void ValidarCampo_AtributosDoTipo_IguaisAosChecksDoBanco(DefinicaoCampo definicao, string chaveErro)
    {
        // RN53 (CK_Campo_Texto, CK_Campo_Decimal, CK_Campo_Lista, CK_Campo_Referencia)
        var erros = ValidarCampoNovo(definicao);

        Assert.True(erros.ContainsKey(chaveErro), $"Esperado erro em '{chaveErro}'. Erros: {string.Join(", ", erros.Keys)}");
    }

    [Theory]
    [InlineData(38, 10)]   // parte inteira de 28 dígitos: o limite
    [InlineData(1, 0)]
    [InlineData(10, 10)]
    public void ValidarCampo_DecimalNosLimites_Aceita(int precisao, int escala)
    {
        // RN53 e RN22
        Assert.Empty(ValidarCampoNovo(new DefinicaoCampo("c", "C", "DECIMAL", Precisao: precisao, Escala: escala)));
    }

    [Fact]
    public void ValidarCampo_VariosErros_VoltamTodosJuntos()
    {
        // RN05
        var erros = ValidarCampoNovo(new DefinicaoCampo("Chave Ruim", "", "LISTA", TamanhoMaximo: 5, Opcoes: ["A", "A"]));

        Assert.Equal(["chave", "nome", "opcoes[1]", "tamanhoMaximo"], erros.Keys.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void ValidarCampo_ListaNormalizaAsOpcoes()
    {
        var erros = new ErrosPorCampo();

        var campo = RegrasMetadados.ValidarCampo(_inv.Grupo, _inv.Equipamento, null,
            new DefinicaoCampo("cor", "Cor", "lista", Opcoes: [" Azul ", "Verde"]), erros);

        Assert.Equal(["Azul", "Verde"], campo!.Opcoes);
        Assert.Equal(TipoDado.Lista, campo.TipoDado);
        Assert.Equal(10, campo.Ordem);   // depois do último campo
    }

    // ---- Tipo imutável (RN66) ----

    [Fact]
    public void ValidarCampo_MudarOTipo_Erro()
    {
        var erros = ValidarAlteracao(_inv.DataAquisicao, new DefinicaoCampo(null, "Data", "TEXTO", TamanhoMaximo: 10));

        Assert.Contains("crie um campo novo", Assert.Single(erros["tipoDado"]));
    }

    [Fact]
    public void ValidarCampo_TextoParaTextoLongo_Aceita()
    {
        var local = _inv.Local;
        var erros = new ErrosPorCampo();

        var campo = RegrasMetadados.ValidarCampo(_inv.Grupo, local, _inv.LocalNome, new DefinicaoCampo(null, "Nome", "TEXTO_LONGO"), erros);

        Assert.False(erros.PossuiErros);
        Assert.Equal(TipoDado.TextoLongo, campo!.TipoDado);
    }

    [Fact]
    public void ValidarCampo_TextoParaTextoLongoNoCampoDeExibicaoEEmRestricao_Erro()
    {
        // RN54 e RN55: TEXTO_LONGO não é campo de exibição nem entra em restrição única.
        var erros = ValidarAlteracao(_inv.Nome, new DefinicaoCampo(null, "Nome", "TEXTO_LONGO", Obrigatorio: true));

        Assert.Equal(2, erros["tipoDado"].Length);
    }

    // ---- Mudança de tipo para e de URL (RN66, ADR 0042) ----

    [Theory]
    [InlineData("observacao")]   // TEXTO_LONGO → URL
    [InlineData("nome")]         // TEXTO → URL (mesmo sendo exibição e estando numa restrição: a URL pode ser as duas coisas)
    public void ValidarCampo_TextoOuTextoLongoParaUrl_Aceita(string chave)
    {
        var atual = _inv.Equipamento.ObterCampo(chave)!;
        var erros = new ErrosPorCampo();

        var campo = RegrasMetadados.ValidarCampo(_inv.Grupo, _inv.Equipamento, atual,
            new DefinicaoCampo(null, atual.Nome, "URL", Obrigatorio: atual.Obrigatorio), erros);

        Assert.False(erros.PossuiErros);
        Assert.Equal(TipoDado.Url, campo!.TipoDado);
        Assert.Null(campo.TamanhoMaximo);
    }

    [Fact]
    public void ValidarCampo_UrlParaTextoLongo_Aceita()
    {
        var url = new Campo { Id = Guid.NewGuid(), EntidadeId = GrupoInventario.LocalId, Chave = "site", Nome = "Site", TipoDado = TipoDado.Url, Ordem = 2 };
        var local = new Entidade { Id = GrupoInventario.LocalId, GrupoId = Guid.Empty, Chave = "local", Nome = "Local", Campos = [_inv.LocalNome, url] };
        var erros = new ErrosPorCampo();

        var campo = RegrasMetadados.ValidarCampo(_inv.Grupo, local, url, new DefinicaoCampo(null, "Site", "TEXTO_LONGO"), erros);

        Assert.False(erros.PossuiErros);
        Assert.Equal(TipoDado.TextoLongo, campo!.TipoDado);
    }

    [Theory]
    [InlineData("TEXTO")]
    [InlineData("INTEIRO")]
    public void ValidarCampo_UrlParaOutroTipo_Erro(string tipo)
    {
        var url = new Campo { Id = Guid.NewGuid(), EntidadeId = GrupoInventario.LocalId, Chave = "site", Nome = "Site", TipoDado = TipoDado.Url, Ordem = 2 };
        var local = new Entidade { Id = GrupoInventario.LocalId, GrupoId = Guid.Empty, Chave = "local", Nome = "Local", Campos = [_inv.LocalNome, url] };
        var erros = new ErrosPorCampo();

        RegrasMetadados.ValidarCampo(_inv.Grupo, local, url, new DefinicaoCampo(null, "Site", tipo, TamanhoMaximo: 100), erros);

        Assert.Contains("URL → TEXTO_LONGO", Assert.Single(erros.ParaDicionario()["tipoDado"]));
    }

    [Fact]
    public void ValidarCampo_UrlComTamanhoMaximo_Erro()
    {
        // Como o TEXTO_LONGO, a URL não tem atributos próprios (o limite de 2000 é do tipo).
        var erros = new ErrosPorCampo();

        RegrasMetadados.ValidarCampo(_inv.Grupo, _inv.Local, null, new DefinicaoCampo("site", "Site", "URL", TamanhoMaximo: 100), erros);

        Assert.True(erros.ParaDicionario().ContainsKey("tamanhoMaximo"));
    }

    // ---- Nome no plural da entidade ----

    [Theory]
    [InlineData("  Locais  ", "Locais")]
    [InlineData("   ", null)]
    [InlineData(null, null)]
    public void ValidarEntidade_NomePlural_OpcionalSemEspacosNasPontas(string? plural, string? esperado)
    {
        var erros = new ErrosPorCampo();

        var definida = RegrasMetadados.ValidarEntidade(_inv.Grupo, _inv.Local,
            new DefinicaoEntidade(null, "Local", null, null, null, null, plural), erros);

        Assert.False(erros.PossuiErros);
        Assert.Equal(esperado, definida!.NomePlural);
    }

    [Fact]
    public void ValidarEntidade_NomePluralAcimaDe150_Erro()
    {
        var erros = new ErrosPorCampo();

        RegrasMetadados.ValidarEntidade(_inv.Grupo, _inv.Local,
            new DefinicaoEntidade(null, "Local", null, null, null, null, new string('a', 151)), erros);

        Assert.Contains("150", Assert.Single(erros.ParaDicionario()["nomePlural"]));
    }

    // ---- Ícone da entidade (RN95) ----

    [Theory]
    [InlineData(" pi-chart-line ", "pi-chart-line")]
    [InlineData("pi-users", "pi-users")]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void ValidarEntidade_Icone_OpcionalSemEspacosNasPontas(string? icone, string? esperado)
    {
        var erros = new ErrosPorCampo();

        var definida = RegrasMetadados.ValidarEntidade(_inv.Grupo, _inv.Local,
            new DefinicaoEntidade(null, "Local", null, null, null, null, null, icone), erros);

        Assert.False(erros.PossuiErros);
        Assert.Equal(esperado, definida!.Icone);
    }

    [Theory]
    [InlineData("chart-line")]
    [InlineData("pi pi-chart-line")]
    [InlineData("pi-Chart")]
    [InlineData("pi-")]
    [InlineData("pi-chart--line")]
    [InlineData("pi-<script>")]
    public void ValidarEntidade_IconeForaDoFormato_Erro(string icone)
    {
        var erros = new ErrosPorCampo();

        RegrasMetadados.ValidarEntidade(_inv.Grupo, _inv.Local,
            new DefinicaoEntidade(null, "Local", null, null, null, null, null, icone), erros);

        Assert.Contains("PrimeIcons", Assert.Single(erros.ParaDicionario()["icone"]));
    }

    [Fact]
    public void ValidarEntidade_IconeAcimaDe50_Erro()
    {
        var erros = new ErrosPorCampo();

        RegrasMetadados.ValidarEntidade(_inv.Grupo, _inv.Local,
            new DefinicaoEntidade(null, "Local", null, null, null, null, null, "pi-" + new string('a', 48)), erros);

        Assert.True(erros.ParaDicionario().ContainsKey("icone"));
    }

    // ---- Referências (RN54) ----

    [Fact]
    public void ValidarCampo_EntidadeGlobalReferenciandoEntidadeDeEquipe_Erro()
    {
        var erros = new ErrosPorCampo();

        RegrasMetadados.ValidarCampo(_inv.Grupo, _inv.CategoriaEntidade, null,
            new DefinicaoCampo("local", "Local", "REFERENCIA", EntidadeReferenciada: "local", AcaoExclusao: "RESTRINGIR"), erros);

        Assert.Contains("global", Assert.Single(erros.ParaDicionario()["entidadeReferenciada"]));
    }

    [Fact]
    public void ValidarCampo_EntidadeDeEquipeReferenciandoGlobal_Aceita()
    {
        Assert.Empty(ValidarCampoNovo(
            new DefinicaoCampo("outra_categoria", "Categoria", "REFERENCIA", EntidadeReferenciada: "categoria", AcaoExclusao: "CASCATA")));
    }

    [Fact]
    public void ValidarCampo_MudarAEntidadeReferenciada_Erro_MasAAcaoDeExclusaoMuda()
    {
        // RN51: a entidade referenciada não muda; a ação de exclusão pode mudar.
        var erros = ValidarAlteracao(_inv.Categoria, new DefinicaoCampo(null, "Categoria", null, EntidadeReferenciada: "local", AcaoExclusao: "CASCATA"));
        var semMudarDestino = new ErrosPorCampo();
        var campo = RegrasMetadados.ValidarCampo(_inv.Grupo, _inv.Equipamento, _inv.Categoria,
            new DefinicaoCampo(null, "Categoria", null, AcaoExclusao: "CASCATA"), semMudarDestino);

        Assert.Equal(["entidadeReferenciada"], erros.Keys.ToList());
        Assert.Equal(AcaoExclusao.Cascata, campo!.AcaoExclusao);
        Assert.Equal(GrupoInventario.CategoriaId, campo.EntidadeReferenciadaId);
    }

    [Fact]
    public void ValidarEntidade_TornarGlobalComReferenciaParaEntidadeDeEquipe_Erro()
    {
        // RN54 e RN74: a entidade que vira global só referencia globais (equipamento → ... só globais, mas local → ...).
        var comReferencia = new Entidade
        {
            Id = GrupoInventario.LocalId, GrupoId = Guid.Empty, Chave = "local", Nome = "Local", EscopoEquipe = true,
            Campos = [_inv.LocalNome, GrupoInventario.Campo(GrupoInventario.LocalId, "equipamento", TipoDado.Referencia, 2, referencia: GrupoInventario.EquipamentoId)],
        };
        var grupo = new Grupo
        {
            Id = _inv.Grupo.Id, Chave = "inventario", Nome = "Inventário",
            Entidades = _inv.Grupo.Entidades.Select(e => e.Id == GrupoInventario.LocalId ? comReferencia : e).ToList(),
        };
        var erros = new ErrosPorCampo();

        RegrasMetadados.ValidarEntidade(grupo, comReferencia, new DefinicaoEntidade(null, "Local", null, null, false, null), erros);

        Assert.Contains("equipamento", Assert.Single(erros.ParaDicionario()["escopoEquipe"]));
    }

    [Fact]
    public void ValidarEntidade_TornarPorEquipeUmaEntidadeReferenciadaPorGlobal_Erro()
    {
        // RN54: categoria (global) aponta para fabricante; fabricante não pode virar por equipe.
        var erros = new ErrosPorCampo();

        RegrasMetadados.ValidarEntidade(_inv.Grupo, _inv.FabricanteEntidade, new DefinicaoEntidade(null, "Fabricante", null, null, true, null), erros);

        Assert.Equal(2, erros.ParaDicionario()["escopoEquipe"].Length);   // categoria.fabricante e modelo.fabricante
    }

    // ---- Campo de exibição (RN54) ----

    [Theory]
    [InlineData("categoria", "REFERENCIA")]
    [InlineData("observacao", "TEXTO_LONGO")]
    [InlineData("antigo", "inativo")]
    [InlineData("nao_existe", "não existe")]
    public void ValidarEntidade_CampoDeExibicaoInvalido_Erro(string campo, string trecho)
    {
        var erros = new ErrosPorCampo();

        RegrasMetadados.ValidarEntidade(_inv.Grupo, _inv.Equipamento, new DefinicaoEntidade(null, "Equipamento", null, null, null, campo), erros);

        Assert.Contains(trecho, Assert.Single(erros.ParaDicionario()["campoExibicao"]));
    }

    [Fact]
    public void ValidarEntidade_CampoDeExibicaoValido_Aceita()
    {
        var erros = new ErrosPorCampo();

        var entidade = RegrasMetadados.ValidarEntidade(_inv.Grupo, _inv.Equipamento,
            new DefinicaoEntidade(null, "Equipamento", null, 3, null, "data_aquisicao"), erros);

        Assert.False(erros.PossuiErros);
        Assert.Equal((_inv.DataAquisicao.Id, 3, true), (entidade!.CampoExibicaoId, entidade.Ordem, entidade.EscopoEquipe));
    }

    // ---- Restrição única (RN55) ----

    public static IEnumerable<object?[]> RestricoesInvalidas() =>
    [
        [new DefinicaoRestricao("uq_vazia", []), "campos"],
        [new DefinicaoRestricao("uq_seis", ["nome", "categoria", "fabricante", "data_aquisicao", "valor", "situacao"]), "campos"],
        [new DefinicaoRestricao("uq_longo", ["observacao"]), "campos[0]"],
        [new DefinicaoRestricao("uq_inativo", ["antigo"]), "campos[0]"],
        [new DefinicaoRestricao("uq_repetido", ["nome", "nome"]), "campos[1]"],
        [new DefinicaoRestricao("uq_desconhecido", ["cor"]), "campos[0]"],
        [new DefinicaoRestricao("uq_equipamento_nome", ["valor"]), "nome"],
        [new DefinicaoRestricao("UQ Ruim", ["valor"]), "nome"],
    ];

    [Theory]
    [MemberData(nameof(RestricoesInvalidas))]
    public void ValidarRestricao_Invalida_Erro(DefinicaoRestricao definicao, string chaveErro)
    {
        var erros = new ErrosPorCampo();

        RegrasMetadados.ValidarRestricao(_inv.Equipamento, definicao, erros);

        Assert.True(erros.ParaDicionario().ContainsKey(chaveErro), string.Join(", ", erros.ParaDicionario().Keys));
    }

    [Fact]
    public void ValidarRestricao_CamposNaOrdemDoHash()
    {
        // RN09: a ordem do hash é Campo.Ordem, qualquer que seja a ordem enviada.
        var erros = new ErrosPorCampo();

        var restricao = RegrasMetadados.ValidarRestricao(_inv.Equipamento, new DefinicaoRestricao("uq_composta", ["valor", "nome"]), erros);

        Assert.Equal(["nome", "valor"], restricao!.Campos.Select(c => c.Chave));
    }

    // ---- Dependência (RN55, semântica da RN08) ----

    [Fact]
    public void ValidarDependencia_SemVia_Aceita()
    {
        // modelo do equipamento: o modelo.fabricante tem de ser o fabricante do próprio equipamento.
        var erros = new ErrosPorCampo();

        var dependencia = RegrasMetadados.ValidarDependencia(_inv.Grupo, _inv.Equipamento, _inv.Modelo,
            new DefinicaoDependencia(null, "fabricante", "fabricante"), erros);

        Assert.False(erros.PossuiErros, string.Join(", ", erros.ParaDicionario().Keys));
        Assert.Equal((_inv.Fabricante.Id, _inv.ModeloFabricante.Id), (dependencia!.CampoOrigem.Id, dependencia.CampoAlvo.Id));
    }

    [Fact]
    public void ValidarDependencia_ComVia_LeAOrigemNaEntidadeDoVia()
    {
        // o modelo.fabricante tem de ser o fabricante da categoria do equipamento.
        var erros = new ErrosPorCampo();

        var dependencia = RegrasMetadados.ValidarDependencia(_inv.Grupo, _inv.Equipamento, _inv.Modelo,
            new DefinicaoDependencia("categoria", "fabricante", "fabricante"), erros);

        Assert.False(erros.PossuiErros);
        Assert.Equal(_inv.CategoriaFabricante.Id, dependencia!.CampoOrigem.Id);
    }

    public static IEnumerable<object?[]> DependenciasMalFormadas() =>
    [
        ["nome", new DefinicaoDependencia(null, "fabricante", "fabricante"), "campo"],                 // dependente não é REFERENCIA
        ["modelo", new DefinicaoDependencia("nome", "fabricante", "fabricante"), "campoVia"],         // via não é REFERENCIA
        ["modelo", new DefinicaoDependencia("modelo", "fabricante", "fabricante"), "campoVia"],       // via = o próprio campo
        ["modelo", new DefinicaoDependencia("categoria", "nao_existe", "fabricante"), "campoOrigem"], // origem fora da entidade do via
        ["modelo", new DefinicaoDependencia(null, "fabricante", "nao_existe"), "campoAlvo"],          // alvo fora da entidade referenciada
        ["modelo", new DefinicaoDependencia(null, "nome", "fabricante"), "campoAlvo"],                // origem não é REFERENCIA
        ["modelo", new DefinicaoDependencia(null, "categoria", "fabricante"), "campoAlvo"],           // origem e alvo apontam para entidades diferentes
        ["modelo", new DefinicaoDependencia(null, "antigo", "fabricante"), "campoOrigem"],            // origem inativa
        ["modelo", new DefinicaoDependencia(null, null, null), "campoOrigem"],
    ];

    [Theory]
    [MemberData(nameof(DependenciasMalFormadas))]
    public void ValidarDependencia_MalFormada_Erro(string campo, DefinicaoDependencia definicao, string chaveErro)
    {
        var erros = new ErrosPorCampo();

        RegrasMetadados.ValidarDependencia(_inv.Grupo, _inv.Equipamento, _inv.Equipamento.ObterCampo(campo)!, definicao, erros);

        Assert.True(erros.ParaDicionario().ContainsKey(chaveErro), string.Join(", ", erros.ParaDicionario().Keys));
    }

    [Fact]
    public void ValidarDependencia_Ciclo_Erro()
    {
        // O modelo já depende do fabricante; o fabricante passaria a depender do modelo → ciclo.
        var grupo = ComDependencia(_inv, new CampoDependencia { Campo = _inv.Modelo, CampoOrigem = _inv.Fabricante, CampoAlvo = _inv.ModeloFabricante });
        var equipamento = grupo.ObterEntidade(GrupoInventario.EquipamentoId)!;
        var erros = new ErrosPorCampo();

        RegrasMetadados.ValidarDependencia(grupo, equipamento, equipamento.ObterCampo("fabricante")!,
            new DefinicaoDependencia(null, "modelo", "modelo_padrao"), erros);

        Assert.Contains("ciclo", Assert.Single(erros.ParaDicionario()["campoOrigem"]));
    }

    // ---- Ordem e valor padrão ----

    [Fact]
    public void ValidarOrdem_FaltandoERepetido_Erro()
    {
        var erros = new ErrosPorCampo();

        RegrasMetadados.ValidarOrdem(_inv.Local, ["nome", "nome", "outro"], erros);

        Assert.Equal(["campos[1]", "campos[2]"], erros.ParaDicionario().Keys.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void ValidarOrdem_TodosOsCampos_DevolveNaNovaOrdem()
    {
        var erros = new ErrosPorCampo();
        var chaves = _inv.Equipamento.Campos.Select(c => (string?)c.Chave).Reverse().ToList();

        var ordem = RegrasMetadados.ValidarOrdem(_inv.Equipamento, chaves, erros);

        Assert.Equal(chaves, ordem!.Select(c => (string?)c.Chave));
    }

    [Theory]
    [InlineData("situacao", false, true, null)]                 // tornar obrigatório: aceita
    [InlineData("situacao", false, false, "Só vale")]           // continua opcional
    [InlineData("categoria", false, true, "REFERENCIA")]
    [InlineData("nome", true, true, "Só vale")]                 // já era obrigatório
    public void ValidarValorPadrao_SoAoTornarObrigatorio(string chave, bool jaObrigatorio, bool novoObrigatorio, string? trecho)
    {
        // RN60 e RN61
        var atual = _inv.Equipamento.ObterCampo(chave)!;
        Assert.Equal(jaObrigatorio, atual.Obrigatorio);
        var novo = new Campo
        {
            Id = atual.Id, EntidadeId = atual.EntidadeId, Chave = atual.Chave, Nome = atual.Nome, TipoDado = atual.TipoDado,
            Obrigatorio = novoObrigatorio, EntidadeReferenciadaId = atual.EntidadeReferenciadaId, Opcoes = atual.Opcoes,
        };
        var erros = new ErrosPorCampo();

        RegrasMetadados.ValidarValorPadrao(_inv.Equipamento, atual, novo, temValorPadrao: true, erros);

        if (trecho is null)
            Assert.False(erros.PossuiErros);
        else
            Assert.Contains(trecho, Assert.Single(erros.ParaDicionario()["valorPadrao"]));
    }

    private static Grupo ComDependencia(GrupoInventario inv, CampoDependencia dependencia)
    {
        var equipamento = inv.Equipamento;
        var comDependencia = new Entidade
        {
            Id = equipamento.Id, GrupoId = equipamento.GrupoId, Chave = equipamento.Chave, Nome = equipamento.Nome,
            EscopoEquipe = true, CampoExibicaoId = equipamento.CampoExibicaoId, Campos = equipamento.Campos,
            RestricoesUnicas = equipamento.RestricoesUnicas, Dependencias = [dependencia],
        };
        return new Grupo
        {
            Id = inv.Grupo.Id, Chave = inv.Grupo.Chave, Nome = inv.Grupo.Nome,
            Entidades = inv.Grupo.Entidades.Select(e => e.Id == equipamento.Id ? comDependencia : e).ToList(),
        };
    }

    private Dictionary<string, string[]> ValidarCampoNovo(DefinicaoCampo definicao)
    {
        var erros = new ErrosPorCampo();
        RegrasMetadados.ValidarCampo(_inv.Grupo, _inv.Equipamento, null, definicao, erros);
        return erros.ParaDicionario().ToDictionary(e => e.Key, e => e.Value);
    }

    private Dictionary<string, string[]> ValidarAlteracao(Campo atual, DefinicaoCampo definicao)
    {
        var erros = new ErrosPorCampo();
        RegrasMetadados.ValidarCampo(_inv.Grupo, _inv.Equipamento, atual, definicao, erros);
        return erros.ParaDicionario().ToDictionary(e => e.Key, e => e.Value);
    }
}
