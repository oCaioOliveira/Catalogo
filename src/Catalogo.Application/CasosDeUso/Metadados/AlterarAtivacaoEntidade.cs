using Catalogo.Application.Abstracoes;
using Catalogo.Application.Comum;
using Catalogo.Application.Dtos;
using Catalogo.Application.Mapeamentos;
using Catalogo.Domain.Excecoes;
using Catalogo.Domain.Historico;
using Catalogo.Domain.Metadados.Administracao;

namespace Catalogo.Application.CasosDeUso.Metadados;

/// <summary>
/// Caso de uso: desativar ou reativar uma entidade (RN75). Desativada, ela some para os não-administradores (404) e fica
/// somente leitura para o administrador; as referências a ela continuam valendo, mas ela não aceita vínculos novos.
/// </summary>
public sealed class AlterarAtivacaoEntidade(ExecutorAlteracaoMetadados executor, IMetadadosAdminRepository admin)
{
    public async Task<ResultadoAdministracaoDto> ExecutarAsync(
        string grupoChave, string entidadeChave, AtivacaoMetadadoEntrada entrada, bool simular, CancellationToken cancellationToken)
    {
        var usuario = await executor.ExigirAdministradorAsync(cancellationToken);
        var grupo = await executor.ObterGrupoAsync(grupoChave, cancellationToken);
        var entidade = ExecutorAlteracaoMetadados.ObterEntidade(grupo, entidadeChave);
        var versao = ExecutorAlteracaoMetadados.ConferirVersao(entrada.Versao, entidade.Versao, "da entidade");
        var ativo = entrada.Ativo
            ?? throw new ValidacaoRegistroException(new Dictionary<string, string[]> { ["ativo"] = ["Obrigatório: true ou false."] });

        return await executor.ExecutarAsync(usuario, new AlteracaoMetadados(
            grupo.Chave, grupo.Id, OperacaoHistorico.Alteracao, PlanoAlteracao.Vazio,
            async ct =>
            {
                await admin.TocarEntidadeAsync(entidade.Id, versao, executor.AgoraUtc, ct);
                await admin.AlterarEntidadeAsync(entidade.Id, CopiasMetadados.Definicao(entidade), ativo, ct);
            },
            entidade.ParaHistorico(),
            depois => depois?.ObterEntidade(entidade.Id)?.ParaHistorico()), simular, cancellationToken);
    }
}
