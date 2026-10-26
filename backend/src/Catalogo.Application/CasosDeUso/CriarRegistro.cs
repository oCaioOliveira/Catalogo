using System.Text.Json.Nodes;
using Catalogo.Application.Abstracoes;
using Catalogo.Application.Comum;
using Catalogo.Application.Dtos;
using Catalogo.Domain.Excecoes;

namespace Catalogo.Application.CasosDeUso;

/// <summary>
/// Caso de uso: criar um registro numa entidade. Fluxo descrito em docs/arquitetura.md.
/// Lança <see cref="RecursoNaoEncontradoException"/>, <see cref="ValidacaoRegistroException"/>,
/// <see cref="AcessoNegadoException"/> ou <see cref="ConflitoUnicidadeException"/>.
/// </summary>
public sealed class CriarRegistro(
    IUsuarioAtual usuarioAtual,
    IMetadadosProvider metadados,
    IUnidadeDeTrabalho unidadeDeTrabalho,
    GravadorRegistro gravador,
    MontadorRegistroDto montador)
{
    public async Task<RegistroDto> ExecutarAsync(
        string grupoChave, string entidadeChave, JsonObject dados, CancellationToken cancellationToken)
    {
        var usuario = await usuarioAtual.ObterAsync(cancellationToken);

        // 1. Metadados da entidade.
        (var grupo, var entidade, usuario) = await metadados.ObterEntidadeOuFalharAsync(usuario, grupoChave, entidadeChave, UsoEntidade.Escrita, cancellationToken);

        // 2 a 7. Equipe, permissão, validação completa, dados e histórico: o mesmo caminho da importação (GravadorRegistro).
        var registro = await gravador.CriarAsync(grupo, entidade, usuario, dados, OrigemGravacao.Manual(), cancellationToken);

        try
        {
            await unidadeDeTrabalho.SalvarAlteracoesAsync(cancellationToken);
        }
        catch (ViolacaoUnicidadeBancoException ex)
        {
            // Corrida: outra requisição gravou o mesmo valor ao mesmo tempo.
            throw PreparadorRegistro.ConverterViolacao(entidade, ex);
        }

        return await montador.MontarAsync(registro, entidade, cancellationToken);
    }
}
