using Catalogo.Domain.Acesso;
using Catalogo.Domain.Excecoes;

namespace Catalogo.Application.Comum;

/// <summary>
/// Transforma as decisões da <see cref="PoliticaPermissao"/> (Domain) nas exceções que viram
/// 404 e 403. A REGRA fica na política; aqui só a tradução.
/// </summary>
public static class Permissoes
{
    /// <param name="descricaoDoRecurso">Ex.: "Registro {id} em 'Projeto'" (usado na mensagem do 404).</param>
    /// <param name="codigoNaoEncontrado">Código do 404 (de <see cref="CodigosErro"/>): o recurso que "não existe" para o usuário.</param>
    public static void Exigir(
        DecisaoPermissao decisao, string descricaoDoRecurso, string codigoNaoEncontrado = CodigosErro.RegistroNaoEncontrado)
    {
        switch (decisao)
        {
            case DecisaoPermissao.NaoEncontrado:
                // RN28: mesma mensagem de um registro que não existe.
                throw new RecursoNaoEncontradoException($"{descricaoDoRecurso} não encontrado.", codigoNaoEncontrado);
            case DecisaoPermissao.Proibido:
                throw new AcessoNegadoException("Seu perfil não permite esta operação.");
        }
    }

    /// <summary>Administração de usuários e equipes e histórico deles: só ADMINISTRADOR.</summary>
    public static void ExigirAdministrador(UsuarioAtual usuario)
    {
        if (!PoliticaPermissao.PodeAdministrarAcessos(usuario))
            throw new AcessoNegadoException("Somente administradores podem fazer esta operação.");
    }
}
