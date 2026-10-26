using Catalogo.Application.Abstracoes;
using Catalogo.Application.Comum;
using Catalogo.Application.Dtos;
using Catalogo.Application.Mapeamentos;
using Catalogo.Domain.Historico;
using Catalogo.Domain.Metadados.Administracao;
using Catalogo.Domain.Validacao;

namespace Catalogo.Application.CasosDeUso.Metadados;

/// <summary>Caso de uso: criar um grupo (listagem), ativo e sem entidades (RN50, RN53). Chave única e imutável (RN51).</summary>
public sealed class CriarGrupo(ExecutorAlteracaoMetadados executor, IMetadadosAdminRepository admin)
{
    public async Task<ResultadoAdministracaoDto> ExecutarAsync(GrupoEntrada entrada, bool simular, CancellationToken cancellationToken)
    {
        var usuario = await executor.ExigirAdministradorAsync(cancellationToken);

        var erros = new ErrosPorCampo();
        var existentes = (await admin.ListarGruposAsync(cancellationToken)).Select(g => g.Chave);
        var definido = RegrasMetadados.ValidarNovoGrupo(new DefinicaoGrupo(entrada.Chave, entrada.Nome, entrada.Descricao), existentes, erros);
        erros.LancarSeHouverErros();

        return await executor.ExecutarAsync(usuario, new AlteracaoMetadados(
            definido!.Chave!, Guid.Empty, OperacaoHistorico.Criacao, PlanoAlteracao.Vazio,
            _ =>
            {
                admin.AdicionarGrupo(definido.Chave!, definido.Nome!, definido.Descricao);
                return Task.CompletedTask;
            },
            null,
            depois => depois?.ParaHistorico()), simular, cancellationToken);
    }
}
