using Catalogo.Application.Abstracoes;
using Catalogo.Application.CasosDeUso.Acesso;
using Catalogo.Application.Comum;
using Catalogo.Application.Dtos;
using Catalogo.Application.Mapeamentos;
using Catalogo.Domain.Historico;
using Catalogo.Domain.Validacao;

namespace Catalogo.Application.CasosDeUso.Administracao;

/// <summary>
/// Caso de uso: alterar nome, perfil e equipes de um usuário (só ADMINISTRADOR). A matrícula não muda.
/// Protege o último administrador (RN30) e exige equipe ativa para não-administradores (RN31).
/// O efeito nas permissões é imediato (o cache do usuário é descartado).
/// </summary>
public sealed class AtualizarUsuario(
    IUsuarioAtual usuarioAtual,
    IUsuarioRepository usuarios,
    IEquipeRepository equipes,
    IUnidadeDeTrabalho unidadeDeTrabalho,
    ServicoHistorico historico,
    ISituacaoUsuarios situacao)
{
    public async Task<UsuarioDto> ExecutarAsync(Guid id, UsuarioEntrada entrada, CancellationToken cancellationToken)
    {
        var autor = await usuarioAtual.ObterAsync(cancellationToken);
        Permissoes.ExigirAdministrador(autor);
        var usuario = await ValidacaoUsuario.ObterOuFalharAsync(usuarios, id, cancellationToken);

        var erros = new ErrosPorCampo();
        CriarPrimeiroAdministrador.ValidarIdentificacao(usuario.Matricula, entrada.Nome, erros);
        var perfil = ValidacaoUsuario.LerPerfil(entrada.Perfil, erros);
        var equipeIds = await ValidacaoUsuario.ValidarEquipesAsync(equipes, perfil, entrada.Equipes, erros, cancellationToken);
        erros.LancarSeHouverErros();

        await ValidacaoUsuario.ProtegerUltimoAdministradorAsync(usuarios, usuario, perfil!.Value, usuario.Ativo, cancellationToken);

        var antes = usuario.ParaJsonHistorico();
        usuario.Nome = entrada.Nome!.Trim();
        usuario.Perfil = perfil.Value;
        ValidacaoUsuario.SincronizarEquipes(usuario, equipeIds);

        // O histórico registra também as mudanças na lista de equipes (RN32).
        historico.RegistrarUsuario(OperacaoHistorico.Alteracao, usuario.Id, autor.Id, autor.Matricula, antes, usuario.ParaJsonHistorico());
        await unidadeDeTrabalho.SalvarAlteracoesAsync(cancellationToken);
        situacao.Invalidar(usuario.Id);

        return usuario.ParaDto(await ValidacaoUsuario.EquipesDosUsuariosAsync(equipes, [usuario], cancellationToken));
    }
}
