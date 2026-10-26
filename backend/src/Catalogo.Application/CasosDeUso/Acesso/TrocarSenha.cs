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
/// Caso de uso: o próprio usuário troca a senha (RN25). Obrigatório depois de uma senha provisória.
/// Lança <see cref="ValidacaoRegistroException"/> (senha atual errada ou senha nova fora da política).
/// </summary>
public sealed class TrocarSenha(
    IUsuarioAtual usuarioAtual,
    IUsuarioRepository usuarios,
    IHasherSenha hasher,
    IUnidadeDeTrabalho unidadeDeTrabalho,
    ServicoHistorico historico,
    ISituacaoUsuarios situacao)
{
    public async Task ExecutarAsync(TrocaSenhaEntrada entrada, CancellationToken cancellationToken)
    {
        var atual = await usuarioAtual.ObterAsync(cancellationToken);
        var usuario = await usuarios.ObterPorIdAsync(atual.Id, cancellationToken)
            ?? throw new NaoAutenticadoException("Usuário não encontrado.");

        var erros = new ErrosPorCampo();
        if (string.IsNullOrEmpty(entrada.SenhaAtual) || !hasher.Verificar(usuario.SenhaHash, entrada.SenhaAtual))
            erros.Adicionar("senhaAtual", "Senha atual incorreta.");
        foreach (var erro in PoliticaSenha.Validar(entrada.NovaSenha, entrada.SenhaAtual))
            erros.Adicionar("novaSenha", erro);
        erros.LancarSeHouverErros();

        var antes = usuario.ParaJsonHistorico();
        usuario.DefinirSenha(hasher.GerarHash(entrada.NovaSenha!), provisoria: false);

        // Histórico sem a senha: só a indicação de que foi alterada (RN32).
        var depois = usuario.ParaJsonHistorico();
        depois["senha"] = "alterada";
        historico.RegistrarUsuario(OperacaoHistorico.Alteracao, usuario.Id, atual.Id, atual.Matricula, antes, depois);

        await unidadeDeTrabalho.SalvarAlteracoesAsync(cancellationToken);
        situacao.Invalidar(usuario.Id);
    }
}
