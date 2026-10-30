using Catalogo.Infrastructure.Persistencia;
using Catalogo.Infrastructure.Persistencia.Tabelas;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Catalogo.Infrastructure.Acesso;

/// <summary>
/// A chave de assinatura do JWT fica no banco (cat.CATTB025_CHAVE_JWT, docs/adr/0053), não em variável nem em arquivo: na primeira
/// subida a API gera uma chave aleatória e grava; nas seguintes, lê a mesma. Assim todas as instâncias e todas as subidas assinam e
/// validam com a mesma chave, e o deploy não precisa de mais um segredo.
/// </summary>
internal static class ArmazemChaveJwt
{
    /// <summary>Erro 208 do SQL Server: objeto inexistente (a tabela ainda não foi criada).</summary>
    private const int ErroObjetoInexistente = 208;

    public static async Task<string> ObterOuCriarAsync(CatalogoDbContext contexto, Func<string> gerarNova, CancellationToken cancellationToken)
    {
        try
        {
            var existente = await LerAsync(contexto, cancellationToken);
            if (existente is not null)
                return existente;

            contexto.ChavesJwt.Add(new ChaveJwtTabela { Id = ChaveJwtTabela.IdUnico, Chave = gerarNova(), CriadoEm = DateTime.UtcNow });
            try
            {
                await contexto.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException ex) when (ex.InnerException is SqlException
                { Number: TradutorErrosSqlServer.ErroRestricaoUnicaDuplicada or TradutorErrosSqlServer.ErroIndiceUnicoDuplicado })
            {
                // Outra instância subiu ao mesmo tempo e gravou primeiro: vale a dela.
                contexto.ChangeTracker.Clear();
            }

            return await LerAsync(contexto, cancellationToken)
                   ?? throw new InvalidOperationException("A chave do JWT não foi encontrada no banco logo depois de gravada.");
        }
        catch (SqlException ex) when (ex.Number == ErroObjetoInexistente)
        {
            throw new InvalidOperationException(
                $"A tabela {contexto.Schema}.{NomesBanco.Tabela("ChaveJwt")} (chave do JWT) não existe no banco. Aplique o script " +
                "database/scripts/V018__chave_jwt.sql (local) ou o pacote database/entrega/2026-10-30-chave-jwt (CAIXA).", ex);
        }
    }

    private static Task<string?> LerAsync(CatalogoDbContext contexto, CancellationToken cancellationToken) =>
        contexto.ChavesJwt.AsNoTracking()
            .Where(c => c.Id == ChaveJwtTabela.IdUnico)
            .Select(c => (string?)c.Chave)
            .SingleOrDefaultAsync(cancellationToken);
}
