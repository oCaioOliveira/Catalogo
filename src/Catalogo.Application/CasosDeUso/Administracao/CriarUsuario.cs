using Catalogo.Application.Abstracoes;
using Catalogo.Application.CasosDeUso.Acesso;
using Catalogo.Application.Comum;
using Catalogo.Application.Dtos;
using Catalogo.Application.Mapeamentos;
using Catalogo.Domain.Acesso;
using Catalogo.Domain.Excecoes;
using Catalogo.Domain.Historico;
using Catalogo.Domain.Validacao;

namespace Catalogo.Application.CasosDeUso.Administracao;

/// <summary>
/// Caso de uso: criar usuário (só ADMINISTRADOR). A senha é PROVISÓRIA, gerada pelo sistema e
/// devolvida uma única vez; o usuário precisa trocá-la no primeiro acesso (RN25).
/// </summary>
public sealed class CriarUsuario(
    IUsuarioAtual usuarioAtual,
    IUsuarioRepository usuarios,
    IEquipeRepository equipes,
    IHasherSenha hasher,
    IUnidadeDeTrabalho unidadeDeTrabalho,
    ServicoHistorico historico)
{
    public async Task<UsuarioCriadoDto> ExecutarAsync(UsuarioEntrada entrada, CancellationToken cancellationToken)
    {
        var autor = await usuarioAtual.ObterAsync(cancellationToken);
        Permissoes.ExigirAdministrador(autor);

        var erros = new ErrosPorCampo();
        CriarPrimeiroAdministrador.ValidarIdentificacao(entrada.Matricula, entrada.Nome, erros);
        var perfil = ValidacaoUsuario.LerPerfil(entrada.Perfil, erros);
        var equipeIds = await ValidacaoUsuario.ValidarEquipesAsync(equipes, perfil, entrada.Equipes, erros, cancellationToken);
        erros.LancarSeHouverErros();

        var matricula = entrada.Matricula!.Trim();
        if (await usuarios.ExisteMatriculaAsync(matricula, cancellationToken))
            throw new ConflitoUnicidadeException("UQ_Usuario_Matricula", ["matricula"], $"Já existe um usuário com a matrícula '{matricula}'.");

        var senhaProvisoria = PoliticaSenha.GerarProvisoria();
        var usuario = new Usuario { Matricula = matricula, Nome = entrada.Nome!.Trim(), Perfil = perfil!.Value };
        usuario.DefinirSenha(hasher.GerarHash(senhaProvisoria), provisoria: true);
        usuarios.Adicionar(usuario);   // gera o Id
        ValidacaoUsuario.SincronizarEquipes(usuario, equipeIds);

        historico.RegistrarUsuario(OperacaoHistorico.Criacao, usuario.Id, autor.Id, autor.Matricula, null, usuario.ParaJsonHistorico());

        try
        {
            await unidadeDeTrabalho.SalvarAlteracoesAsync(cancellationToken);
        }
        catch (ViolacaoUnicidadeBancoException)
        {
            throw new ConflitoUnicidadeException("UQ_Usuario_Matricula", ["matricula"], $"Já existe um usuário com a matrícula '{matricula}'.");
        }

        var dto = usuario.ParaDto(await ValidacaoUsuario.EquipesDosUsuariosAsync(equipes, [usuario], cancellationToken));
        return new UsuarioCriadoDto(dto, senhaProvisoria);
    }
}
