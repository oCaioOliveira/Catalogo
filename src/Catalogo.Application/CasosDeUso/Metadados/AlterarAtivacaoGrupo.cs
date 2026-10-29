using Catalogo.Application.Abstracoes;
using Catalogo.Application.Comum;
using Catalogo.Application.Dtos;
using Catalogo.Application.Mapeamentos;
using Catalogo.Domain.Excecoes;
using Catalogo.Domain.Historico;
using Catalogo.Domain.Metadados.Administracao;

namespace Catalogo.Application.CasosDeUso.Metadados;

/// <summary>
/// Caso de uso: desativar ou reativar um grupo (RN77). Desativado, ele some para os não-administradores (404) e as entidades
/// ficam somente leitura para o administrador. Os registros ficam.
/// </summary>
public sealed class AlterarAtivacaoGrupo(ExecutorAlteracaoMetadados executor, IMetadadosAdminRepository admin)
{
    public async Task<ResultadoAdministracaoDto> ExecutarAsync(
        string grupoChave, AtivacaoMetadadoEntrada entrada, bool simular, CancellationToken cancellationToken)
    {
        var usuario = await executor.ExigirAdministradorAsync(cancellationToken);
        var grupo = await executor.ObterGrupoAsync(grupoChave, cancellationToken);
        var versao = ExecutorAlteracaoMetadados.ConferirVersao(entrada.Versao, grupo.Versao, "do grupo");
        var ativo = entrada.Ativo
            ?? throw new ValidacaoRegistroException(new Dictionary<string, string[]> { ["ativo"] = ["Obrigatório: true ou false."] });

        return await executor.ExecutarAsync(usuario, new AlteracaoMetadados(
            grupo.Chave, grupo.Id, OperacaoHistorico.Alteracao, PlanoAlteracao.Vazio,
            async ct =>
            {
                await admin.TocarGrupoAsync(grupo.Id, versao, executor.AgoraUtc, ct);
                await admin.AlterarGrupoAsync(grupo.Id, grupo.Nome, grupo.Descricao, ativo, ct);
            },
            grupo.ParaHistorico(),
            depois => depois?.ParaHistorico()), simular, cancellationToken);
    }
}
