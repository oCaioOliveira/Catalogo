using System.Text.Json;
using Catalogo.Application.Abstracoes;
using Catalogo.Domain.Excecoes;
using Catalogo.Domain.Metadados;
using Catalogo.Domain.Metadados.Administracao;
using Catalogo.Infrastructure.Metadados;
using Catalogo.Infrastructure.Persistencia;
using Catalogo.Infrastructure.Persistencia.Tabelas;
using Microsoft.EntityFrameworkCore;

namespace Catalogo.Infrastructure.Repositorios;

// Escrita dos metadados pela administração (RN50 a RN77). Todas as consultas são LINQ do EF Core (docs/adr/0011).
// Ids novos são gerados aqui (Guid), para o caso de uso usá-los antes do SaveChanges.
internal sealed class MetadadosAdminRepository(CatalogoDbContext contexto) : IMetadadosAdminRepository
{
    public Task<Grupo?> ObterGrupoAtualAsync(string grupoChave, CancellationToken cancellationToken) =>
        MetadadosProvider.CarregarGrupoAsync(contexto, grupoChave, cancellationToken);

    public async Task<IReadOnlyList<ResumoGrupoAdmin>> ListarGruposAsync(CancellationToken cancellationToken) =>
        await contexto.Grupos.AsNoTracking()
            .OrderBy(g => g.Nome).ThenBy(g => g.Chave)
            .Select(g => new ResumoGrupoAdmin(
                g.Id, g.Chave, g.Nome, g.Descricao, g.Ativo, g.Versao,
                contexto.Entidades.Count(e => e.GrupoId == g.Id),
                // Itens cadastrados em todas as entidades do grupo (a tela "Listagens"), numa subconsulta só.
                contexto.Registros.Count(r => contexto.Entidades.Any(e => e.Id == r.EntidadeId && e.GrupoId == g.Id)),
                contexto.GruposEquipes.Count(v => v.GrupoId == g.Id)))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, int>> ContarRegistrosAsync(IReadOnlyCollection<Guid> entidadeIds, CancellationToken cancellationToken)
    {
        if (entidadeIds.Count == 0)
            return new Dictionary<Guid, int>();

        return await contexto.Registros.AsNoTracking()
            .Where(r => entidadeIds.Contains(r.EntidadeId))
            .GroupBy(r => r.EntidadeId)
            .Select(g => new { EntidadeId = g.Key, Quantidade = g.Count() })
            .ToDictionaryAsync(x => x.EntidadeId, x => x.Quantidade, cancellationToken);
    }

    public async Task DefinirEquipesAsync(
        Guid grupoId, IReadOnlySet<Guid> equipes, Guid usuarioId, DateTime agoraUtc, CancellationToken cancellationToken)
    {
        var atuais = await contexto.GruposEquipes.Where(v => v.GrupoId == grupoId).ToListAsync(cancellationToken);

        // As que saem: só o vínculo. Os itens da equipe ficam (RN92).
        contexto.GruposEquipes.RemoveRange(atuais.Where(v => !equipes.Contains(v.EquipeId)));

        var jaHabilitadas = atuais.Select(v => v.EquipeId).ToHashSet();
        contexto.GruposEquipes.AddRange(equipes
            .Where(e => !jaHabilitadas.Contains(e))
            .Select(e => new GrupoEquipeTabela { GrupoId = grupoId, EquipeId = e, CriadoPorId = usuarioId, CriadoEm = agoraUtc }));
    }

    public async Task<IReadOnlyDictionary<Guid, int>> ContarRegistrosPorEquipeAsync(Guid grupoId, CancellationToken cancellationToken) =>
        await contexto.Registros.AsNoTracking()
            .Where(r => r.EquipeId != null && contexto.Entidades.Any(e => e.Id == r.EntidadeId && e.GrupoId == grupoId))
            .GroupBy(r => r.EquipeId!.Value)
            .Select(g => new { EquipeId = g.Key, Quantidade = g.Count() })
            .ToDictionaryAsync(x => x.EquipeId, x => x.Quantidade, cancellationToken);

    public async Task TocarGrupoAsync(Guid grupoId, byte[] versaoEsperada, DateTime agoraUtc, CancellationToken cancellationToken)
    {
        var grupo = await contexto.Grupos.FirstAsync(g => g.Id == grupoId, cancellationToken);
        // O UPDATE sai com "WHERE Versao = @versaoEsperada": se alguém alterou no meio, o SaveChanges falha (RN52).
        contexto.Entry(grupo).Property(g => g.Versao).OriginalValue = versaoEsperada;
        grupo.AtualizadoEm = agoraUtc;
    }

    public async Task TocarEntidadeAsync(Guid entidadeId, byte[] versaoEsperada, DateTime agoraUtc, CancellationToken cancellationToken)
    {
        var entidade = await contexto.Entidades.FirstAsync(e => e.Id == entidadeId, cancellationToken);
        contexto.Entry(entidade).Property(e => e.Versao).OriginalValue = versaoEsperada;
        // Muda a Versao da entidade mesmo quando a alteração é num campo, restrição ou dependência dela (RN52).
        entidade.AtualizadoEm = agoraUtc;
    }

    public Guid AdicionarGrupo(string chave, string nome, string? descricao)
    {
        var grupo = new GrupoTabela { Id = Guid.NewGuid(), Chave = chave, Nome = nome, Descricao = descricao, Ativo = true };
        contexto.Grupos.Add(grupo);
        return grupo.Id;
    }

    public async Task AlterarGrupoAsync(Guid grupoId, string nome, string? descricao, bool ativo, CancellationToken cancellationToken)
    {
        var grupo = await contexto.Grupos.FirstAsync(g => g.Id == grupoId, cancellationToken);
        grupo.Nome = nome;
        grupo.Descricao = descricao;
        grupo.Ativo = ativo;
    }

    public async Task ExcluirGrupoAsync(Guid grupoId, byte[] versaoEsperada, CancellationToken cancellationToken)
    {
        // As equipes habilitadas saem com o grupo (RN93): são configuração dele, não dados.
        await contexto.GruposEquipes.Where(v => v.GrupoId == grupoId).ExecuteDeleteAsync(cancellationToken);

        // DELETE ... WHERE Versao = @versaoEsperada: se alguém alterou o grupo no meio, nada é excluído (RN52).
        var excluidos = await contexto.Grupos.Where(g => g.Id == grupoId && g.Versao == versaoEsperada).ExecuteDeleteAsync(cancellationToken);
        if (excluidos == 0)
            throw Conflito("do grupo");
    }

    public Guid AdicionarEntidade(Guid grupoId, EntidadeDefinida entidade)
    {
        var tabela = new EntidadeTabela
        {
            Id = Guid.NewGuid(),
            GrupoId = grupoId,
            Chave = entidade.Chave,
            Nome = entidade.Nome,
            Descricao = entidade.Descricao,
            NomePlural = entidade.NomePlural,
            Icone = entidade.Icone,
            Ordem = entidade.Ordem,
            EscopoEquipe = entidade.EscopoEquipe,
            Ativo = true,
        };
        contexto.Entidades.Add(tabela);
        return tabela.Id;
    }

    public async Task AlterarEntidadeAsync(Guid entidadeId, EntidadeDefinida entidade, bool ativo, CancellationToken cancellationToken)
    {
        var tabela = await contexto.Entidades.FirstAsync(e => e.Id == entidadeId, cancellationToken);
        tabela.Nome = entidade.Nome;
        tabela.Descricao = entidade.Descricao;
        tabela.NomePlural = entidade.NomePlural;
        tabela.Icone = entidade.Icone;
        tabela.Ordem = entidade.Ordem;
        tabela.EscopoEquipe = entidade.EscopoEquipe;
        tabela.CampoExibicaoId = entidade.CampoExibicaoId;
        tabela.Ativo = ativo;
    }

    public async Task ExcluirEntidadeAsync(Guid entidadeId, byte[] versaoEsperada, CancellationToken cancellationToken)
    {
        // Confere a versão e solta o campo de exibição (FK para um campo da própria entidade) num UPDATE só (RN52).
        var conferidas = await contexto.Entidades
            .Where(e => e.Id == entidadeId && e.Versao == versaoEsperada)
            .ExecuteUpdateAsync(s => s.SetProperty(e => e.CampoExibicaoId, (Guid?)null), cancellationToken);
        if (conferidas == 0)
            throw Conflito("da entidade");

        // Depois, na ordem das FKs: dependências e restrições apontam para os campos; os campos, para a entidade.
        var campoIds = await contexto.Campos.Where(c => c.EntidadeId == entidadeId).Select(c => c.Id).ToListAsync(cancellationToken);
        await contexto.CamposDependencias.Where(d => campoIds.Contains(d.CampoId)).ExecuteDeleteAsync(cancellationToken);
        await contexto.RestricoesUnicasCampos.Where(rc => rc.EntidadeId == entidadeId).ExecuteDeleteAsync(cancellationToken);
        await contexto.RestricoesUnicas.Where(r => r.EntidadeId == entidadeId).ExecuteDeleteAsync(cancellationToken);
        await contexto.Campos.Where(c => c.EntidadeId == entidadeId).ExecuteDeleteAsync(cancellationToken);
        await contexto.Entidades.Where(e => e.Id == entidadeId).ExecuteDeleteAsync(cancellationToken);
    }

    public Guid AdicionarCampo(Campo campo)
    {
        var tabela = new CampoTabela { Id = Guid.NewGuid(), EntidadeId = campo.EntidadeId, Chave = campo.Chave, Ativo = true };
        Preencher(tabela, campo);
        contexto.Campos.Add(tabela);
        return tabela.Id;
    }

    public async Task AlterarCampoAsync(Campo campo, CancellationToken cancellationToken)
    {
        var tabela = await contexto.Campos.FirstAsync(c => c.Id == campo.Id, cancellationToken);
        Preencher(tabela, campo);
        tabela.Ativo = campo.Ativo;
    }

    public async Task ReordenarCamposAsync(IReadOnlyList<Campo> camposNaOrdem, CancellationToken cancellationToken)
    {
        var ids = camposNaOrdem.Select(c => c.Id).ToList();
        var tabelas = await contexto.Campos.Where(c => ids.Contains(c.Id)).ToDictionaryAsync(c => c.Id, cancellationToken);
        for (var i = 0; i < camposNaOrdem.Count; i++)
            tabelas[camposNaOrdem[i].Id].Ordem = i + 1;
    }

    public async Task ExcluirCampoAsync(Guid campoId, CancellationToken cancellationToken)
    {
        var campo = await contexto.Campos.FirstAsync(c => c.Id == campoId, cancellationToken);
        contexto.Campos.Remove(campo);
    }

    public Guid AdicionarRestricao(Guid entidadeId, RestricaoDefinida restricao)
    {
        var tabela = new RestricaoUnicaTabela { Id = Guid.NewGuid(), EntidadeId = entidadeId, Nome = restricao.Nome };
        contexto.RestricoesUnicas.Add(tabela);
        foreach (var campo in restricao.Campos)
            contexto.RestricoesUnicasCampos.Add(new RestricaoUnicaCampoTabela { RestricaoUnicaId = tabela.Id, EntidadeId = entidadeId, CampoId = campo.Id });
        return tabela.Id;
    }

    public async Task ExcluirRestricaoAsync(Guid restricaoId, CancellationToken cancellationToken)
    {
        // RegistroChaveUnica → RestricaoUnica não tem CASCADE: as chaves saem antes (RN72).
        await contexto.RegistrosChavesUnicas.Where(c => c.RestricaoUnicaId == restricaoId).ExecuteDeleteAsync(cancellationToken);
        await contexto.RestricoesUnicasCampos.Where(rc => rc.RestricaoUnicaId == restricaoId).ExecuteDeleteAsync(cancellationToken);
        await contexto.RestricoesUnicas.Where(r => r.Id == restricaoId).ExecuteDeleteAsync(cancellationToken);
    }

    public async Task DefinirDependenciaAsync(CampoDependencia dependencia, CancellationToken cancellationToken)
    {
        var tabela = await contexto.CamposDependencias.FirstOrDefaultAsync(d => d.CampoId == dependencia.Campo.Id, cancellationToken);
        if (tabela is null)
        {
            tabela = new CampoDependenciaTabela { CampoId = dependencia.Campo.Id };
            contexto.CamposDependencias.Add(tabela);
        }

        tabela.CampoViaId = dependencia.CampoVia?.Id;
        tabela.CampoOrigemId = dependencia.CampoOrigem.Id;
        tabela.CampoAlvoId = dependencia.CampoAlvo.Id;
    }

    public async Task ExcluirDependenciaAsync(Guid campoId, CancellationToken cancellationToken) =>
        await contexto.CamposDependencias.Where(d => d.CampoId == campoId).ExecuteDeleteAsync(cancellationToken);

    private static ConflitoConcorrenciaException Conflito(string doQue) =>
        new($"Os metadados {doQue} foram alterados por outra pessoa depois que você os leu. Recarregue e tente de novo.");

    /// <summary>Os atributos alteráveis do campo (chave e entidade não mudam, RN51; a entidade referenciada só na criação).</summary>
    private static void Preencher(CampoTabela tabela, Campo campo)
    {
        tabela.Nome = campo.Nome;
        tabela.TipoDado = campo.TipoDado;
        tabela.Obrigatorio = campo.Obrigatorio;
        tabela.TamanhoMaximo = campo.TamanhoMaximo;
        tabela.Precisao = campo.Precisao;
        tabela.Escala = campo.Escala;
        tabela.Opcoes = campo.TipoDado == TipoDado.Lista ? JsonSerializer.Serialize(campo.Opcoes) : null;
        tabela.EntidadeReferenciadaId = campo.EntidadeReferenciadaId;
        tabela.AcaoExclusao = campo.AcaoExclusao;
        tabela.Ordem = campo.Ordem;
    }
}
