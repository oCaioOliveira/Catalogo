using System.Data;
using Catalogo.Application.Abstracoes;
using Catalogo.Domain.Excecoes;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Catalogo.Infrastructure.Persistencia;

/// <summary>
/// Grava tudo num único SaveChanges (uma transação) e traduz os erros do banco
/// para exceções que a Application entende.
/// </summary>
internal sealed class UnidadeDeTrabalho(CatalogoDbContext contexto) : IUnidadeDeTrabalho
{
    public void DescartarAlteracoes() => contexto.ChangeTracker.Clear();

    public async Task SalvarAlteracoesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await contexto.SaveChangesAsync(cancellationToken);
        }
        // DbUpdateConcurrencyException herda de DbUpdateException, por isso vem primeiro.
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConflitoConcorrenciaException(
                "O registro foi alterado por outra pessoa depois que você o leu. Recarregue e tente de novo.", ex);
        }
        catch (DbUpdateException ex) when (TradutorErrosSqlServer.EhViolacaoUnicidade(ex, out var restricaoUnicaId))
        {
            throw new ViolacaoUnicidadeBancoException(restricaoUnicaId, ex);
        }
    }

    public async Task<T> ExecutarSerializavelAsync<T>(
        Func<CancellationToken, Task<(T Resultado, bool Confirmar)>> operacao, CancellationToken cancellationToken)
    {
        try
        {
            await using var transacao = await contexto.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
            var (resultado, confirmar) = await operacao(cancellationToken);
            if (confirmar)
                await transacao.CommitAsync(cancellationToken);
            else
                await transacao.RollbackAsync(cancellationToken);
            return resultado;
        }
        // 1205: o SQL Server escolheu esta transação como vítima de um deadlock (comum em Serializable com acessos simultâneos).
        catch (Exception ex) when (EhDeadlock(ex))
        {
            throw new ConflitoConcorrenciaException("Outra alteração estava sendo feita ao mesmo tempo. Tente de novo.", ex);
        }
    }

    private static bool EhDeadlock(Exception ex) =>
        ex is SqlException { Number: 1205 } || (ex.InnerException is { } interna && EhDeadlock(interna));
}
