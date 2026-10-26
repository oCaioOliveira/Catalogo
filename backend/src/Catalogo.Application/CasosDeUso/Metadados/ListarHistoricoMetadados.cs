using System.Text.Json.Nodes;
using Catalogo.Application.Abstracoes;
using Catalogo.Application.Comum;
using Catalogo.Application.Dtos;
using Catalogo.Domain.Consultas;
using Catalogo.Domain.Excecoes;
using Catalogo.Domain.Historico;
using Catalogo.Domain.Validacao;

namespace Catalogo.Application.CasosDeUso.Metadados;

/// <summary>
/// Caso de uso: histórico das alterações de metadados de um grupo (RN52), do mais recente para o mais antigo: o grupo, as
/// entidades, os campos, as restrições e as dependências dele (a entidade vem nos dados). Só ADMINISTRADOR.
///   - <c>?entidade=</c>: só as linhas daquela entidade (ela mesma, os campos, as restrições e as dependências dela), pelo
///     "entidade" que toda linha de entidade, campo, restrição e dependência leva nos dados;
///   - cada linha diz o que foi alterado (<see cref="HistoricoDto.Alvo"/>), porque as mudanças só trazem o que mudou.
/// </summary>
public sealed class ListarHistoricoMetadados(ExecutorAlteracaoMetadados executor, IHistoricoRepository historico, MontadorHistorico montador)
{
    public async Task<PaginaDto<HistoricoDto>> ExecutarAsync(
        string grupoChave, IEnumerable<KeyValuePair<string, string>> parametros, CancellationToken cancellationToken)
    {
        await executor.ExigirAdministradorAsync(cancellationToken);
        var grupo = await executor.ObterGrupoAsync(grupoChave, cancellationToken);

        var lista = parametros.ToList();
        var erros = new ErrosPorCampo();
        var paginacao = InterpretadorConsulta.InterpretarPaginacao(lista, erros);
        erros.LancarSeHouverErros();

        string? dadosContendo = null;
        if (lista.FirstOrDefault(p => p.Key == "entidade").Value is { Length: > 0 } chaveEntidade)
        {
            var entidade = grupo.Entidades.FirstOrDefault(e => string.Equals(e.Chave, chaveEntidade, StringComparison.OrdinalIgnoreCase))
                ?? throw new RecursoNaoEncontradoException(
                    $"Entidade '{chaveEntidade}' não encontrada no grupo '{grupo.Chave}'.", CodigosErro.EntidadeNaoEncontrada);
            // O mesmo texto que o ServicoHistorico grava (JSON compacto): "entidade":"<chave>".
            dadosContendo = $"\"entidade\":\"{entidade.Chave}\"";
        }

        var pagina = await historico.ListarAsync(AlvoHistorico.Metadado, grupo.Id, paginacao, cancellationToken, dadosContendo: dadosContendo);
        var itens = await montador.MontarAsync(pagina.Itens, entidade: null, cancellationToken);
        var comAlvo = itens.Zip(pagina.Itens, (item, linha) => item with { Alvo = DescreverAlvo(linha) }).ToList();
        return new PaginaDto<HistoricoDto>(comAlvo, paginacao.Pagina, paginacao.Tamanho, pagina.Total, paginacao.TotalPaginas(pagina.Total));
    }

    /// <summary>"Listagem", "Tipo de item 'equipamento'", "Campo 'nome' de 'equipamento'"... a partir do "alvo" gravado nos dados.</summary>
    internal static string DescreverAlvo(HistoricoAlteracao linha)
    {
        var dados = Ler(linha.DadosDepois) ?? Ler(linha.DadosAntes);
        string? Texto(string chave) => dados?[chave]?.GetValue<string>();
        var entidade = Texto("entidade");
        return Texto("alvo") switch
        {
            "entidade" => $"Tipo de item '{entidade}'",
            "campo" => $"Campo '{Texto("campo")}' de '{entidade}'",
            "restricao" => $"Unicidade '{Texto("nome")}' de '{entidade}'",
            "dependencia" => $"Dependência do campo '{Texto("campo")}' de '{entidade}'",
            "equipes" => "Equipes habilitadas",
            _ => "Listagem",
        };
    }

    private static JsonObject? Ler(string? json) => json is null ? null : JsonNode.Parse(json) as JsonObject;
}
