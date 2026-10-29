using System.Text.Json.Nodes;
using Catalogo.Application.Abstracoes;
using Catalogo.Application.Dtos;
using Catalogo.Domain.Acesso;
using Catalogo.Domain.Aprovacao;
using Catalogo.Domain.Historico;
using Catalogo.Domain.Consultas;
using Catalogo.Domain.Exclusao;
using Catalogo.Domain.Metadados;
using Catalogo.Domain.Registros;

namespace Catalogo.Application.Tests.Dubles;

// Dublês ("fakes") escritos à mão: simples de ler e sem biblioteca de mock.

internal sealed class MetadadosProviderFalso(Grupo grupo) : IMetadadosProvider
{
    public Task<Grupo?> ObterGrupoAsync(string grupoChave, CancellationToken cancellationToken) =>
        Task.FromResult(string.Equals(grupoChave, grupo.Chave, StringComparison.OrdinalIgnoreCase) ? grupo : null);

    public Task<IReadOnlyList<ResumoGrupo>> ListarGruposAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ResumoGrupo>>([new ResumoGrupo(grupo.Chave, grupo.Nome, grupo.Descricao, grupo.Ativo, grupo.EquipesHabilitadas)]);

    public void InvalidarCache(string grupoChave)
    {
    }
}

/// <summary>
/// Guarda os registros numa lista. Inclusões só viram "gravadas" quando a unidade de trabalho salva.
/// Usa os metadados do grupo para rótulos e ações de exclusão, como o banco faria.
/// </summary>
internal sealed class RegistroRepositoryFalso(Grupo grupo) : IRegistroRepository
{
    public List<Registro> Gravados { get; } = [];
    public List<Registro> Pendentes { get; } = [];

    /// <summary>A prévia da importação descarta o que foi marcado para gravar (IUnidadeDeTrabalho.DescartarAlteracoes).</summary>
    public void DescartarPendentes() => Pendentes.Clear();

    /// <summary>Versão que o caso de uso disse ter lido, por registro (RN12).</summary>
    public Dictionary<Guid, byte[]> VersoesEsperadas { get; } = [];

    /// <summary>Ordem em que os registros foram excluídos (RN16).</summary>
    public List<Guid> Excluidos { get; } = [];

    public Task<Registro?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Gravados.FirstOrDefault(r => r.Id == id));

    public Task<IReadOnlyList<Registro>> ObterPorIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Registro>>(Gravados.Where(r => ids.Contains(r.Id)).ToList());

    public Task<Registro?> ObterParaAlteracaoAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Gravados.FirstOrDefault(r => r.Id == id));

    public void DefinirVersaoEsperada(Registro registro, byte[] versao) => VersoesEsperadas[registro.Id] = versao;

    public Task<bool> ExisteChaveUnicaAsync(
        Guid restricaoUnicaId, byte[] valorHash, Guid? ignorarRegistroId, CancellationToken cancellationToken) =>
        Task.FromResult(Gravados
            .Where(r => r.Id != ignorarRegistroId)
            .SelectMany(r => r.ChavesUnicas)
            .Any(c => c.RestricaoUnicaId == restricaoUnicaId && c.ValorHash.SequenceEqual(valorHash)));

    // Como o EF, gera o Id ao adicionar (o histórico usa o Id antes do SaveChanges).
    public void Adicionar(Registro registro)
    {
        if (registro.Id == Guid.Empty)
            registro.Id = Guid.NewGuid();
        Pendentes.Add(registro);
    }

    public Task<IReadOnlyDictionary<Guid, SituacaoAprovacao>> ObterSituacoesAsync(
        IReadOnlyCollection<Guid> registroIds, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyDictionary<Guid, SituacaoAprovacao>>(
            Gravados.Where(r => registroIds.Contains(r.Id)).ToDictionary(r => r.Id, r => r.SituacaoAprovacao));

    public Task<IReadOnlyDictionary<Guid, string?>> ObterRotulosAsync(
        IReadOnlyCollection<Guid> registroIds, CancellationToken cancellationToken)
    {
        var rotulos = new Dictionary<Guid, string?>();
        foreach (var registro in Gravados.Where(r => registroIds.Contains(r.Id)))
        {
            var entidade = grupo.ObterEntidade(registro.EntidadeId)!;
            var exibicao = entidade.CampoExibicaoId is Guid id ? entidade.ObterCampo(id) : null;
            var valor = exibicao is null ? null : (JsonNode.Parse(registro.Dados) as JsonObject)?[exibicao.Chave];
            if (valor is not null)
                rotulos[registro.Id] = valor is JsonValue v && v.TryGetValue<string>(out var texto) ? texto : valor.ToJsonString();
        }

        return Task.FromResult<IReadOnlyDictionary<Guid, string?>>(rotulos);
    }

    /// <summary>Versão simplificada: aplica entidade, escopo de equipe (RN26) e de situação (RN37), na ordem de criação.</summary>
    /// <summary>Quantas vezes a listagem foi consultada (a exportação lê em lotes).</summary>
    public int ConsultasDeListagem { get; private set; }

    public Task<PaginaDeIds> ListarIdsAsync(Guid entidadeId, ConsultaRegistros consulta, CancellationToken cancellationToken)
    {
        ConsultasDeListagem++;
        if (consulta.Condicoes.Count > 0)
            throw new NotSupportedException("Filtros são LINQ sobre o banco: cobertos pelos testes de integração.");

        var ids = Gravados
            .Where(r => r.EntidadeId == entidadeId)
            .Where(r => consulta.EquipesPermitidas is null || (r.EquipeId is Guid e && consulta.EquipesPermitidas.Contains(e)))
            .Where(r => consulta.FiltroEquipe is null || r.EquipeId == consulta.FiltroEquipe)
            .Where(r => consulta.NaoAprovadosSomenteDoAutor is null
                        || r.SituacaoAprovacao == SituacaoAprovacao.Aprovado
                        || r.CriadoPorId == consulta.NaoAprovadosSomenteDoAutor)
            .Where(r => consulta.FiltroSituacao is null || r.SituacaoAprovacao == consulta.FiltroSituacao)
            .Select(r => r.Id)
            .ToList();
        return Task.FromResult(new PaginaDeIds(ids.Skip(consulta.Paginacao.Pular).Take(consulta.Paginacao.Tamanho).ToList(), ids.Count));
    }

    public Task<int> ContarPendentesAsync(Guid entidadeId, Guid equipeId, CancellationToken cancellationToken) =>
        Task.FromResult(Gravados.Count(r => r.EntidadeId == entidadeId && r.EquipeId == equipeId && r.SituacaoAprovacao == SituacaoAprovacao.Pendente));

    public Task<PaginaDe<PendenteResumo>> ListarPendentesAsync(FiltroPendentes filtro, Paginacao paginacao, CancellationToken cancellationToken)
    {
        var itens = Gravados
            .Where(r => r.SituacaoAprovacao == SituacaoAprovacao.Pendente)
            .Where(_ => filtro.IncluirListagensDesativadas || grupo.Ativo) // RN77
            .Where(r => r.EquipeId is Guid h && grupo.EquipesHabilitadas.Contains(h)) // RN91, RN92
            .Where(r => filtro.EquipesPermitidas is null || (r.EquipeId is Guid e && filtro.EquipesPermitidas.Contains(e)))
            .Where(r => filtro.EquipeId is null || r.EquipeId == filtro.EquipeId)
            .OrderBy(r => r.CriadoEm)
            .Select(r =>
            {
                var entidade = grupo.ObterEntidade(r.EntidadeId)!;
                return new PendenteResumo(r.Id, grupo.Chave, entidade.Chave, entidade.Nome, r.EquipeId, r.CriadoPorId, r.CriadoEm);
            })
            .Where(p => filtro.EntidadeChave is null || p.EntidadeChave == filtro.EntidadeChave)
            .ToList();
        return Task.FromResult(new PaginaDe<PendenteResumo>(itens, itens.Count));
    }

    public Task<IReadOnlyList<ReferenciaEntrante>> ObterReferenciasQueApontamParaAsync(
        IReadOnlyCollection<Guid> registroIds, CancellationToken cancellationToken)
    {
        var campos = grupo.Entidades.SelectMany(e => e.Campos).ToDictionary(c => c.Id);
        var referencias = Gravados
            .SelectMany(r => r.Referencias)
            .Where(r => registroIds.Contains(r.RegistroDestinoId))
            .Select(r => new ReferenciaEntrante(r.RegistroOrigemId, r.EntidadeOrigemId, r.CampoId, r.RegistroDestinoId,
                campos[r.CampoId].AcaoExclusao!.Value))
            .ToList();

        return Task.FromResult<IReadOnlyList<ReferenciaEntrante>>(referencias);
    }

    /// <summary>As contagens pedidas na última chamada (RN94): para conferir o escopo que o caso de uso montou.</summary>
    public IReadOnlyList<ContagemPorCampo> ContagensPedidas { get; private set; } = [];

    /// <summary>Como o banco faria: referências para o registro, só de origens dentro do escopo de cada contagem.</summary>
    public Task<IReadOnlyDictionary<Guid, int>> ContarQueApontamParaAsync(
        Guid registroId, IReadOnlyList<ContagemPorCampo> contagens, CancellationToken cancellationToken)
    {
        ContagensPedidas = contagens;
        var resultado = new Dictionary<Guid, int>();
        foreach (var contagem in contagens)
        {
            var escopo = contagem.Escopo;
            var quantidade = Gravados
                .Where(r => r.EntidadeId == contagem.EntidadeOrigemId)
                .Where(r => escopo.EquipesPermitidas is null || (r.EquipeId is Guid e && escopo.EquipesPermitidas.Contains(e)))
                .Where(r => escopo.NaoAprovadosSomenteDoAutor is not Guid autor
                            || r.SituacaoAprovacao == Domain.Aprovacao.SituacaoAprovacao.Aprovado || r.CriadoPorId == autor)
                .Count(r => r.Referencias.Any(rr => rr.CampoId == contagem.CampoId && rr.RegistroDestinoId == registroId));
            if (quantidade > 0)
                resultado[contagem.CampoId] = quantidade;
        }

        return Task.FromResult<IReadOnlyDictionary<Guid, int>>(resultado);
    }

    /// <summary>Os escopos pedidos na última busca de recentes (RN96): para conferir o que o caso de uso montou.</summary>
    public IReadOnlyList<EscopoDaEntidade> EscoposRecentesPedidos { get; private set; } = [];

    /// <summary>O que o "banco" devolve na busca de recentes (o teste preenche; a consulta de verdade é testada na integração).</summary>
    public List<ItemRecente> Recentes { get; } = [];

    public Task<IReadOnlyList<ItemRecente>> ListarRecentesDoUsuarioAsync(
        Guid usuarioId, IReadOnlyList<EscopoDaEntidade> escopos, int limite, CancellationToken cancellationToken)
    {
        EscoposRecentesPedidos = escopos;
        var entidades = escopos.Select(e => e.EntidadeId).ToHashSet();
        return Task.FromResult<IReadOnlyList<ItemRecente>>(
            Recentes.Where(r => entidades.Contains(r.EntidadeId)).OrderByDescending(r => r.OcorridoEm).Take(limite).ToList());
    }

    public Task ExcluirAsync(PlanoExclusao plano, CancellationToken cancellationToken)
    {
        foreach (var id in plano.OrdemExclusao)
        {
            Excluidos.Add(id);
            Gravados.RemoveAll(r => r.Id == id);
        }

        return Task.CompletedTask;
    }
}

/// <summary>
/// Simula o SaveChanges: gera o Id dos novos (como o EF faz), preenche as FKs dos filhos e
/// muda a Versao de quem foi alterado (como o rowversion do SQL Server).
/// </summary>
internal sealed class UnidadeDeTrabalhoFalsa(RegistroRepositoryFalso repositorio, AvaliacoesEmMemoria? avaliacoes = null) : IUnidadeDeTrabalho
{
    private long _proximaVersao = 1;

    /// <summary>Se preenchida, é lançada no lugar de salvar (para simular erros do banco).</summary>
    public Exception? ErroAoSalvar { get; set; }

    /// <summary>Quantas vezes a prévia descartou o que estava marcado para gravar.</summary>
    public int Descartes { get; private set; }

    public void DescartarAlteracoes()
    {
        Descartes++;
        repositorio.DescartarPendentes();
    }

    public int VezesQueSalvou { get; private set; }

    /// <summary>Sem banco não há transação: executa a operação (a transação Serializable é coberta pelos testes de integração).</summary>
    public async Task<T> ExecutarSerializavelAsync<T>(
        Func<CancellationToken, Task<(T Resultado, bool Confirmar)>> operacao, CancellationToken cancellationToken) =>
        (await operacao(cancellationToken)).Resultado;

    public Task SalvarAlteracoesAsync(CancellationToken cancellationToken)
    {
        if (ErroAoSalvar is not null)
            throw ErroAoSalvar;

        foreach (var registro in repositorio.Pendentes)
        {
            registro.CriadoEm = DateTime.UtcNow;
            repositorio.Gravados.Add(registro);
        }

        var alterados = repositorio.Pendentes.Concat(repositorio.Gravados.Where(r => repositorio.VersoesEsperadas.ContainsKey(r.Id)));
        foreach (var registro in alterados.Distinct())
        {
            registro.Versao = BitConverter.GetBytes(_proximaVersao++);
            foreach (var referencia in registro.Referencias)
                referencia.RegistroOrigemId = registro.Id;
            foreach (var chave in registro.ChavesUnicas)
                chave.RegistroId = registro.Id;
            foreach (var valor in registro.Valores)
                valor.RegistroId = registro.Id;
        }

        repositorio.Pendentes.Clear();
        repositorio.VersoesEsperadas.Clear();
        avaliacoes?.AoSalvar();
        VezesQueSalvou++;
        return Task.CompletedTask;
    }
}

/// <summary>Usuário da "requisição", trocável pelo teste.</summary>
internal sealed class UsuarioAtualFalso : IUsuarioAtual
{
    public UsuarioAtual Usuario { get; set; } = Criar(Perfil.Administrador);

    public Task<UsuarioAtual> ObterAsync(CancellationToken cancellationToken) => Task.FromResult(Usuario);

    public static UsuarioAtual Criar(Perfil perfil, params Guid[] equipes) => new()
    {
        Id = Guid.NewGuid(),
        Matricula = PerfilConversor.ParaTexto(perfil).ToLowerInvariant(),
        Nome = "Usuário " + perfil,
        Perfil = perfil,
        Equipes = equipes.ToHashSet(),
    };
}

internal sealed class EquipeRepositoryFalso : IEquipeRepository
{
    public List<Equipe> Equipes { get; } = [];

    public Task<Equipe?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Equipes.FirstOrDefault(e => e.Id == id));

    public Task<IReadOnlyList<Equipe>> ObterPorIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Equipe>>(Equipes.Where(e => ids.Contains(e.Id)).ToList());

    public Task<PaginaDe<Equipe>> ListarAsync(bool? ativa, string? busca, Paginacao paginacao, CancellationToken cancellationToken)
    {
        var filtradas = Equipes
            .Where(e => (ativa is null || e.Ativa == ativa) && (busca is null || e.Nome.Contains(busca, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(e => e.Nome)
            .ToList();
        return Task.FromResult(new PaginaDe<Equipe>(filtradas.Skip(paginacao.Pular).Take(paginacao.Tamanho).ToList(), filtradas.Count));
    }

    public Task<bool> ExisteNomeAsync(string nome, Guid? ignorarEquipeId, CancellationToken cancellationToken) =>
        Task.FromResult(Equipes.Any(e => e.Id != ignorarEquipeId && string.Equals(e.Nome, nome, StringComparison.OrdinalIgnoreCase)));

    public Task<int> ContarUsuariosQueFicariamSemEquipeAsync(Guid equipeId, CancellationToken cancellationToken) => Task.FromResult(0);

    /// <summary>Os testes de caso de uso não têm membros: todas as equipes com 0.</summary>
    public Task<IReadOnlyDictionary<Guid, int>> ContarMembrosAtivosAsync(
        IReadOnlyCollection<Guid> equipeIds, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyDictionary<Guid, int>>(equipeIds.ToDictionary(id => id, _ => 0));

    public void Adicionar(Equipe equipe)
    {
        equipe.Id = Guid.NewGuid();
        Equipes.Add(equipe);
    }

    public Task<IReadOnlyList<Equipe>> ListarTodasAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Equipe>>(Equipes.OrderBy(e => e.Nome).ToList());

    /// <summary>Listagens habilitadas por equipe (RN90): os testes preenchem à mão quando precisam.</summary>
    public Dictionary<Guid, List<ListagemHabilitada>> Listagens { get; } = [];

    public Task<IReadOnlyDictionary<Guid, IReadOnlyList<ListagemHabilitada>>> ListarListagensAsync(
        IReadOnlyCollection<Guid> equipeIds, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyDictionary<Guid, IReadOnlyList<ListagemHabilitada>>>(Listagens
            .Where(l => equipeIds.Contains(l.Key))
            .ToDictionary(l => l.Key, l => (IReadOnlyList<ListagemHabilitada>)l.Value));
}

internal sealed class UsuarioRepositoryFalso : IUsuarioRepository
{
    public List<Usuario> Usuarios { get; } = [];

    public Task<Usuario?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Usuarios.FirstOrDefault(u => u.Id == id));

    public Task<Usuario?> ObterPorMatriculaAsync(string matricula, CancellationToken cancellationToken) =>
        Task.FromResult(Usuarios.FirstOrDefault(u => string.Equals(u.Matricula, matricula, StringComparison.OrdinalIgnoreCase)));

    public Task<bool> ExisteMatriculaAsync(string matricula, CancellationToken cancellationToken) =>
        Task.FromResult(Usuarios.Any(u => string.Equals(u.Matricula, matricula, StringComparison.OrdinalIgnoreCase)));

    public Task<int> ContarAdministradoresAtivosAsync(CancellationToken cancellationToken) =>
        Task.FromResult(Usuarios.Count(u => u.Ativo && u.EhAdministrador));

    public Task<PaginaDe<Usuario>> ListarAsync(FiltroUsuarios filtro, Paginacao paginacao, CancellationToken cancellationToken) =>
        Task.FromResult(new PaginaDe<Usuario>(Usuarios, Usuarios.Count));

    public Task<IReadOnlyDictionary<Guid, UsuarioResumoDto>> ObterResumosAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyDictionary<Guid, UsuarioResumoDto>>(Usuarios
            .Where(u => ids.Contains(u.Id))
            .ToDictionary(u => u.Id, u => new UsuarioResumoDto(u.Id, u.Matricula, u.Nome)));

    public void Adicionar(Usuario usuario)
    {
        usuario.Id = Guid.NewGuid();
        Usuarios.Add(usuario);
    }
}

internal sealed class HistoricoRepositoryFalso : IHistoricoRepository
{
    public List<HistoricoAlteracao> Linhas { get; } = [];

    public void Adicionar(HistoricoAlteracao historico) => Linhas.Add(historico);

    public Task<PaginaDe<HistoricoAlteracao>> ListarAsync(
        AlvoHistorico alvoTipo, Guid alvoId, Paginacao paginacao, CancellationToken cancellationToken, Guid? entidadeId = null,
        string? dadosContendo = null)
    {
        var itens = Linhas
            .Where(h => h.AlvoTipo == alvoTipo && h.AlvoId == alvoId && (entidadeId is null || h.EntidadeId == entidadeId))
            .Where(h => dadosContendo is null || (h.DadosAntes?.Contains(dadosContendo) ?? false) || (h.DadosDepois?.Contains(dadosContendo) ?? false))
            .Reverse()
            .ToList();
        return Task.FromResult(new PaginaDe<HistoricoAlteracao>(itens, itens.Count));
    }

    public Task<HistoricoAlteracao?> ObterMaisRecenteAsync(AlvoHistorico alvoTipo, Guid alvoId, CancellationToken cancellationToken) =>
        Task.FromResult(Linhas.LastOrDefault(h => h.AlvoTipo == alvoTipo && h.AlvoId == alvoId));
}

/// <summary>Hasher de mentira, legível nos testes: "hash:" + senha.</summary>
internal sealed class HasherSenhaFalso : IHasherSenha
{
    public string GerarHash(string senha) => "hash:" + senha;

    public bool Verificar(string senhaHash, string senha) => senhaHash == "hash:" + senha;
}

internal sealed class SituacaoUsuariosFalsa : ISituacaoUsuarios
{
    public List<Guid> Invalidados { get; } = [];

    public Task<UsuarioAtual?> ObterAsync(Guid usuarioId, CancellationToken cancellationToken) => Task.FromResult<UsuarioAtual?>(null);

    public void Invalidar(Guid usuarioId) => Invalidados.Add(usuarioId);

    public void InvalidarTodos()
    {
    }
}

/// <summary>Relógio parado, que o teste avança quando quiser.</summary>
internal sealed class RelogioFalso : TimeProvider
{
    public DateTimeOffset Agora { get; set; } = new(2026, 10, 21, 12, 0, 0, TimeSpan.Zero);

    public override DateTimeOffset GetUtcNow() => Agora;
}

internal sealed class ConfiguracaoRepositoryFalso : IConfiguracaoRepository
{
    public List<ConfiguracaoEntidadeEquipe> Configuracoes { get; } = [];

    public Task<ConfiguracaoEntidadeEquipe?> ObterAsync(Guid entidadeId, Guid equipeId, CancellationToken cancellationToken) =>
        Task.FromResult(Configuracoes.FirstOrDefault(c => c.EntidadeId == entidadeId && c.EquipeId == equipeId));

    public Task<bool> RequerAprovacaoAsync(Guid entidadeId, Guid equipeId, CancellationToken cancellationToken) =>
        Task.FromResult(Configuracoes.Any(c => c.EntidadeId == entidadeId && c.EquipeId == equipeId && c.RequerAprovacao));

    public Task<bool> RequerAprovacaoEmAlgumaAsync(Guid entidadeId, IReadOnlyCollection<Guid>? equipeIds, CancellationToken cancellationToken) =>
        Task.FromResult(Configuracoes.Any(c => c.EntidadeId == entidadeId && c.RequerAprovacao && (equipeIds is null || equipeIds.Contains(c.EquipeId))));

    public Task<IReadOnlySet<Guid>> EntidadesComAprovacaoAsync(Guid equipeId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlySet<Guid>>(Configuracoes.Where(c => c.EquipeId == equipeId && c.RequerAprovacao).Select(c => c.EntidadeId).ToHashSet());

    public Task<IReadOnlyList<(Guid EntidadeId, Guid EquipeId)>> AprovacoesAtivasAsync(
        IReadOnlyCollection<Guid>? equipeIds, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<(Guid EntidadeId, Guid EquipeId)>>(Configuracoes
            .Where(c => c.RequerAprovacao && (equipeIds is null || equipeIds.Contains(c.EquipeId)))
            .Select(c => (c.EntidadeId, c.EquipeId))
            .ToList());

    public void Adicionar(ConfiguracaoEntidadeEquipe configuracao) => Configuracoes.Add(configuracao);
}

/// <summary>Gerador de arquivo que só guarda o que receberia (colunas e linhas), sem escrever nada.</summary>
internal sealed class GeradorPlanilhaFalso : IGeradorArquivoExportacao
{
    public string Formato => "csv";
    public string TipoConteudo => "text/csv";

    public IReadOnlyList<string> Colunas { get; private set; } = [];
    public List<IReadOnlyList<object?>> Linhas { get; } = [];

    public async Task EscreverAsync(PlanilhaExportacao planilha, Stream destino, CancellationToken cancellationToken)
    {
        Colunas = planilha.Colunas;
        Linhas.Clear();
        await foreach (var linha in planilha.Linhas.WithCancellation(cancellationToken))
            Linhas.Add(linha);
    }

    /// <summary>Os valores de uma coluna, pelo título.</summary>
    public List<object?> Coluna(string titulo)
    {
        var indice = Colunas.ToList().IndexOf(titulo);
        Assert.True(indice >= 0, $"Coluna '{titulo}' não exportada. Colunas: {string.Join(", ", Colunas)}");
        return Linhas.Select(l => l[indice]).ToList();
    }
}
