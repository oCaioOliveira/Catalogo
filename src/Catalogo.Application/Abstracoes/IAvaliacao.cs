using Catalogo.Domain.Avaliacoes;
using Catalogo.Domain.Consultas;

namespace Catalogo.Application.Abstracoes;

/// <summary>Acesso aos modelos de avaliação (RN40 a RN43). Só os métodos que os casos de uso usam.</summary>
public interface IModeloAvaliacaoRepository
{
    /// <summary>Modelo ATIVO da entidade na equipe, com os critérios, somente leitura. Nulo se não há.</summary>
    Task<ModeloAvaliacao?> ObterAtivoAsync(Guid entidadeId, Guid equipeId, CancellationToken cancellationToken);

    /// <summary>Modelo ATIVO para ALTERAÇÃO (gravado no SaveChanges), com os critérios.</summary>
    Task<ModeloAvaliacao?> ObterAtivoParaAlteracaoAsync(Guid entidadeId, Guid equipeId, CancellationToken cancellationToken);

    /// <summary>Qualquer modelo (ativo ou não) para ALTERAÇÃO, com os critérios.</summary>
    Task<ModeloAvaliacao?> ObterParaAlteracaoAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Todos os modelos da entidade na equipe (ativos e inativos), com os critérios, mais recentes primeiro.</summary>
    Task<IReadOnlyList<ModeloAvaliacao>> ListarAsync(Guid entidadeId, Guid equipeId, CancellationToken cancellationToken);

    /// <summary>Modelos pelos ids, com os critérios, somente leitura.</summary>
    Task<IReadOnlyList<ModeloAvaliacao>> ObterPorIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken);

    /// <summary>
    /// Modelos ATIVOS com os critérios, numa consulta só. <paramref name="entidadeId"/> nulo = todas as
    /// entidades; <paramref name="equipeIds"/> nulo = todas as equipes.
    /// </summary>
    Task<IReadOnlyList<ModeloAvaliacao>> ListarAtivosAsync(
        Guid? entidadeId, IReadOnlyCollection<Guid>? equipeIds, CancellationToken cancellationToken);

    /// <summary>Ids dos critérios do modelo que têm pelo menos uma resposta (RN43).</summary>
    Task<IReadOnlySet<Guid>> CriteriosComRespostasAsync(Guid modeloId, CancellationToken cancellationToken);

    /// <summary>Quantas avaliações o modelo tem.</summary>
    Task<int> ContarAvaliacoesAsync(Guid modeloId, CancellationToken cancellationToken);

    /// <summary>A "versao" lida pelo cliente: o UPDATE só acontece se ela ainda for a do banco (RN12).</summary>
    void DefinirVersaoEsperada(ModeloAvaliacao modelo, byte[] versao);

    /// <summary>Marca o modelo (com os critérios) para inclusão. O Id é gerado AQUI, para o histórico.</summary>
    void Adicionar(ModeloAvaliacao modelo);

    /// <summary>Marca critérios SEM respostas para exclusão (RN43).</summary>
    void RemoverCriterios(IEnumerable<CriterioAvaliacao> criterios);
}

/// <summary>Acesso às avaliações (RN45 a RN48). As agregações (médias, contagens) são feitas NO BANCO.</summary>
public interface IAvaliacaoRepository
{
    /// <summary>A avaliação do usuário no registro, neste modelo, para ALTERAÇÃO (com as respostas). Nula se não há.</summary>
    Task<Avaliacao?> ObterParaAlteracaoAsync(Guid registroId, Guid modeloId, Guid usuarioId, CancellationToken cancellationToken);

    void DefinirVersaoEsperada(Avaliacao avaliacao, byte[] versao);

    /// <summary>Marca a avaliação (com as respostas) para inclusão. O Id é gerado AQUI, para o histórico.</summary>
    void Adicionar(Avaliacao avaliacao);

    /// <summary>Marca a avaliação (e as respostas) para exclusão.</summary>
    void Remover(Avaliacao avaliacao);

    /// <summary>Marca uma resposta para exclusão (o critério deixou de ser respondido na edição).</summary>
    void RemoverResposta(AvaliacaoResposta resposta);

    /// <summary>Modelos que têm avaliações deste registro.</summary>
    Task<IReadOnlyList<Guid>> ModelosAvaliadosAsync(Guid registroId, CancellationToken cancellationToken);

    /// <summary>
    /// Resumo de um modelo num registro (RN47): avaliadores e, por critério ESTRELAS ATIVO, média,
    /// quantidade e distribuição de 1 a 5. Calculado no banco (GroupBy, Average, Count).
    /// </summary>
    Task<ResumoNotas> ResumirAsync(Guid registroId, Guid modeloId, CancellationToken cancellationToken);

    /// <summary>Avaliações do registro (de todos os modelos), com as respostas, das mais recentes para as mais antigas.</summary>
    Task<PaginaDe<Avaliacao>> ListarDoRegistroAsync(Guid registroId, Paginacao paginacao, CancellationToken cancellationToken);

    /// <summary>As avaliações de um usuário num registro (no máximo uma por modelo), com as respostas.</summary>
    Task<IReadOnlyList<Avaliacao>> ListarDoUsuarioAsync(Guid registroId, Guid usuarioId, CancellationToken cancellationToken);

    /// <summary>Avaliações (com as respostas) de vários registros: usado para o histórico da exclusão.</summary>
    Task<IReadOnlyList<Avaliacao>> ListarDosRegistrosAsync(IReadOnlyCollection<Guid> registroIds, CancellationToken cancellationToken);

    /// <summary>
    /// Médias para o retorno dos registros (RN48), numa consulta por informação, para vários registros:
    /// só avaliações dos <paramref name="modeloIds"/> (os modelos ativos) e só critérios ESTRELAS ativos.
    /// </summary>
    Task<MediasDosRegistros> MediasAsync(
        IReadOnlyCollection<Guid> registroIds, IReadOnlyCollection<Guid> modeloIds, Guid usuarioId, CancellationToken cancellationToken);
}

/// <summary>Notas de um critério ESTRELAS num registro. <see cref="Distribuicao"/>: estrelas (1 a 5) → quantidade.</summary>
public sealed record NotasCriterio(Guid CriterioId, double? Media, int Quantidade, IReadOnlyDictionary<int, int> Distribuicao);

/// <summary>Resumo de um modelo num registro: quantos avaliaram e as notas por critério.</summary>
public sealed record ResumoNotas(int Avaliadores, IReadOnlyList<NotasCriterio> Criterios);

/// <summary>Médias de vários registros (RN48).</summary>
/// <param name="Avaliadores">Registro → quantas avaliações.</param>
/// <param name="Medias">(Registro, Critério) → média das estrelas.</param>
/// <param name="AvaliadosPeloUsuario">Registros que o usuário informado avaliou.</param>
public sealed record MediasDosRegistros(
    IReadOnlyDictionary<Guid, int> Avaliadores,
    IReadOnlyDictionary<(Guid RegistroId, Guid CriterioId), double> Medias,
    IReadOnlySet<Guid> AvaliadosPeloUsuario);
