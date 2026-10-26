using Catalogo.Application.Abstracoes;
using Catalogo.Application.Comum;
using Catalogo.Application.Dtos;
using Catalogo.Application.Mapeamentos;
using Catalogo.Domain.Excecoes;
using Catalogo.Domain.Historico;
using Catalogo.Domain.Metadados.Administracao;

namespace Catalogo.Application.CasosDeUso.Metadados;

/// <summary>Caso de uso: remover uma restrição única (RN72). Sempre permitido; as chaves dela em RegistroChaveUnica saem junto.</summary>
public sealed class ExcluirRestricao(ExecutorAlteracaoMetadados executor, IMetadadosAdminRepository admin)
{
    public async Task<ResultadoAdministracaoDto> ExecutarAsync(
        string grupoChave, string entidadeChave, string nome, string? versaoTexto, bool simular, CancellationToken cancellationToken)
    {
        var usuario = await executor.ExigirAdministradorAsync(cancellationToken);
        var grupo = await executor.ObterGrupoAsync(grupoChave, cancellationToken);
        var entidade = ExecutorAlteracaoMetadados.ObterEntidade(grupo, entidadeChave);
        var restricao = entidade.RestricoesUnicas.FirstOrDefault(r => string.Equals(r.Nome, nome, StringComparison.OrdinalIgnoreCase))
            ?? throw new RecursoNaoEncontradoException($"Restrição '{nome}' não encontrada em '{entidade.Chave}'.", CodigosErro.RestricaoNaoEncontrada);
        var versao = ExecutorAlteracaoMetadados.ConferirVersao(versaoTexto, entidade.Versao, "da entidade");

        return await executor.ExecutarAsync(usuario, new AlteracaoMetadados(
            grupo.Chave, grupo.Id, OperacaoHistorico.Exclusao, PlanoAlteracao.Vazio,
            async ct =>
            {
                await admin.TocarEntidadeAsync(entidade.Id, versao, executor.AgoraUtc, ct);
                await admin.ExcluirRestricaoAsync(restricao.Id, ct);
            },
            restricao.ParaHistorico(entidade),
            _ => null), simular, cancellationToken);
    }
}
