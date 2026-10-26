using Catalogo.Application.Abstracoes;
using Catalogo.Application.Comum;
using Catalogo.Application.Dtos;
using Catalogo.Domain.Acesso;
using Catalogo.Domain.Aprovacao;
using Catalogo.Domain.Consultas;
using Catalogo.Domain.Excecoes;
using Catalogo.Domain.Metadados;
using Catalogo.Domain.Validacao;

namespace Catalogo.Application.CasosDeUso;

/// <summary>
/// Caso de uso: opções de um campo REFERENCIA para o front montar o select (RN21), já filtradas
/// pela dependência do campo (RN08), pela EQUIPE do registro do formulário (RN29) e pela situação de
/// aprovação (RN38: aprovados e os não aprovados de quem está preenchendo), ordenadas por
/// rótulo (RN20) e paginadas. É só conveniência: o POST e o PUT continuam validando tudo no servidor.
/// Lança <see cref="RecursoNaoEncontradoException"/>, <see cref="ValidacaoRegistroException"/> ou
/// <see cref="AcessoNegadoException"/>.
/// </summary>
public sealed class ListarOpcoes(
    IUsuarioAtual usuarioAtual, IMetadadosProvider metadados, IRegistroRepository registros, ResolvedorEquipe resolvedorEquipe)
{
    public async Task<PaginaDto<OpcaoDto>> ExecutarAsync(
        string grupoChave, string entidadeChave, string campoChave,
        IEnumerable<KeyValuePair<string, string>> parametros, CancellationToken cancellationToken)
    {
        var usuario = await usuarioAtual.ObterAsync(cancellationToken);
        (var grupo, var entidade, usuario) = await metadados.ObterEntidadeOuFalharAsync(usuario, grupoChave, entidadeChave, UsoEntidade.Leitura, cancellationToken);

        var campo = entidade.ObterCampo(campoChave);
        if (campo is null || !campo.Ativo)
            throw new RecursoNaoEncontradoException($"Campo '{campoChave}' não encontrado em '{entidade.Nome}'.", CodigosErro.CampoNaoEncontrado);

        var erros = new ErrosPorCampo();
        if (!campo.EhReferencia)
        {
            erros.Adicionar(campo.Chave, "Opções só existem para campos REFERENCIA.");
            erros.LancarSeHouverErros();
        }

        var entidadeReferenciada = grupo.ObterEntidade(campo.EntidadeReferenciadaId!.Value);
        if (entidadeReferenciada is null)
        {
            erros.Adicionar(campo.Chave, "Campo aponta para uma entidade de outro grupo: não suportado nesta versão (ver RN21).");
            erros.LancarSeHouverErros();
        }

        var lista = parametros.ToList();

        // Equipe do registro do formulário (o registro pode ainda não existir): mesma regra do POST (RN27).
        var equipeDoFormulario = await resolvedorEquipe.ResolverAsync(
            usuario, entidade, InterpretadorConsulta.LerUnico(lista, ChavesReservadas.EquipeId, erros)?.Trim(), cancellationToken);

        var paginacao = InterpretadorConsulta.InterpretarPaginacao(lista, erros);
        var busca = InterpretadorConsulta.LerUnico(lista, ChavesReservadas.Busca, erros)?.Trim();
        var dependeDe = InterpretadorConsulta.LerUnico(lista, ChavesReservadas.DependeDe, erros)?.Trim();
        var dependencia = entidade.Dependencias.FirstOrDefault(d => d.Campo.Id == campo.Id);
        var condicoes = new List<CondicaoFiltro>();

        // "busca": contém, sobre o campo de exibição da entidade referenciada.
        var campoExibicao = entidadeReferenciada!.CampoExibicaoId is Guid exibicaoId ? entidadeReferenciada.ObterCampo(exibicaoId) : null;
        if (!string.IsNullOrEmpty(busca))
        {
            if (campoExibicao?.TipoDado is TipoDado.Texto or TipoDado.TextoLongo or TipoDado.Lista or TipoDado.Url)
                condicoes.Add(new CondicaoFiltro(campoExibicao, Comparacao.Contem, Texto: busca));
            else
                erros.Adicionar(ChavesReservadas.Busca, $"Busca indisponível: o rótulo de '{entidadeReferenciada.Nome}' não é um texto.");
        }

        Guid? dependeDeId = null;
        if (dependencia is not null)
        {
            if (!DependenciaSuportada(dependencia, entidadeReferenciada))
            {
                erros.Adicionar(campo.Chave,
                    "Dependência não suportada nesta versão: CampoOrigem e CampoAlvo precisam ser REFERENCIA (ver RN21).");
            }
            else if (string.IsNullOrEmpty(dependeDe))
            {
                erros.Adicionar(ChavesReservadas.DependeDe,
                    $"Obrigatório: este campo depende do valor de '{(dependencia.CampoVia ?? dependencia.CampoOrigem).Chave}'.");
            }
            else if (!Guid.TryParseExact(dependeDe, "D", out var id))
            {
                erros.Adicionar(ChavesReservadas.DependeDe, "Deve ser o id (GUID) de um registro.");
            }
            else
            {
                dependeDeId = id;
            }
        }

        erros.LancarSeHouverErros();

        var paginaVazia = new PaginaDto<OpcaoDto>([], paginacao.Pagina, paginacao.Tamanho, 0, 0);

        // RN29: um registro global não aponta para registros de equipe.
        if (!entidadeReferenciada.EhGlobal && equipeDoFormulario is null)
            return paginaVazia;

        // RN75: entidade desativada não aceita vínculos novos (os que existem continuam valendo).
        if (!entidadeReferenciada.Ativo || !grupo.Ativo)
            return paginaVazia;

        if (dependencia is not null)
        {
            var valorOrigem = await ObterValorOrigemAsync(grupo, usuario, equipeDoFormulario, dependencia, dependeDeId!.Value, cancellationToken);

            // Sem valor de origem não há filtro (RN08).
            if (valorOrigem is Guid origem)
                condicoes.Add(new CondicaoFiltro(dependencia.CampoAlvo, Comparacao.Igual, Referencia: origem));
        }

        // Ordem por rótulo. Sem campo de exibição utilizável, pela data de criação.
        var ordenacao = campoExibicao is not null && !campoExibicao.EhReferencia
            ? new Ordenacao(campoExibicao, null, Descendente: false)
            : new Ordenacao(null, CampoSistema.CriadoEm, Descendente: false);

        // RN29: só registros da equipe do formulário (ou globais). O filtro vai DENTRO da consulta LINQ.
        // RN38: as opções são só os registros APROVADOS e os não aprovados criados por quem está preenchendo.
        var consulta = new ConsultaRegistros(condicoes, ordenacao, paginacao, NaoAprovadosSomenteDoAutor: usuario.Id);
        if (!entidadeReferenciada.EhGlobal)
            consulta = consulta with { FiltroEquipe = equipeDoFormulario, EquipesPermitidas = PoliticaPermissao.EquipesVisiveis(usuario) };

        var pagina = await registros.ListarIdsAsync(entidadeReferenciada.Id, consulta, cancellationToken);
        var rotulos = pagina.Ids.Count == 0
            ? new Dictionary<Guid, string?>()
            : await registros.ObterRotulosAsync(pagina.Ids, cancellationToken);

        var situacoes = pagina.Ids.Count == 0
            ? new Dictionary<Guid, SituacaoAprovacao>()
            : await registros.ObterSituacoesAsync(pagina.Ids, cancellationToken);

        var itens = pagina.Ids
            .Select(id => new OpcaoDto(id, rotulos.GetValueOrDefault(id),
                AprovacaoConversor.ParaTexto(situacoes.GetValueOrDefault(id, SituacaoAprovacao.Aprovado))))
            .ToList();
        return new PaginaDto<OpcaoDto>(itens, paginacao.Pagina, paginacao.Tamanho, pagina.Total, paginacao.TotalPaginas(pagina.Total));
    }

    private static bool DependenciaSuportada(CampoDependencia dependencia, Entidade entidadeReferenciada) =>
        dependencia.CampoOrigem.EhReferencia
        && dependencia.CampoAlvo.EhReferencia
        && dependencia.CampoAlvo.EntidadeId == entidadeReferenciada.Id;

    /// <summary>
    /// Com CampoVia: "dependeDe" é o registro escolhido no campo via (ex.: o relatório), e a origem é
    /// lida nele. Sem CampoVia: "dependeDe" já é o valor de origem (o que está no formulário).
    /// O registro via precisa ser visível ao usuário e poder ser referenciado pelo formulário (RN28, RN29).
    /// </summary>
    private async Task<Guid?> ObterValorOrigemAsync(
        Grupo grupo, UsuarioAtual usuario, Guid? equipeDoFormulario, CampoDependencia dependencia, Guid dependeDe,
        CancellationToken cancellationToken)
    {
        if (dependencia.CampoVia is null)
            return dependeDe;

        var registroVia = await registros.ObterPorIdAsync(dependeDe, cancellationToken);
        var entidadeVia = registroVia is null ? null : grupo.ObterEntidade(registroVia.EntidadeId);
        var viaGlobal = entidadeVia?.EhGlobal ?? false;

        if (registroVia is null
            || registroVia.EntidadeId != dependencia.CampoVia.EntidadeReferenciadaId
            || !PoliticaPermissao.PodeVer(usuario, viaGlobal, registroVia)
            || !PoliticaPermissao.PodeReferenciar(equipeDoFormulario, viaGlobal ? null : registroVia.EquipeId)
            || !PoliticaPermissao.PodeReferenciarSituacao(registroVia.SituacaoAprovacao, registroVia.CriadoPorId, usuario.Id))
        {
            var erros = new ErrosPorCampo();
            erros.Adicionar(ChavesReservadas.DependeDe,
                $"Registro {dependeDe} não encontrado entre os valores possíveis de '{dependencia.CampoVia.Nome}'.");
            erros.LancarSeHouverErros();
        }

        return registroVia!.Referencias.FirstOrDefault(r => r.CampoId == dependencia.CampoOrigem.Id)?.RegistroDestinoId;
    }
}
