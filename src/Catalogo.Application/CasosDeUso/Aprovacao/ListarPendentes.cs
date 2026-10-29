using Catalogo.Application.Abstracoes;
using Catalogo.Application.Dtos;
using Catalogo.Application.Mapeamentos;
using Catalogo.Domain.Acesso;
using Catalogo.Domain.Consultas;
using Catalogo.Domain.Excecoes;
using Catalogo.Domain.Validacao;

namespace Catalogo.Application.CasosDeUso.Aprovacao;

/// <summary>
/// Caso de uso: fila de aprovação (RN36). Registros PENDENTES de todas as entidades das equipes do
/// GESTOR (o ADMINISTRADOR vê todas), dos mais antigos para os mais novos, paginada. Só listagens que o usuário vê e só
/// equipes habilitadas na listagem de cada item (RN90, RN91).
/// Filtros opcionais: grupo, entidade e equipeId. INTERNO e EXTERNO recebem 403.
/// </summary>
public sealed class ListarPendentes(
    IUsuarioAtual usuarioAtual, IRegistroRepository registros, IUsuarioRepository usuarios, IEquipeRepository equipes,
    IMetadadosProvider metadados)
{
    public async Task<PaginaDto<PendenteDto>> ExecutarAsync(
        IEnumerable<KeyValuePair<string, string>> parametros, CancellationToken cancellationToken)
    {
        var usuario = await usuarioAtual.ObterAsync(cancellationToken);
        if (!PoliticaPermissao.PodeDecidirAlgo(usuario))
            throw new AcessoNegadoException("Somente gestores e administradores têm fila de aprovação.");

        var lista = parametros.ToList();
        var erros = new ErrosPorCampo();
        var paginacao = InterpretadorConsulta.InterpretarPaginacao(lista, erros);
        var grupo = InterpretadorConsulta.LerUnico(lista, "grupo", erros)?.Trim();
        var entidade = InterpretadorConsulta.LerUnico(lista, "entidade", erros)?.Trim();

        Guid? equipeId = null;
        if (InterpretadorConsulta.LerUnico(lista, "equipeId", erros) is string texto)
        {
            if (Guid.TryParseExact(texto.Trim(), "D", out var id))
                equipeId = id;
            else
                erros.Adicionar("equipeId", "Deve ser o id (GUID) de uma equipe.");
        }

        erros.LancarSeHouverErros();

        // RN90: filtrar por uma listagem que o usuário não vê é 404, como em todo endpoint sob ela.
        if (NuloSeVazio(grupo) is string grupoChave)
            await metadados.ObterGrupoOuFalharAsync(usuario, grupoChave, cancellationToken);

        var permitidas = PoliticaPermissao.EquipesDaFilaDeAprovacao(usuario);
        if (equipeId is Guid filtro && permitidas is not null && !permitidas.Contains(filtro))
            throw new AcessoNegadoException("Você não é gestor dessa equipe.");

        var pagina = await registros.ListarPendentesAsync(
            new FiltroPendentes(NuloSeVazio(grupo), NuloSeVazio(entidade), equipeId, permitidas,
                PoliticaPermissao.VeListagensDesativadas(usuario)), paginacao, cancellationToken);

        // Rótulos, autores e equipes em lote (sem N+1).
        var ids = pagina.Itens.Select(p => p.RegistroId).ToList();
        var rotulos = ids.Count == 0 ? new Dictionary<Guid, string?>() : await registros.ObterRotulosAsync(ids, cancellationToken);
        var autores = await usuarios.ObterResumosAsync(pagina.Itens.Select(p => p.CriadoPorId).OfType<Guid>().Distinct().ToList(), cancellationToken);
        var equipesPorId = (await equipes.ObterPorIdsAsync(pagina.Itens.Select(p => p.EquipeId).OfType<Guid>().Distinct().ToList(), cancellationToken))
            .ToDictionary(e => e.Id);

        var itens = pagina.Itens
            .Select(p => new PendenteDto(
                p.RegistroId,
                p.GrupoChave,
                new EntidadeResumoDto(p.EntidadeChave, p.EntidadeNome),
                p.EquipeId is Guid e && equipesPorId.TryGetValue(e, out var equipe) ? equipe.ParaResumoDto() : null,
                rotulos.GetValueOrDefault(p.RegistroId),
                p.CriadoPorId is Guid autor ? autores.GetValueOrDefault(autor) : null,
                p.CriadoEm))
            .ToList();

        return new PaginaDto<PendenteDto>(itens, paginacao.Pagina, paginacao.Tamanho, pagina.Total, paginacao.TotalPaginas(pagina.Total));
    }

    private static string? NuloSeVazio(string? texto) => string.IsNullOrEmpty(texto) ? null : texto;
}
