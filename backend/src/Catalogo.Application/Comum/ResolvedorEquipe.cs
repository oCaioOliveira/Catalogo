using System.Text.Json.Nodes;
using Catalogo.Application.Abstracoes;
using Catalogo.Domain.Acesso;
using Catalogo.Domain.Excecoes;
using Catalogo.Domain.Metadados;
using Catalogo.Domain.Validacao;

namespace Catalogo.Application.Comum;

/// <summary>
/// Descobre a equipe de um registro novo (POST) ou do formulário (opções de campo) a partir do
/// "equipeId" informado (RN27). A regra está na <see cref="PoliticaPermissao.ResolverEquipe"/>;
/// aqui só a leitura do valor, a tradução para 400/403 e a conferência da equipe no banco.
/// </summary>
public sealed class ResolvedorEquipe(IEquipeRepository equipes)
{
    // Por requisição: a importação (RN97) confere a mesma equipe em muitas linhas.
    private readonly Dictionary<Guid, bool> _ativas = [];

    /// <summary>Valor da chave reservada "equipeId" no corpo (nulo se ausente).</summary>
    public static string? LerDoCorpo(JsonObject dados) =>
        dados.TryGetPropertyValue(ChavesReservadas.EquipeId, out var no) && no is JsonValue valor
            && valor.TryGetValue<string>(out var texto) && !string.IsNullOrWhiteSpace(texto)
            ? texto.Trim()
            : null;

    /// <returns>A equipe (nula se a entidade é global).</returns>
    /// <exception cref="ValidacaoRegistroException">equipeId inválido, faltando, inexistente ou numa entidade global (400).</exception>
    /// <exception cref="AcessoNegadoException">Equipe da qual o usuário não participa (403).</exception>
    public async Task<Guid?> ResolverAsync(
        UsuarioAtual usuario, Entidade entidade, string? equipeIdInformado, CancellationToken cancellationToken)
    {
        var erros = new ErrosPorCampo();
        Guid? informada = null;

        if (equipeIdInformado is not null)
        {
            if (Guid.TryParseExact(equipeIdInformado, "D", out var id))
                informada = id;
            else
                erros.Adicionar(ChavesReservadas.EquipeId, "Deve ser o id (GUID) de uma equipe.");
        }

        erros.LancarSeHouverErros();

        // As equipes de um não-administrador já são as ativas dele; a do administrador é conferida aqui, antes de saber se ela
        // está habilitada na listagem (RN91): uma equipe que não existe é "não encontrada", não "não habilitada".
        if (informada is Guid equipeId && usuario.EhAdministrador && !entidade.EhGlobal)
        {
            if (!_ativas.TryGetValue(equipeId, out var ativa))
                _ativas[equipeId] = ativa = await equipes.ObterPorIdAsync(equipeId, cancellationToken) is { Ativa: true };
            if (!ativa)
                erros.Adicionar(ChavesReservadas.EquipeId, $"Equipe {equipeId} não encontrada ou inativa.");
            erros.LancarSeHouverErros();
        }

        var resolucao = PoliticaPermissao.ResolverEquipe(usuario, entidade, informada);
        if (resolucao.Proibido)
            throw new AcessoNegadoException("Você não pertence a uma equipe ativa que permita esta operação.");

        if (resolucao.ErroValidacao is not null)
            erros.Adicionar(ChavesReservadas.EquipeId, resolucao.ErroValidacao);

        erros.LancarSeHouverErros();
        return resolucao.EquipeId;
    }
}
