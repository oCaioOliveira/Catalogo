using Catalogo.Domain.Aprovacao;
using Catalogo.Domain.Consultas;
using Catalogo.Domain.Exclusao;
using Catalogo.Domain.Registros;

namespace Catalogo.Application.Abstracoes;

/// <summary>
/// Acesso aos registros (dados). Tem só os métodos que os casos de uso usam:
/// não é um repositório genérico. Inclusões e alterações só vão para o banco em
/// <see cref="IUnidadeDeTrabalho.SalvarAlteracoesAsync"/>; a exceção é <see cref="ExcluirAsync"/>.
/// </summary>
public interface IRegistroRepository
{
    /// <summary>Registro com as suas Referencias, ou nulo se não existe. Somente leitura.</summary>
    Task<Registro?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Registros encontrados entre os ids informados, com as suas Referencias. Somente leitura.
    /// Ids que não existem simplesmente não aparecem no resultado (a ordem não é garantida).
    /// </summary>
    Task<IReadOnlyList<Registro>> ObterPorIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken);

    /// <summary>
    /// Registro para ALTERAR, com Referencias, Valores e ChavesUnicas. As mudanças feitas no objeto
    /// são gravadas em <see cref="IUnidadeDeTrabalho.SalvarAlteracoesAsync"/>.
    /// </summary>
    Task<Registro?> ObterParaAlteracaoAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Diz ao EF qual <c>Versao</c> o cliente leu: o UPDATE só acontece se ela ainda for a do banco
    /// (UPDATE ... WHERE Versao = @versao). Se não for, a gravação lança ConflitoConcorrenciaException (RN12).
    /// </summary>
    void DefinirVersaoEsperada(Registro registro, byte[] versao);

    /// <summary>
    /// Indica se já existe um registro com este hash nesta restrição única.
    /// <paramref name="ignorarRegistroId"/>: o próprio registro numa atualização (não conflita consigo).
    /// </summary>
    Task<bool> ExisteChaveUnicaAsync(Guid restricaoUnicaId, byte[] valorHash, Guid? ignorarRegistroId, CancellationToken cancellationToken);

    /// <summary>
    /// Marca o registro (com Referencias, Valores e ChavesUnicas) para inclusão. O Id do registro é
    /// gerado AQUI (antes do SaveChanges), para o histórico poder usá-lo.
    /// </summary>
    void Adicionar(Registro registro);

    /// <summary>
    /// Rótulo de cada registro (RN20), numa única consulta: o valor do campo de exibição da entidade
    /// do registro, em texto. Registros sem rótulo (ou inexistentes) não aparecem: use GetValueOrDefault.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, string?>> ObterRotulosAsync(IReadOnlyCollection<Guid> registroIds, CancellationToken cancellationToken);

    /// <summary>A situação de aprovação de cada registro (RN34), numa única consulta. Inexistentes não aparecem.</summary>
    Task<IReadOnlyDictionary<Guid, SituacaoAprovacao>> ObterSituacoesAsync(
        IReadOnlyCollection<Guid> registroIds, CancellationToken cancellationToken);

    /// <summary>
    /// Ids da página pedida, já filtrados e ordenados, e o total de registros que passam nos filtros (RN17 a RN19).
    /// </summary>
    Task<PaginaDeIds> ListarIdsAsync(Guid entidadeId, ConsultaRegistros consulta, CancellationToken cancellationToken);

    /// <summary>Quantos registros PENDENTES a entidade tem nesta equipe (RN33).</summary>
    Task<int> ContarPendentesAsync(Guid entidadeId, Guid equipeId, CancellationToken cancellationToken);

    /// <summary>Fila de aprovação: registros PENDENTES, dos mais antigos para os mais novos (RN36).</summary>
    Task<PaginaDe<PendenteResumo>> ListarPendentesAsync(FiltroPendentes filtro, Paginacao paginacao, CancellationToken cancellationToken);

    /// <summary>Referências (com a AcaoExclusao do campo) cujo destino é um dos registros informados.</summary>
    Task<IReadOnlyList<ReferenciaEntrante>> ObterReferenciasQueApontamParaAsync(
        IReadOnlyCollection<Guid> registroIds, CancellationToken cancellationToken);

    /// <summary>
    /// RN94: quantos registros VISÍVEIS apontam para <paramref name="registroId"/>, por campo REFERENCIA, numa consulta só, no banco.
    /// Cada contagem traz o escopo de visibilidade da entidade de origem (o mesmo da listagem, <c>PreparadorConsultaRegistros.Escopo</c>).
    /// Campo sem nenhum registro: fica fora do dicionário (quantidade 0).
    /// </summary>
    Task<IReadOnlyDictionary<Guid, int>> ContarQueApontamParaAsync(
        Guid registroId, IReadOnlyList<ContagemPorCampo> contagens, CancellationToken cancellationToken);

    /// <summary>
    /// RN96: os registros VISÍVEIS que o usuário criou ou alterou por último (pelo histórico), do mais recente para o mais antigo,
    /// no máximo <paramref name="limite"/>. Cada escopo é o da listagem da entidade (<c>PreparadorConsultaRegistros.Escopo</c>): o que
    /// o usuário não vê mais (excluído, outra equipe, entidade fora da lista) não aparece. Numa consulta só, no banco.
    /// </summary>
    Task<IReadOnlyList<ItemRecente>> ListarRecentesDoUsuarioAsync(
        Guid usuarioId, IReadOnlyList<EscopoDaEntidade> escopos, int limite, CancellationToken cancellationToken);

    /// <summary>
    /// Executa um plano de exclusão válido NA HORA, numa única transação, na ordem do plano (RN16).
    /// Na mesma transação, antes das exclusões, grava o que estiver pendente (o histórico, RN32).
    /// Se outra requisição criar uma referência no meio, lança ConflitoConcorrenciaException e nada é excluído.
    /// </summary>
    Task ExcluirAsync(PlanoExclusao plano, CancellationToken cancellationToken);
}

/// <summary>Uma contagem de relacionados (RN94): o campo REFERENCIA, a entidade dele e o escopo de visibilidade dela.</summary>
public sealed record ContagemPorCampo(Guid CampoId, Guid EntidadeOrigemId, ConsultaRegistros Escopo);

/// <summary>O escopo de visibilidade de uma entidade (o mesmo da listagem), para consultas que juntam várias entidades.</summary>
public sealed record EscopoDaEntidade(Guid EntidadeId, ConsultaRegistros Escopo);

/// <summary>Um registro criado ou alterado pelo usuário (RN96) e quando foi a última vez.</summary>
public sealed record ItemRecente(Guid RegistroId, Guid EntidadeId, DateTime OcorridoEm);

/// <summary>Uma página de ids e o total de registros da consulta.</summary>
public sealed record PaginaDeIds(IReadOnlyList<Guid> Ids, int Total);
