using System.Text.Json.Nodes;

namespace Catalogo.Application.Dtos;

// DTOs de autenticação, usuários, equipes e histórico.
// Os de ENTRADA têm propriedades anuláveis de propósito: a validação (com mensagens em português,
// todas juntas) é feita pelo caso de uso, e não pela validação automática do ASP.NET.
// Nenhum DTO tem senha nem hash de senha, exceto a senha provisória devolvida UMA vez.

/// <summary>Quem fez algo: { id, matricula, nome }.</summary>
public sealed record UsuarioResumoDto(Guid Id, string Matricula, string Nome);

public sealed record EquipeResumoDto(Guid Id, string Nome, bool Ativa);

public sealed record LoginEntrada(string? Matricula, string? Senha);

public sealed record LoginDto(string Token, DateTime ExpiraEm, bool DeveTrocarSenha, UsuarioLogadoDto Usuario);

/// <summary>Dados do usuário logado (GET /api/auth/eu). <see cref="Equipes"/> só traz as ativas.</summary>
public sealed record UsuarioLogadoDto(
    Guid Id, string Matricula, string Nome, string Perfil, bool DeveTrocarSenha, IReadOnlyList<EquipeResumoDto> Equipes);

public sealed record TrocaSenhaEntrada(string? SenhaAtual, string? NovaSenha);

public sealed record EquipeEntrada(string? Nome);

/// <param name="MembrosAtivos">Usuários ATIVOS que pertencem à equipe (qualquer perfil).</param>
/// <param name="Listagens">As listagens em que a equipe está habilitada (RN90), por nome. Vazia = nenhuma.</param>
public sealed record EquipeDto(
    Guid Id, string Nome, bool Ativa, DateTime CriadoEm, int MembrosAtivos, IReadOnlyList<ListagemDaEquipeDto> Listagens);

/// <summary>Uma listagem em que a equipe está habilitada: a chave (das URLs), o nome e se está ativa.</summary>
public sealed record ListagemDaEquipeDto(string Chave, string Nome, bool Ativo);

/// <summary>Corpo do PATCH /api/equipes/{id}/ativacao: { "ativa": true|false }.</summary>
public sealed record AtivacaoEquipeEntrada(bool? Ativa);

/// <summary>Corpo do PATCH /api/usuarios/{id}/ativacao: { "ativo": true|false }.</summary>
public sealed record AtivacaoUsuarioEntrada(bool? Ativo);

/// <summary>Criação e alteração de usuário. Na alteração, a matrícula é ignorada (não muda).</summary>
public sealed record UsuarioEntrada(string? Matricula, string? Nome, string? Perfil, IReadOnlyList<Guid>? Equipes);

/// <summary>Usuário para a administração. Nunca traz senha nem hash.</summary>
public sealed record UsuarioDto(
    Guid Id,
    string Matricula,
    string Nome,
    string Perfil,
    bool Ativo,
    bool DeveTrocarSenha,
    DateTime? BloqueadoAte,
    DateTime? UltimoAcessoEm,
    DateTime CriadoEm,
    IReadOnlyList<EquipeResumoDto> Equipes);

/// <summary>Resposta da criação: a senha provisória aparece só aqui, uma única vez.</summary>
public sealed record UsuarioCriadoDto(UsuarioDto Usuario, string SenhaProvisoria);

/// <summary>Resposta da redefinição de senha: a nova senha provisória, uma única vez.</summary>
public sealed record SenhaProvisoriaDto(string SenhaProvisoria);

/// <summary>Uma linha do histórico (RN32), com as mudanças por campo.</summary>
public sealed record HistoricoDto(
    long Id,
    string Operacao,
    DateTime OcorridoEm,
    UsuarioResumoDto Usuario,
    Guid CorrelacaoId,
    IReadOnlyList<MudancaDto> Mudancas,
    // Justificativa das operações APROVACAO e REJEICAO (RN36). Nula nas demais.
    string? Justificativa,
    // Só no histórico de metadados: o que foi alterado ("Campo 'nome' de 'equipamento'"). Nulo nos demais.
    string? Alvo = null,
    // De onde veio a gravação: IMPORTACAO (planilha, RN97) ou nulo (cadastro manual, pela tela ou pela API).
    string? Origem = null);

/// <summary>
/// Um campo que mudou. Em campos REFERENCIA de registro, "de" e "para" vêm como { id, rotulo },
/// com o rótulo ATUAL (nulo se o registro apontado não existe mais).
/// </summary>
public sealed record MudancaDto(string Campo, JsonNode? De, JsonNode? Para);
