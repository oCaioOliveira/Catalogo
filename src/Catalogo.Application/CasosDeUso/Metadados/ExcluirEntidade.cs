using Catalogo.Application.Abstracoes;
using Catalogo.Application.Comum;
using Catalogo.Application.Dtos;
using Catalogo.Application.Mapeamentos;
using Catalogo.Domain.Historico;
using Catalogo.Domain.Metadados.Administracao;

namespace Catalogo.Application.CasosDeUso.Metadados;

/// <summary>
/// Caso de uso: excluir uma entidade (RN76), com os campos, restrições e dependências dela. Só se nunca teve registros, não
/// tem configuração de equipe e não é referenciada por campos de outras entidades. Senão, a orientação é desativar (RN75).
/// </summary>
public sealed class ExcluirEntidade(ExecutorAlteracaoMetadados executor, IMetadadosAdminRepository admin)
{
    public async Task<ResultadoAdministracaoDto> ExecutarAsync(
        string grupoChave, string entidadeChave, string? versaoTexto, bool simular, CancellationToken cancellationToken)
    {
        var usuario = await executor.ExigirAdministradorAsync(cancellationToken);
        var grupo = await executor.ObterGrupoAsync(grupoChave, cancellationToken);
        var entidade = ExecutorAlteracaoMetadados.ObterEntidade(grupo, entidadeChave);
        var versao = ExecutorAlteracaoMetadados.ConferirVersao(versaoTexto, entidade.Versao, "da entidade");

        return await executor.ExecutarAsync(usuario, new AlteracaoMetadados(
            grupo.Chave, grupo.Id, OperacaoHistorico.Exclusao, PlanejadorAlteracoes.ExcluirEntidade(grupo, entidade),
            ct => admin.ExcluirEntidadeAsync(entidade.Id, versao, ct),
            entidade.ParaHistorico(),
            _ => null), simular, cancellationToken);
    }
}
