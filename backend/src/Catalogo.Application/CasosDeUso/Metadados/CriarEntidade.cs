using Catalogo.Application.Abstracoes;
using Catalogo.Application.Comum;
using Catalogo.Application.Dtos;
using Catalogo.Application.Mapeamentos;
using Catalogo.Domain.Historico;
using Catalogo.Domain.Metadados.Administracao;
using Catalogo.Domain.Validacao;

namespace Catalogo.Application.CasosDeUso.Metadados;

/// <summary>
/// Caso de uso: criar uma entidade (tipo de item) num grupo, ativa e sem campos, com a versão do GRUPO (RN52, RN53). O
/// escopo (global ou por equipe, RN27) é definido aqui; o campo de exibição, depois de criar os campos.
/// </summary>
public sealed class CriarEntidade(ExecutorAlteracaoMetadados executor, IMetadadosAdminRepository admin)
{
    public async Task<ResultadoAdministracaoDto> ExecutarAsync(
        string grupoChave, EntidadeEntrada entrada, bool simular, CancellationToken cancellationToken)
    {
        var usuario = await executor.ExigirAdministradorAsync(cancellationToken);
        var grupo = await executor.ObterGrupoAsync(grupoChave, cancellationToken);
        var versao = ExecutorAlteracaoMetadados.ConferirVersao(entrada.Versao, grupo.Versao, "do grupo");

        var erros = new ErrosPorCampo();
        var definida = RegrasMetadados.ValidarEntidade(grupo, null, Definicao(entrada), erros);
        erros.LancarSeHouverErros();

        return await executor.ExecutarAsync(usuario, new AlteracaoMetadados(
            grupo.Chave, grupo.Id, OperacaoHistorico.Criacao, PlanoAlteracao.Vazio,
            async ct =>
            {
                await admin.TocarGrupoAsync(grupo.Id, versao, executor.AgoraUtc, ct);
                admin.AdicionarEntidade(grupo.Id, definida!);
            },
            null,
            depois => depois?.ObterEntidade(definida!.Chave)?.ParaHistorico()), simular, cancellationToken);
    }

    internal static DefinicaoEntidade Definicao(EntidadeEntrada entrada) =>
        new(entrada.Chave, entrada.Nome, entrada.Descricao, entrada.Ordem, entrada.EscopoEquipe, entrada.CampoExibicao, entrada.NomePlural,
            entrada.Icone);
}
