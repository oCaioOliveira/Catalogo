using Catalogo.Application.Abstracoes;
using Catalogo.Application.Comum;
using Catalogo.Application.Mapeamentos;
using Catalogo.Domain.Acesso;
using Catalogo.Domain.Excecoes;
using Catalogo.Domain.Exclusao;
using Catalogo.Domain.Historico;
using Catalogo.Domain.Metadados;

namespace Catalogo.Application.CasosDeUso;

/// <summary>
/// Caso de uso: excluir um registro (exclusão física, RN11 e RN16). Primeiro monta o plano de
/// exclusão (CASCATA/RESTRINGIR, no Domain) e só exclui se não houver nenhum bloqueio e se o usuário
/// puder excluir TODOS os registros do plano. Grava uma linha de histórico por registro excluído,
/// e uma por avaliação removida junto (alvo AVALIACAO), todas com o mesmo CorrelacaoId (RN32, RN45).
/// Lança <see cref="RecursoNaoEncontradoException"/>, <see cref="AcessoNegadoException"/> ou
/// <see cref="ExclusaoBloqueadaException"/>.
/// </summary>
public sealed class ExcluirRegistro(
    IUsuarioAtual usuarioAtual,
    IMetadadosProvider metadados,
    IRegistroRepository registros,
    IModeloAvaliacaoRepository modelos,
    IAvaliacaoRepository avaliacoes,
    ServicoHistorico historico)
{
    /// <summary>Quantos rótulos de exemplo mostrar por dependência que bloqueia.</summary>
    public const int MaximoExemplos = 5;

    public async Task ExecutarAsync(string grupoChave, string entidadeChave, Guid id, CancellationToken cancellationToken)
    {
        var usuario = await usuarioAtual.ObterAsync(cancellationToken);
        (var grupo, var entidade, usuario) = await metadados.ObterEntidadeOuFalharAsync(usuario, grupoChave, entidadeChave, UsoEntidade.Escrita, cancellationToken);

        var registro = await registros.ObterPorIdAsync(id, cancellationToken);
        var descricao = $"Registro {id} em '{entidade.Nome}'";
        if (registro is null || registro.EntidadeId != entidade.Id)
            throw new RecursoNaoEncontradoException($"{descricao} não encontrado.", CodigosErro.RegistroNaoEncontrado);
        // A aprovação não muda quem exclui (o EXTERNO nunca exclui), mas a situação afeta quem vê (RN37).
        Permissoes.Exigir(PoliticaPermissao.Decidir(usuario, OperacaoRegistro.Excluir, RegistroAlvo.De(entidade, registro, false)), descricao);

        // O Domain decide quem entra no plano; aqui só buscamos as referências, nível a nível.
        var planejador = new PlanejadorExclusao(id);
        for (var pendentes = planejador.ObterPendentes(); pendentes.Count > 0; pendentes = planejador.ObterPendentes())
            planejador.Registrar(pendentes, await registros.ObterReferenciasQueApontamParaAsync(pendentes, cancellationToken));

        var plano = planejador.Concluir();
        if (!plano.PodeExcluir)
            throw new ExclusaoBloqueadaException(await DescreverBloqueiosAsync(grupo, plano.Bloqueios, cancellationToken));

        // O plano inteiro precisa estar dentro do que o usuário pode excluir. Como referências entre
        // equipes são bloqueadas (RN29), isso vale por construção; conferimos mesmo assim.
        var noPlano = await registros.ObterPorIdsAsync(plano.OrdemExclusao, cancellationToken);
        var correlacaoId = Guid.NewGuid();
        foreach (var alvo in noPlano)
        {
            var entidadeAlvo = grupo.ObterEntidade(alvo.EntidadeId)
                ?? throw new AcessoNegadoException("A exclusão em cascata alcança registros de outro grupo.");
            if (PoliticaPermissao.Decidir(usuario, OperacaoRegistro.Excluir, RegistroAlvo.De(entidadeAlvo, alvo, false)) != DecisaoPermissao.Permitido)
                throw new AcessoNegadoException("A exclusão em cascata alcança registros que você não pode excluir.");

            historico.RegistrarRegistro(OperacaoHistorico.Exclusao, alvo, usuario, correlacaoId,
                antes: alvo.ParaJsonPlano(entidadeAlvo), depois: null);
        }

        // As avaliações dos registros excluídos saem junto (CASCADE no banco); o histórico guarda o que existia (RN45).
        var avaliacoesNoPlano = await avaliacoes.ListarDosRegistrosAsync(plano.OrdemExclusao, cancellationToken);
        if (avaliacoesNoPlano.Count > 0)
        {
            var modelosPorId = (await modelos.ObterPorIdsAsync(
                avaliacoesNoPlano.Select(a => a.ModeloAvaliacaoId).Distinct().ToList(), cancellationToken)).ToDictionary(m => m.Id);
            var registrosPorId = noPlano.ToDictionary(r => r.Id);
            foreach (var avaliacao in avaliacoesNoPlano)
            {
                historico.RegistrarAvaliacao(OperacaoHistorico.Exclusao, registrosPorId[avaliacao.RegistroId], usuario.Id, usuario.Matricula,
                    correlacaoId, avaliacao.ParaJsonHistorico(modelosPorId[avaliacao.ModeloAvaliacaoId]), null);
            }
        }

        // Grava o histórico e exclui, na mesma transação.
        await registros.ExcluirAsync(plano, cancellationToken);
    }

    /// <summary>Agrupa os bloqueios por entidade e campo, com até 5 rótulos de exemplo (buscados numa consulta só).</summary>
    private async Task<IReadOnlyList<DependenciaBloqueante>> DescreverBloqueiosAsync(
        Grupo grupo, IReadOnlyList<ReferenciaEntrante> bloqueios, CancellationToken cancellationToken)
    {
        var grupos = bloqueios
            .GroupBy(b => (b.EntidadeOrigemId, b.CampoId))
            .Select(g => (Chave: g.Key, Origens: g.Select(b => b.RegistroOrigemId).Distinct().ToList()))
            .ToList();

        var idsExemplo = grupos.SelectMany(g => g.Origens.Take(MaximoExemplos)).Distinct().ToList();
        var rotulos = await registros.ObterRotulosAsync(idsExemplo, cancellationToken);

        return grupos
            .Select(g =>
            {
                var entidadeOrigem = grupo.ObterEntidade(g.Chave.EntidadeOrigemId);
                var campo = entidadeOrigem?.ObterCampo(g.Chave.CampoId);
                return new DependenciaBloqueante(
                    entidadeOrigem?.Chave ?? g.Chave.EntidadeOrigemId.ToString(),
                    campo?.Chave ?? g.Chave.CampoId.ToString(),
                    g.Origens.Count,
                    g.Origens.Take(MaximoExemplos).Select(o => rotulos.GetValueOrDefault(o) ?? o.ToString()).ToList());
            })
            .OrderBy(d => d.Entidade, StringComparer.Ordinal)
            .ThenBy(d => d.Campo, StringComparer.Ordinal)
            .ToList();
    }
}
