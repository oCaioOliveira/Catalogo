using Catalogo.Application.Abstracoes;
using Catalogo.Application.Dtos;
using Catalogo.Domain.Avaliacoes;
using Catalogo.Domain.Excecoes;
using Catalogo.Domain.Metadados;
using Catalogo.Domain.Registros;
using Catalogo.Domain.Validacao;

namespace Catalogo.Application.CasosDeUso.Avaliacoes;

/// <summary>Passos comuns aos casos de uso de avaliação (RN40 a RN48).</summary>
internal static class AvaliacaoComum
{
    /// <summary>Para a mensagem de entidade global em <see cref="Comum.ConfiguracaoDaEquipe"/>.</summary>
    public const string Funcionalidade = "avaliação";

    public static DefinicaoModelo ParaDefinicao(ModeloAvaliacaoEntrada entrada) => new(
        entrada.Nome,
        entrada.Descricao,
        entrada.Criterios?
            .Select(c => c is null
                ? null
                : new DefinicaoCriterio(c.Chave, c.Nome, c.Descricao, c.Tipo, c.Obrigatorio, c.Comentario, c.TamanhoMaximoTexto, c.Ordem, c.Ativo))
            .ToList());

    public static RegraDeNegocioException ModeloAtivoExistente(Entidade entidade, Guid? modeloAtivoId) => new(
        $"Já existe um modelo de avaliação ativo para '{entidade.Nome}' nesta equipe. Altere-o, ou desative-o antes de criar outro.",
        CodigosErro.ModeloAtivoExistente,
        new Dictionary<string, object?> { ["modeloId"] = modeloAtivoId });

    /// <summary>A "versao" vinda do corpo, em Base64 como o GET devolve. Erro em "versao" se ausente ou inválida.</summary>
    public static byte[]? LerVersao(string? texto, string comoObter, ErrosPorCampo erros)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            erros.Adicionar(ChavesReservadas.Versao, $"Obrigatória: envie a \"versao\" devolvida {comoObter}.");
            return null;
        }

        try
        {
            return Convert.FromBase64String(texto);
        }
        catch (FormatException)
        {
            erros.Adicionar(ChavesReservadas.Versao, "Inválida: envie a \"versao\" exatamente como a API devolveu.");
            return null;
        }
    }

    public static string DescreverRegistro(Guid id, Entidade entidade) => $"Registro {id} em '{entidade.Nome}'";

    /// <summary>O registro precisa existir e ser desta entidade (404). A permissão é conferida por quem chama.</summary>
    public static async Task<Registro> ObterRegistroAsync(
        IRegistroRepository registros, Entidade entidade, Guid id, CancellationToken cancellationToken)
    {
        var registro = await registros.ObterPorIdAsync(id, cancellationToken);
        if (registro is null || registro.EntidadeId != entidade.Id)
            throw new RecursoNaoEncontradoException($"{DescreverRegistro(id, entidade)} não encontrado.", CodigosErro.RegistroNaoEncontrado);

        return registro;
    }

    /// <summary>Modelo ATIVO da entidade na equipe do registro (RN44). Sem modelo: 400 com mensagem clara.</summary>
    public static async Task<ModeloAvaliacao> ObterModeloAtivoOuFalharAsync(
        IModeloAvaliacaoRepository modelos, Entidade entidade, Registro registro, CancellationToken cancellationToken)
    {
        var modelo = registro.EquipeId is Guid equipe && !entidade.EhGlobal
            ? await modelos.ObterAtivoAsync(entidade.Id, equipe, cancellationToken)
            : null;

        return modelo ?? throw new ValidacaoRegistroException(new Dictionary<string, string[]>
        {
            ["modelo"] = [$"'{entidade.Nome}' não tem modelo de avaliação ativo na equipe deste item: ele não pode ser avaliado."],
        });
    }
}
