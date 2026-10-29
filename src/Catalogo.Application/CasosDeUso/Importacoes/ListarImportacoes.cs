using Catalogo.Application.Abstracoes;
using Catalogo.Application.Comum;
using Catalogo.Application.Dtos;
using Catalogo.Domain.Acesso;
using Catalogo.Domain.Consultas;
using Catalogo.Domain.Validacao;

namespace Catalogo.Application.CasosDeUso.Importacoes;

/// <summary>
/// Caso de uso: as importações recentes de um tipo de item ("Importações anteriores", GET .../importacoes), das mais recentes para as
/// mais antigas, paginadas (RN17). Cada usuário vê as próprias; o GESTOR, também as de quem é das suas equipes nesta área de gestão;
/// o ADMINISTRADOR, todas (RN97). O filtro é feito no banco.
/// </summary>
public sealed class ListarImportacoes(AcessoImportacao acesso, IImportacaoRepository importacoes, IUsuarioRepository usuarios)
{
    public async Task<PaginaDto<ImportacaoResumoDto>> ExecutarAsync(
        string grupoChave, string entidadeChave, IEnumerable<KeyValuePair<string, string>> parametros, CancellationToken cancellationToken)
    {
        var (_, entidade, usuario) = await acesso.EntrarParaLerAsync(grupoChave, entidadeChave, UsoEntidade.Leitura, cancellationToken);

        var erros = new ErrosPorCampo();
        var paginacao = InterpretadorConsulta.InterpretarPaginacao(parametros.ToList(), erros);
        erros.LancarSeHouverErros();

        var escopo = new EscopoImportacoes(
            usuario.Id, PoliticaPermissao.VeTodasAsImportacoes(usuario), PoliticaPermissao.EquipesDasImportacoesVisiveis(usuario));
        var pagina = await importacoes.ListarAsync(entidade.Id, escopo, paginacao, cancellationToken);
        var autores = await usuarios.ObterResumosAsync(pagina.Itens.Select(i => i.UsuarioId).Distinct().ToList(), cancellationToken);

        return new PaginaDto<ImportacaoResumoDto>(
            pagina.Itens.Select(i => acesso.Resumo(i, usuario, autores)).ToList(),
            paginacao.Pagina, paginacao.Tamanho, pagina.Total, paginacao.TotalPaginas(pagina.Total));
    }
}
