using Catalogo.Application.Abstracoes;
using Catalogo.Application.Dtos;
using Catalogo.Application.Mapeamentos;
using Catalogo.Domain.Acesso;
using Catalogo.Domain.Consultas;
using Catalogo.Domain.Excecoes;
using Catalogo.Domain.Importacoes;
using Catalogo.Domain.Metadados;
using Catalogo.Domain.Validacao;

namespace Catalogo.Application.Comum;

/// <summary>
/// O que todos os casos de uso da importação fazem antes de trabalhar (RN97): entrar no tipo de item, conferir se o usuário pode
/// importar nele, achar a importação (404 se ele não a vê), ler o arquivo guardado e montar as respostas. As REGRAS de quem pode
/// estão na <see cref="PoliticaPermissao"/>.
/// </summary>
public sealed class AcessoImportacao(
    IUsuarioAtual usuarioAtual,
    IMetadadosProvider metadados,
    IImportacaoRepository importacoes,
    IArquivosImportacao arquivos,
    IUsuarioRepository usuarios,
    ContextoAprovacao contextoAprovacao,
    TimeProvider relogio)
{
    /// <summary>
    /// Entra no tipo de item para importar: 404 se não o vê (RN90), 409 se está desativado (RN75) e 403 se o usuário não pode criar
    /// itens nele em nenhuma das suas equipes (importar = criar, RN97).
    /// </summary>
    public async Task<(Grupo Grupo, Entidade Entidade, UsuarioAtual Usuario)> EntrarParaImportarAsync(
        string grupoChave, string entidadeChave, CancellationToken cancellationToken)
    {
        var usuario = await usuarioAtual.ObterAsync(cancellationToken);
        (var grupo, var entidade, usuario) = await metadados.ObterEntidadeOuFalharAsync(
            usuario, grupoChave, entidadeChave, UsoEntidade.Escrita, cancellationToken);

        var comAprovacao = new HashSet<Guid>();
        foreach (var equipe in entidade.EhGlobal ? new HashSet<Guid>() : usuario.Equipes)
        {
            if (await contextoAprovacao.RequerAprovacaoAsync(entidade, equipe, cancellationToken))
                comAprovacao.Add(equipe);
        }

        if (!PoliticaPermissao.PodeImportar(usuario, entidade, comAprovacao))
            throw new AcessoNegadoException($"Você não pode criar itens em '{entidade.Nome}', então não pode importar uma planilha.");
        return (grupo, entidade, usuario);
    }

    /// <summary>Entra no tipo de item só para ler (o histórico de importações e o relatório).</summary>
    public async Task<(Grupo Grupo, Entidade Entidade, UsuarioAtual Usuario)> EntrarParaLerAsync(
        string grupoChave, string entidadeChave, UsoEntidade uso, CancellationToken cancellationToken)
    {
        var usuario = await usuarioAtual.ObterAsync(cancellationToken);
        return await metadados.ObterEntidadeOuFalharAsync(usuario, grupoChave, entidadeChave, uso, cancellationToken);
    }

    /// <summary>
    /// A importação (rastreada), se é deste tipo de item e o usuário a vê (RN97); senão, 404. <paramref name="somenteAutor"/>: a
    /// prévia e a execução são só de quem enviou o arquivo (403 para quem só a vê).
    /// </summary>
    public async Task<Importacao> ObterAsync(
        UsuarioAtual usuario, Entidade entidade, Guid id, bool somenteAutor, CancellationToken cancellationToken)
    {
        var importacao = await importacoes.ObterAsync(id, cancellationToken);
        var visivel = importacao is not null && importacao.EntidadeId == entidade.Id
            && (importacao.UsuarioId == usuario.Id
                || PoliticaPermissao.PodeVerImportacao(usuario, importacao.UsuarioId,
                    await importacoes.ObterEquipesDoUsuarioAsync(importacao.UsuarioId, cancellationToken)));
        if (!visivel)
            throw new RecursoNaoEncontradoException($"Importação {id} não encontrada.", CodigosErro.ImportacaoNaoEncontrada);
        if (somenteAutor && importacao!.UsuarioId != usuario.Id)
            throw new AcessoNegadoException("Só quem enviou o arquivo pode fazer a prévia e importar.");
        return importacao!;
    }

    /// <summary>O arquivo guardado, já lido. 409 se a limpeza já o apagou (RN104).</summary>
    public async Task<PlanilhaImportada> LerArquivoAsync(Importacao importacao, CancellationToken cancellationToken)
    {
        var conteudo = await importacoes.ObterConteudoAsync(importacao.Id, cancellationToken)
            ?? throw new RegraDeNegocioException(
                $"O arquivo desta importação foi apagado: os arquivos ficam guardados por {LimitesImportacao.DiasRetencaoArquivo} dias. Envie de novo.",
                CodigosErro.ImportacaoArquivoExpirado);
        return arquivos.Ler(conteudo, importacao.Formato);
    }

    /// <summary>Confere a situação antes de uma operação: 409 IMPORTACAO_SITUACAO_INVALIDA com a situação atual.</summary>
    public static void ExigirSituacao(Importacao importacao, string mensagem, params SituacaoImportacao[] aceitas)
    {
        if (!aceitas.Contains(importacao.Situacao))
        {
            throw new RegraDeNegocioException(mensagem, CodigosErro.ImportacaoSituacaoInvalida,
                new Dictionary<string, object?> { ["situacao"] = ImportacaoConversor.ParaTexto(importacao.Situacao) });
        }
    }

    public async Task<ImportacaoResumoDto> ResumoAsync(Importacao importacao, UsuarioAtual usuario, CancellationToken cancellationToken)
    {
        var autores = await usuarios.ObterResumosAsync([importacao.UsuarioId], cancellationToken);
        return Resumo(importacao, usuario, autores);
    }

    public ImportacaoResumoDto Resumo(
        Importacao importacao, UsuarioAtual usuario, IReadOnlyDictionary<Guid, UsuarioResumoDto> autores)
    {
        var resumo = ImportacaoMapeamentos.DeJson<ResumoImportacao>(importacao.Resumo);
        var arquivoDisponivel = importacao.Situacao != SituacaoImportacao.Expirada
                                && importacao.CriadoEm > relogio.GetUtcNow().UtcDateTime.AddDays(-LimitesImportacao.DiasRetencaoArquivo);
        return new ImportacaoResumoDto(
            importacao.Id,
            importacao.NomeArquivo,
            ImportacaoConversor.ParaTexto(importacao.Formato),
            ImportacaoConversor.ParaTexto(importacao.Situacao),
            importacao.CriadoEm,
            importacao.ConcluidaEm,
            importacao.DesfeitaEm,
            autores.GetValueOrDefault(importacao.UsuarioId) ?? new UsuarioResumoDto(importacao.UsuarioId, "", ""),
            resumo?.Totais.ParaDto(),
            arquivoDisponivel,
            // RN103: desfazer é excluir; quem não exclui itens (INTERNO, EXTERNO) não desfaz.
            importacao.Situacao == SituacaoImportacao.Concluida && resumo?.Totais.Novos > 0
                && PoliticaPermissao.PerfilPermite(usuario.Perfil, OperacaoRegistro.Excluir));
    }

    /// <summary>
    /// A prévia (ou o resultado) para a tela: as linhas da página pedida, com as células do arquivo. "somenteComErro=true" filtra.
    /// </summary>
    public async Task<PreviaImportacaoDto> PreviaAsync(
        Importacao importacao, UsuarioAtual usuario, PlanilhaImportada planilha, IEnumerable<KeyValuePair<string, string>> parametros,
        CancellationToken cancellationToken)
    {
        var lista = parametros.ToList();
        var erros = new ErrosPorCampo();
        var paginacao = InterpretadorConsulta.InterpretarPaginacao(lista, erros);
        var somenteComErro = false;
        if (lista.FirstOrDefault(p => p.Key == "somenteComErro") is { Key: not null } filtro && !bool.TryParse(filtro.Value, out somenteComErro))
            erros.Adicionar("somenteComErro", "Use true ou false.");
        erros.LancarSeHouverErros();

        var resumo = ImportacaoMapeamentos.DeJson<ResumoImportacao>(importacao.Resumo)
            ?? new ResumoImportacao(new TotaisImportacao(0, 0, 0, 0), [], false);
        // Antes da primeira prévia ainda não há mapeamento escolhido.
        var mapeamento = ImportacaoMapeamentos.DeJson<MapeamentoImportacao>(importacao.Mapeamento) ?? new MapeamentoImportacao([]);
        var opcoes = ImportacaoMapeamentos.DeJson<OpcoesImportacao>(importacao.Opcoes) ?? OpcoesImportacao.Padrao;

        var filtradas = somenteComErro ? resumo.Linhas.Where(l => l.Situacao == SituacaoLinha.Erro).ToList() : resumo.Linhas;
        var originais = planilha.Linhas.ToDictionary(l => l.Numero);
        var itens = filtradas
            .Skip(paginacao.Pular)
            .Take(paginacao.Tamanho)
            .Select(l => l.ParaDto(originais.GetValueOrDefault(l.Numero), planilha.Cabecalhos.Count))
            .ToList();

        return new PreviaImportacaoDto(
            await ResumoAsync(importacao, usuario, cancellationToken),
            resumo.Totais.ParaDto(),
            resumo.Executavel,
            planilha.Cabecalhos,
            mapeamento.Colunas.Select(c => new ColunaMapeadaEntrada(c.Indice, c.Destino.ParaDto())).ToList(),
            opcoes.ParaDto(),
            new PaginaDto<LinhaImportacaoDto>(itens, paginacao.Pagina, paginacao.Tamanho, filtradas.Count, paginacao.TotalPaginas(filtradas.Count)));
    }
}
