namespace Catalogo.Domain.Importacoes;

/// <summary>Limites da importação (RN98). Mensagens claras quando passam: ver <see cref="LeitorCsv"/> e a leitura do XLSX.</summary>
public static class LimitesImportacao
{
    public const int TamanhoMaximoBytes = 5 * 1024 * 1024;
    public const int MaximoLinhas = 5000;
    public const int MaximoColunas = 100;

    /// <summary>Soma dos tamanhos descompactados das partes de um XLSX (proteção contra arquivo compactado malicioso).</summary>
    public const long TamanhoDescompactadoMaximo = 100L * 1024 * 1024;

    /// <summary>Referências com até este número de opções visíveis ganham lista suspensa no modelo.</summary>
    public const int MaximoOpcoesListaNoModelo = 1000;

    /// <summary>Depois disso o arquivo é apagado e a importação nunca executada vira EXPIRADA (RN104).</summary>
    public const int DiasRetencaoArquivo = 7;

    /// <summary>Quantos valores de exemplo mostrar por coluna na etapa "Colunas".</summary>
    public const int ExemplosPorColuna = 3;

    /// <summary>Nome da aba de instruções do modelo (ignorada na leitura).</summary>
    public const string AbaInstrucoes = "Instruções";
}

/// <summary>O que fazer com uma linha que já existe no banco (mesmo valor numa restrição única do tipo de item, RN09).</summary>
public enum AcaoDuplicados
{
    /// <summary>A linha é ignorada (o item do banco fica como está).</summary>
    Pular,

    /// <summary>O item do banco é alterado com os valores da linha. Exige permissão de alterar (RN26).</summary>
    Atualizar,
}

/// <summary>O que fazer quando há linhas com erro.</summary>
public enum TratamentoErros
{
    /// <summary>Importa as linhas válidas; as com erro ficam no relatório.</summary>
    SoValidas,

    /// <summary>Não importa nada enquanto houver uma linha com erro.</summary>
    TudoOuNada,
}

/// <summary>As opções da importação, escolhidas na etapa "Opções" (coluna Importacao.Opcoes).</summary>
public sealed record OpcoesImportacao(AcaoDuplicados Duplicados, TratamentoErros LinhasComErro, Guid? EquipePadraoId)
{
    public static OpcoesImportacao Padrao { get; } = new(AcaoDuplicados.Pular, TratamentoErros.SoValidas, null);
}

/// <summary>Para onde vai uma coluna do arquivo.</summary>
public enum TipoDestinoColuna
{
    /// <summary>A coluna não é importada.</summary>
    Ignorar,

    /// <summary>O valor de um campo. Em campos de referência, o NOME do item (o valor do campo de exibição dele, RN99).</summary>
    Campo,

    /// <summary>O identificador (GUID) do item de um campo de referência: a coluna "&lt;campo&gt; (ID)", para nomes repetidos.</summary>
    CampoId,

    /// <summary>O nome da equipe do item (tipos de item por equipe).</summary>
    Equipe,
}

/// <param name="CampoChave">A chave do campo, nos destinos <see cref="TipoDestinoColuna.Campo"/> e <see cref="TipoDestinoColuna.CampoId"/>.</param>
public sealed record DestinoColuna(TipoDestinoColuna Tipo, string? CampoChave = null)
{
    public static DestinoColuna Ignorar { get; } = new(TipoDestinoColuna.Ignorar);
    public static DestinoColuna Equipe { get; } = new(TipoDestinoColuna.Equipe);
}

/// <summary>Uma coluna do arquivo (posição a partir de 0 e o texto do cabeçalho) e o destino dela.</summary>
public sealed record ColunaMapeada(int Indice, string Cabecalho, DestinoColuna Destino);

/// <summary>O mapeamento coluna do arquivo → campo (coluna Importacao.Mapeamento).</summary>
public sealed record MapeamentoImportacao(IReadOnlyList<ColunaMapeada> Colunas);

/// <summary>O que acontece (ou aconteceu) com uma linha.</summary>
public enum SituacaoLinha
{
    /// <summary>Cria um item novo.</summary>
    Nova,

    /// <summary>Altera um item que já existe (opção "atualizar").</summary>
    Atualizacao,

    /// <summary>Já existe no banco e a opção é "pular".</summary>
    Ignorada,

    /// <summary>Recusada: os erros dizem por quê.</summary>
    Erro,
}

/// <summary>
/// Um erro de uma linha. <paramref name="Coluna"/> é a posição da coluna no arquivo (para destacar a célula); nula quando o erro é
/// da linha inteira (ex.: duplicada no arquivo) ou de um campo sem coluna (ex.: obrigatório não mapeado).
/// </summary>
public sealed record ErroLinha(int? Coluna, string? Campo, string Mensagem);

/// <summary>Uma referência resolvida pelo nome (ou pelo ID): mostrada na prévia ("Transferências Alfa" → o item encontrado).</summary>
public sealed record ReferenciaResolvida(int Coluna, string Campo, Guid Id, string? Nome);

/// <param name="Numero">Número da linha na planilha (a do cabeçalho é a 1).</param>
/// <param name="RegistroId">O item criado ou alterado (depois de executar) ou o item do banco que a linha repete (prévia).</param>
public sealed record ResultadoLinha(
    int Numero,
    SituacaoLinha Situacao,
    IReadOnlyList<ErroLinha> Erros,
    IReadOnlyList<ReferenciaResolvida> Referencias,
    Guid? RegistroId);

public sealed record TotaisImportacao(int Novos, int Atualizacoes, int Ignorados, int ComErro)
{
    public int Total => Novos + Atualizacoes + Ignorados + ComErro;

    public static TotaisImportacao Contar(IReadOnlyCollection<ResultadoLinha> linhas) => new(
        linhas.Count(l => l.Situacao == SituacaoLinha.Nova),
        linhas.Count(l => l.Situacao == SituacaoLinha.Atualizacao),
        linhas.Count(l => l.Situacao == SituacaoLinha.Ignorada),
        linhas.Count(l => l.Situacao == SituacaoLinha.Erro));
}

/// <summary>
/// O resultado de uma prévia ou de uma execução (coluna Importacao.Resumo): os totais e cada linha.
/// <paramref name="Executavel"/> é falso quando não há nada a gravar ou quando a opção é "tudo ou nada" e há erros.
/// </summary>
public sealed record ResumoImportacao(TotaisImportacao Totais, IReadOnlyList<ResultadoLinha> Linhas, bool Executavel)
{
    public static ResumoImportacao De(IReadOnlyList<ResultadoLinha> linhas, OpcoesImportacao opcoes)
    {
        var totais = TotaisImportacao.Contar(linhas);
        var aGravar = totais.Novos + totais.Atualizacoes;
        var executavel = aGravar > 0 && !(opcoes.LinhasComErro == TratamentoErros.TudoOuNada && totais.ComErro > 0);
        return new ResumoImportacao(totais, linhas, executavel);
    }

    /// <summary>
    /// A execução encontrou o mesmo que a prévia? Compara a situação de cada linha e o item que ela repete (RN101: se os dados
    /// mudaram entre a prévia e a execução, nada é gravado e a prévia nova é mostrada).
    /// </summary>
    public bool MesmoResultadoQue(ResumoImportacao outro) =>
        Linhas.Count == outro.Linhas.Count
        && Linhas.Zip(outro.Linhas).All(par => par.First.Numero == par.Second.Numero
                                              && par.First.Situacao == par.Second.Situacao
                                              && (par.First.Situacao != SituacaoLinha.Atualizacao
                                                  || par.First.RegistroId == par.Second.RegistroId));
}
