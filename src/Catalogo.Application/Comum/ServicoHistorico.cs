using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Catalogo.Application.Abstracoes;
using Catalogo.Domain.Acesso;
using Catalogo.Domain.Aprovacao;
using Catalogo.Domain.Avaliacoes;
using Catalogo.Domain.Historico;
using Catalogo.Domain.Registros;

namespace Catalogo.Application.Comum;

/// <summary>
/// Monta as linhas de histórico (RN32) e as marca para gravação no MESMO SaveChanges da operação.
/// Os casos de uso chamam este serviço EXPLICITAMENTE (docs/adr/0018): quem lê o caso de uso vê
/// que ele grava histórico, sem mágica no SaveChanges.
/// </summary>
public sealed class ServicoHistorico(IHistoricoRepository historico, TimeProvider relogio)
{
    private static readonly JsonSerializerOptions OpcoesJson = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>Uma linha para um registro. <paramref name="antes"/>/<paramref name="depois"/> no formato plano (refs como GUID).</summary>
    /// <param name="origem">Nula no cadastro manual; <see cref="OrigemHistorico.Importacao"/> quando vem de uma importação (RN97).</param>
    public void RegistrarRegistro(
        OperacaoHistorico operacao, Registro registro, UsuarioAtual autor, Guid correlacaoId, JsonObject? antes, JsonObject? depois,
        OrigemHistorico? origem = null) =>
        Adicionar(AlvoHistorico.Registro, registro.Id, operacao, autor.Id, autor.Matricula, correlacaoId, antes, depois,
            registro.EntidadeId, registro.EquipeId, origem: origem);

    /// <summary>
    /// Uma linha de APROVACAO ou REJEICAO (RN36), com a justificativa. Antes e depois mostram a mudança
    /// de situação ({ "situacaoAprovacao": ... }).
    /// </summary>
    public void RegistrarDecisao(
        Registro registro, DecisaoAprovacao decisao, UsuarioAtual autor, string justificativa,
        SituacaoAprovacao situacaoAntes) =>
        Adicionar(AlvoHistorico.Registro, registro.Id,
            decisao == DecisaoAprovacao.Aprovar ? OperacaoHistorico.Aprovacao : OperacaoHistorico.Rejeicao,
            autor.Id, autor.Matricula, Guid.NewGuid(),
            Situacao(situacaoAntes), Situacao(registro.SituacaoAprovacao), registro.EntidadeId, registro.EquipeId, justificativa);

    /// <summary>
    /// Uma linha para a configuração de uma entidade numa equipe (RN33): AlvoId = equipe, EntidadeId = entidade.
    /// </summary>
    public void RegistrarConfiguracao(
        OperacaoHistorico operacao, Guid entidadeId, Guid equipeId, UsuarioAtual autor, JsonObject? antes, JsonObject? depois) =>
        Adicionar(AlvoHistorico.Configuracao, equipeId, operacao, autor.Id, autor.Matricula, Guid.NewGuid(), antes, depois,
            entidadeId, equipeId);

    /// <summary>
    /// Uma linha para uma alteração de metadados (RN52): AlvoId = grupo, EntidadeId nulo (a entidade pode ser excluída depois;
    /// ela vai nos dados). Uma linha por alteração, mesmo que muitos registros tenham sido reescritos (a quantidade vai nos dados).
    /// </summary>
    public void RegistrarMetadado(OperacaoHistorico operacao, Guid grupoId, UsuarioAtual autor, JsonObject? antes, JsonObject? depois) =>
        Adicionar(AlvoHistorico.Metadado, grupoId, operacao, autor.Id, autor.Matricula, Guid.NewGuid(), antes, depois, null, null);

    /// <summary>Uma linha para um modelo de avaliação (RN40): AlvoId = equipe, EntidadeId = entidade, como a configuração.</summary>
    public void RegistrarModeloAvaliacao(
        OperacaoHistorico operacao, ModeloAvaliacao modelo, UsuarioAtual autor, JsonObject? antes, JsonObject? depois) =>
        Adicionar(AlvoHistorico.ModeloAvaliacao, modelo.EquipeId, operacao, autor.Id, autor.Matricula, Guid.NewGuid(), antes, depois,
            modelo.EntidadeId, modelo.EquipeId);

    /// <summary>
    /// Uma linha para uma avaliação (RN45): AlvoId = registro avaliado, com a entidade e a equipe dele (o histórico
    /// sobrevive à exclusão do registro). <paramref name="autorId"/> é quem fez a operação.
    /// </summary>
    public void RegistrarAvaliacao(
        OperacaoHistorico operacao, Registro registro, Guid autorId, string autorMatricula, Guid correlacaoId,
        JsonObject? antes, JsonObject? depois) =>
        Adicionar(AlvoHistorico.Avaliacao, registro.Id, operacao, autorId, autorMatricula, correlacaoId, antes, depois,
            registro.EntidadeId, registro.EquipeId);

    /// <summary>{ "situacaoAprovacao": "PENDENTE" } — usado nas decisões e no reenvio.</summary>
    public static JsonObject Situacao(SituacaoAprovacao situacao) =>
        new() { ["situacaoAprovacao"] = AprovacaoConversor.ParaTexto(situacao) };

    /// <summary>Uma linha para um usuário. Os objetos NUNCA podem ter senha nem hash (ver UsuarioMapeamentos).</summary>
    public void RegistrarUsuario(
        OperacaoHistorico operacao, Guid usuarioAlvoId, Guid autorId, string autorMatricula, JsonObject? antes, JsonObject? depois) =>
        Adicionar(AlvoHistorico.Usuario, usuarioAlvoId, operacao, autorId, autorMatricula, Guid.NewGuid(), antes, depois, null, null);

    /// <summary>Uma linha para uma equipe (a própria equipe vai em EquipeId).</summary>
    public void RegistrarEquipe(
        OperacaoHistorico operacao, Equipe equipe, UsuarioAtual autor, JsonObject? antes, JsonObject? depois) =>
        Adicionar(AlvoHistorico.Equipe, equipe.Id, operacao, autor.Id, autor.Matricula, Guid.NewGuid(), antes, depois, null, equipe.Id);

    private void Adicionar(
        AlvoHistorico alvo, Guid alvoId, OperacaoHistorico operacao, Guid autorId, string autorMatricula, Guid correlacaoId,
        JsonObject? antes, JsonObject? depois, Guid? entidadeId, Guid? equipeId, string? justificativa = null,
        OrigemHistorico? origem = null)
    {
        historico.Adicionar(new HistoricoAlteracao
        {
            AlvoTipo = alvo,
            AlvoId = alvoId,
            EntidadeId = entidadeId,
            EquipeId = equipeId,
            Operacao = operacao,
            UsuarioId = autorId,
            UsuarioMatricula = autorMatricula,
            OcorridoEm = relogio.GetUtcNow().UtcDateTime,
            CorrelacaoId = correlacaoId,
            DadosAntes = antes?.ToJsonString(OpcoesJson),
            DadosDepois = depois?.ToJsonString(OpcoesJson),
            Justificativa = justificativa,
            Origem = origem,
        });
    }
}
