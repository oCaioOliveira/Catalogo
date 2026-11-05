using Catalogo.Application.Abstracoes;
using Catalogo.Application.Comum;
using Catalogo.Application.Dtos;
using Catalogo.Application.Mapeamentos;
using Catalogo.Domain.Acesso;
using Catalogo.Domain.Excecoes;
using Catalogo.Domain.Historico;

namespace Catalogo.Application.CasosDeUso.Acesso;

/// <summary>
/// Caso de uso: login pela autenticação integrada do Windows (RN23, docs/adr/0054). Recebe o nome que o Windows autenticou
/// (<c>DOMINIO\c161587</c>), tira a matrícula (<c>C161587</c>) e devolve o JWT e os dados do usuário. Matrícula ainda não
/// cadastrada vira um usuário EXTERNO, ativo e sem equipe (RN105): perfil e equipes continuam sendo dados pelo administrador.
/// </summary>
public sealed class Login(
    IUsuarioRepository usuarios,
    IEquipeRepository equipes,
    IGeradorToken geradorToken,
    IUnidadeDeTrabalho unidadeDeTrabalho,
    ServicoHistorico historico,
    TimeProvider relogio)
{
    public const string MensagemSemMatricula = "Não foi possível identificar a sua matrícula no login do Windows.";
    public const string MensagemDesativado = "O seu acesso ao Catálogo está desativado. Procure o administrador.";

    /// <param name="nomeWindows">O nome autenticado pelo Windows (HttpContext.User.Identity.Name).</param>
    /// <exception cref="NaoAutenticadoException">Sem nome do Windows ou sem uma matrícula válida nele.</exception>
    /// <exception cref="AcessoNegadoException">Usuário desativado (RN31).</exception>
    public async Task<LoginDto> ExecutarAsync(string? nomeWindows, CancellationToken cancellationToken)
    {
        var matricula = MatriculaWindows.Extrair(nomeWindows)
            ?? throw new NaoAutenticadoException(MensagemSemMatricula);

        var usuario = await ObterOuCadastrarAsync(matricula, cancellationToken);
        if (!usuario.Ativo)
            throw new AcessoNegadoException(MensagemDesativado);

        usuario.RegistrarAcesso(relogio.GetUtcNow().UtcDateTime);
        await unidadeDeTrabalho.SalvarAlteracoesAsync(cancellationToken);

        var token = geradorToken.Gerar(usuario);
        var dados = await ObterUsuarioLogado.MontarAsync(usuario, equipes, cancellationToken);
        return new LoginDto(token.Token, token.ExpiraEm, dados);
    }

    private async Task<Usuario> ObterOuCadastrarAsync(string matricula, CancellationToken cancellationToken)
    {
        var existente = await usuarios.ObterPorMatriculaAsync(matricula, cancellationToken);
        if (existente is not null)
            return existente;

        // RN105: primeiro acesso. O nome começa como a matrícula; o administrador o corrige em Usuários.
        var novo = new Usuario { Matricula = matricula, Nome = matricula, Perfil = Perfil.Externo };
        usuarios.Adicionar(novo);   // gera o Id, usado no histórico
        historico.RegistrarUsuario(OperacaoHistorico.Criacao, novo.Id, novo.Id, novo.Matricula, null, novo.ParaJsonHistorico());

        try
        {
            await unidadeDeTrabalho.SalvarAlteracoesAsync(cancellationToken);
            return novo;
        }
        catch (ViolacaoUnicidadeBancoException)
        {
            // Dois primeiros acessos ao mesmo tempo (ex.: duas abas): o outro cadastrou antes. Vale o que já está no banco.
            unidadeDeTrabalho.DescartarAlteracoes();
            return await usuarios.ObterPorMatriculaAsync(matricula, cancellationToken)
                ?? throw new NaoAutenticadoException(MensagemSemMatricula);
        }
    }
}
