using Catalogo.Application.Abstracoes;
using Catalogo.Application.Comum;
using Catalogo.Application.Dtos;
using Catalogo.Domain.Consultas;
using Catalogo.Domain.Excecoes;
using Catalogo.Domain.Historico;
using Catalogo.Domain.Validacao;

namespace Catalogo.Application.CasosDeUso.Historico;

/// <summary>
/// Caso de uso: histórico de um usuário ou de uma equipe (só ADMINISTRADOR, RN32), do mais recente
/// para o mais antigo. O histórico de usuário nunca contém senha nem hash.
/// </summary>
public sealed class ListarHistoricoAcesso(
    IUsuarioAtual usuarioAtual,
    IUsuarioRepository usuarios,
    IEquipeRepository equipes,
    IHistoricoRepository historico,
    MontadorHistorico montador)
{
    public async Task<PaginaDto<HistoricoDto>> ExecutarAsync(
        AlvoHistorico alvo, Guid id, IEnumerable<KeyValuePair<string, string>> parametros, CancellationToken cancellationToken)
    {
        Permissoes.ExigirAdministrador(await usuarioAtual.ObterAsync(cancellationToken));

        var existe = alvo switch
        {
            AlvoHistorico.Usuario => await usuarios.ObterPorIdAsync(id, cancellationToken) is not null,
            AlvoHistorico.Equipe => await equipes.ObterPorIdAsync(id, cancellationToken) is not null,
            _ => throw new ArgumentOutOfRangeException(nameof(alvo), alvo, "Use ListarHistoricoRegistro para registros."),
        };
        if (!existe)
            throw alvo == AlvoHistorico.Usuario
                ? new RecursoNaoEncontradoException($"Usuário {id} não encontrado.", CodigosErro.UsuarioNaoEncontrado)
                : new RecursoNaoEncontradoException($"Equipe {id} não encontrada.", CodigosErro.EquipeNaoEncontrada);

        var erros = new ErrosPorCampo();
        var paginacao = InterpretadorConsulta.InterpretarPaginacao(parametros.ToList(), erros);
        erros.LancarSeHouverErros();

        var pagina = await historico.ListarAsync(alvo, id, paginacao, cancellationToken);
        var itens = await montador.MontarAsync(pagina.Itens, entidade: null, cancellationToken);
        return new PaginaDto<HistoricoDto>(itens, paginacao.Pagina, paginacao.Tamanho, pagina.Total, paginacao.TotalPaginas(pagina.Total));
    }
}
