using Catalogo.Application.Abstracoes;
using Catalogo.Application.Comum;
using Catalogo.Application.Dtos;
using Catalogo.Domain.Acesso;
using Catalogo.Domain.Excecoes;
using Catalogo.Domain.Metadados;

namespace Catalogo.Application.CasosDeUso;

/// <summary>
/// Caso de uso: os itens relacionados a um item (RN94, ADR 0047). Uma entrada por campo REFERENCIA ativo, de entidade ativa do mesmo
/// grupo, que aponta para a entidade do item (<see cref="Relacoes.QueApontamPara"/>), com a quantidade que o usuário vê. A
/// quantidade usa o MESMO escopo da listagem (<see cref="PreparadorConsultaRegistros.Escopo"/>: equipes do usuário nesta listagem,
/// aprovação, globais) e é contada no banco, numa consulta só. O item precisa ser visível: senão, 404 (RN28), como o detalhe.
/// </summary>
public sealed class ListarRelacionados(IUsuarioAtual usuarioAtual, IMetadadosProvider metadados, IRegistroRepository registros)
{
    public async Task<IReadOnlyList<RelacionadoDto>> ExecutarAsync(
        string grupoChave, string entidadeChave, Guid id, CancellationToken cancellationToken)
    {
        var usuario = await usuarioAtual.ObterAsync(cancellationToken);
        (var grupo, var entidade, usuario) = await metadados.ObterEntidadeOuFalharAsync(
            usuario, grupoChave, entidadeChave, UsoEntidade.Leitura, cancellationToken);

        var registro = await registros.ObterPorIdAsync(id, cancellationToken);
        var descricao = $"Registro {id} em '{entidade.Nome}'";
        if (registro is null || registro.EntidadeId != entidade.Id)
            throw new RecursoNaoEncontradoException($"{descricao} não encontrado.", CodigosErro.RegistroNaoEncontrado);
        Permissoes.Exigir(PoliticaPermissao.Decidir(usuario, OperacaoRegistro.Ver, RegistroAlvo.De(entidade, registro, false)), descricao);

        var relacoes = Relacoes.QueApontamPara(grupo, entidade);
        var contagens = relacoes
            .Select(r => new ContagemPorCampo(r.Campo.Id, r.Origem.Id, PreparadorConsultaRegistros.Escopo(usuario, r.Origem, null)))
            .ToList();
        var quantidades = await registros.ContarQueApontamParaAsync(id, contagens, cancellationToken);

        return relacoes
            .Select(r => new RelacionadoDto(
                new EntidadeRelacionadaDto(r.Origem.Chave, r.Origem.Nome, r.Origem.NomePlural),
                new CampoRelacionadoDto(r.Campo.Chave, r.Campo.Nome),
                r.Filha,
                quantidades.GetValueOrDefault(r.Campo.Id)))
            .ToList();
    }
}
