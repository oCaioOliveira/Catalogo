using Catalogo.Application.Abstracoes;
using Catalogo.Application.Comum;
using Catalogo.Application.Dtos;
using Catalogo.Application.Mapeamentos;
using Catalogo.Domain.Historico;

namespace Catalogo.Application.CasosDeUso.Administracao;

/// <summary>Caso de uso: renomear equipe (só ADMINISTRADOR).</summary>
public sealed class AtualizarEquipe(
    IUsuarioAtual usuarioAtual, IEquipeRepository equipes, IUnidadeDeTrabalho unidadeDeTrabalho, ServicoHistorico historico)
{
    public async Task<EquipeDto> ExecutarAsync(Guid id, EquipeEntrada entrada, CancellationToken cancellationToken)
    {
        var autor = await usuarioAtual.ObterAsync(cancellationToken);
        Permissoes.ExigirAdministrador(autor);
        var equipe = await ObterEquipe.ObterOuFalharAsync(equipes, id, cancellationToken);

        var nome = await CriarEquipe.ValidarNomeAsync(equipes, entrada.Nome, equipe.Id, cancellationToken);
        if (nome != equipe.Nome)
        {
            var antes = equipe.ParaJsonHistorico();
            equipe.Nome = nome;
            historico.RegistrarEquipe(OperacaoHistorico.Alteracao, equipe, autor, antes, equipe.ParaJsonHistorico());
            await CriarEquipe.SalvarAsync(unidadeDeTrabalho, nome, cancellationToken);
        }

        return await ObterEquipe.ParaDtoAsync(equipes, equipe, cancellationToken);
    }
}
