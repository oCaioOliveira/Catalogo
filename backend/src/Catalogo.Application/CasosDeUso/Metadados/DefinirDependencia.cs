using Catalogo.Application.Abstracoes;
using Catalogo.Application.Comum;
using Catalogo.Application.Dtos;
using Catalogo.Application.Mapeamentos;
using Catalogo.Domain.Historico;
using Catalogo.Domain.Metadados.Administracao;
using Catalogo.Domain.Validacao;

namespace Catalogo.Application.CasosDeUso.Metadados;

/// <summary>
/// Caso de uso: criar ou substituir a dependência de um campo REFERENCIA (RN08, RN55, RN73), com a versão da entidade.
/// Bloqueada se algum registro existente já viola a regra nova.
/// </summary>
public sealed class DefinirDependencia(ExecutorAlteracaoMetadados executor, IMetadadosAdminRepository admin)
{
    public async Task<ResultadoAdministracaoDto> ExecutarAsync(
        string grupoChave, string entidadeChave, string campoChave, DependenciaEntrada entrada, bool simular, CancellationToken cancellationToken)
    {
        var usuario = await executor.ExigirAdministradorAsync(cancellationToken);
        var grupo = await executor.ObterGrupoAsync(grupoChave, cancellationToken);
        var entidade = ExecutorAlteracaoMetadados.ObterEntidade(grupo, entidadeChave);
        var campo = ExecutorAlteracaoMetadados.ObterCampo(entidade, campoChave);
        var versao = ExecutorAlteracaoMetadados.ConferirVersao(entrada.Versao, entidade.Versao, "da entidade");

        var erros = new ErrosPorCampo();
        var dependencia = RegrasMetadados.ValidarDependencia(grupo, entidade, campo,
            new DefinicaoDependencia(entrada.CampoVia, entrada.CampoOrigem, entrada.CampoAlvo), erros);
        erros.LancarSeHouverErros();

        var atual = entidade.Dependencias.FirstOrDefault(d => d.Campo.Id == campo.Id);
        return await executor.ExecutarAsync(usuario, new AlteracaoMetadados(
            grupo.Chave, grupo.Id, atual is null ? OperacaoHistorico.Criacao : OperacaoHistorico.Alteracao,
            PlanejadorAlteracoes.NovaDependencia(dependencia!),
            async ct =>
            {
                await admin.TocarEntidadeAsync(entidade.Id, versao, executor.AgoraUtc, ct);
                await admin.DefinirDependenciaAsync(dependencia!, ct);
            },
            atual?.ParaHistorico(entidade),
            depois => depois?.ObterEntidade(entidade.Id) is { } e
                ? e.Dependencias.FirstOrDefault(d => d.Campo.Id == campo.Id)?.ParaHistorico(e)
                : null),
            simular, cancellationToken);
    }
}
