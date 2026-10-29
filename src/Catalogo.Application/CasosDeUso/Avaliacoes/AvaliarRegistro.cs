using System.Text.Json.Nodes;
using Catalogo.Application.Abstracoes;
using Catalogo.Application.Comum;
using Catalogo.Application.Dtos;
using Catalogo.Application.Mapeamentos;
using Catalogo.Domain.Acesso;
using Catalogo.Domain.Aprovacao;
using Catalogo.Domain.Avaliacoes;
using Catalogo.Domain.Excecoes;
using Catalogo.Domain.Historico;
using Catalogo.Domain.Validacao;

namespace Catalogo.Application.CasosDeUso.Avaliacoes;

/// <summary>
/// Caso de uso: criar ou editar a avaliação do usuário logado sobre um item (PUT .../avaliacoes/minha).
///   - Quem: INTERNO, GESTOR e ADMINISTRADOR, em itens que veem; EXTERNO, 403 (RN44, PoliticaPermissao).
///   - O quê: item com modelo ATIVO na equipe dele (400 sem modelo) e APROVADO (409 se não, RN44).
///   - Uma avaliação por usuário, item e modelo (RN45): a segunda chamada edita, com a "versao" (RN12).
///   - Respostas validadas contra o modelo ATUAL, todos os erros juntos (RN46).
/// Gera histórico (alvo AVALIACAO).
/// </summary>
public sealed class AvaliarRegistro(
    IUsuarioAtual usuarioAtual,
    IMetadadosProvider metadados,
    IRegistroRepository registros,
    ContextoAprovacao contextoAprovacao,
    IModeloAvaliacaoRepository modelos,
    IAvaliacaoRepository avaliacoes,
    IUsuarioRepository usuarios,
    IUnidadeDeTrabalho unidadeDeTrabalho,
    ServicoHistorico historico,
    TimeProvider relogio)
{
    public async Task<ResultadoAvaliacao> ExecutarAsync(
        string grupoChave, string entidadeChave, Guid id, AvaliacaoEntrada entrada, CancellationToken cancellationToken)
    {
        var usuario = await usuarioAtual.ObterAsync(cancellationToken);
        (_, var entidade, usuario) = await metadados.ObterEntidadeOuFalharAsync(usuario, grupoChave, entidadeChave, UsoEntidade.Escrita, cancellationToken);

        // 1. Quem avalia (404 se não vê o item, 403 para o EXTERNO).
        var registro = await AvaliacaoComum.ObterRegistroAsync(registros, entidade, id, cancellationToken);
        var alvo = await contextoAprovacao.ExistenteAsync(entidade, registro, cancellationToken);
        Permissoes.Exigir(PoliticaPermissao.PodeAvaliar(usuario, alvo), AvaliacaoComum.DescreverRegistro(id, entidade));

        // 2. O que pode ser avaliado: modelo ativo (400) e item aprovado (409).
        var modelo = await AvaliacaoComum.ObterModeloAtivoOuFalharAsync(modelos, entidade, registro, cancellationToken);
        if (!RegrasAvaliacao.PodeSerAvaliado(registro.SituacaoAprovacao))
        {
            throw new RegraDeNegocioException(
                $"Só itens aprovados podem ser avaliados. Este está {AprovacaoConversor.ParaTexto(registro.SituacaoAprovacao)}.",
                CodigosErro.RegistroNaoAprovado);
        }

        // 3. Versão (só ao editar) e respostas: todos os erros juntos.
        var existente = await avaliacoes.ObterParaAlteracaoAsync(registro.Id, modelo.Id, usuario.Id, cancellationToken);
        var erros = new ErrosPorCampo();
        var versao = existente is null ? null : AvaliacaoComum.LerVersao(entrada.Versao, "pelo GET das avaliações (\"minha\")", erros);
        var respostas = RegrasAvaliacao.ValidarRespostas(modelo, entrada.Respostas, erros);
        erros.LancarSeHouverErros();

        // 4. Cria ou edita, com o histórico, num único SaveChanges.
        Avaliacao avaliacao;
        if (existente is null)
        {
            avaliacao = new Avaliacao { RegistroId = registro.Id, ModeloAvaliacaoId = modelo.Id, UsuarioId = usuario.Id };
            avaliacoes.Adicionar(avaliacao);
            RegrasAvaliacao.AplicarRespostas(avaliacao, modelo, respostas);
            historico.RegistrarAvaliacao(OperacaoHistorico.Criacao, registro, usuario.Id, usuario.Matricula, Guid.NewGuid(),
                null, avaliacao.ParaJsonHistorico(modelo));
        }
        else
        {
            avaliacao = existente;
            if (!versao!.AsSpan().SequenceEqual(avaliacao.Versao))
                throw new ConflitoConcorrenciaException("Sua avaliação foi alterada depois que você a leu. Recarregue e tente de novo.");
            avaliacoes.DefinirVersaoEsperada(avaliacao, versao!);

            var antes = avaliacao.ParaJsonHistorico(modelo);
            foreach (var removida in RegrasAvaliacao.AplicarRespostas(avaliacao, modelo, respostas))
                avaliacoes.RemoverResposta(removida);
            var depois = avaliacao.ParaJsonHistorico(modelo);

            // Nada mudou: não grava nem gera histórico.
            if (JsonNode.DeepEquals(antes, depois))
                return new ResultadoAvaliacao(await MontarAsync(avaliacao, modelo, cancellationToken), Criada: false);

            avaliacao.AtualizadoEm = relogio.GetUtcNow().UtcDateTime;
            historico.RegistrarAvaliacao(OperacaoHistorico.Alteracao, registro, usuario.Id, usuario.Matricula, Guid.NewGuid(), antes, depois);
        }

        try
        {
            await unidadeDeTrabalho.SalvarAlteracoesAsync(cancellationToken);
        }
        catch (ViolacaoUnicidadeBancoException)
        {
            // RN45: a mesma pessoa avaliou o item em outra requisição, ao mesmo tempo.
            throw new ConflitoConcorrenciaException("Você já avaliou este item em outra requisição. Recarregue e edite a sua avaliação.");
        }

        return new ResultadoAvaliacao(await MontarAsync(avaliacao, modelo, cancellationToken), Criada: existente is null);
    }

    private async Task<AvaliacaoDto> MontarAsync(Avaliacao avaliacao, ModeloAvaliacao modelo, CancellationToken cancellationToken) =>
        avaliacao.ParaDto(modelo, await usuarios.ObterResumosAsync([avaliacao.UsuarioId], cancellationToken));
}
