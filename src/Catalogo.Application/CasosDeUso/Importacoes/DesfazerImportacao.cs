using Catalogo.Application.Abstracoes;
using Catalogo.Application.Comum;
using Catalogo.Application.Dtos;
using Catalogo.Application.Mapeamentos;
using Catalogo.Domain.Acesso;
using Catalogo.Domain.Excecoes;
using Catalogo.Domain.Exclusao;
using Catalogo.Domain.Historico;
using Catalogo.Domain.Importacoes;

namespace Catalogo.Application.CasosDeUso.Importacoes;

/// <summary>
/// Caso de uso: desfazer uma importação concluída (POST .../importacoes/{id}/desfazer, RN103). Remove só os itens que ela CRIOU (as
/// atualizações ficam), pelo MESMO plano de exclusão da exclusão manual (RN16), numa transação, com uma linha de histórico EXCLUSAO por
/// item (CorrelacaoId e origem da importação). Desfazer é excluir: precisa poder excluir cada item (RN26: GESTOR e ADMINISTRADOR).
/// Recusa com 409 IMPORTACAO_NAO_PODE_SER_DESFEITA, dizendo o motivo e os itens, se algum item foi alterado depois ou se um item de
/// fora aponta para eles (RESTRINGIR bloquearia; CASCATA levaria junto o que não veio da importação).
/// </summary>
public sealed class DesfazerImportacao(
    AcessoImportacao acesso,
    IRegistroRepository registros,
    IConsultasImportacao consultas,
    ServicoHistorico historico,
    IUnidadeDeTrabalho unidadeDeTrabalho,
    TimeProvider relogio)
{
    /// <summary>Quantos itens de exemplo mostrar por motivo.</summary>
    public const int MaximoExemplos = 10;

    public async Task<ImportacaoResumoDto> ExecutarAsync(string grupoChave, string entidadeChave, Guid id, CancellationToken cancellationToken)
    {
        var (grupo, entidade, usuario) = await acesso.EntrarParaLerAsync(grupoChave, entidadeChave, UsoEntidade.Escrita, cancellationToken);
        var importacao = await acesso.ObterAsync(usuario, entidade, id, somenteAutor: false, cancellationToken);
        AcessoImportacao.ExigirSituacao(importacao, importacao.Situacao == SituacaoImportacao.Desfeita
                ? "Esta importação já foi desfeita."
                : "Só uma importação concluída pode ser desfeita.",
            SituacaoImportacao.Concluida);

        var resumo = ImportacaoMapeamentos.DeJson<ResumoImportacao>(importacao.Resumo)!;
        var criados = resumo.Linhas.Where(l => l.Situacao == SituacaoLinha.Nova).Select(l => l.RegistroId).OfType<Guid>().ToHashSet();

        // Os que ainda existem (alguém pode ter excluído um deles à mão).
        var existentes = await registros.ObterPorIdsAsync(criados, cancellationToken);
        foreach (var item in existentes)
        {
            if (PoliticaPermissao.Decidir(usuario, OperacaoRegistro.Excluir, RegistroAlvo.De(entidade, item, false)) != DecisaoPermissao.Permitido)
                throw new AcessoNegadoException("Desfazer a importação exclui os itens criados por ela, e você não pode excluí-los.");
        }

        importacao.Situacao = SituacaoImportacao.Desfeita;
        importacao.DesfeitaEm = relogio.GetUtcNow().UtcDateTime;
        if (existentes.Count == 0)
        {
            await unidadeDeTrabalho.SalvarAlteracoesAsync(cancellationToken);
            return await acesso.ResumoAsync(importacao, usuario, cancellationToken);
        }

        var ids = existentes.Select(r => r.Id).ToList();
        var planejador = new PlanejadorExclusao(ids);
        for (var pendentes = planejador.ObterPendentes(); pendentes.Count > 0; pendentes = planejador.ObterPendentes())
            planejador.Registrar(pendentes, await registros.ObterReferenciasQueApontamParaAsync(pendentes, cancellationToken));
        var plano = planejador.Concluir();

        var alterados = await consultas.AlteradosForaDaCorrelacaoAsync(ids, importacao.CorrelacaoId, cancellationToken);
        var impedimentos = RegrasDesfazer.Avaliar(criados, alterados, plano);
        if (impedimentos.Count > 0)
            throw await RecusaAsync(impedimentos, cancellationToken);

        foreach (var item in existentes)
        {
            historico.RegistrarRegistro(OperacaoHistorico.Exclusao, item, usuario, importacao.CorrelacaoId,
                antes: item.ParaJsonPlano(entidade), depois: null, OrigemHistorico.Importacao);
        }

        // Grava o histórico e a situação da importação e exclui, na mesma transação (RN16).
        await registros.ExcluirAsync(plano, cancellationToken);
        return await acesso.ResumoAsync(importacao, usuario, cancellationToken);
    }

    private async Task<RegraDeNegocioException> RecusaAsync(IReadOnlyList<ImpedimentoDesfazer> impedimentos, CancellationToken cancellationToken)
    {
        var exemplos = impedimentos.SelectMany(i => i.Itens.Take(MaximoExemplos)).Distinct().ToList();
        var rotulos = await registros.ObterRotulosAsync(exemplos, cancellationToken);
        var detalhes = impedimentos
            .Select(i => new
            {
                motivo = i.Motivo,
                quantidade = i.Itens.Count,
                itens = i.Itens.Take(MaximoExemplos).Select(item => new { id = item, rotulo = rotulos.GetValueOrDefault(item) }).ToList(),
            })
            .ToList();

        return new RegraDeNegocioException(
            "A importação não pode ser desfeita: " + string.Join(" ", impedimentos.Select(i => i.Motivo)),
            CodigosErro.ImportacaoNaoPodeSerDesfeita,
            new Dictionary<string, object?> { ["impedimentos"] = detalhes });
    }
}
