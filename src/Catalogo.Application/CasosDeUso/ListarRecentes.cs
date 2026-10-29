using Catalogo.Application.Abstracoes;
using Catalogo.Application.Comum;
using Catalogo.Application.Dtos;
using Catalogo.Domain.Acesso;

namespace Catalogo.Application.CasosDeUso;

/// <summary>
/// Caso de uso: os itens que o usuário criou ou alterou por último numa área de gestão (RN96), para o "Continuar de onde parou" da
/// página Início. Vem do HISTÓRICO (RN32), mas só com os itens que ele ainda VÊ: o escopo de cada tipo de item é o mesmo da listagem
/// (<see cref="PreparadorConsultaRegistros.Escopo"/>: equipes do usuário nesta área, aprovação, globais). Item excluído, de equipe que
/// deixou de ser habilitada ou de tipo de item desativado não aparece. Tudo no banco, em LINQ, numa consulta só.
/// </summary>
public sealed class ListarRecentes(IUsuarioAtual usuarioAtual, IMetadadosProvider metadados, IRegistroRepository registros)
{
    /// <summary>Quantos itens a página Início mostra.</summary>
    public const int Limite = 8;

    public async Task<IReadOnlyList<RecenteDto>> ExecutarAsync(string grupoChave, CancellationToken cancellationToken)
    {
        var usuario = await usuarioAtual.ObterAsync(cancellationToken);
        (var grupo, usuario) = await metadados.ObterGrupoOuFalharAsync(usuario, grupoChave, cancellationToken);

        var entidades = grupo.Entidades
            .Where(e => PoliticaPermissao.PodeUsarEntidade(usuario, grupo, e, escrita: false) == DecisaoPermissao.Permitido)
            .ToList();
        if (entidades.Count == 0)
            return [];

        var escopos = entidades.Select(e => new EscopoDaEntidade(e.Id, PreparadorConsultaRegistros.Escopo(usuario, e, null))).ToList();
        var itens = await registros.ListarRecentesDoUsuarioAsync(usuario.Id, escopos, Limite, cancellationToken);
        var rotulos = await registros.ObterRotulosAsync(itens.Select(i => i.RegistroId).ToList(), cancellationToken);
        var chaves = entidades.ToDictionary(e => e.Id, e => e.Chave);

        return itens
            .Select(i => new RecenteDto(i.RegistroId, chaves[i.EntidadeId], rotulos.GetValueOrDefault(i.RegistroId), i.OcorridoEm))
            .ToList();
    }
}
