using Catalogo.Application.Abstracoes;
using Catalogo.Application.Comum;
using Catalogo.Application.Dtos;
using Catalogo.Application.Mapeamentos;
using Catalogo.Domain.Excecoes;
using Catalogo.Domain.Historico;
using Catalogo.Domain.Metadados.Administracao;

namespace Catalogo.Application.CasosDeUso.Metadados;

/// <summary>Caso de uso: remover a dependência de um campo (RN73). Sempre permitido: nenhum registro fica inválido.</summary>
public sealed class ExcluirDependencia(ExecutorAlteracaoMetadados executor, IMetadadosAdminRepository admin)
{
    public async Task<ResultadoAdministracaoDto> ExecutarAsync(
        string grupoChave, string entidadeChave, string campoChave, string? versaoTexto, bool simular, CancellationToken cancellationToken)
    {
        var usuario = await executor.ExigirAdministradorAsync(cancellationToken);
        var grupo = await executor.ObterGrupoAsync(grupoChave, cancellationToken);
        var entidade = ExecutorAlteracaoMetadados.ObterEntidade(grupo, entidadeChave);
        var campo = ExecutorAlteracaoMetadados.ObterCampo(entidade, campoChave);
        var atual = entidade.Dependencias.FirstOrDefault(d => d.Campo.Id == campo.Id)
            ?? throw new RecursoNaoEncontradoException($"O campo '{campo.Chave}' não tem dependência.", CodigosErro.DependenciaNaoEncontrada);
        var versao = ExecutorAlteracaoMetadados.ConferirVersao(versaoTexto, entidade.Versao, "da entidade");

        return await executor.ExecutarAsync(usuario, new AlteracaoMetadados(
            grupo.Chave, grupo.Id, OperacaoHistorico.Exclusao, PlanoAlteracao.Vazio,
            async ct =>
            {
                await admin.TocarEntidadeAsync(entidade.Id, versao, executor.AgoraUtc, ct);
                await admin.ExcluirDependenciaAsync(campo.Id, ct);
            },
            atual.ParaHistorico(entidade),
            _ => null), simular, cancellationToken);
    }
}
