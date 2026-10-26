using Catalogo.Domain.Metadados;
using Catalogo.Domain.Metadados.Administracao;

namespace Catalogo.Application.Abstracoes;

/// <summary>
/// Escrita dos metadados (grupos, entidades, campos, restrições e dependências) pela administração (RN50 a RN77).
/// As mudanças vão para o banco em <see cref="IUnidadeDeTrabalho.SalvarAlteracoesAsync"/>. Ids novos são gerados aqui.
/// Toda alteração de campo, restrição ou dependência "toca" a entidade dona (<see cref="TocarEntidadeAsync"/>), o que
/// confere e muda a Versao dela (RN52).
/// </summary>
public interface IMetadadosAdminRepository
{
    /// <summary>
    /// O grupo como está no banco AGORA, sem o cache de metadados: dentro da transação da alteração, já com o que ela
    /// gravou. Nulo se não existe. Inclui entidades e campos inativos.
    /// </summary>
    Task<Grupo?> ObterGrupoAtualAsync(string grupoChave, CancellationToken cancellationToken);

    /// <summary>Todos os grupos, ativos e inativos, com a quantidade de entidades.</summary>
    Task<IReadOnlyList<ResumoGrupoAdmin>> ListarGruposAsync(CancellationToken cancellationToken);

    /// <summary>Quantos registros cada entidade tem (as que não têm não aparecem).</summary>
    Task<IReadOnlyDictionary<Guid, int>> ContarRegistrosAsync(IReadOnlyCollection<Guid> entidadeIds, CancellationToken cancellationToken);

    /// <summary>
    /// Marca o grupo como alterado, exigindo que a Versao no banco seja <paramref name="versaoEsperada"/>. Se não for (alguém
    /// alterou depois que o cliente leu), o SaveChanges lança ConflitoConcorrenciaException (RN52).
    /// </summary>
    Task TocarGrupoAsync(Guid grupoId, byte[] versaoEsperada, DateTime agoraUtc, CancellationToken cancellationToken);

    /// <summary>Como <see cref="TocarGrupoAsync"/>, para a entidade (e assim para os campos, restrições e dependências dela).</summary>
    Task TocarEntidadeAsync(Guid entidadeId, byte[] versaoEsperada, DateTime agoraUtc, CancellationToken cancellationToken);

    /// <summary>Grupo novo, ativo. Devolve o id.</summary>
    Guid AdicionarGrupo(string chave, string nome, string? descricao);

    Task AlterarGrupoAsync(Guid grupoId, string nome, string? descricao, bool ativo, CancellationToken cancellationToken);

    /// <summary>Exclui um grupo SEM entidades (RN77).</summary>
    Task ExcluirGrupoAsync(Guid grupoId, byte[] versaoEsperada, CancellationToken cancellationToken);

    /// <summary>Entidade nova, ativa e sem campos. Devolve o id.</summary>
    Guid AdicionarEntidade(Guid grupoId, EntidadeDefinida entidade);

    Task AlterarEntidadeAsync(Guid entidadeId, EntidadeDefinida entidade, bool ativo, CancellationToken cancellationToken);

    /// <summary>Exclui a entidade com os campos, restrições e dependências dela (RN76: só sem registros nem histórico).</summary>
    Task ExcluirEntidadeAsync(Guid entidadeId, byte[] versaoEsperada, CancellationToken cancellationToken);

    /// <summary>Campo novo (o <see cref="Campo.Id"/> é ignorado). Devolve o id.</summary>
    Guid AdicionarCampo(Campo campo);

    /// <summary>Grava os atributos alteráveis do campo (nome, tipo, obrigatoriedade, limites, opções, ação, ordem, ativo).</summary>
    Task AlterarCampoAsync(Campo campo, CancellationToken cancellationToken);

    /// <summary>Ordem nova dos campos: Ordem = posição + 1.</summary>
    Task ReordenarCamposAsync(IReadOnlyList<Campo> camposNaOrdem, CancellationToken cancellationToken);

    /// <summary>Exclui um campo sem valores e sem usos (RN70).</summary>
    Task ExcluirCampoAsync(Guid campoId, CancellationToken cancellationToken);

    /// <summary>Restrição única nova. Devolve o id.</summary>
    Guid AdicionarRestricao(Guid entidadeId, RestricaoDefinida restricao);

    /// <summary>Exclui a restrição e as chaves dela em RegistroChaveUnica (RN72).</summary>
    Task ExcluirRestricaoAsync(Guid restricaoId, CancellationToken cancellationToken);

    /// <summary>Cria ou substitui a dependência do campo (um campo tem no máximo uma).</summary>
    Task DefinirDependenciaAsync(CampoDependencia dependencia, CancellationToken cancellationToken);

    Task ExcluirDependenciaAsync(Guid campoId, CancellationToken cancellationToken);

    /// <summary>
    /// RN90: as equipes habilitadas na listagem passam a ser exatamente <paramref name="equipes"/>: as que saem são removidas e as
    /// novas, incluídas com quem habilitou e quando. As que ficam não mudam.
    /// </summary>
    Task DefinirEquipesAsync(Guid grupoId, IReadOnlySet<Guid> equipes, Guid usuarioId, DateTime agoraUtc, CancellationToken cancellationToken);

    /// <summary>Quantos registros cada equipe tem nas entidades do grupo (as que não têm não aparecem).</summary>
    Task<IReadOnlyDictionary<Guid, int>> ContarRegistrosPorEquipeAsync(Guid grupoId, CancellationToken cancellationToken);
}

/// <summary>Um grupo na lista da administração.</summary>
public sealed record ResumoGrupoAdmin(
    Guid Id, string Chave, string Nome, string? Descricao, bool Ativo, byte[] Versao, int QuantidadeEntidades, int QuantidadeRegistros,
    int QuantidadeEquipes);

/// <summary>
/// Responde às <see cref="VerificacaoDados"/> do <see cref="PlanejadorAlteracoes"/> com LINQ sobre Registro,
/// RegistroValor, RegistroReferencia e RegistroChaveUnica (RN58). Chamado dentro da transação Serializable da alteração.
/// </summary>
public interface IVerificadorDadosMetadados
{
    Task<ResultadoVerificacao> VerificarAsync(VerificacaoDados verificacao, CancellationToken cancellationToken);
}

/// <summary>
/// Quantos registros a verificação encontrou, até <see cref="ImpactoDados.MaximoExemplos"/> ids de exemplo e, para
/// duplicatas, os grupos de ids repetidos (até 10 grupos, 10 ids cada).
/// </summary>
public sealed record ResultadoVerificacao(int Quantidade, IReadOnlyList<Guid> Exemplos, IReadOnlyList<IReadOnlyList<Guid>>? Grupos = null)
{
    public static ResultadoVerificacao Nenhum { get; } = new(0, []);
}

/// <summary>
/// Executa as <see cref="EscritaRegistros"/> (valor padrão, chaves únicas) em LOTES, mantendo Dados, RegistroValor e
/// RegistroChaveUnica coerentes, na transação da alteração (RN58). Devolve quantos registros foram reescritos.
/// </summary>
public interface IEscritorRegistrosMetadados
{
    /// <param name="entidade">A estrutura NOVA da entidade, já gravada na transação.</param>
    Task<int> AplicarAsync(EscritaRegistros escrita, Entidade entidade, CancellationToken cancellationToken);
}
