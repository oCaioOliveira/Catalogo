namespace Catalogo.Application.Dtos;

/// <summary>
/// Para onde vai uma coluna do arquivo. <see cref="Tipo"/>: IGNORAR, CAMPO (o valor de um campo; numa referência, o NOME do item),
/// CAMPO_ID (o identificador do item de uma referência, a coluna "&lt;campo&gt; (ID)") ou EQUIPE (o nome da equipe).
/// </summary>
/// <param name="Campo">A chave do campo, em CAMPO e CAMPO_ID.</param>
public sealed record DestinoColunaDto(string Tipo, string? Campo = null);

/// <summary>Uma coluna do arquivo enviado: a posição (a partir de 0), o cabeçalho, até 3 valores de exemplo e o destino sugerido (RN98).</summary>
public sealed record ColunaArquivoDto(int Indice, string Cabecalho, IReadOnlyList<string> Exemplos, DestinoColunaDto Destino);

/// <summary>Um destino que o mapeamento aceita neste tipo de item, com o rótulo para o select e se o campo é obrigatório.</summary>
public sealed record DestinoDisponivelDto(string Tipo, string? Campo, string Rotulo, bool Obrigatorio);

/// <summary>Resposta do envio do arquivo (POST .../importacoes): as colunas, com a sugestão de mapeamento, e os destinos possíveis.</summary>
public sealed record ImportacaoEnviadaDto(
    Guid Id,
    string NomeArquivo,
    string Formato,
    string Situacao,
    int TotalLinhas,
    IReadOnlyList<ColunaArquivoDto> Colunas,
    IReadOnlyList<DestinoDisponivelDto> Destinos);

/// <summary>Uma coluna no corpo da prévia: a posição no arquivo e o destino escolhido.</summary>
public sealed record ColunaMapeadaEntrada(int Indice, DestinoColunaDto? Destino);

/// <summary>
/// As opções da importação. <see cref="Duplicados"/>: PULAR ou ATUALIZAR (o item que já existe, identificado pelas restrições únicas;
/// exige permissão de alterar). <see cref="LinhasComErro"/>: SO_VALIDAS ou TUDO_OU_NADA. <see cref="EquipePadraoId"/>: a equipe das
/// linhas sem a coluna Equipe.
/// </summary>
public sealed record OpcoesImportacaoDto(string? Duplicados, string? LinhasComErro, Guid? EquipePadraoId);

/// <summary>Corpo de POST .../importacoes/{id}/validar.</summary>
public sealed record ValidarImportacaoEntrada(IReadOnlyList<ColunaMapeadaEntrada>? Mapeamento, OpcoesImportacaoDto? Opcoes);

public sealed record TotaisImportacaoDto(int Novos, int Atualizacoes, int Ignorados, int ComErro, int Total);

/// <summary>Um erro de uma linha. <see cref="Coluna"/> é a posição da célula no arquivo; nula se o erro é da linha inteira.</summary>
public sealed record ErroLinhaDto(int? Coluna, string? Campo, string Mensagem);

/// <summary>Uma referência resolvida pelo nome (ou pelo ID): a célula, o campo e o item encontrado.</summary>
public sealed record ReferenciaResolvidaDto(int Coluna, string Campo, Guid Id, string? Nome);

/// <summary>
/// Uma linha da prévia ou do resultado. <see cref="Situacao"/>: NOVA, ATUALIZACAO, IGNORADA ou ERRO. <see cref="Valores"/> são as
/// células como estão no arquivo (texto), na ordem das colunas. <see cref="RegistroId"/>: o item criado ou alterado (depois de executar)
/// ou o item que já existe (IGNORADA e ATUALIZACAO na prévia).
/// </summary>
public sealed record LinhaImportacaoDto(
    int Numero,
    string Situacao,
    IReadOnlyList<string> Valores,
    IReadOnlyList<ErroLinhaDto> Erros,
    IReadOnlyList<ReferenciaResolvidaDto> Referencias,
    Guid? RegistroId);

/// <summary>
/// Uma importação no histórico de importações (RN97). <see cref="Situacao"/>: ENVIADA, VALIDADA, CONCLUIDA, DESFEITA ou EXPIRADA.
/// <see cref="Totais"/> é nulo antes da prévia. <see cref="ArquivoDisponivel"/>: o arquivo ainda não foi apagado pela limpeza (RN104),
/// então o relatório pode ser baixado. <see cref="PodeDesfazer"/>: concluída, com itens criados, e o usuário tem o perfil que exclui
/// (as outras condições, RN103, são conferidas ao desfazer).
/// </summary>
public sealed record ImportacaoResumoDto(
    Guid Id,
    string NomeArquivo,
    string Formato,
    string Situacao,
    DateTime CriadoEm,
    DateTime? ConcluidaEm,
    DateTime? DesfeitaEm,
    UsuarioResumoDto Usuario,
    TotaisImportacaoDto? Totais,
    bool ArquivoDisponivel,
    bool PodeDesfazer);

/// <summary>
/// A prévia (ou o resultado) de uma importação: o resumo, os totais, se pode ser executada, os cabeçalhos do arquivo, o mapeamento e
/// as opções usados, e as linhas, paginadas (com o filtro "só com erro").
/// </summary>
public sealed record PreviaImportacaoDto(
    ImportacaoResumoDto Importacao,
    TotaisImportacaoDto Totais,
    bool Executavel,
    IReadOnlyList<string> Cabecalhos,
    IReadOnlyList<ColunaMapeadaEntrada> Mapeamento,
    OpcoesImportacaoDto Opcoes,
    PaginaDto<LinhaImportacaoDto> Linhas);
