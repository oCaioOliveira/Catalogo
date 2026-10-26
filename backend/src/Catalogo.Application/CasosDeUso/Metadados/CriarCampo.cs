using Catalogo.Application.Abstracoes;
using Catalogo.Application.Comum;
using Catalogo.Application.Dtos;
using Catalogo.Application.Mapeamentos;
using Catalogo.Domain.Historico;
using Catalogo.Domain.Metadados.Administracao;
using Catalogo.Domain.Validacao;

namespace Catalogo.Application.CasosDeUso.Metadados;

/// <summary>
/// Caso de uso: criar um campo numa entidade, com a versão dela (RN52, RN53, RN54). Opcional: sempre permitido (RN59).
/// Obrigatório numa entidade com registros: exige "valorPadrao", gravado em todos os registros existentes (RN60).
/// </summary>
public sealed class CriarCampo(ExecutorAlteracaoMetadados executor, IMetadadosAdminRepository admin, ValidadorRegistro validador)
{
    public async Task<ResultadoAdministracaoDto> ExecutarAsync(
        string grupoChave, string entidadeChave, CampoEntrada entrada, bool simular, CancellationToken cancellationToken)
    {
        var usuario = await executor.ExigirAdministradorAsync(cancellationToken);
        var grupo = await executor.ObterGrupoAsync(grupoChave, cancellationToken);
        var entidade = ExecutorAlteracaoMetadados.ObterEntidade(grupo, entidadeChave);
        var versao = ExecutorAlteracaoMetadados.ConferirVersao(entrada.Versao, entidade.Versao, "da entidade");

        var erros = new ErrosPorCampo();
        var campo = RegrasMetadados.ValidarCampo(grupo, entidade, null, CampoComum.Definicao(entrada), erros);
        var valorPadrao = campo is null ? null : CampoComum.ValidarValorPadrao(validador, entidade, null, campo, entrada.ValorPadrao, erros);
        erros.LancarSeHouverErros();

        return await executor.ExecutarAsync(usuario, new AlteracaoMetadados(
            grupo.Chave, grupo.Id, OperacaoHistorico.Criacao, PlanejadorAlteracoes.NovoCampo(entidade, campo!, valorPadrao),
            async ct =>
            {
                await admin.TocarEntidadeAsync(entidade.Id, versao, executor.AgoraUtc, ct);
                admin.AdicionarCampo(campo!);
            },
            null,
            depois => depois?.ObterEntidade(entidade.Id) is { } e ? e.ObterCampo(campo!.Chave)?.ParaHistorico(depois, e) : null),
            simular, cancellationToken);
    }
}
