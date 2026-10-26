using Catalogo.Application.Abstracoes;
using Catalogo.Application.Comum;
using Catalogo.Application.Dtos;
using Catalogo.Domain.Acesso;
using Catalogo.Domain.Excecoes;

namespace Catalogo.Application.CasosDeUso;

/// <summary>
/// Caso de uso: buscar um registro pelo id. Devolve os dados planos (campos simples e
/// referências juntos, referências como { id, rotulo }). Lança <see cref="RecursoNaoEncontradoException"/>,
/// inclusive para registro de outra equipe (RN28).
/// </summary>
public sealed class ObterRegistro(
    IUsuarioAtual usuarioAtual, IMetadadosProvider metadados, IRegistroRepository registros, MontadorRegistroDto montador)
{
    public async Task<RegistroDto> ExecutarAsync(
        string grupoChave, string entidadeChave, Guid id, CancellationToken cancellationToken)
    {
        var usuario = await usuarioAtual.ObterAsync(cancellationToken);
        (_, var entidade, usuario) = await metadados.ObterEntidadeOuFalharAsync(usuario, grupoChave, entidadeChave, UsoEntidade.Leitura, cancellationToken);

        var registro = await registros.ObterPorIdAsync(id, cancellationToken);
        var descricao = $"Registro {id} em '{entidade.Nome}'";

        // Um id de outra entidade também é "não encontrado" nesta URL.
        if (registro is null || registro.EntidadeId != entidade.Id)
            throw new RecursoNaoEncontradoException($"{descricao} não encontrado.", CodigosErro.RegistroNaoEncontrado);

        // RN28 e RN37: equipe e situação de aprovação decidem quem vê.
        Permissoes.Exigir(PoliticaPermissao.Decidir(usuario, OperacaoRegistro.Ver, RegistroAlvo.De(entidade, registro, false)), descricao);

        return await montador.MontarAsync(registro, entidade, cancellationToken);
    }
}
