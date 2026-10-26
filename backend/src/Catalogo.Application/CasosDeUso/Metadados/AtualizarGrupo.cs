using Catalogo.Application.Abstracoes;
using Catalogo.Application.Comum;
using Catalogo.Application.Dtos;
using Catalogo.Application.Mapeamentos;
using Catalogo.Domain.Historico;
using Catalogo.Domain.Metadados.Administracao;
using Catalogo.Domain.Validacao;

namespace Catalogo.Application.CasosDeUso.Metadados;

/// <summary>Caso de uso: alterar nome e descrição de um grupo, com a versão dele (RN52). A chave não muda (RN51).</summary>
public sealed class AtualizarGrupo(ExecutorAlteracaoMetadados executor, IMetadadosAdminRepository admin)
{
    public async Task<ResultadoAdministracaoDto> ExecutarAsync(
        string grupoChave, GrupoEntrada entrada, bool simular, CancellationToken cancellationToken)
    {
        var usuario = await executor.ExigirAdministradorAsync(cancellationToken);
        var grupo = await executor.ObterGrupoAsync(grupoChave, cancellationToken);
        var versao = ExecutorAlteracaoMetadados.ConferirVersao(entrada.Versao, grupo.Versao, "do grupo");

        var erros = new ErrosPorCampo();
        var definido = RegrasMetadados.ValidarAlteracaoGrupo(grupo, new DefinicaoGrupo(entrada.Chave, entrada.Nome, entrada.Descricao), erros);
        erros.LancarSeHouverErros();

        return await executor.ExecutarAsync(usuario, new AlteracaoMetadados(
            grupo.Chave, grupo.Id, OperacaoHistorico.Alteracao, PlanoAlteracao.Vazio,
            async ct =>
            {
                await admin.TocarGrupoAsync(grupo.Id, versao, executor.AgoraUtc, ct);
                await admin.AlterarGrupoAsync(grupo.Id, definido!.Nome!, definido.Descricao, grupo.Ativo, ct);
            },
            grupo.ParaHistorico(),
            depois => depois?.ParaHistorico()), simular, cancellationToken);
    }
}
