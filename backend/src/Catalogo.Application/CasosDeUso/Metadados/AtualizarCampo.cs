using Catalogo.Application.Abstracoes;
using Catalogo.Application.Comum;
using Catalogo.Application.Dtos;
using Catalogo.Application.Mapeamentos;
using Catalogo.Domain.Historico;
using Catalogo.Domain.Metadados.Administracao;
using Catalogo.Domain.Validacao;

namespace Catalogo.Application.CasosDeUso.Metadados;

/// <summary>
/// Caso de uso: alterar um campo, com a versão da entidade (RN52). Chave, entidade referenciada e tipo não mudam (RN51, RN66;
/// a exceção é TEXTO → TEXTO_LONGO). Cada mudança que pode invalidar registros é conferida contra os dados (RN61 a RN68):
/// tornar obrigatório, diminuir tamanho, precisão ou escala, remover opção de LISTA.
/// </summary>
public sealed class AtualizarCampo(ExecutorAlteracaoMetadados executor, IMetadadosAdminRepository admin, ValidadorRegistro validador)
{
    public async Task<ResultadoAdministracaoDto> ExecutarAsync(
        string grupoChave, string entidadeChave, string campoChave, CampoEntrada entrada, bool simular, CancellationToken cancellationToken)
    {
        var usuario = await executor.ExigirAdministradorAsync(cancellationToken);
        var grupo = await executor.ObterGrupoAsync(grupoChave, cancellationToken);
        var entidade = ExecutorAlteracaoMetadados.ObterEntidade(grupo, entidadeChave);
        var atual = ExecutorAlteracaoMetadados.ObterCampo(entidade, campoChave);
        var versao = ExecutorAlteracaoMetadados.ConferirVersao(entrada.Versao, entidade.Versao, "da entidade");

        var erros = new ErrosPorCampo();
        var novo = RegrasMetadados.ValidarCampo(grupo, entidade, atual, CampoComum.Definicao(entrada), erros);
        var valorPadrao = novo is null ? null : CampoComum.ValidarValorPadrao(validador, entidade, atual, novo, entrada.ValorPadrao, erros);
        erros.LancarSeHouverErros();

        return await executor.ExecutarAsync(usuario, new AlteracaoMetadados(
            grupo.Chave, grupo.Id, OperacaoHistorico.Alteracao, PlanejadorAlteracoes.AlteracaoCampo(entidade, atual, novo!, valorPadrao),
            async ct =>
            {
                await admin.TocarEntidadeAsync(entidade.Id, versao, executor.AgoraUtc, ct);
                await admin.AlterarCampoAsync(novo!, ct);
            },
            atual.ParaHistorico(grupo, entidade),
            depois => depois?.ObterEntidade(entidade.Id) is { } e ? e.ObterCampo(atual.Id)?.ParaHistorico(depois, e) : null),
            simular, cancellationToken);
    }
}
