using Catalogo.Application.CasosDeUso.Metadados;
using Catalogo.Application.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace Catalogo.Api.Controllers;

/// <summary>
/// Administração de metadados: grupos (listagens) e o histórico deles (RN50 a RN52, RN77). Só ADMINISTRADOR (403 para os demais).
/// Toda escrita aceita <c>?simular=true</c> (RN56): valida tudo, inclusive contra os dados, e devolve o relatório sem gravar.
/// </summary>
[ApiController]
[Route(RotasApi.AdminMetadados + "/grupos")]
[Produces("application/json")]
public sealed class AdminGruposController(
    ListarGruposAdmin listarGrupos,
    ObterGrupoAdmin obterGrupo,
    CriarGrupo criarGrupo,
    AtualizarGrupo atualizarGrupo,
    AlterarAtivacaoGrupo alterarAtivacao,
    ExcluirGrupo excluirGrupo,
    ListarHistoricoMetadados listarHistorico,
    ObterEquipesListagem obterEquipes,
    DefinirEquipesListagem definirEquipes,
    ILogger<AdminGruposController> logger) : ControllerBase
{
    /// <summary>Todos os grupos, ativos e inativos, com a versão e a quantidade de entidades.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<GrupoResumoAdminDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<GrupoResumoAdminDto>>> Listar(CancellationToken cancellationToken) =>
        Ok(await listarGrupos.ExecutarAsync(cancellationToken));

    /// <summary>
    /// A estrutura completa do grupo para edição: entidades e campos ativos e inativos, restrições, dependências, as versões e a
    /// quantidade de registros de cada entidade.
    /// </summary>
    [HttpGet("{grupo}")]
    [ProducesResponseType<GrupoAdminDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GrupoAdminDto>> Obter(string grupo, CancellationToken cancellationToken) =>
        Ok(await obterGrupo.ExecutarAsync(grupo, cancellationToken));

    /// <summary>Cria um grupo, ativo e sem entidades: { "chave", "nome", "descricao" }. A chave não muda depois (RN51).</summary>
    /// <param name="entrada">Chave, nome e descrição.</param>
    /// <param name="simular">true: só valida e devolve o relatório, sem gravar (RN56).</param>
    /// <param name="cancellationToken">Cancelamento da requisição.</param>
    [HttpPost]
    [Consumes("application/json")]
    [ProducesResponseType<ResultadoAdministracaoDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ResultadoAdministracaoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ResultadoAdministracaoDto>> Criar(
        [FromBody] GrupoEntrada entrada, [FromQuery] bool simular, CancellationToken cancellationToken)
    {
        var resultado = await criarGrupo.ExecutarAsync(entrada, simular, cancellationToken);
        return resultado.Simulado
            ? Ok(resultado)
            : Criado(resultado);
    }

    /// <summary>Altera nome e descrição, com a "versao" do grupo (RN52). A chave, se vier, tem de ser a mesma (RN51).</summary>
    [HttpPut("{grupo}")]
    [Consumes("application/json")]
    [ProducesResponseType<ResultadoAdministracaoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ResultadoAdministracaoDto>> Atualizar(
        string grupo, [FromBody] GrupoEntrada entrada, [FromQuery] bool simular, CancellationToken cancellationToken) =>
        Ok(await atualizarGrupo.ExecutarAsync(grupo, entrada, simular, cancellationToken));

    /// <summary>
    /// { "versao": "...", "ativo": false } desativa: o grupo some para os não-administradores e as entidades ficam somente leitura (RN77).
    /// </summary>
    [HttpPatch("{grupo}/ativacao")]
    [Consumes("application/json")]
    [ProducesResponseType<ResultadoAdministracaoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ResultadoAdministracaoDto>> AlterarAtivacao(
        string grupo, [FromBody] AtivacaoMetadadoEntrada entrada, [FromQuery] bool simular, CancellationToken cancellationToken) =>
        Ok(await alterarAtivacao.ExecutarAsync(grupo, entrada, simular, cancellationToken));

    /// <summary>Exclui um grupo SEM entidades (RN77). Com entidades: 409 METADADO_EM_USO (desative-o).</summary>
    /// <param name="grupo">Chave do grupo.</param>
    /// <param name="versao">A "versao" do grupo, como o GET devolveu (URL-encoded).</param>
    /// <param name="simular">true: só valida, sem excluir.</param>
    /// <param name="cancellationToken">Cancelamento da requisição.</param>
    [HttpDelete("{grupo}")]
    [ProducesResponseType<ResultadoAdministracaoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ResultadoAdministracaoDto>> Excluir(
        string grupo, [FromQuery] string? versao, [FromQuery] bool simular, CancellationToken cancellationToken) =>
        Ok(await excluirGrupo.ExecutarAsync(grupo, versao, simular, cancellationToken));

    /// <summary>
    /// As equipes da listagem (RN90): TODAS as equipes, ativas e inativas, cada uma com "habilitada" e a quantidade de itens dela
    /// na listagem, e a "versao" do grupo para o PUT.
    /// </summary>
    [HttpGet("{grupo}/equipes")]
    [ProducesResponseType<EquipesListagemDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EquipesListagemDto>> ObterEquipes(string grupo, CancellationToken cancellationToken) =>
        Ok(await obterEquipes.ExecutarAsync(grupo, cancellationToken));

    /// <summary>
    /// Define as equipes habilitadas na listagem: { "versao": "...", "equipes": ["id", ...] }, com a lista COMPLETA (vazia = só o
    /// administrador vê a listagem, RN93). Tirar uma equipe com itens é permitido: o impacto ITENS_OCULTOS diz quantos ficam ocultos
    /// para os membros dela (nada é apagado; voltam ao habilitá-la de novo, RN92). Aceita ?simular=true (RN56).
    /// </summary>
    [HttpPut("{grupo}/equipes")]
    [Consumes("application/json")]
    [ProducesResponseType<ResultadoAdministracaoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ResultadoAdministracaoDto>> DefinirEquipes(
        string grupo, [FromBody] EquipesListagemEntrada entrada, [FromQuery] bool simular, CancellationToken cancellationToken)
    {
        var resultado = await definirEquipes.ExecutarAsync(grupo, entrada, simular, cancellationToken);
        _ = resultado.Simulado ? 0 : RegistrarAlteracaoEquipes(grupo);
        return Ok(resultado);
    }

    private CreatedAtActionResult Criado(ResultadoAdministracaoDto resultado)
    {
        logger.LogInformation("Grupo {Grupo} criado", resultado.Grupo!.Chave);
        return CreatedAtAction(nameof(Obter), new { grupo = resultado.Grupo.Chave }, resultado);
    }

    private int RegistrarAlteracaoEquipes(string grupo)
    {
        logger.LogInformation("Equipes habilitadas na listagem {Grupo} alteradas", grupo);
        return 0;
    }

    /// <summary>
    /// Histórico das alterações de metadados do grupo (alvo METADADO), do mais recente para o mais antigo (RN52). Com
    /// <c>entidade</c>, só as da entidade (ela, os campos, as restrições e as dependências dela).
    /// </summary>
    [HttpGet("{grupo}/historico")]
    [ProducesResponseType<PaginaDto<HistoricoDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PaginaDto<HistoricoDto>>> Historico(
        string grupo, [FromQuery] string? pagina, [FromQuery] string? tamanho, [FromQuery] string? entidade,
        CancellationToken cancellationToken) =>
        Ok(await listarHistorico.ExecutarAsync(grupo, ParametrosDaUrl.Ler(Request), cancellationToken));
}
