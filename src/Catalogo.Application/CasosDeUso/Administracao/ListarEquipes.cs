using Catalogo.Application.Abstracoes;
using Catalogo.Application.Comum;
using Catalogo.Application.Dtos;
using Catalogo.Application.Mapeamentos;
using Catalogo.Domain.Consultas;
using Catalogo.Domain.Validacao;

namespace Catalogo.Application.CasosDeUso.Administracao;

/// <summary>
/// Caso de uso: listar equipes (só ADMINISTRADOR), ordenadas por nome, paginadas como as demais listagens (RN17).
/// Filtros opcionais: "ativa" (true ou false; ausente = todas) e "busca" (contido no nome, sem diferenciar maiúsculas nem
/// acentos). Cada equipe traz a quantidade de membros ativos, contada no banco numa consulta só, e as listagens em que está habilitada (RN90).
/// </summary>
public sealed class ListarEquipes(IUsuarioAtual usuarioAtual, IEquipeRepository equipes)
{
    /// <param name="parametros">Parâmetros da URL: ativa, busca, pagina, tamanho.</param>
    public async Task<PaginaDto<EquipeDto>> ExecutarAsync(
        IEnumerable<KeyValuePair<string, string>> parametros, CancellationToken cancellationToken)
    {
        Permissoes.ExigirAdministrador(await usuarioAtual.ObterAsync(cancellationToken));

        var lista = parametros.ToList();
        var erros = new ErrosPorCampo();
        var paginacao = InterpretadorConsulta.InterpretarPaginacao(lista, erros);

        bool? ativa = null;
        if (InterpretadorConsulta.LerUnico(lista, "ativa", erros) is string texto)
        {
            if (bool.TryParse(texto, out var valor))
                ativa = valor;
            else
                erros.Adicionar("ativa", "Deve ser true ou false.");
        }

        var busca = ListarUsuarios.LerBusca(lista, erros);
        erros.LancarSeHouverErros();

        var pagina = await equipes.ListarAsync(ativa, busca, paginacao, cancellationToken);
        var ids = pagina.Itens.Select(e => e.Id).ToList();
        var membros = await equipes.ContarMembrosAtivosAsync(ids, cancellationToken);
        var listagens = await equipes.ListarListagensAsync(ids, cancellationToken);
        return new PaginaDto<EquipeDto>(
            pagina.Itens.Select(e => e.ParaDto(membros.GetValueOrDefault(e.Id), listagens.GetValueOrDefault(e.Id))).ToList(),
            paginacao.Pagina, paginacao.Tamanho, pagina.Total, paginacao.TotalPaginas(pagina.Total));
    }
}
