using System.Text.Json.Nodes;

namespace Catalogo.Application.Dtos;

// DTOs da avaliação de itens (RN40 a RN48). Os de ENTRADA têm propriedades anuláveis de propósito:
// a validação é do Domain (RegrasModeloAvaliacao e RegrasAvaliacao), com mensagens em português.

/// <summary>Um critério no POST/PUT do modelo. Padrões: obrigatorio false, comentario NENHUM, tamanhoMaximoTexto 2000, ordem = posição, ativo true.</summary>
public sealed record CriterioAvaliacaoEntrada(
    string? Chave, string? Nome, string? Descricao, string? Tipo, bool? Obrigatorio, string? Comentario,
    int? TamanhoMaximoTexto, int? Ordem, bool? Ativo);

/// <summary>POST e PUT .../avaliacao. A "versao" é obrigatória só no PUT (RN12).</summary>
public sealed record ModeloAvaliacaoEntrada(string? Versao, string? Nome, string? Descricao, IReadOnlyList<CriterioAvaliacaoEntrada?>? Criterios);

/// <summary>PATCH .../avaliacao/ativacao: { "ativo": false } desativa o modelo ativo; { "ativo": true, "modeloId": "..." } reativa um modelo.</summary>
public sealed record AtivacaoModeloEntrada(bool? Ativo, Guid? ModeloId);

/// <summary>Um critério do modelo. <see cref="PossuiRespostas"/> só vem na configuração (nulo no formulário).</summary>
public sealed record CriterioAvaliacaoDto(
    Guid Id, string Chave, string Nome, string? Descricao, string Tipo, bool Obrigatorio, string Comentario,
    int TamanhoMaximoTexto, int Ordem, bool Ativo, bool? PossuiRespostas);

/// <summary>Um modelo de avaliação, para quem configura. Traz todos os critérios, ativos e inativos, em ordem.</summary>
public sealed record ModeloAvaliacaoDto(
    Guid Id,
    Guid EquipeId,
    string Grupo,
    string Entidade,
    string Nome,
    string? Descricao,
    bool Ativo,
    // rowversion em Base64: enviada de volta no corpo do PUT (RN12).
    string Versao,
    int QuantidadeAvaliacoes,
    UsuarioResumoDto? CriadoPor,
    DateTime CriadoEm,
    UsuarioResumoDto? AtualizadoPor,
    DateTime? AtualizadoEm,
    IReadOnlyList<CriterioAvaliacaoDto> Criterios);

/// <summary>O formulário de avaliação de uma entidade numa equipe (em ListarEntidades): só os critérios ativos.</summary>
public sealed record FormularioAvaliacaoDto(Guid ModeloId, string Nome, string? Descricao, IReadOnlyList<CriterioAvaliacaoDto> Criterios);

/// <summary>
/// PUT .../avaliacoes/minha: { "versao": "...", "respostas": { "qualidade": { "estrelas": 4, "comentario": "..." },
/// "observacoes": { "texto": "..." } } }. A "versao" é obrigatória só ao editar.
/// </summary>
public sealed record AvaliacaoEntrada(string? Versao, JsonObject? Respostas);

public sealed record ModeloResumoDto(Guid Id, string Nome, bool Ativo);

/// <summary>Uma avaliação individual. <see cref="Respostas"/> no mesmo formato da entrada, só com critérios ativos.</summary>
public sealed record AvaliacaoDto(
    Guid Id, ModeloResumoDto Modelo, UsuarioResumoDto? Usuario, DateTime CriadoEm, DateTime? AtualizadoEm, string Versao, JsonObject Respostas);

/// <summary>Resultado do PUT .../avaliacoes/minha: a API responde 201 se <see cref="Criada"/>, senão 200.</summary>
public sealed record ResultadoAvaliacao(AvaliacaoDto Avaliacao, bool Criada);

/// <summary>
/// Notas de um critério ESTRELAS: média com uma casa decimal (nula sem notas), quantas notas e a
/// distribuição { "1": n, ..., "5": n }.
/// </summary>
public sealed record ResumoCriterioDto(string Chave, string Nome, decimal? Media, int Quantidade, IReadOnlyDictionary<string, int> Distribuicao);

/// <summary>Resumo de um modelo num item (RN47). <see cref="Minha"/>: a avaliação do usuário logado, se houver.</summary>
public sealed record ResumoAvaliacaoDto(
    ModeloResumoDto Modelo, int QuantidadeAvaliadores, IReadOnlyList<ResumoCriterioDto> Criterios, AvaliacaoDto? Minha);

/// <summary>
/// GET .../avaliacoes (RN47). <see cref="Resumo"/>: o do modelo ativo (nulo se não há). <see cref="ResumosAnteriores"/>:
/// modelos inativos que têm avaliações deste item, separados. <see cref="Avaliacoes"/>: as individuais, das mais
/// recentes para as mais antigas; NULO para o EXTERNO, que vê só os resumos.
/// </summary>
public sealed record AvaliacoesDoRegistroDto(
    ResumoAvaliacaoDto? Resumo, IReadOnlyList<ResumoAvaliacaoDto> ResumosAnteriores, PaginaDto<AvaliacaoDto>? Avaliacoes);

/// <summary>
/// "avaliacao" no retorno de um registro (RN48), quando há modelo ativo: média por critério ESTRELAS ativo
/// (nula sem notas), quantas avaliações e se o usuário logado já avaliou.
/// </summary>
public sealed record AvaliacaoRegistroDto(IReadOnlyDictionary<string, decimal?> Medias, int Quantidade, bool Minha);
