using Catalogo.Application.Abstracoes;
using Catalogo.Application.Comum;
using Catalogo.Application.Dtos;
using Catalogo.Application.Mapeamentos;
using Catalogo.Domain.Importacoes;
using Catalogo.Domain.Metadados;
using Catalogo.Domain.Validacao;

namespace Catalogo.Application.CasosDeUso.Importacoes;

/// <summary>
/// Caso de uso: receber o arquivo de uma importação (POST .../importacoes, RN97 e RN98). Confere o tamanho, o tipo REAL (pelo conteúdo)
/// e os limites de linhas e colunas, guarda o arquivo no banco (funciona igual em qualquer servidor) e devolve as colunas com a
/// sugestão de mapeamento (nome ou chave do campo, sem diferenciar maiúsculas e acentos). Nada é validado contra os dados ainda.
/// </summary>
public sealed class EnviarImportacao(
    AcessoImportacao acesso,
    IArquivosImportacao arquivos,
    IImportacaoRepository importacoes,
    IUnidadeDeTrabalho unidadeDeTrabalho,
    TimeProvider relogio)
{
    private static readonly System.Globalization.CultureInfo Cultura = System.Globalization.CultureInfo.GetCultureInfo("pt-BR");

    public async Task<ImportacaoEnviadaDto> ExecutarAsync(
        string grupoChave, string entidadeChave, string? nomeArquivo, byte[]? conteudo, CancellationToken cancellationToken)
    {
        var (grupo, entidade, usuario) = await acesso.EntrarParaImportarAsync(grupoChave, entidadeChave, cancellationToken);

        var erros = new ErrosPorCampo();
        if (conteudo is null || conteudo.Length == 0 || string.IsNullOrWhiteSpace(nomeArquivo))
            erros.Adicionar(PlanilhaImportada.ChaveErro, "Envie o arquivo (campo \"arquivo\" do formulário).");
        else if (conteudo.Length > LimitesImportacao.TamanhoMaximoBytes)
            erros.Adicionar(PlanilhaImportada.ChaveErro,
                $"O arquivo tem {(conteudo.Length / 1024.0 / 1024.0).ToString("N1", Cultura)} MB; o máximo é {LimitesImportacao.TamanhoMaximoBytes / 1024 / 1024} MB.");
        erros.LancarSeHouverErros();

        var nome = Path.GetFileName(nomeArquivo!.Trim());
        if (nome.Length > 260)
            nome = nome[^260..];
        var formato = arquivos.DetectarFormato(conteudo!, nome);
        var planilha = arquivos.Ler(conteudo!, formato);

        var importacao = new Importacao
        {
            GrupoId = grupo.Id,
            EntidadeId = entidade.Id,
            UsuarioId = usuario.Id,
            NomeArquivo = nome,
            Formato = formato,
            Situacao = SituacaoImportacao.Enviada,
            CorrelacaoId = Guid.NewGuid(),
            CriadoEm = relogio.GetUtcNow().UtcDateTime,
        };
        importacoes.Adicionar(importacao, new ImportacaoArquivo { Conteudo = conteudo! });
        await unidadeDeTrabalho.SalvarAlteracoesAsync(cancellationToken);

        var sugestao = SugestaoMapeamento.Sugerir(entidade, planilha.Cabecalhos);
        return new ImportacaoEnviadaDto(
            importacao.Id,
            importacao.NomeArquivo,
            ImportacaoConversor.ParaTexto(formato),
            ImportacaoConversor.ParaTexto(importacao.Situacao),
            planilha.Linhas.Count,
            sugestao.Colunas.Select(c => new ColunaArquivoDto(c.Indice, c.Cabecalho, planilha.Exemplos(c.Indice), c.Destino.ParaDto())).ToList(),
            Destinos(entidade));
    }

    /// <summary>
    /// Os destinos que o select de cada coluna oferece: os campos ativos na ordem do formulário, o "(ID)" de cada referência e, nos
    /// tipos de item por equipe, a Equipe.
    /// </summary>
    public static IReadOnlyList<DestinoDisponivelDto> Destinos(Entidade entidade)
    {
        var destinos = new List<DestinoDisponivelDto>();
        foreach (var campo in entidade.Campos.Where(c => c.Ativo))
        {
            destinos.Add(new DestinoDisponivelDto(ImportacaoMapeamentos.ParaTexto(TipoDestinoColuna.Campo), campo.Chave, campo.Nome, campo.Obrigatorio));
            if (campo.EhReferencia)
            {
                destinos.Add(new DestinoDisponivelDto(ImportacaoMapeamentos.ParaTexto(TipoDestinoColuna.CampoId), campo.Chave,
                    $"{campo.Nome} {SugestaoMapeamento.SufixoId}", false));
            }
        }

        if (!entidade.EhGlobal)
            destinos.Add(new DestinoDisponivelDto(ImportacaoMapeamentos.ParaTexto(TipoDestinoColuna.Equipe), null, SugestaoMapeamento.CabecalhoEquipe, false));
        return destinos;
    }
}
