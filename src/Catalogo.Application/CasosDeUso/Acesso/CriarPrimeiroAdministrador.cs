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
/// Caso de uso: criar o PRIMEIRO administrador (comando "criar-admin"). Só funciona se não houver
/// nenhum administrador ativo (RN30). A senha vem de fora (variável de ambiente) e precisa ser trocada
/// no primeiro acesso. No histórico, o autor é o próprio administrador criado.
/// </summary>
public sealed class CriarPrimeiroAdministrador(
    IUsuarioRepository usuarios,
    IHasherSenha hasher,
    IUnidadeDeTrabalho unidadeDeTrabalho,
    ServicoHistorico historico)
{
    public async Task<UsuarioResumoDto> ExecutarAsync(string? matricula, string? nome, string? senha, CancellationToken cancellationToken)
    {
        if (await usuarios.ContarAdministradoresAtivosAsync(cancellationToken) > 0)
            throw new RegraDeNegocioException("Já existe um administrador ativo. Use a API para criar outros usuários.", CodigosErro.AdministradorJaExiste);

        var erros = new ErrosPorCampo();
        ValidarIdentificacao(matricula, nome, erros);
        foreach (var erro in PoliticaSenha.Validar(senha))
            erros.Adicionar("senha", erro);
        erros.LancarSeHouverErros();

        if (await usuarios.ExisteMatriculaAsync(matricula!.Trim(), cancellationToken))
            throw new ConflitoUnicidadeException("UQ_Usuario_Matricula", ["matricula"], $"Já existe um usuário com a matrícula '{matricula.Trim()}'.");

        var usuario = new Usuario
        {
            Matricula = matricula.Trim(),
            Nome = nome!.Trim(),
            Perfil = Perfil.Administrador,
        };
        usuario.DefinirSenha(hasher.GerarHash(senha!), provisoria: true);
        usuarios.Adicionar(usuario);   // gera o Id, usado no histórico

        historico.RegistrarUsuario(OperacaoHistorico.Criacao, usuario.Id, usuario.Id, usuario.Matricula, null, usuario.ParaJsonHistorico());
        await unidadeDeTrabalho.SalvarAlteracoesAsync(cancellationToken);

        return new UsuarioResumoDto(usuario.Id, usuario.Matricula, usuario.Nome);
    }

    /// <summary>Matrícula (até 20, sem espaços) e nome (até 150) obrigatórios.</summary>
    public static void ValidarIdentificacao(string? matricula, string? nome, ErrosPorCampo erros)
    {
        var m = matricula?.Trim();
        if (string.IsNullOrEmpty(m))
            erros.Adicionar("matricula", "Campo obrigatório.");
        else if (m.Length > 20 || m.Any(char.IsWhiteSpace))
            erros.Adicionar("matricula", "Deve ter até 20 caracteres, sem espaços.");

        var n = nome?.Trim();
        if (string.IsNullOrEmpty(n))
            erros.Adicionar("nome", "Campo obrigatório.");
        else if (n.Length > 150)
            erros.Adicionar("nome", "Deve ter no máximo 150 caracteres.");
    }
}
