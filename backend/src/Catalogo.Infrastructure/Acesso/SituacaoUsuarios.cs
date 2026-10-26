using Catalogo.Application.Abstracoes;
using Catalogo.Domain.Acesso;
using Catalogo.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;

namespace Catalogo.Infrastructure.Acesso;

/// <summary>
/// Perfil, equipes ATIVAS e situação de cada usuário, lidos do banco com cache curto
/// ("Seguranca:CacheUsuarioSegundos", padrão 60). Assim desativar um usuário, mudar o perfil ou as
/// equipes vale em no máximo esse tempo, mesmo com o token ainda válido (RN23).
/// Cada servidor tem o seu cache; os casos de uso de administração o descartam na hora.
/// </summary>
internal sealed class SituacaoUsuarios(CatalogoDbContext contexto, IMemoryCache cache, IConfiguration configuracao) : ISituacaoUsuarios
{
    // Sinal compartilhado: cancelá-lo descarta de uma vez as entradas de todos os usuários.
    private static CancellationTokenSource _sinalTodos = new();
    private static readonly object Trava = new();

    public async Task<UsuarioAtual?> ObterAsync(Guid usuarioId, CancellationToken cancellationToken)
    {
        var chave = Chave(usuarioId);
        if (cache.TryGetValue(chave, out UsuarioAtual? emCache))
            return emCache;

        var usuario = await contexto.Usuarios.AsNoTracking()
            .Where(u => u.Id == usuarioId)
            .Select(u => new { u.Id, u.Matricula, u.Nome, u.Perfil, u.Ativo, u.DeveTrocarSenha })
            .FirstOrDefaultAsync(cancellationToken);
        if (usuario is null)
            return null;

        // Vínculos com equipes desativadas não contam ("suas equipes" = só as ativas).
        var equipesAtivas = await (
                from vinculo in contexto.UsuariosEquipes
                join equipe in contexto.Equipes on vinculo.EquipeId equals equipe.Id
                where vinculo.UsuarioId == usuarioId && equipe.Ativa
                select equipe.Id)
            .ToListAsync(cancellationToken);

        var atual = new UsuarioAtual
        {
            Id = usuario.Id,
            Matricula = usuario.Matricula,
            Nome = usuario.Nome,
            Perfil = usuario.Perfil,
            Ativo = usuario.Ativo,
            DeveTrocarSenha = usuario.DeveTrocarSenha,
            Equipes = equipesAtivas.ToHashSet(),
        };

        var opcoes = new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = TempoCache() };
        lock (Trava)
            opcoes.AddExpirationToken(new CancellationChangeToken(_sinalTodos.Token));
        cache.Set(chave, atual, opcoes);

        return atual;
    }

    public void Invalidar(Guid usuarioId) => cache.Remove(Chave(usuarioId));

    public void InvalidarTodos()
    {
        lock (Trava)
        {
            var anterior = _sinalTodos;
            _sinalTodos = new CancellationTokenSource();
            anterior.Cancel();
        }
    }

    private TimeSpan TempoCache() =>
        TimeSpan.FromSeconds(int.TryParse(configuracao["Seguranca:CacheUsuarioSegundos"], out var segundos) && segundos >= 0 ? segundos : 60);

    private static string Chave(Guid usuarioId) => $"catalogo:usuario:{usuarioId}";
}
