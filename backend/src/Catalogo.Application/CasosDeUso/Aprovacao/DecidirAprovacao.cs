using Catalogo.Application.Abstracoes;
using Catalogo.Application.Comum;
using Catalogo.Application.Dtos;
using Catalogo.Domain.Acesso;
using Catalogo.Domain.Aprovacao;
using Catalogo.Domain.Excecoes;
using Catalogo.Domain.Registros;
using Catalogo.Domain.Validacao;

namespace Catalogo.Application.CasosDeUso.Aprovacao;

/// <summary>
/// Caso de uso: aprovar ou rejeitar um registro PENDENTE (RN36). Só o GESTOR da equipe do registro ou o
/// ADMINISTRADOR. Justificativa obrigatória (10 a 1000 caracteres). Grava a situação, a última decisão
/// no registro e uma linha no histórico, no mesmo SaveChanges.
/// Lança <see cref="RecursoNaoEncontradoException"/>, <see cref="AcessoNegadoException"/>,
/// <see cref="ValidacaoRegistroException"/> ou <see cref="RegraDeNegocioException"/> (não está pendente).
/// </summary>
public sealed class DecidirAprovacao(
    IUsuarioAtual usuarioAtual,
    IMetadadosProvider metadados,
    IRegistroRepository registros,
    IUnidadeDeTrabalho unidadeDeTrabalho,
    ServicoHistorico historico,
    MontadorRegistroDto montador,
    TimeProvider relogio)
{
    public async Task<RegistroDto> ExecutarAsync(
        string grupoChave, string entidadeChave, Guid id, DecisaoEntrada entrada, CancellationToken cancellationToken)
    {
        var usuario = await usuarioAtual.ObterAsync(cancellationToken);
        (_, var entidade, usuario) = await metadados.ObterEntidadeOuFalharAsync(usuario, grupoChave, entidadeChave, UsoEntidade.Escrita, cancellationToken);

        var registro = await registros.ObterParaAlteracaoAsync(id, cancellationToken);
        var descricao = $"Registro {id} em '{entidade.Nome}'";
        if (registro is null || registro.EntidadeId != entidade.Id)
            throw new RecursoNaoEncontradoException($"{descricao} não encontrado.", CodigosErro.RegistroNaoEncontrado);

        // 404 para quem não vê (ex.: gestor de outra equipe); 403 para INTERNO e EXTERNO.
        Permissoes.Exigir(PoliticaPermissao.Decidir(usuario, OperacaoRegistro.Decidir, RegistroAlvo.De(entidade, registro, false)), descricao);

        var erros = new ErrosPorCampo();
        if (!AprovacaoConversor.TentarConverterDecisao(entrada.Decisao, out var decisao))
            erros.Adicionar("decisao", "Deve ser APROVAR ou REJEITAR.");
        foreach (var erro in FluxoAprovacao.ValidarJustificativa(entrada.Justificativa))
            erros.Adicionar("justificativa", erro);
        erros.LancarSeHouverErros();

        // Só sobre PENDENTE (409 caso contrário). Duas decisões simultâneas: a segunda vira 409 pela Versao.
        var situacaoAntes = registro.SituacaoAprovacao;
        registros.DefinirVersaoEsperada(registro, registro.Versao);
        FluxoAprovacao.Decidir(registro, decisao, entrada.Justificativa, usuario.Id, relogio.GetUtcNow().UtcDateTime);

        historico.RegistrarDecisao(registro, decisao, usuario, registro.JustificativaDecisao!, situacaoAntes);
        await unidadeDeTrabalho.SalvarAlteracoesAsync(cancellationToken);

        return await montador.MontarAsync(registro, entidade, cancellationToken);
    }
}
