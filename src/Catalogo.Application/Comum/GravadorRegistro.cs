using System.Text.Json.Nodes;
using Catalogo.Application.Abstracoes;
using Catalogo.Application.Mapeamentos;
using Catalogo.Domain.Acesso;
using Catalogo.Domain.Aprovacao;
using Catalogo.Domain.Excecoes;
using Catalogo.Domain.Historico;
using Catalogo.Domain.Metadados;
using Catalogo.Domain.Registros;

namespace Catalogo.Application.Comum;

/// <summary>De onde vem uma gravação, para o histórico: o CorrelacaoId e a origem (nula = cadastro manual).</summary>
public sealed record OrigemGravacao(Guid CorrelacaoId, OrigemHistorico? Origem)
{
    /// <summary>Cadastro manual (tela ou API): um CorrelacaoId novo, sem origem.</summary>
    public static OrigemGravacao Manual() => new(Guid.NewGuid(), null);
}

/// <summary>
/// O ÚNICO caminho de gravação de um registro: criar e alterar, pelo cadastro manual (<c>CriarRegistro</c>, <c>AtualizarRegistro</c>)
/// e pela importação (RN97). Equipe e permissão (RN26, RN27, RN34, RN35), validação completa (<see cref="PreparadorRegistro"/>:
/// RN01 a RN09, RN29, RN38), dados sincronizados (<see cref="SincronizadorRegistro"/>), situação de aprovação e histórico (RN32).
/// NÃO chama o SaveChanges: quem usa grava (um registro no cadastro manual; todos numa transação na importação). Assim nenhuma regra
/// existe em dois lugares.
/// </summary>
public sealed class GravadorRegistro(
    IRegistroRepository registros,
    ResolvedorEquipe resolvedorEquipe,
    ContextoAprovacao contextoAprovacao,
    PreparadorRegistro preparador,
    ServicoHistorico historico,
    TimeProvider relogio)
{
    /// <summary>
    /// Prepara um registro novo e o deixa pronto para o SaveChanges (com o histórico). "equipeId" vem nos dados (chave reservada).
    /// Lança <see cref="ValidacaoRegistroException"/>, <see cref="AcessoNegadoException"/>, <see cref="RecursoNaoEncontradoException"/>
    /// ou <see cref="ConflitoUnicidadeException"/>.
    /// </summary>
    public async Task<Registro> CriarAsync(
        Grupo grupo, Entidade entidade, UsuarioAtual usuario, JsonObject dados, OrigemGravacao origem,
        CancellationToken cancellationToken, LotePreparacao? lote = null)
    {
        // Equipe do registro (chave reservada "equipeId", RN27) e permissão de criar (RN26), considerando a aprovação da entidade
        // nessa equipe: com ela ativa, o EXTERNO também cria (RN34).
        var equipeId = await resolvedorEquipe.ResolverAsync(usuario, entidade, ResolvedorEquipe.LerDoCorpo(dados), cancellationToken);
        var alvo = await contextoAprovacao.NovoAsync(entidade, equipeId, cancellationToken);
        Permissoes.Exigir(PoliticaPermissao.Decidir(usuario, OperacaoRegistro.Criar, alvo), $"Equipe {equipeId}", CodigosErro.EquipeNaoEncontrada);

        // Validação completa (campos, referências, equipes, dependências) e verificação prévia de unicidade.
        var preparado = await preparador.PrepararAsync(
            grupo, entidade, dados, registroAtualId: null, equipeId, usuario, autorDoRegistro: usuario.Id, referenciasAtuais: [],
            cancellationToken, lote);

        // Dados, referências, valores tipados e chaves únicas, gravados juntos.
        var registro = new Registro
        {
            EquipeId = equipeId,
            CriadoPorId = usuario.Id,
            // RN34: EXTERNO com aprovação ativa → PENDENTE; os demais → APROVADO.
            SituacaoAprovacao = FluxoAprovacao.SituacaoInicial(usuario, alvo.RequerAprovacao),
        };
        SincronizadorRegistro.Aplicar(registro, entidade, preparado.Validado, preparado.ChavesUnicas);
        registros.Adicionar(registro);

        // Histórico no mesmo SaveChanges (RN32). O Id do registro já foi gerado ao adicionar.
        historico.RegistrarRegistro(OperacaoHistorico.Criacao, registro, usuario, origem.CorrelacaoId,
            antes: null, depois: registro.ParaJsonPlano(entidade), origem.Origem);
        return registro;
    }

    /// <summary>
    /// O usuário pode alterar este registro (404 se não o vê, 403 se o perfil não permite)? Com aprovação ativa, o EXTERNO altera só
    /// os seus registros PENDENTES ou REJEITADOS (RN35). O <see cref="AlterarAsync"/> já confere; o PUT confere antes da versão.
    /// </summary>
    public async Task ExigirPermissaoDeAlterarAsync(Entidade entidade, UsuarioAtual usuario, Registro registro, CancellationToken cancellationToken)
    {
        var alvo = await contextoAprovacao.ExistenteAsync(entidade, registro, cancellationToken);
        Permissoes.Exigir(PoliticaPermissao.Decidir(usuario, OperacaoRegistro.Alterar, alvo), $"Registro {registro.Id} em '{entidade.Nome}'");
    }

    /// <summary>
    /// Substitui os dados de um registro existente (carregado para alteração) e deixa tudo pronto para o SaveChanges. A conferência
    /// da "versao" (RN12) é de quem chama. Lança <see cref="RecursoNaoEncontradoException"/>, <see cref="AcessoNegadoException"/>,
    /// <see cref="ValidacaoRegistroException"/> ou <see cref="ConflitoUnicidadeException"/>.
    /// </summary>
    public async Task AlterarAsync(
        Grupo grupo, Entidade entidade, UsuarioAtual usuario, Registro registro, JsonObject dados, OrigemGravacao origem,
        CancellationToken cancellationToken, LotePreparacao? lote = null)
    {
        await ExigirPermissaoDeAlterarAsync(entidade, usuario, registro, cancellationToken);

        // A mesma validação do criar, com a equipe do registro. O próprio registro não conflita consigo.
        var preparado = await preparador.PrepararAsync(
            grupo, entidade, dados, registro.Id, registro.EquipeId, usuario, registro.CriadoPorId ?? usuario.Id, registro.Referencias,
            cancellationToken, lote);

        // Substitui Dados e sincroniza referências, valores tipados e chaves únicas.
        var antes = registro.ParaJsonPlano(entidade);
        SincronizadorRegistro.Aplicar(registro, entidade, preparado.Validado, preparado.ChavesUnicas);

        // RN35: o EXTERNO autor alterando um REJEITADO faz um REENVIO (volta para PENDENTE).
        var situacaoAntes = registro.SituacaoAprovacao;
        registro.SituacaoAprovacao = FluxoAprovacao.SituacaoAposAlteracao(usuario, registro);

        registro.AtualizadoEm = relogio.GetUtcNow().UtcDateTime;
        registro.AtualizadoPorId = usuario.Id;
        var depois = registro.ParaJsonPlano(entidade);
        if (registro.SituacaoAprovacao != situacaoAntes)
        {
            // O reenvio aparece no histórico como a mudança de situação, junto com os campos.
            antes["situacaoAprovacao"] = AprovacaoConversor.ParaTexto(situacaoAntes);
            depois["situacaoAprovacao"] = AprovacaoConversor.ParaTexto(registro.SituacaoAprovacao);
        }

        historico.RegistrarRegistro(OperacaoHistorico.Alteracao, registro, usuario, origem.CorrelacaoId, antes, depois, origem.Origem);
    }
}
