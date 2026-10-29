using Catalogo.Application.Abstracoes;
using Catalogo.Application.Comum;
using Catalogo.Application.Dtos;
using Catalogo.Application.Mapeamentos;
using Catalogo.Domain.Historico;
using Catalogo.Domain.Metadados;
using Catalogo.Domain.Metadados.Administracao;
using Catalogo.Domain.Validacao;

namespace Catalogo.Application.CasosDeUso.Metadados;

/// <summary>
/// Caso de uso: criar uma restrição única (RN55, RN71), com a versão da entidade. Bloqueada se os registros existentes já têm
/// duplicatas (o relatório lista os grupos de repetidos). Se passar, gera as chaves de todos os registros na mesma transação,
/// com a mesma CalculadoraChaveUnica: a partir daí, duplicatas novas são barradas (RN09).
/// </summary>
public sealed class CriarRestricao(ExecutorAlteracaoMetadados executor, IMetadadosAdminRepository admin)
{
    public async Task<ResultadoAdministracaoDto> ExecutarAsync(
        string grupoChave, string entidadeChave, RestricaoEntrada entrada, bool simular, CancellationToken cancellationToken)
    {
        var usuario = await executor.ExigirAdministradorAsync(cancellationToken);
        var grupo = await executor.ObterGrupoAsync(grupoChave, cancellationToken);
        var entidade = ExecutorAlteracaoMetadados.ObterEntidade(grupo, entidadeChave);
        var versao = ExecutorAlteracaoMetadados.ConferirVersao(entrada.Versao, entidade.Versao, "da entidade");

        var erros = new ErrosPorCampo();
        var definida = RegrasMetadados.ValidarRestricao(entidade, new DefinicaoRestricao(entrada.Nome, entrada.Campos), erros);
        erros.LancarSeHouverErros();

        // Para procurar duplicatas antes de gravar: a entidade "como ficaria", com a restrição nova.
        var restricao = new RestricaoUnica { Id = Guid.NewGuid(), EntidadeId = entidade.Id, Nome = definida!.Nome, Campos = definida.Campos };
        var comRestricao = CopiasMetadados.Entidade(entidade, [.. entidade.RestricoesUnicas, restricao]);

        return await executor.ExecutarAsync(usuario, new AlteracaoMetadados(
            grupo.Chave, grupo.Id, OperacaoHistorico.Criacao, PlanejadorAlteracoes.NovaRestricao(comRestricao, restricao),
            async ct =>
            {
                await admin.TocarEntidadeAsync(entidade.Id, versao, executor.AgoraUtc, ct);
                admin.AdicionarRestricao(entidade.Id, definida);
            },
            null,
            depois => depois?.ObterEntidade(entidade.Id) is { } e
                ? e.RestricoesUnicas.FirstOrDefault(r => r.Nome == definida.Nome)?.ParaHistorico(e)
                : null),
            simular, cancellationToken);
    }
}
