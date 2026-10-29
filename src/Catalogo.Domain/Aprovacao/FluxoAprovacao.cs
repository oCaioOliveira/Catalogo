using Catalogo.Domain.Acesso;
using Catalogo.Domain.Excecoes;
using Catalogo.Domain.Registros;

namespace Catalogo.Domain.Aprovacao;

/// <summary>
/// Transições de situação de um registro (RN34 a RN36). Código puro. QUEM pode fazer cada coisa
/// está na <see cref="PoliticaPermissao"/>; aqui fica O QUE acontece com a situação.
///
///   criação por EXTERNO com aprovação ativa → PENDENTE; demais criações → APROVADO
///   PENDENTE  --APROVAR--> APROVADO
///   PENDENTE  --REJEITAR--> REJEITADO
///   REJEITADO --alteração pelo autor (reenvio)--> PENDENTE
/// </summary>
public static class FluxoAprovacao
{
    public const int TamanhoMinimoJustificativa = 10;
    public const int TamanhoMaximoJustificativa = 1000;

    /// <summary>Situação de um registro novo (RN34).</summary>
    public static SituacaoAprovacao SituacaoInicial(UsuarioAtual autor, bool requerAprovacao) =>
        requerAprovacao && autor.Perfil == Perfil.Externo ? SituacaoAprovacao.Pendente : SituacaoAprovacao.Aprovado;

    /// <summary>
    /// Situação depois de uma alteração (RN35). O EXTERNO autor alterando um registro REJEITADO faz um
    /// REENVIO: volta para PENDENTE. Nos demais casos a situação não muda (inclusive quando INTERNO,
    /// GESTOR ou ADMINISTRADOR alteram um registro pendente).
    /// </summary>
    public static SituacaoAprovacao SituacaoAposAlteracao(UsuarioAtual autorDaAlteracao, Registro registro) =>
        registro.SituacaoAprovacao == SituacaoAprovacao.Rejeitado
        && autorDaAlteracao.Perfil == Perfil.Externo
        && registro.CriadoPorId == autorDaAlteracao.Id
            ? SituacaoAprovacao.Pendente
            : registro.SituacaoAprovacao;

    /// <summary>Erros da justificativa (lista vazia = válida): obrigatória, de 10 a 1000 caracteres (RN36).</summary>
    public static IReadOnlyList<string> ValidarJustificativa(string? justificativa)
    {
        var texto = justificativa?.Trim() ?? "";
        if (texto.Length == 0)
            return ["Obrigatória para aprovar ou rejeitar."];
        if (texto.Length < TamanhoMinimoJustificativa)
            return [$"Deve ter pelo menos {TamanhoMinimoJustificativa} caracteres."];
        if (texto.Length > TamanhoMaximoJustificativa)
            return [$"Deve ter no máximo {TamanhoMaximoJustificativa} caracteres."];
        return [];
    }

    /// <summary>
    /// Aplica a decisão (RN36): só sobre registro PENDENTE; guarda a situação e a ÚLTIMA decisão no registro.
    /// </summary>
    /// <exception cref="ValidacaoRegistroException">Justificativa ausente, curta ou longa demais (400).</exception>
    /// <exception cref="RegraDeNegocioException">Registro não está pendente (409, código REGISTRO_NAO_PENDENTE).</exception>
    public static void Decidir(Registro registro, DecisaoAprovacao decisao, string? justificativa, Guid decididoPorId, DateTime agoraUtc)
    {
        var erros = ValidarJustificativa(justificativa);
        if (erros.Count > 0)
            throw new ValidacaoRegistroException(new Dictionary<string, string[]> { ["justificativa"] = erros.ToArray() });

        if (registro.SituacaoAprovacao != SituacaoAprovacao.Pendente)
        {
            throw new RegraDeNegocioException(
                $"Só é possível decidir sobre registros pendentes. Este está {AprovacaoConversor.ParaTexto(registro.SituacaoAprovacao)}.",
                CodigosErro.RegistroNaoPendente);
        }

        registro.SituacaoAprovacao = decisao == DecisaoAprovacao.Aprovar ? SituacaoAprovacao.Aprovado : SituacaoAprovacao.Rejeitado;
        registro.DecididoPorId = decididoPorId;
        registro.DecididoEm = agoraUtc;
        registro.JustificativaDecisao = justificativa!.Trim();
    }

    /// <summary>
    /// A última decisão registrada, deduzida da situação: APROVADO com decisão = APROVAR; REJEITADO, ou
    /// PENDENTE depois de um reenvio, = REJEITAR. Nula se ninguém decidiu ainda.
    /// </summary>
    public static DecisaoAprovacao? UltimaDecisao(Registro registro) => registro.DecididoEm is null
        ? null
        : registro.SituacaoAprovacao == SituacaoAprovacao.Aprovado ? DecisaoAprovacao.Aprovar : DecisaoAprovacao.Rejeitar;
}
