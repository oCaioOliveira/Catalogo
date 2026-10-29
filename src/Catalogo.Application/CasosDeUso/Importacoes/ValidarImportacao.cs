using Catalogo.Application.Abstracoes;
using Catalogo.Application.Comum;
using Catalogo.Application.Dtos;
using Catalogo.Application.Mapeamentos;
using Catalogo.Domain.Historico;
using Catalogo.Domain.Importacoes;
using Catalogo.Domain.Metadados;
using Catalogo.Domain.Validacao;

namespace Catalogo.Application.CasosDeUso.Importacoes;

/// <summary>
/// Caso de uso: a PRÉVIA de uma importação (POST .../importacoes/{id}/validar, RN97 a RN102). Recebe o mapeamento e as opções e faz a
/// simulação completa, linha a linha, pelo mesmo caminho do cadastro manual (<see cref="SimuladorImportacao"/>), sem gravar nenhum
/// item. Guarda o mapeamento, as opções e o resultado de cada linha (a execução confere se nada mudou desde aqui).
/// </summary>
public sealed class ValidarImportacao(
    AcessoImportacao acesso,
    SimuladorImportacao simulador,
    IImportacaoRepository importacoes,
    IUnidadeDeTrabalho unidadeDeTrabalho)
{
    public async Task<PreviaImportacaoDto> ExecutarAsync(
        string grupoChave, string entidadeChave, Guid id, ValidarImportacaoEntrada entrada,
        IEnumerable<KeyValuePair<string, string>> parametros, CancellationToken cancellationToken)
    {
        var (grupo, entidade, usuario) = await acesso.EntrarParaImportarAsync(grupoChave, entidadeChave, cancellationToken);
        var importacao = await acesso.ObterAsync(usuario, entidade, id, somenteAutor: true, cancellationToken);
        AcessoImportacao.ExigirSituacao(importacao, "Esta importação já foi executada (ou expirou): envie o arquivo de novo para outra prévia.",
            SituacaoImportacao.Enviada, SituacaoImportacao.Validada);

        var planilha = await acesso.LerArquivoAsync(importacao, cancellationToken);
        var (mapeamento, opcoes) = Interpretar(entidade, planilha, entrada);

        var resumo = await simulador.SimularAsync(grupo, entidade, usuario, planilha, mapeamento, opcoes,
            new OrigemGravacao(importacao.CorrelacaoId, OrigemHistorico.Importacao), manterParaGravar: false, cancellationToken);

        // A simulação descartou o que estava marcado (inclusive a importação): lê de novo para gravar o resultado.
        importacao = await importacoes.ObterAsync(id, cancellationToken)
            ?? throw new InvalidOperationException($"A importação {id} sumiu durante a prévia.");
        importacao.Mapeamento = ImportacaoMapeamentos.ParaJson(mapeamento);
        importacao.Opcoes = ImportacaoMapeamentos.ParaJson(opcoes);
        importacao.EquipePadraoId = opcoes.EquipePadraoId;
        importacao.Resumo = ImportacaoMapeamentos.ParaJson(resumo);
        importacao.Situacao = SituacaoImportacao.Validada;
        await unidadeDeTrabalho.SalvarAlteracoesAsync(cancellationToken);

        return await acesso.PreviaAsync(importacao, usuario, planilha, parametros, cancellationToken);
    }

    /// <summary>
    /// O corpo da prévia, conferido contra o arquivo e os metadados (400 com todos os erros juntos, RN05): cada coluna existe, cada
    /// destino é um campo ativo (ou o "(ID)" de uma referência, ou a Equipe num tipo de item por equipe) e nenhum destino se repete.
    /// Colunas que não vêm no corpo são ignoradas.
    /// </summary>
    public static (MapeamentoImportacao Mapeamento, OpcoesImportacao Opcoes) Interpretar(
        Entidade entidade, PlanilhaImportada planilha, ValidarImportacaoEntrada entrada)
    {
        var erros = new ErrosPorCampo();
        var destinos = new Dictionary<int, DestinoColuna>();
        var usados = new HashSet<(TipoDestinoColuna, string?)>();

        var lista = entrada.Mapeamento ?? [];
        for (var i = 0; i < lista.Count; i++)
        {
            var coluna = lista[i];
            var chave = $"mapeamento[{i}]";
            if (coluna.Indice < 0 || coluna.Indice >= planilha.Cabecalhos.Count)
            {
                erros.Adicionar($"{chave}.indice", $"O arquivo não tem a coluna {coluna.Indice}.");
                continue;
            }

            var tipo = ImportacaoMapeamentos.TipoDestinoDeTexto(coluna.Destino?.Tipo ?? "IGNORAR");
            if (tipo is null)
            {
                erros.Adicionar($"{chave}.destino.tipo", "Use IGNORAR, CAMPO, CAMPO_ID ou EQUIPE.");
                continue;
            }

            var destino = tipo.Value switch
            {
                TipoDestinoColuna.Ignorar => DestinoColuna.Ignorar,
                TipoDestinoColuna.Equipe => DestinoColuna.Equipe,
                _ => new DestinoColuna(tipo.Value, coluna.Destino?.Campo),
            };

            var campo = destino.CampoChave is string campoChave ? entidade.ObterCampo(campoChave) : null;
            if (destino.Tipo is TipoDestinoColuna.Campo or TipoDestinoColuna.CampoId && campo is not { Ativo: true })
                erros.Adicionar($"{chave}.destino.campo", $"'{destino.CampoChave}' não é um campo ativo de '{entidade.Nome}'.");
            else if (destino.Tipo == TipoDestinoColuna.CampoId && !campo!.EhReferencia)
                erros.Adicionar($"{chave}.destino.campo", $"'{campo.Nome}' não é uma referência: só referências têm a coluna {SugestaoMapeamento.SufixoId}.");
            else if (destino.Tipo == TipoDestinoColuna.Equipe && entidade.EhGlobal)
                erros.Adicionar($"{chave}.destino.tipo", $"'{entidade.Nome}' é um cadastro de apoio: os itens não têm equipe.");
            else if (destino.Tipo != TipoDestinoColuna.Ignorar && !usados.Add((destino.Tipo, destino.CampoChave)))
                erros.Adicionar($"{chave}.destino", "Duas colunas foram ligadas ao mesmo campo.");
            else if (!destinos.TryAdd(coluna.Indice, destino))
                erros.Adicionar($"{chave}.indice", "A coluna aparece duas vezes no mapeamento.");
        }

        var duplicados = ImportacaoMapeamentos.AcaoDuplicadosDeTexto(entrada.Opcoes?.Duplicados ?? "PULAR");
        var linhasComErro = ImportacaoMapeamentos.TratamentoErrosDeTexto(entrada.Opcoes?.LinhasComErro ?? "SO_VALIDAS");
        if (duplicados is null)
            erros.Adicionar("opcoes.duplicados", "Use PULAR ou ATUALIZAR.");
        if (linhasComErro is null)
            erros.Adicionar("opcoes.linhasComErro", "Use SO_VALIDAS ou TUDO_OU_NADA.");
        if (entrada.Opcoes?.EquipePadraoId is not null && entidade.EhGlobal)
            erros.Adicionar("opcoes.equipePadraoId", $"'{entidade.Nome}' é um cadastro de apoio: os itens não têm equipe.");
        erros.LancarSeHouverErros();

        var mapeamento = new MapeamentoImportacao(planilha.Cabecalhos
            .Select((cabecalho, indice) => new ColunaMapeada(indice, cabecalho, destinos.GetValueOrDefault(indice, DestinoColuna.Ignorar)))
            .ToList());
        return (mapeamento, new OpcoesImportacao(duplicados!.Value, linhasComErro!.Value, entrada.Opcoes?.EquipePadraoId));
    }
}
