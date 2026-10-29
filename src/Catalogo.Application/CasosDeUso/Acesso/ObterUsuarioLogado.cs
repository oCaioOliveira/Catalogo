using Catalogo.Application.Abstracoes;
using Catalogo.Application.Comum;
using Catalogo.Application.Dtos;
using Catalogo.Application.Mapeamentos;
using Catalogo.Domain.Acesso;
using Catalogo.Domain.Excecoes;
using Catalogo.Domain.Historico;
using Catalogo.Domain.Validacao;

namespace Catalogo.Application.CasosDeUso.Acesso;

/// <summary>Caso de uso: dados do usuário logado (GET /api/auth/eu). Funciona mesmo com troca de senha pendente.</summary>
public sealed class ObterUsuarioLogado(IUsuarioAtual usuarioAtual, IUsuarioRepository usuarios, IEquipeRepository equipes)
{
    public async Task<UsuarioLogadoDto> ExecutarAsync(CancellationToken cancellationToken)
    {
        var atual = await usuarioAtual.ObterAsync(cancellationToken);
        var usuario = await usuarios.ObterPorIdAsync(atual.Id, cancellationToken)
            ?? throw new NaoAutenticadoException("Usuário não encontrado.");

        return await MontarAsync(usuario, equipes, cancellationToken);
    }

    /// <summary>Dados do usuário com as equipes ATIVAS dele.</summary>
    public static async Task<UsuarioLogadoDto> MontarAsync(Usuario usuario, IEquipeRepository equipes, CancellationToken cancellationToken)
    {
        var vinculadas = await equipes.ObterPorIdsAsync(usuario.Equipes.Select(e => e.EquipeId).ToList(), cancellationToken);
        return new UsuarioLogadoDto(
            usuario.Id,
            usuario.Matricula,
            usuario.Nome,
            PerfilConversor.ParaTexto(usuario.Perfil),
            usuario.DeveTrocarSenha,
            vinculadas.Where(e => e.Ativa).OrderBy(e => e.Nome, StringComparer.CurrentCulture).Select(e => e.ParaResumoDto()).ToList());
    }
}
