using System.Text.Json;
using Catalogo.Application.Abstracoes;
using Catalogo.Domain.Metadados;
using Catalogo.Infrastructure.Persistencia;
using Catalogo.Infrastructure.Persistencia.Tabelas;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Catalogo.Infrastructure.Metadados;

/// <summary>
/// Lê os metadados de um grupo do banco e guarda em cache (IMemoryCache) por
/// <see cref="TempoCache"/>. Metadados mudam pouco e são lidos em toda requisição.
///
/// Cada servidor tem o seu cache. Ao alterar metadados direto no banco (script V00x),
/// o valor novo aparece em até <see cref="TempoCache"/> ou depois de reiniciar a aplicação.
/// Quando existir edição de metadados pela aplicação, ela deve chamar <see cref="InvalidarCache"/>.
/// </summary>
internal sealed class MetadadosProvider(CatalogoDbContext contexto, IMemoryCache cache) : IMetadadosProvider
{
    public static readonly TimeSpan TempoCache = TimeSpan.FromMinutes(10);

    public async Task<Grupo?> ObterGrupoAsync(string grupoChave, CancellationToken cancellationToken)
    {
        var chaveCache = ChaveCache(grupoChave);
        if (cache.TryGetValue(chaveCache, out Grupo? grupoEmCache))
            return grupoEmCache;

        var grupo = await CarregarGrupoAsync(contexto, grupoChave, cancellationToken);

        // Grupo inexistente não vai para o cache: se for criado depois, aparece na hora.
        if (grupo is not null)
            cache.Set(chaveCache, grupo, TempoCache);

        return grupo;
    }

    public async Task<IReadOnlyList<ResumoGrupo>> ListarGruposAsync(CancellationToken cancellationToken)
    {
        if (cache.TryGetValue(ChaveCacheGrupos, out IReadOnlyList<ResumoGrupo>? emCache))
            return emCache!;

        var linhas = await contexto.Grupos.AsNoTracking()
            .OrderBy(g => g.Nome).ThenBy(g => g.Chave)
            .Select(g => new { g.Id, g.Chave, g.Nome, g.Descricao, g.Ativo })
            .ToListAsync(cancellationToken);
        var habilitadas = await EquipesHabilitadasAsync(contexto, null, cancellationToken);

        IReadOnlyList<ResumoGrupo> grupos = linhas
            .Select(g => new ResumoGrupo(g.Chave, g.Nome, g.Descricao, g.Ativo, habilitadas.GetValueOrDefault(g.Id) ?? new HashSet<Guid>()))
            .ToList();

        cache.Set(ChaveCacheGrupos, grupos, TempoCache);
        return grupos;
    }

    // A lista de grupos também sai do cache: um grupo alterado pode ter mudado de nome.
    public void InvalidarCache(string grupoChave)
    {
        cache.Remove(ChaveCache(grupoChave));
        cache.Remove(ChaveCacheGrupos);
    }

    private const string ChaveCacheGrupos = "catalogo:metadados:grupos";

    // A collation do banco não diferencia maiúsculas, então o cache também não.
    private static string ChaveCache(string grupoChave) => $"catalogo:metadados:grupo:{grupoChave.ToLowerInvariant()}";

    /// <summary>
    /// Lê o grupo do banco, SEM cache. A administração de metadados usa isto dentro da transação da alteração, para enxergar
    /// a estrutura que ela mesma acabou de gravar.
    /// </summary>
    internal static async Task<Grupo?> CarregarGrupoAsync(CatalogoDbContext contexto, string grupoChave, CancellationToken cancellationToken)
    {
        // Consultas simples, uma por tabela, em vez de Includes encadeados.
        var grupo = await contexto.Grupos.AsNoTracking()
            .FirstOrDefaultAsync(g => g.Chave == grupoChave, cancellationToken);
        if (grupo is null)
            return null;

        var entidades = await contexto.Entidades.AsNoTracking()
            .Where(e => e.GrupoId == grupo.Id)
            .ToListAsync(cancellationToken);
        var entidadeIds = entidades.Select(e => e.Id).ToList();

        var campos = await contexto.Campos.AsNoTracking()
            .Where(c => entidadeIds.Contains(c.EntidadeId))
            .ToListAsync(cancellationToken);
        var campoIds = campos.Select(c => c.Id).ToList();

        var restricoes = await contexto.RestricoesUnicas.AsNoTracking()
            .Where(r => entidadeIds.Contains(r.EntidadeId))
            .ToListAsync(cancellationToken);

        var restricoesCampos = await contexto.RestricoesUnicasCampos.AsNoTracking()
            .Where(rc => entidadeIds.Contains(rc.EntidadeId))
            .ToListAsync(cancellationToken);

        var dependencias = await contexto.CamposDependencias.AsNoTracking()
            .Where(d => campoIds.Contains(d.CampoId))
            .ToListAsync(cancellationToken);

        // Uma dependência pode citar campos de entidades de outro grupo: carrega esses também.
        var idsDeOutrosGrupos = dependencias
            .SelectMany(d => new[] { d.CampoViaId, d.CampoOrigemId, d.CampoAlvoId })
            .OfType<Guid>()
            .Except(campoIds)
            .ToList();
        if (idsDeOutrosGrupos.Count > 0)
        {
            campos.AddRange(await contexto.Campos.AsNoTracking()
                .Where(c => idsDeOutrosGrupos.Contains(c.Id))
                .ToListAsync(cancellationToken));
        }

        var equipes = await EquipesHabilitadasAsync(contexto, grupo.Id, cancellationToken);
        var montado = MontarGrupo(grupo, entidades, campos, restricoes, restricoesCampos, dependencias,
            equipes.GetValueOrDefault(grupo.Id) ?? new HashSet<Guid>());

        // RN14: falha com mensagem clara se algum campo usa chave reservada (erro de configuração).
        ChavesReservadas.ValidarCampos(montado);
        return montado;
    }

    /// <summary>Converte as linhas das tabelas no modelo de leitura imutável do Domain.</summary>
    private static Grupo MontarGrupo(
        GrupoTabela grupo,
        List<EntidadeTabela> entidades,
        List<CampoTabela> campos,
        List<RestricaoUnicaTabela> restricoes,
        List<RestricaoUnicaCampoTabela> restricoesCampos,
        List<CampoDependenciaTabela> dependencias,
        IReadOnlySet<Guid> equipesHabilitadas)
    {
        var camposPorId = campos.Select(ParaDominio).ToDictionary(c => c.Id);

        var dependenciasDominio = dependencias
            .Select(d => new CampoDependencia
            {
                Campo = camposPorId[d.CampoId],
                CampoVia = d.CampoViaId is Guid viaId ? camposPorId[viaId] : null,
                CampoOrigem = camposPorId[d.CampoOrigemId],
                CampoAlvo = camposPorId[d.CampoAlvoId],
            })
            .ToList();

        var restricoesDominio = restricoes
            .Select(r => new RestricaoUnica
            {
                Id = r.Id,
                EntidadeId = r.EntidadeId,
                Nome = r.Nome,
                // RestricaoUnicaCampo não tem coluna de ordem: a ordem do hash é Campo.Ordem e depois Chave.
                Campos = OrdenarCampos(restricoesCampos
                    .Where(rc => rc.RestricaoUnicaId == r.Id)
                    .Select(rc => camposPorId[rc.CampoId])),
            })
            .ToList();

        return new Grupo
        {
            Id = grupo.Id,
            Chave = grupo.Chave,
            Nome = grupo.Nome,
            Descricao = grupo.Descricao,
            Ativo = grupo.Ativo,
            Versao = grupo.Versao,
            EquipesHabilitadas = equipesHabilitadas,
            Entidades = entidades
                .Select(e => new Entidade
                {
                    Id = e.Id,
                    GrupoId = e.GrupoId,
                    Chave = e.Chave,
                    Nome = e.Nome,
                    Descricao = e.Descricao,
                    NomePlural = e.NomePlural,
                    Icone = e.Icone,
                    CampoExibicaoId = e.CampoExibicaoId,
                    EscopoEquipe = e.EscopoEquipe,
                    Ativo = e.Ativo,
                    Ordem = e.Ordem,
                    Versao = e.Versao,
                    Campos = OrdenarCampos(camposPorId.Values.Where(c => c.EntidadeId == e.Id)),
                    RestricoesUnicas = restricoesDominio.Where(r => r.EntidadeId == e.Id).OrderBy(r => r.Nome, StringComparer.Ordinal).ToList(),
                    Dependencias = dependenciasDominio.Where(d => d.Campo.EntidadeId == e.Id).ToList(),
                })
                .OrderBy(e => e.Ordem).ThenBy(e => e.Chave, StringComparer.Ordinal)
                .ToList(),
        };
    }

    /// <summary>As equipes habilitadas (catalogo.GrupoEquipe, RN90) por grupo: de um só (<paramref name="grupoId"/>) ou de todos (nulo).</summary>
    private static async Task<Dictionary<Guid, IReadOnlySet<Guid>>> EquipesHabilitadasAsync(
        CatalogoDbContext contexto, Guid? grupoId, CancellationToken cancellationToken)
    {
        var vinculos = await contexto.GruposEquipes.AsNoTracking()
            .Where(v => grupoId == null || v.GrupoId == grupoId)
            .Select(v => new { v.GrupoId, v.EquipeId })
            .ToListAsync(cancellationToken);
        return vinculos
            .GroupBy(v => v.GrupoId)
            .ToDictionary(g => g.Key, g => (IReadOnlySet<Guid>)g.Select(v => v.EquipeId).ToHashSet());
    }

    private static List<Campo> OrdenarCampos(IEnumerable<Campo> campos) =>
        campos.OrderBy(c => c.Ordem).ThenBy(c => c.Chave, StringComparer.Ordinal).ToList();

    private static Campo ParaDominio(CampoTabela campo) => new()
    {
        Id = campo.Id,
        EntidadeId = campo.EntidadeId,
        Chave = campo.Chave,
        Nome = campo.Nome,
        TipoDado = campo.TipoDado,
        Obrigatorio = campo.Obrigatorio,
        TamanhoMaximo = campo.TamanhoMaximo,
        Precisao = campo.Precisao,
        Escala = campo.Escala,
        Opcoes = LerOpcoes(campo),
        EntidadeReferenciadaId = campo.EntidadeReferenciadaId,
        AcaoExclusao = campo.AcaoExclusao,
        Ordem = campo.Ordem,
        Ativo = campo.Ativo,
    };

    /// <summary>Campo.Opcoes é um array JSON de textos (o CHECK do banco só garante que é JSON válido).</summary>
    private static IReadOnlyList<string> LerOpcoes(CampoTabela campo)
    {
        if (string.IsNullOrWhiteSpace(campo.Opcoes))
            return [];

        try
        {
            return JsonSerializer.Deserialize<List<string>>(campo.Opcoes) ?? [];
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                $"Campo '{campo.Chave}' ({campo.Id}): Opcoes precisa ser um array JSON de textos, ex.: [\"GET\",\"POST\"].", ex);
        }
    }
}
