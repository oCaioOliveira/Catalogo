using Catalogo.Application.Abstracoes;
using Catalogo.Application.Comum;
using Catalogo.Application.Dtos;
using Catalogo.Application.Mapeamentos;
using Catalogo.Domain.Acesso;
using Catalogo.Domain.Excecoes;
using Catalogo.Domain.Historico;
using Catalogo.Domain.Validacao;

namespace Catalogo.Application.CasosDeUso.Acesso;

/// <summary>
/// Caso de uso: login com matrícula e senha (RN23). Devolve o token (JWT) e os dados do usuário.
/// Lança <see cref="NaoAutenticadoException"/> com uma mensagem SEMPRE igual, sem dizer o motivo.
/// </summary>
public sealed class Login(IProvedorIdentidade provedorIdentidade, IGeradorToken geradorToken, IEquipeRepository equipes)
{
    public const string MensagemFalha = "Matrícula ou senha inválidas.";

    public async Task<LoginDto> ExecutarAsync(LoginEntrada entrada, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(entrada.Matricula) || string.IsNullOrEmpty(entrada.Senha))
            throw new NaoAutenticadoException(MensagemFalha, CodigosErro.CredenciaisInvalidas);

        var usuario = await provedorIdentidade.AutenticarAsync(entrada.Matricula, entrada.Senha, cancellationToken)
            ?? throw new NaoAutenticadoException(MensagemFalha, CodigosErro.CredenciaisInvalidas);

        var token = geradorToken.Gerar(usuario);
        var dados = await ObterUsuarioLogado.MontarAsync(usuario, equipes, cancellationToken);
        return new LoginDto(token.Token, token.ExpiraEm, usuario.DeveTrocarSenha, dados);
    }
}
