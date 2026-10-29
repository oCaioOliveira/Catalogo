using Catalogo.Application.Abstracoes;
using Catalogo.Domain.Acesso;
using Catalogo.Domain.Excecoes;
using Catalogo.Domain.Metadados;

namespace Catalogo.Application.Comum;

/// <summary>Passos comuns às configurações de uma entidade numa equipe: aprovação (RN33) e modelos de avaliação (RN40).</summary>
public static class ConfiguracaoDaEquipe
{
    /// <summary>
    /// Confere quem pode configurar a equipe (GESTOR das suas, ADMINISTRADOR de qualquer uma), a
    /// existência da equipe e que a entidade é POR EQUIPE. Devolve a entidade.
    /// A listagem vem primeiro: só se configura uma equipe HABILITADA numa listagem que o usuário vê (RN90, RN91).
    /// </summary>
    /// <param name="funcionalidade">Para a mensagem de entidade global, ex.: "aprovação".</param>
    public static async Task<Entidade> ValidarAsync(
        UsuarioAtual usuario, Guid equipeId, string grupoChave, string entidadeChave, string funcionalidade,
        IEquipeRepository equipes, IMetadadosProvider metadados, CancellationToken cancellationToken)
    {
        (_, var entidade, usuario) = await metadados.ObterEntidadeOuFalharAsync(
            usuario, grupoChave, entidadeChave, UsoEntidade.Leitura, cancellationToken);

        // 404 para a equipe de outro gestor ou não habilitada na listagem (não revela que ela existe); 403 para INTERNO e EXTERNO.
        var decisao = PoliticaPermissao.PodeConfigurarEquipe(usuario, equipeId);
        if (decisao == DecisaoPermissao.NaoEncontrado || await equipes.ObterPorIdAsync(equipeId, cancellationToken) is null)
            throw new RecursoNaoEncontradoException($"Equipe {equipeId} não encontrada.", CodigosErro.EquipeNaoEncontrada);
        Permissoes.Exigir(decisao, $"Equipe {equipeId}", CodigosErro.EquipeNaoEncontrada);

        // Globais só são escritas pelo administrador: aprovação e avaliação não se aplicam a elas.
        if (entidade.EhGlobal)
        {
            throw new ValidacaoRegistroException(new Dictionary<string, string[]>
            {
                ["entidade"] = [$"'{entidade.Nome}' é global: {funcionalidade} só existe em entidades por equipe."],
            });
        }

        return entidade;
    }
}
