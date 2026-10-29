using Catalogo.Application.Abstracoes;
using Catalogo.Application.Comum;
using Catalogo.Application.Dtos;

namespace Catalogo.Application.CasosDeUso.Importacoes;

/// <summary>
/// Caso de uso: a prévia (ou o resultado, depois de executar) já calculada, para paginar e filtrar as linhas sem simular de novo
/// (GET .../importacoes/{id}). Quem vê a importação (RN97) vê o resultado; o arquivo precisa ainda estar guardado (RN104).
/// </summary>
public sealed class ObterImportacao(AcessoImportacao acesso)
{
    public async Task<PreviaImportacaoDto> ExecutarAsync(
        string grupoChave, string entidadeChave, Guid id, IEnumerable<KeyValuePair<string, string>> parametros,
        CancellationToken cancellationToken)
    {
        var (_, entidade, usuario) = await acesso.EntrarParaLerAsync(grupoChave, entidadeChave, UsoEntidade.Leitura, cancellationToken);
        var importacao = await acesso.ObterAsync(usuario, entidade, id, somenteAutor: false, cancellationToken);
        var planilha = await acesso.LerArquivoAsync(importacao, cancellationToken);
        return await acesso.PreviaAsync(importacao, usuario, planilha, parametros, cancellationToken);
    }
}
