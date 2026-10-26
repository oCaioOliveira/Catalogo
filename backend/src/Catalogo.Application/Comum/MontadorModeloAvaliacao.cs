using Catalogo.Application.Abstracoes;
using Catalogo.Application.Dtos;
using Catalogo.Application.Mapeamentos;
using Catalogo.Domain.Avaliacoes;
using Catalogo.Domain.Metadados;

namespace Catalogo.Application.Comum;

/// <summary>
/// Monta o <see cref="ModeloAvaliacaoDto"/> para quem configura (RN40): autores, quantas avaliações o
/// modelo tem e, por critério, se ele já tem respostas (o front sabe o que não pode excluir, RN43).
/// </summary>
public sealed class MontadorModeloAvaliacao(IModeloAvaliacaoRepository modelos, IUsuarioRepository usuarios)
{
    public async Task<ModeloAvaliacaoDto> MontarAsync(
        ModeloAvaliacao modelo, string grupoChave, Entidade entidade, CancellationToken cancellationToken)
    {
        var comRespostas = await modelos.CriteriosComRespostasAsync(modelo.Id, cancellationToken);
        var quantidade = await modelos.ContarAvaliacoesAsync(modelo.Id, cancellationToken);
        var autores = await usuarios.ObterResumosAsync(
            new[] { modelo.CriadoPorId, modelo.AtualizadoPorId }.OfType<Guid>().Distinct().ToList(), cancellationToken);

        return new ModeloAvaliacaoDto(
            modelo.Id,
            modelo.EquipeId,
            grupoChave,
            entidade.Chave,
            modelo.Nome,
            modelo.Descricao,
            modelo.Ativo,
            Convert.ToBase64String(modelo.Versao),
            quantidade,
            autores.GetValueOrDefault(modelo.CriadoPorId),
            modelo.CriadoEm,
            modelo.AtualizadoPorId is Guid atualizadoPor ? autores.GetValueOrDefault(atualizadoPor) : null,
            modelo.AtualizadoEm,
            modelo.CriteriosEmOrdem().Select(c => c.ParaDto(comRespostas.Contains(c.Id))).ToList());
    }
}
