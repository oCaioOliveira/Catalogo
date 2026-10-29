namespace Catalogo.Application.Abstracoes;

/// <summary>
/// Grava de uma vez, numa única transação, tudo o que os repositórios marcaram
/// (um único SaveChanges do EF Core).
/// </summary>
public interface IUnidadeDeTrabalho
{
    /// <exception cref="ViolacaoUnicidadeBancoException">
    /// O banco recusou por chave duplicada (erros SQL 2601/2627).
    /// </exception>
    /// <exception cref="Domain.Excecoes.ConflitoConcorrenciaException">
    /// O registro foi alterado por outra pessoa (Versao diferente).
    /// </exception>
    Task SalvarAlteracoesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Descarta tudo o que foi marcado para gravar e ainda não foi salvo. Usado pela prévia da importação (RN97), que passa pelo
    /// mesmo caminho de gravação do cadastro manual e depois joga fora o resultado, sem gravar nada.
    /// </summary>
    void DescartarAlteracoes();

    /// <summary>
    /// Executa <paramref name="operacao"/> numa transação SERIALIZABLE: o que ela lê fica travado até o fim, então um registro
    /// inserido no meio não escapa de uma verificação feita antes da gravação (RN58). A operação pode chamar
    /// <see cref="SalvarAlteracoesAsync"/> várias vezes (ex.: lotes). Confirma se ela devolver <c>Confirmar = true</c>; senão (ou
    /// numa exceção), desfaz tudo. Um deadlock vira ConflitoConcorrenciaException (409, tente de novo).
    /// </summary>
    Task<T> ExecutarSerializavelAsync<T>(Func<CancellationToken, Task<(T Resultado, bool Confirmar)>> operacao, CancellationToken cancellationToken);
}

/// <summary>
/// O banco recusou a gravação por violação de unicidade (erros SQL 2601/2627). Acontece quando
/// duas requisições gravam o mesmo valor ao mesmo tempo e ambas passam pela verificação prévia.
/// A Infrastructure lança esta exceção; o caso de uso a converte em
/// <see cref="Domain.Excecoes.ConflitoUnicidadeException"/>, que tem a mensagem amigável.
/// </summary>
public sealed class ViolacaoUnicidadeBancoException(Guid? restricaoUnicaId, Exception excecaoOriginal)
    : Exception("O banco recusou a gravação por violação de unicidade.", excecaoOriginal)
{
    /// <summary>Restrição violada, quando foi possível identificá-la pela mensagem do banco.</summary>
    public Guid? RestricaoUnicaId { get; } = restricaoUnicaId;
}
