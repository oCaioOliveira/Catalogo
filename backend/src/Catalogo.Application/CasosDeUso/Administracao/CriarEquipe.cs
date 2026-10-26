using Catalogo.Application.Abstracoes;
using Catalogo.Application.Comum;
using Catalogo.Application.Dtos;
using Catalogo.Application.Mapeamentos;
using Catalogo.Domain.Acesso;
using Catalogo.Domain.Excecoes;
using Catalogo.Domain.Historico;
using Catalogo.Domain.Validacao;

namespace Catalogo.Application.CasosDeUso.Administracao;

/// <summary>Caso de uso: criar equipe (só ADMINISTRADOR). Nome único (409 se repetido).</summary>
public sealed class CriarEquipe(
    IUsuarioAtual usuarioAtual, IEquipeRepository equipes, IUnidadeDeTrabalho unidadeDeTrabalho, ServicoHistorico historico)
{
    public async Task<EquipeDto> ExecutarAsync(EquipeEntrada entrada, CancellationToken cancellationToken)
    {
        var autor = await usuarioAtual.ObterAsync(cancellationToken);
        Permissoes.ExigirAdministrador(autor);

        var nome = await ValidarNomeAsync(equipes, entrada.Nome, null, cancellationToken);
        var equipe = new Equipe { Nome = nome };
        equipes.Adicionar(equipe);   // gera o Id

        historico.RegistrarEquipe(OperacaoHistorico.Criacao, equipe, autor, null, equipe.ParaJsonHistorico());
        await SalvarAsync(unidadeDeTrabalho, nome, cancellationToken);
        return equipe.ParaDto(membrosAtivos: 0, listagens: []); // RN93: equipe nova não é habilitada em nenhuma listagem
    }

    /// <summary>Nome obrigatório, até 150 caracteres e único (sem diferenciar maiúsculas nem acentos).</summary>
    public static async Task<string> ValidarNomeAsync(
        IEquipeRepository equipes, string? nomeInformado, Guid? equipeAtualId, CancellationToken cancellationToken)
    {
        var nome = nomeInformado?.Trim();
        var erros = new ErrosPorCampo();
        if (string.IsNullOrEmpty(nome))
            erros.Adicionar("nome", "Campo obrigatório.");
        else if (nome.Length > 150)
            erros.Adicionar("nome", "Deve ter no máximo 150 caracteres.");
        erros.LancarSeHouverErros();

        if (await equipes.ExisteNomeAsync(nome!, equipeAtualId, cancellationToken))
            throw ConflitoDeNome(nome!);

        return nome!;
    }

    /// <summary>Salva traduzindo a corrida no UNIQUE do nome para o mesmo 409.</summary>
    public static async Task SalvarAsync(IUnidadeDeTrabalho unidadeDeTrabalho, string nome, CancellationToken cancellationToken)
    {
        try
        {
            await unidadeDeTrabalho.SalvarAlteracoesAsync(cancellationToken);
        }
        catch (ViolacaoUnicidadeBancoException)
        {
            throw ConflitoDeNome(nome);
        }
    }

    private static ConflitoUnicidadeException ConflitoDeNome(string nome) =>
        new("UQ_Equipe_Nome", ["nome"], $"Já existe uma equipe com o nome '{nome}'.");
}
