using Catalogo.Application.Abstracoes;
using Catalogo.Domain.Consultas;
using Catalogo.Domain.Importacoes;
using Catalogo.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace Catalogo.Infrastructure.Repositorios;

// Todas as consultas são LINQ do EF Core (docs/adr/0011).
internal sealed class ImportacaoRepository(CatalogoDbContext contexto) : IImportacaoRepository
{
    public void Adicionar(Importacao importacao, ImportacaoArquivo arquivo)
    {
        contexto.Importacoes.Add(importacao);
        // O Id da importação foi gerado no Add acima: o arquivo usa o mesmo, no mesmo SaveChanges.
        arquivo.ImportacaoId = importacao.Id;
        contexto.ImportacoesArquivos.Add(arquivo);
    }

    public Task<Importacao?> ObterAsync(Guid id, CancellationToken cancellationToken) =>
        contexto.Importacoes.FirstOrDefaultAsync(i => i.Id == id, cancellationToken);

    public async Task<byte[]?> ObterConteudoAsync(Guid id, CancellationToken cancellationToken) =>
        await contexto.ImportacoesArquivos.AsNoTracking()
            .Where(a => a.ImportacaoId == id)
            .Select(a => a.Conteudo)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<PaginaDe<Importacao>> ListarAsync(
        Guid entidadeId, EscopoImportacoes escopo, Paginacao paginacao, CancellationToken cancellationToken)
    {
        var consulta = contexto.Importacoes.AsNoTracking().Where(i => i.EntidadeId == entidadeId);

        // RN97: as próprias; o GESTOR, também as de quem é de uma das suas equipes; o ADMINISTRADOR, todas. No WHERE.
        if (!escopo.Todas)
        {
            var usuarioId = escopo.UsuarioId;
            if (escopo.EquipesDosAutores is { Count: > 0 } equipes)
            {
                var ids = equipes.ToList();
                consulta = consulta.Where(i => i.UsuarioId == usuarioId
                    || contexto.UsuariosEquipes.Any(v => v.UsuarioId == i.UsuarioId && ids.Contains(v.EquipeId)));
            }
            else
            {
                consulta = consulta.Where(i => i.UsuarioId == usuarioId);
            }
        }

        var total = await consulta.CountAsync(cancellationToken);
        var itens = await consulta
            .OrderByDescending(i => i.CriadoEm).ThenByDescending(i => i.Id)
            .Skip(paginacao.Pular)
            .Take(paginacao.Tamanho)
            .ToListAsync(cancellationToken);
        return new PaginaDe<Importacao>(itens, total);
    }

    public async Task<IReadOnlySet<Guid>> ObterEquipesDoUsuarioAsync(Guid usuarioId, CancellationToken cancellationToken) =>
        (await (
                from vinculo in contexto.UsuariosEquipes
                join equipe in contexto.Equipes on vinculo.EquipeId equals equipe.Id
                where vinculo.UsuarioId == usuarioId && equipe.Ativa
                select vinculo.EquipeId)
            .ToListAsync(cancellationToken))
        .ToHashSet();

    public async Task<(int ArquivosApagados, int Expiradas)> LimparAntigasAsync(DateTime limite, CancellationToken cancellationToken)
    {
        // RN104: o arquivo sai; o registro da importação fica (com o resumo, para o histórico de importações).
        var arquivos = await contexto.ImportacoesArquivos
            .Where(a => contexto.Importacoes.Any(i => i.Id == a.ImportacaoId && i.CriadoEm < limite))
            .ExecuteDeleteAsync(cancellationToken);

        var expiradas = await contexto.Importacoes
            .Where(i => i.CriadoEm < limite
                        && (i.Situacao == SituacaoImportacao.Enviada || i.Situacao == SituacaoImportacao.Validada))
            .ExecuteUpdateAsync(s => s.SetProperty(i => i.Situacao, SituacaoImportacao.Expirada), cancellationToken);

        return (arquivos, expiradas);
    }
}
