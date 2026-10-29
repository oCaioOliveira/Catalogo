using Catalogo.Application.Abstracoes;
using Catalogo.Application.Dtos;
using Catalogo.Domain.Acesso;
using Catalogo.Domain.Consultas;
using Catalogo.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace Catalogo.Infrastructure.Repositorios;

// Todas as consultas são LINQ do EF Core (docs/adr/0011).
internal sealed class UsuarioRepository(CatalogoDbContext contexto) : IUsuarioRepository
{
    public Task<Usuario?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken) =>
        contexto.Usuarios.Include(u => u.Equipes).FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

    // A collation do banco não diferencia maiúsculas: "ABC123" encontra "abc123".
    public Task<Usuario?> ObterPorMatriculaAsync(string matricula, CancellationToken cancellationToken) =>
        contexto.Usuarios.Include(u => u.Equipes).FirstOrDefaultAsync(u => u.Matricula == matricula, cancellationToken);

    public Task<bool> ExisteMatriculaAsync(string matricula, CancellationToken cancellationToken) =>
        contexto.Usuarios.AnyAsync(u => u.Matricula == matricula, cancellationToken);

    public Task<int> ContarAdministradoresAtivosAsync(CancellationToken cancellationToken) =>
        contexto.Usuarios.CountAsync(u => u.Ativo && u.Perfil == Perfil.Administrador, cancellationToken);

    public async Task<PaginaDe<Usuario>> ListarAsync(FiltroUsuarios filtro, Paginacao paginacao, CancellationToken cancellationToken)
    {
        var consulta = contexto.Usuarios.AsNoTracking();
        if (filtro.EquipeId is Guid equipeId)
            consulta = consulta.Where(u => u.Equipes.Any(v => v.EquipeId == equipeId));
        if (filtro.Perfil is Perfil perfil)
            consulta = consulta.Where(u => u.Perfil == perfil);
        if (filtro.Ativo is bool ativo)
            consulta = consulta.Where(u => u.Ativo == ativo);
        // Contém, pela collation da coluna (CI_AI): "joao" encontra "João". O texto vai como parâmetro.
        if (filtro.Busca is string busca)
            consulta = consulta.Where(u => u.Matricula.Contains(busca) || u.Nome.Contains(busca));

        var total = await consulta.CountAsync(cancellationToken);
        var itens = await Ordenar(consulta.Include(u => u.Equipes), filtro.Ordenacao)
            .Skip(paginacao.Pular)
            .Take(paginacao.Tamanho)
            .ToListAsync(cancellationToken);

        return new PaginaDe<Usuario>(itens, total);
    }

    /// <summary>Pela coluna pedida (padrão: nome) e, para a página ser estável, pelo id.</summary>
    private static IOrderedQueryable<Usuario> Ordenar(IQueryable<Usuario> consulta, OrdenacaoUsuarios? ordenacao)
    {
        var descendente = ordenacao?.Descendente ?? false;
        IOrderedQueryable<Usuario> ordenada = (ordenacao?.Campo ?? CampoOrdenacaoUsuario.Nome) switch
        {
            CampoOrdenacaoUsuario.Matricula =>
                descendente ? consulta.OrderByDescending(u => u.Matricula) : consulta.OrderBy(u => u.Matricula),
            CampoOrdenacaoUsuario.UltimoAcessoEm =>
                descendente ? consulta.OrderByDescending(u => u.UltimoAcessoEm) : consulta.OrderBy(u => u.UltimoAcessoEm),
            CampoOrdenacaoUsuario.CriadoEm =>
                descendente ? consulta.OrderByDescending(u => u.CriadoEm) : consulta.OrderBy(u => u.CriadoEm),
            _ => descendente ? consulta.OrderByDescending(u => u.Nome) : consulta.OrderBy(u => u.Nome),
        };
        return ordenada.ThenBy(u => u.Id);
    }

    public async Task<IReadOnlyDictionary<Guid, UsuarioResumoDto>> ObterResumosAsync(
        IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken)
    {
        return await contexto.Usuarios
            .Where(u => ids.Contains(u.Id))
            .Select(u => new UsuarioResumoDto(u.Id, u.Matricula, u.Nome))
            .ToDictionaryAsync(u => u.Id, cancellationToken);
    }

    public void Adicionar(Usuario usuario) => contexto.Usuarios.Add(usuario);
}
