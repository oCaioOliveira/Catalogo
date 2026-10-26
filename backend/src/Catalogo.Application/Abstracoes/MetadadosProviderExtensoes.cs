using Catalogo.Domain.Acesso;
using Catalogo.Domain.Excecoes;
using Catalogo.Domain.Metadados;

namespace Catalogo.Application.Abstracoes;

/// <summary>Para que a entidade é usada: decide o que acontece se ela (ou o grupo) estiver desativada (RN75, RN77).</summary>
public enum UsoEntidade
{
    /// <summary>Ver registros, listar, exportar, ver histórico e avaliações.</summary>
    Leitura,

    /// <summary>Criar, alterar e excluir registros, decidir aprovação, avaliar.</summary>
    Escrita,
}

public static class MetadadosProviderExtensoes
{
    /// <summary>
    /// A listagem (grupo) pela chave e o usuário como ele age DENTRO dela (<see cref="PoliticaPermissao.NaListagem"/>: "suas equipes"
    /// passam a ser só as habilitadas na listagem, RN91). <see cref="RecursoNaoEncontradoException"/> se o grupo não existe ou se o
    /// usuário não o vê: desativado (RN77) ou sem nenhuma equipe dele habilitada (RN90).
    /// Use SEMPRE o usuário devolvido daqui em diante: é ele que carrega o escopo da listagem.
    /// </summary>
    public static async Task<(Grupo Grupo, UsuarioAtual Usuario)> ObterGrupoOuFalharAsync(
        this IMetadadosProvider metadados, UsuarioAtual usuario, string grupoChave, CancellationToken cancellationToken)
    {
        var grupo = await metadados.ObterGrupoAsync(grupoChave, cancellationToken);
        if (grupo is null || !PoliticaPermissao.PodeVerListagem(usuario, grupo))
            throw new RecursoNaoEncontradoException($"Grupo '{grupoChave}' não encontrado.", CodigosErro.GrupoNaoEncontrado);
        return (grupo, PoliticaPermissao.NaListagem(usuario, grupo));
    }

    /// <summary>
    /// Grupo, entidade e o usuário na listagem (ver <see cref="ObterGrupoOuFalharAsync"/>), ou <see cref="RecursoNaoEncontradoException"/>.
    /// Entidade (ou grupo) desativada: 404 para o não-administrador; para o administrador, somente leitura (escrita: 409
    /// ENTIDADE_INATIVA). A regra está na <see cref="PoliticaPermissao.PodeUsarEntidade"/> (RN75, RN77).
    /// </summary>
    public static async Task<(Grupo Grupo, Entidade Entidade, UsuarioAtual Usuario)> ObterEntidadeOuFalharAsync(
        this IMetadadosProvider metadados, UsuarioAtual usuario, string grupoChave, string entidadeChave, UsoEntidade uso,
        CancellationToken cancellationToken)
    {
        var (grupo, usuarioNaListagem) = await metadados.ObterGrupoOuFalharAsync(usuario, grupoChave, cancellationToken);
        var entidade = grupo.ObterEntidade(entidadeChave);

        var decisao = entidade is null
            ? DecisaoPermissao.NaoEncontrado
            : PoliticaPermissao.PodeUsarEntidade(usuarioNaListagem, grupo, entidade, uso == UsoEntidade.Escrita);
        return decisao switch
        {
            DecisaoPermissao.NaoEncontrado => throw new RecursoNaoEncontradoException(
                $"Entidade '{entidadeChave}' não encontrada no grupo '{grupo.Chave}'.", CodigosErro.EntidadeNaoEncontrada),
            DecisaoPermissao.Proibido => throw new RegraDeNegocioException(
                $"'{entidade!.Nome}' está desativada{(grupo.Ativo ? "" : " (o grupo está desativado)")}: os registros são somente leitura. " +
                "Reative-a na administração de metadados para alterar.", CodigosErro.EntidadeInativa),
            _ => (grupo, entidade!, usuarioNaListagem),
        };
    }
}
