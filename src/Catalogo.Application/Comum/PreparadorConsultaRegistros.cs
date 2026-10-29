using Catalogo.Application.Abstracoes;
using Catalogo.Domain.Acesso;
using Catalogo.Domain.Avaliacoes;
using Catalogo.Domain.Consultas;
using Catalogo.Domain.Excecoes;
using Catalogo.Domain.Metadados;

namespace Catalogo.Application.Comum;

/// <summary>Uma consulta de registros pronta para o repositório: interpretada E com o escopo do usuário.</summary>
public sealed record ConsultaPreparada(UsuarioAtual Usuario, Entidade Entidade, ConsultaRegistros Consulta);

/// <summary>
/// Interpreta os parâmetros da URL (filtros, ordenação, página, RN17 a RN19 e RN48) e aplica o ESCOPO DE
/// SEGURANÇA do usuário (equipes, RN26, e situação de aprovação, RN37). Usado pela listagem E pela exportação:
/// as duas veem exatamente os mesmos registros, porque a regra está num lugar só.
/// </summary>
public sealed class PreparadorConsultaRegistros(
    IUsuarioAtual usuarioAtual, IMetadadosProvider metadados, IModeloAvaliacaoRepository modelos)
{
    /// <param name="parametros">Todos os parâmetros da URL (um par por valor, inclusive repetidos).</param>
    /// <exception cref="RecursoNaoEncontradoException">Grupo ou entidade não existe.</exception>
    /// <exception cref="Domain.Excecoes.ValidacaoRegistroException">Parâmetro inválido (400, com a chave do parâmetro).</exception>
    /// <exception cref="AcessoNegadoException">"equipeId" de uma equipe do qual o usuário não participa.</exception>
    public async Task<ConsultaPreparada> PrepararAsync(
        string grupoChave, string entidadeChave, IReadOnlyList<KeyValuePair<string, string>> parametros, CancellationToken cancellationToken)
    {
        var usuario = await usuarioAtual.ObterAsync(cancellationToken);
        (var grupo, var entidade, usuario) = await metadados.ObterEntidadeOuFalharAsync(usuario, grupoChave, entidadeChave, UsoEntidade.Leitura, cancellationToken);

        var criteriosAvaliacao = await CriteriosAvaliacaoAsync(usuario, entidade, parametros, cancellationToken);
        var consulta = InterpretadorConsulta.Interpretar(entidade, parametros, criteriosAvaliacao, grupo);

        return new ConsultaPreparada(usuario, entidade, AplicarEscopo(usuario, entidade, consulta));
    }

    /// <summary>
    /// Só o ESCOPO de visibilidade (sem filtros de campo): o que o usuário vê da entidade, opcionalmente só de uma equipe.
    /// Usado pelo painel (RN79), que soma os números de todas as entidades do grupo com a mesma regra da listagem.
    /// </summary>
    /// <exception cref="AcessoNegadoException"><paramref name="filtroEquipe"/> é de uma equipe que o usuário não vê.</exception>
    public static ConsultaRegistros Escopo(UsuarioAtual usuario, Entidade entidade, Guid? filtroEquipe) =>
        AplicarEscopo(usuario, entidade, new ConsultaRegistros(
            [], Ordenacao.Padrao, Paginacao.Padrao, FiltroEquipe: entidade.EhGlobal ? null : filtroEquipe));

    /// <summary>
    /// O escopo de segurança, aplicado DENTRO da consulta (nunca em memória):
    ///   - RN26: entidades globais são vistas por todos; nas por equipe, o não-administrador só vê as suas equipes;
    ///   - RN37: o EXTERNO só vê registros não aprovados que ele mesmo criou.
    /// </summary>
    private static ConsultaRegistros AplicarEscopo(UsuarioAtual usuario, Entidade entidade, ConsultaRegistros consulta)
    {
        if (!entidade.EhGlobal)
        {
            var visiveis = PoliticaPermissao.EquipesVisiveis(usuario);
            if (consulta.FiltroEquipe is Guid filtro && visiveis is not null && !visiveis.Contains(filtro))
                throw new AcessoNegadoException("Você não pertence a essa equipe.");

            consulta = consulta with { EquipesPermitidas = visiveis };
        }

        return consulta with { NaoAprovadosSomenteDoAutor = PoliticaPermissao.NaoAprovadosSomenteDoAutor(usuario) };
    }

    /// <summary>
    /// RN48: critérios ESTRELAS ativos nos modelos ativos da entidade, nas equipes que o usuário vê. Só são
    /// buscados se a URL usa "avaliacao." (na ordenação ou num filtro).
    /// </summary>
    private async Task<IReadOnlySet<string>> CriteriosAvaliacaoAsync(
        UsuarioAtual usuario, Entidade entidade, IReadOnlyList<KeyValuePair<string, string>> parametros, CancellationToken cancellationToken)
    {
        var usaAvaliacao = parametros.Any(p =>
            p.Key.StartsWith(InterpretadorConsulta.PrefixoAvaliacao, StringComparison.Ordinal)
            || (p.Key == ChavesReservadas.Ordenacao && p.Value.Contains(InterpretadorConsulta.PrefixoAvaliacao, StringComparison.Ordinal)));
        if (!usaAvaliacao || entidade.EhGlobal)
            return new HashSet<string>();

        var ativos = await modelos.ListarAtivosAsync(entidade.Id, PoliticaPermissao.EquipesVisiveis(usuario)?.ToList(), cancellationToken);
        return ativos
            .SelectMany(m => m.CriteriosAtivos)
            .Where(c => c.Tipo == TipoCriterio.Estrelas)
            .Select(c => c.Chave)
            .ToHashSet(StringComparer.Ordinal);
    }
}
