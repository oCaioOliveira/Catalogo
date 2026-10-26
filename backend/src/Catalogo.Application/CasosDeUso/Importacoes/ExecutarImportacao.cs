using Catalogo.Application.Abstracoes;
using Catalogo.Application.Comum;
using Catalogo.Application.Dtos;
using Catalogo.Application.Mapeamentos;
using Catalogo.Domain.Excecoes;
using Catalogo.Domain.Historico;
using Catalogo.Domain.Importacoes;

namespace Catalogo.Application.CasosDeUso.Importacoes;

/// <summary>
/// Caso de uso: executar uma importação validada (POST .../importacoes/{id}/executar, RN101). Executa exatamente o que a prévia
/// mostrou: simula de novo, pelo mesmo caminho do cadastro manual (<see cref="GravadorRegistro"/>), e, se o resultado de cada linha
/// for o mesmo da prévia, grava TODOS os itens e o histórico (com o CorrelacaoId da importação e a origem IMPORTACAO) num único
/// SaveChanges, uma transação. Se algo mudou desde a prévia (um item referenciado foi excluído, alguém criou o mesmo item...), não grava
/// nada, guarda a prévia nova e responde 409 IMPORTACAO_DADOS_MUDARAM.
/// </summary>
public sealed class ExecutarImportacao(
    AcessoImportacao acesso,
    SimuladorImportacao simulador,
    IImportacaoRepository importacoes,
    IUnidadeDeTrabalho unidadeDeTrabalho,
    TimeProvider relogio)
{
    public async Task<ImportacaoResumoDto> ExecutarAsync(string grupoChave, string entidadeChave, Guid id, CancellationToken cancellationToken)
    {
        var (grupo, entidade, usuario) = await acesso.EntrarParaImportarAsync(grupoChave, entidadeChave, cancellationToken);
        var importacao = await acesso.ObterAsync(usuario, entidade, id, somenteAutor: true, cancellationToken);
        AcessoImportacao.ExigirSituacao(importacao, importacao.Situacao == SituacaoImportacao.Enviada
                ? "Faça a prévia antes de importar."
                : "Esta importação já foi executada (ou expirou).",
            SituacaoImportacao.Validada);

        var previa = ImportacaoMapeamentos.DeJson<ResumoImportacao>(importacao.Resumo)!;
        if (!previa.Executavel)
        {
            throw new RegraDeNegocioException(previa.Totais.Novos + previa.Totais.Atualizacoes > 0
                    ? "A opção é \"tudo ou nada\" e há linhas com erro: corrija a planilha e envie de novo."
                    : "Não há nenhuma linha para importar.",
                CodigosErro.ImportacaoSituacaoInvalida,
                new Dictionary<string, object?> { ["situacao"] = ImportacaoConversor.ParaTexto(importacao.Situacao) });
        }

        var planilha = await acesso.LerArquivoAsync(importacao, cancellationToken);
        var mapeamento = ImportacaoMapeamentos.DeJson<MapeamentoImportacao>(importacao.Mapeamento)!;
        var opcoes = ImportacaoMapeamentos.DeJson<OpcoesImportacao>(importacao.Opcoes)!;

        var resultado = await simulador.SimularAsync(grupo, entidade, usuario, planilha, mapeamento, opcoes,
            new OrigemGravacao(importacao.CorrelacaoId, OrigemHistorico.Importacao), manterParaGravar: true, cancellationToken);

        if (!resultado.Executavel || !resultado.MesmoResultadoQue(previa))
            await RecusarPorqueMudouAsync(id, resultado, cancellationToken);

        importacao.Resumo = ImportacaoMapeamentos.ParaJson(resultado);
        importacao.Situacao = SituacaoImportacao.Concluida;
        importacao.ConcluidaEm = relogio.GetUtcNow().UtcDateTime;
        try
        {
            // Itens, valores, referências, chaves únicas, histórico e a importação: tudo ou nada, numa transação.
            await unidadeDeTrabalho.SalvarAlteracoesAsync(cancellationToken);
        }
        catch (ViolacaoUnicidadeBancoException)
        {
            // Corrida: alguém gravou o mesmo item entre a simulação e o SaveChanges. Nada foi gravado.
            await RecusarPorqueMudouAsync(id, null, cancellationToken);
        }

        return await acesso.ResumoAsync(importacao, usuario, cancellationToken);
    }

    /// <summary>Descarta o que foi preparado, guarda a prévia nova (se houver) e responde 409 IMPORTACAO_DADOS_MUDARAM.</summary>
    private async Task RecusarPorqueMudouAsync(Guid id, ResumoImportacao? nova, CancellationToken cancellationToken)
    {
        unidadeDeTrabalho.DescartarAlteracoes();
        if (nova is not null && await importacoes.ObterAsync(id, cancellationToken) is { } importacao)
        {
            importacao.Resumo = ImportacaoMapeamentos.ParaJson(nova);
            await unidadeDeTrabalho.SalvarAlteracoesAsync(cancellationToken);
        }

        throw new RegraDeNegocioException(
            "Os dados mudaram desde a prévia (por exemplo, um item referenciado foi excluído ou outro igual foi criado). Nada foi " +
            "importado: confira a prévia atualizada e confirme de novo.",
            CodigosErro.ImportacaoDadosMudaram,
            nova is null ? null : new Dictionary<string, object?> { ["totais"] = nova.Totais.ParaDto() });
    }
}
