using Catalogo.Application.Abstracoes;
using Catalogo.Application.Comum;
using Catalogo.Application.Dtos;
using Catalogo.Application.Mapeamentos;
using Catalogo.Domain.Excecoes;
using Catalogo.Domain.Historico;
using Catalogo.Domain.Metadados.Administracao;

namespace Catalogo.Application.CasosDeUso.Metadados;

/// <summary>
/// Caso de uso: desativar ou reativar um campo (RN69). Desativar: bloqueado se ele é campo de exibição ou está numa restrição
/// única ou dependência ativa. Os valores ficam guardados (o PUT de registro os preserva) e voltam ao reativar.
/// </summary>
public sealed class AlterarAtivacaoCampo(ExecutorAlteracaoMetadados executor, IMetadadosAdminRepository admin)
{
    public async Task<ResultadoAdministracaoDto> ExecutarAsync(
        string grupoChave, string entidadeChave, string campoChave, AtivacaoMetadadoEntrada entrada, bool simular,
        CancellationToken cancellationToken)
    {
        var usuario = await executor.ExigirAdministradorAsync(cancellationToken);
        var grupo = await executor.ObterGrupoAsync(grupoChave, cancellationToken);
        var entidade = ExecutorAlteracaoMetadados.ObterEntidade(grupo, entidadeChave);
        var campo = ExecutorAlteracaoMetadados.ObterCampo(entidade, campoChave);
        var versao = ExecutorAlteracaoMetadados.ConferirVersao(entrada.Versao, entidade.Versao, "da entidade");
        var ativo = entrada.Ativo
            ?? throw new ValidacaoRegistroException(new Dictionary<string, string[]> { ["ativo"] = ["Obrigatório: true ou false."] });

        var plano = ativo == campo.Ativo ? PlanoAlteracao.Vazio : PlanejadorAlteracoes.AtivacaoCampo(grupo, entidade, campo, ativo);

        return await executor.ExecutarAsync(usuario, new AlteracaoMetadados(
            grupo.Chave, grupo.Id, OperacaoHistorico.Alteracao, plano,
            async ct =>
            {
                await admin.TocarEntidadeAsync(entidade.Id, versao, executor.AgoraUtc, ct);
                await admin.AlterarCampoAsync(CopiasMetadados.Campo(campo, ativo: ativo), ct);
            },
            campo.ParaHistorico(grupo, entidade),
            depois => depois?.ObterEntidade(entidade.Id) is { } e ? e.ObterCampo(campo.Id)?.ParaHistorico(depois, e) : null),
            simular, cancellationToken);
    }
}
