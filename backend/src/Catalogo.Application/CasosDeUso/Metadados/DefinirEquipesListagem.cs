using Catalogo.Application.Abstracoes;
using Catalogo.Application.Comum;
using Catalogo.Application.Dtos;
using Catalogo.Application.Mapeamentos;
using Catalogo.Domain.Historico;
using Catalogo.Domain.Metadados.Administracao;
using Catalogo.Domain.Validacao;

namespace Catalogo.Application.CasosDeUso.Metadados;

/// <summary>
/// Caso de uso: as equipes habilitadas numa listagem (RN90 a RN93), com a lista COMPLETA e a versão do grupo (RN52). Só o
/// ADMINISTRADOR (RN50). Pelo mesmo <see cref="ExecutorAlteracaoMetadados"/> das outras alterações de metadados: aceita
/// ?simular=true (RN56), muda a versão do grupo, grava UMA linha METADADO no histórico e invalida o cache.
/// Tirar uma equipe que tem itens é permitido, com o impacto ITENS_OCULTOS (RN92): nada é apagado.
/// </summary>
public sealed class DefinirEquipesListagem(ExecutorAlteracaoMetadados executor, IMetadadosAdminRepository admin, IEquipeRepository equipes)
{
    public const string ChaveEquipes = "equipes";

    public async Task<ResultadoAdministracaoDto> ExecutarAsync(
        string grupoChave, EquipesListagemEntrada entrada, bool simular, CancellationToken cancellationToken)
    {
        var usuario = await executor.ExigirAdministradorAsync(cancellationToken);
        var grupo = await executor.ObterGrupoAsync(grupoChave, cancellationToken);
        var versao = ExecutorAlteracaoMetadados.ConferirVersao(entrada.Versao, grupo.Versao, "do grupo");

        // Todas as equipes (inclusive as inativas, que podem continuar habilitadas): para conferir os ids e para os nomes.
        var todas = (await equipes.ListarTodasAsync(cancellationToken)).ToDictionary(e => e.Id);
        var novas = Validar(entrada, todas.Keys.ToHashSet());
        var nomes = todas.ToDictionary(e => e.Key, e => e.Value.Nome);

        return await executor.ExecutarAsync(usuario, new AlteracaoMetadados(
            grupo.Chave, grupo.Id, OperacaoHistorico.Alteracao,
            PlanejadorAlteracoes.EquipesDaListagem(grupo, novas, nomes),
            async ct =>
            {
                await admin.TocarGrupoAsync(grupo.Id, versao, executor.AgoraUtc, ct);
                await admin.DefinirEquipesAsync(grupo.Id, novas, usuario.Id, executor.AgoraUtc, ct);
            },
            grupo.ParaHistoricoEquipes(nomes),
            depois => depois?.ParaHistoricoEquipes(nomes)), simular, cancellationToken);
    }

    /// <summary>
    /// A lista é obrigatória (pode ser vazia: RN93), sem ids repetidos, inválidos ou de equipes que não existem. Todos os erros
    /// juntos (RN05), com a posição: "equipes[1]".
    /// </summary>
    private static HashSet<Guid> Validar(EquipesListagemEntrada entrada, IReadOnlySet<Guid> existentes)
    {
        var erros = new ErrosPorCampo();
        var novas = new HashSet<Guid>();

        if (entrada.Equipes is null)
            erros.Adicionar(ChaveEquipes, "Obrigatória: a lista completa das equipes habilitadas (vazia = nenhuma).");

        foreach (var (texto, posicao) in (entrada.Equipes ?? []).Select((t, i) => (t, i)))
        {
            var chave = $"{ChaveEquipes}[{posicao}]";
            if (!Guid.TryParseExact(texto?.Trim(), "D", out var id))
                erros.Adicionar(chave, "Deve ser o id (GUID) de uma equipe.");
            else if (!existentes.Contains(id))
                erros.Adicionar(chave, $"Equipe {id} não encontrada.");
            else if (!novas.Add(id))
                erros.Adicionar(chave, "Equipe repetida na lista.");
        }

        erros.LancarSeHouverErros();
        return novas;
    }
}
