using System.Text.Json.Nodes;
using Catalogo.Domain.Metadados;
using Catalogo.Domain.Metadados.Administracao;

namespace Catalogo.Domain.Tests.Metadados.Administracao;

// Para cada alteração da tabela de RN58 a RN77, a verificação contra os dados (e a escrita em massa) certa.
public class PlanejadorAlteracoesTests
{
    private readonly GrupoInventario _inv = new();

    // ---- Campo novo (RN59, RN60) ----

    [Fact]
    public void NovoCampo_Opcional_SempreSemVerificacao()
    {
        // RN59
        var plano = PlanejadorAlteracoes.NovoCampo(_inv.Equipamento, Copia(_inv.DataAquisicao, obrigatorio: false), null);

        Assert.Same(PlanoAlteracao.Vazio, plano);
    }

    [Fact]
    public void NovoCampo_ObrigatorioSemValorPadrao_BloqueiaSeHouverRegistros()
    {
        // RN60
        var plano = PlanejadorAlteracoes.NovoCampo(_inv.Equipamento, Copia(_inv.DataAquisicao, obrigatorio: true), null);

        var verificacao = Assert.IsType<RegistrosSemValor>(Assert.Single(plano.Verificacoes));
        Assert.True(verificacao.Bloqueia);
        Assert.Equal(PlanejadorAlteracoes.CampoObrigatorioSemValor, verificacao.Tipo);
        Assert.Empty(plano.Escritas);
    }

    [Fact]
    public void NovoCampo_ObrigatorioComValorPadrao_PreencheTodosOsRegistros()
    {
        // RN60
        var plano = PlanejadorAlteracoes.NovoCampo(_inv.Equipamento, Copia(_inv.DataAquisicao, obrigatorio: true), JsonValue.Create("2026-01-01"));

        Assert.False(Assert.IsType<RegistrosSemValor>(Assert.Single(plano.Verificacoes)).Bloqueia);
        var escrita = Assert.IsType<PreencherValorPadrao>(Assert.Single(plano.Escritas));
        Assert.Equal(("data_aquisicao", false), (escrita.ChaveCampo, escrita.ApenasVazios));
    }

    // ---- Obrigatoriedade (RN61, RN62) ----

    [Fact]
    public void TornarObrigatorio_SemValorPadrao_VerificaRegistrosSemValor()
    {
        // RN61
        var plano = Alterar(_inv.DataAquisicao, Copia(_inv.DataAquisicao, obrigatorio: true));

        var verificacao = Assert.IsType<RegistrosSemValor>(Assert.Single(plano.Verificacoes));
        Assert.True(verificacao.Bloqueia);
    }

    [Fact]
    public void TornarObrigatorio_ComValorPadrao_PreencheSoOsVazios()
    {
        // RN61
        var plano = PlanejadorAlteracoes.AlteracaoCampo(_inv.Equipamento, _inv.DataAquisicao, Copia(_inv.DataAquisicao, obrigatorio: true),
            JsonValue.Create("2026-01-01"));

        Assert.True(Assert.IsType<PreencherValorPadrao>(Assert.Single(plano.Escritas)).ApenasVazios);
        Assert.DoesNotContain(plano.Verificacoes, v => v.Bloqueia);
    }

    [Fact]
    public void TornarOpcional_SempreSemVerificacao()
    {
        // RN62
        var plano = Alterar(_inv.Nome, Copia(_inv.Nome, obrigatorio: false));

        Assert.Empty(plano.Verificacoes);
        Assert.Empty(plano.Escritas);
    }

    // ---- Tamanho, precisão e escala (RN63 a RN65) ----

    [Fact]
    public void DiminuirTamanho_VerificaTextosAcimaDoNovoTamanho()
    {
        // RN63
        var plano = Alterar(_inv.Nome, Copia(_inv.Nome, tamanho: 20));

        Assert.Equal(20, Assert.IsType<TextosAcimaDoTamanho>(Assert.Single(plano.Verificacoes)).Tamanho);
    }

    [Theory]
    [InlineData(200, null, null)]   // aumentar tamanho
    [InlineData(null, 14, 2)]       // aumentar a precisão
    [InlineData(null, 14, 3)]       // aumentar precisão e escala mantendo a parte inteira (10 → 11)
    public void Aumentar_SemVerificacao(int? tamanho, int? precisao, int? escala)
    {
        // RN64
        var plano = tamanho is not null
            ? Alterar(_inv.Nome, Copia(_inv.Nome, tamanho: tamanho))
            : Alterar(_inv.Valor, Copia(_inv.Valor, precisao: (byte)precisao!, escala: (byte)escala!));

        Assert.Empty(plano.Verificacoes);
    }

    [Theory]
    [InlineData(12, 1)]   // diminuir a escala
    [InlineData(10, 2)]   // diminuir a precisão (parte inteira 10 → 8)
    [InlineData(12, 4)]   // aumentar SÓ a escala: a parte inteira cai de 10 para 8
    public void DiminuirPrecisaoEscalaOuParteInteira_VerificaNumerosQueNaoCabem(int precisao, int escala)
    {
        // RN65
        var plano = Alterar(_inv.Valor, Copia(_inv.Valor, precisao: (byte)precisao, escala: (byte)escala));

        var verificacao = Assert.IsType<NumerosForaDaPrecisao>(Assert.Single(plano.Verificacoes));
        Assert.Equal((precisao, escala), (verificacao.Precisao, verificacao.Escala));
    }

    [Fact]
    public void MudarAEscalaDeCampoEmRestricao_RecalculaAsChavesUnicas()
    {
        // RN09: a escala entra no hash ("12.5" → "12.50").
        var valor = _inv.Valor;
        var restricao = new RestricaoUnica { Id = Guid.NewGuid(), EntidadeId = valor.EntidadeId, Nome = "uq_valor", Campos = [valor] };
        var entidade = ComRestricoes(_inv.Equipamento, [restricao]);

        var plano = PlanejadorAlteracoes.AlteracaoCampo(entidade, valor, Copia(valor, precisao: 14, escala: 3), null);

        Assert.Equal(["uq_valor"], Assert.IsType<RecalcularChavesUnicas>(Assert.Single(plano.Escritas)).Restricoes);
    }

    // ---- LISTA (RN67, RN68) ----

    [Fact]
    public void ListaAdicionarOpcao_SemVerificacao()
    {
        // RN67
        Assert.Empty(Alterar(_inv.Situacao, Copia(_inv.Situacao, opcoes: ["EM_USO", "BAIXADO", "EMPRESTADO"])).Verificacoes);
    }

    [Fact]
    public void ListaRemoverOpcao_VerificaRegistrosQueAUsam()
    {
        // RN68
        var plano = Alterar(_inv.Situacao, Copia(_inv.Situacao, opcoes: ["EM_USO"]));

        Assert.Equal(["BAIXADO"], Assert.IsType<ValoresComOpcoes>(Assert.Single(plano.Verificacoes)).Opcoes);
    }

    // ---- Ativação e exclusão de campo (RN69, RN70) ----

    [Theory]
    [InlineData("nome", "campo de exibição")]
    [InlineData("nome", "restrição única 'uq_equipamento_nome'")]
    public void DesativarCampoEmUso_Bloqueia(string chave, string uso)
    {
        // RN69
        var plano = PlanejadorAlteracoes.AtivacaoCampo(_inv.Grupo, _inv.Equipamento, _inv.Equipamento.ObterCampo(chave)!, ativar: false);

        var impacto = Assert.Single(plano.Usos);
        Assert.True(impacto.Impede);
        Assert.Contains(impacto.Usos!, u => u.Contains(uso));
    }

    [Fact]
    public void DesativarCampoEmDependencia_Bloqueia()
    {
        // RN69: o fabricante é a origem da dependência do modelo.
        var dependencia = new CampoDependencia { Campo = _inv.Modelo, CampoOrigem = _inv.Fabricante, CampoAlvo = _inv.ModeloFabricante };
        var inv = ComDependencia(dependencia);

        var plano = PlanejadorAlteracoes.AtivacaoCampo(inv, inv.ObterEntidade(GrupoInventario.EquipamentoId)!, _inv.Fabricante, ativar: false);

        Assert.Contains(Assert.Single(plano.Usos).Usos!, u => u.Contains("como origem"));
    }

    [Fact]
    public void DesativarCampoLivre_Permitido_EReativarObrigatorioVerificaVazios()
    {
        // RN69
        Assert.Same(PlanoAlteracao.Vazio, PlanejadorAlteracoes.AtivacaoCampo(_inv.Grupo, _inv.Equipamento, _inv.DataAquisicao, ativar: false));
        var reativar = PlanejadorAlteracoes.AtivacaoCampo(_inv.Grupo, _inv.Equipamento, Copia(_inv.DataAquisicao, obrigatorio: true), ativar: true);
        Assert.IsType<RegistrosSemValor>(Assert.Single(reativar.Verificacoes));
    }

    [Fact]
    public void ExcluirCampo_VerificaValoresEUsos()
    {
        // RN70
        var livre = PlanejadorAlteracoes.ExcluirCampo(_inv.Grupo, _inv.Equipamento, _inv.DataAquisicao);
        var emUso = PlanejadorAlteracoes.ExcluirCampo(_inv.Grupo, _inv.Equipamento, _inv.Nome);

        Assert.IsType<RegistrosComValor>(Assert.Single(livre.Verificacoes));
        Assert.Empty(livre.Usos);
        Assert.Equal(2, Assert.Single(emUso.Usos).Usos!.Count);   // exibição e restrição
    }

    // ---- Restrição, dependência e ordem (RN71 a RN73) ----

    [Fact]
    public void NovaRestricao_VerificaDuplicatasEGeraAsChaves()
    {
        // RN71
        var restricao = new RestricaoUnica { Id = Guid.NewGuid(), EntidadeId = _inv.Valor.EntidadeId, Nome = "uq_valor", Campos = [_inv.Valor] };

        var plano = PlanejadorAlteracoes.NovaRestricao(ComRestricoes(_inv.Equipamento, [_inv.UqNome, restricao]), restricao);

        Assert.Contains(plano.Verificacoes, v => v is DuplicatasNaRestricao { Bloqueia: true });
        Assert.Equal(["uq_valor"], Assert.IsType<RecalcularChavesUnicas>(Assert.Single(plano.Escritas)).Restricoes);
    }

    [Fact]
    public void NovaDependencia_VerificaViolacoes()
    {
        // RN73
        var dependencia = new CampoDependencia { Campo = _inv.Modelo, CampoOrigem = _inv.Fabricante, CampoAlvo = _inv.ModeloFabricante };

        Assert.Same(dependencia, Assert.IsType<ViolacoesDaDependencia>(Assert.Single(PlanejadorAlteracoes.NovaDependencia(dependencia).Verificacoes)).Dependencia);
    }

    [Fact]
    public void ReordenarCampos_SoRecalculaRestricoesCompostasCujaOrdemMudou()
    {
        // RN09: a ordem do hash é Campo.Ordem.
        var composta = new RestricaoUnica { Id = Guid.NewGuid(), EntidadeId = _inv.Nome.EntidadeId, Nome = "uq_composta", Campos = [_inv.Nome, _inv.Valor] };
        var entidade = ComRestricoes(_inv.Equipamento, [_inv.UqNome, composta]);
        var invertida = entidade.Campos.Reverse().ToList();

        var plano = PlanejadorAlteracoes.ReordenarCampos(entidade, invertida);
        var mesmaOrdem = PlanejadorAlteracoes.ReordenarCampos(entidade, entidade.Campos);

        Assert.Equal(["uq_composta"], Assert.IsType<RecalcularChavesUnicas>(Assert.Single(plano.Escritas)).Restricoes);
        Assert.Empty(mesmaOrdem.Escritas);
    }

    // ---- Entidade e grupo (RN74 a RN77) ----

    [Fact]
    public void MudarEscopo_VerificaRegistrosEConfiguracoes()
    {
        // RN74
        var plano = PlanejadorAlteracoes.MudarEscopo(_inv.Local);

        Assert.Equal(2, plano.Verificacoes.Count);
        Assert.Contains(plano.Verificacoes, v => v is RegistrosDaEntidade);
        Assert.Contains(plano.Verificacoes, v => v is ConfiguracoesDaEntidade);
    }

    [Fact]
    public void ExcluirEntidade_VerificaRegistrosHistoricoConfiguracoesEReferencias()
    {
        // RN76: categoria é referenciada por equipamento.categoria.
        var plano = PlanejadorAlteracoes.ExcluirEntidade(_inv.Grupo, _inv.CategoriaEntidade);

        Assert.Equal(3, plano.Verificacoes.Count);
        Assert.Contains(plano.Verificacoes, v => v is HistoricoDaEntidade);
        Assert.Contains("equipamento.categoria", Assert.Single(Assert.Single(plano.Usos).Usos!));
    }

    [Fact]
    public void ExcluirGrupo_ComEntidades_Bloqueia()
    {
        // RN77
        var plano = PlanejadorAlteracoes.ExcluirGrupo(_inv.Grupo);

        Assert.Equal(5, Assert.Single(plano.Usos).Usos!.Count);
    }

    // ---- Mudança de tipo para e de URL (RN66, ADR 0042) ----

    [Fact]
    public void MudarTipo_TextoLongoParaUrl_VerificaOsValoresQueNaoSaoUrl()
    {
        var plano = Alterar(_inv.Observacao, Copia(_inv.Observacao, tipo: TipoDado.Url));

        var verificacao = Assert.IsType<ValoresQueNaoSaoUrl>(Assert.Single(plano.Verificacoes));
        Assert.Equal(PlanejadorAlteracoes.ValorNaoEUrl, verificacao.Tipo);
        Assert.Same(_inv.Observacao, verificacao.Campo);
    }

    [Fact]
    public void MudarTipo_TextoDaRestricaoParaUrl_VerificaERecalculaAsChaves()
    {
        // A normalização da chave única muda (o texto ignora maiúsculas; a URL só no esquema e no host).
        var plano = Alterar(_inv.Nome, Copia(_inv.Nome, tipo: TipoDado.Url));

        Assert.IsType<ValoresQueNaoSaoUrl>(Assert.Single(plano.Verificacoes));
        Assert.Contains(plano.Escritas, e => e is RecalcularChavesUnicas);
    }

    [Fact]
    public void MudarTipo_UrlParaTextoLongo_SemVerificacao()
    {
        var url = Copia(_inv.Observacao, tipo: TipoDado.Url);

        var plano = Alterar(url, Copia(url, tipo: TipoDado.TextoLongo));

        Assert.Empty(plano.Verificacoes);
    }

    private PlanoAlteracao Alterar(Campo atual, Campo novo) => PlanejadorAlteracoes.AlteracaoCampo(_inv.Equipamento, atual, novo, null);

    private Grupo ComDependencia(CampoDependencia dependencia)
    {
        var equipamento = _inv.Equipamento;
        var nova = new Entidade
        {
            Id = equipamento.Id, GrupoId = equipamento.GrupoId, Chave = equipamento.Chave, Nome = equipamento.Nome, EscopoEquipe = true,
            CampoExibicaoId = equipamento.CampoExibicaoId, Campos = equipamento.Campos, RestricoesUnicas = equipamento.RestricoesUnicas,
            Dependencias = [dependencia],
        };
        return new Grupo
        {
            Id = _inv.Grupo.Id, Chave = "inventario", Nome = "Inventário",
            Entidades = _inv.Grupo.Entidades.Select(e => e.Id == equipamento.Id ? nova : e).ToList(),
        };
    }

    private static Entidade ComRestricoes(Entidade e, IReadOnlyList<RestricaoUnica> restricoes) => new()
    {
        Id = e.Id, GrupoId = e.GrupoId, Chave = e.Chave, Nome = e.Nome, EscopoEquipe = e.EscopoEquipe, CampoExibicaoId = e.CampoExibicaoId,
        Campos = e.Campos, RestricoesUnicas = restricoes, Dependencias = e.Dependencias,
    };

    private static Campo Copia(Campo c, bool? obrigatorio = null, int? tamanho = null, byte? precisao = null, byte? escala = null,
        IReadOnlyList<string>? opcoes = null, TipoDado? tipo = null) => new()
    {
        Id = c.Id, EntidadeId = c.EntidadeId, Chave = c.Chave, Nome = c.Nome, TipoDado = tipo ?? c.TipoDado,
        Obrigatorio = obrigatorio ?? c.Obrigatorio, TamanhoMaximo = tamanho ?? c.TamanhoMaximo,
        Precisao = precisao ?? c.Precisao, Escala = escala ?? c.Escala, Opcoes = opcoes ?? c.Opcoes,
        EntidadeReferenciadaId = c.EntidadeReferenciadaId, AcaoExclusao = c.AcaoExclusao, Ordem = c.Ordem, Ativo = c.Ativo,
    };
}
