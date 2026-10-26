using Catalogo.Application.Abstracoes;
using Catalogo.Application.Comum;
using Catalogo.Application.Dtos;
using Catalogo.Application.Mapeamentos;
using Catalogo.Domain.Excecoes;
using Catalogo.Domain.Historico;

namespace Catalogo.Application.CasosDeUso.Administracao;

/// <summary>
/// Caso de uso: ativar ou desativar uma equipe (só ADMINISTRADOR). Equipe nunca é excluída (RN31).
/// Vínculos com equipe inativa não dão permissão: a mudança vale para todos na hora. Não desativa
/// uma equipe que é a única equipe ativa de algum usuário não-administrador ativo (RN31).
/// </summary>
public sealed class AlterarAtivacaoEquipe(
    IUsuarioAtual usuarioAtual,
    IEquipeRepository equipes,
    IUnidadeDeTrabalho unidadeDeTrabalho,
    ServicoHistorico historico,
    ISituacaoUsuarios situacao)
{
    public async Task<EquipeDto> ExecutarAsync(Guid id, AtivacaoEquipeEntrada entrada, CancellationToken cancellationToken)
    {
        var autor = await usuarioAtual.ObterAsync(cancellationToken);
        Permissoes.ExigirAdministrador(autor);
        var equipe = await ObterEquipe.ObterOuFalharAsync(equipes, id, cancellationToken);

        var ativa = entrada.Ativa
            ?? throw new ValidacaoRegistroException(new Dictionary<string, string[]> { ["ativa"] = ["Campo obrigatório (true ou false)."] });

        if (!ativa && equipe.Ativa)
        {
            var semEquipe = await equipes.ContarUsuariosQueFicariamSemEquipeAsync(equipe.Id, cancellationToken);
            if (semEquipe > 0)
            {
                throw new RegraDeNegocioException(
                    $"{semEquipe} usuário(s) ficariam sem nenhuma equipe ativa. Inclua-os em outra equipe antes de desativar esta.",
                    CodigosErro.UsuariosSemEquipe,
                    new Dictionary<string, object?> { ["quantidade"] = semEquipe });
            }
        }

        if (equipe.Ativa != ativa)
        {
            var antes = equipe.ParaJsonHistorico();
            equipe.Ativa = ativa;
            historico.RegistrarEquipe(OperacaoHistorico.Alteracao, equipe, autor, antes, equipe.ParaJsonHistorico());
            await unidadeDeTrabalho.SalvarAlteracoesAsync(cancellationToken);

            // As equipes ativas de muitos usuários mudaram: descarta o cache de todos.
            situacao.InvalidarTodos();
        }

        return await ObterEquipe.ParaDtoAsync(equipes, equipe, cancellationToken);
    }
}
