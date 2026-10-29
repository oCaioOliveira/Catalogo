using Catalogo.Application.Abstracoes;
using Catalogo.Domain.Aprovacao;
using Catalogo.Domain.Consultas;
using Catalogo.Domain.Excecoes;
using Catalogo.Domain.Exclusao;
using Catalogo.Domain.Metadados;
using Catalogo.Domain.Registros;
using Catalogo.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace Catalogo.Infrastructure.Repositorios;

// Todas as consultas são LINQ do EF Core (docs/adr/0011): nada de SQL escrito à mão.
internal sealed class RegistroRepository(CatalogoDbContext contexto) : IRegistroRepository
{
    public Task<Registro?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken) =>
        contexto.Registros
            .AsNoTracking()
            .Include(r => r.Referencias)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Registro>> ObterPorIdsAsync(
        IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken)
    {
        return await contexto.Registros
            .AsNoTracking()
            .Include(r => r.Referencias)
            .Where(r => ids.Contains(r.Id))
            .ToListAsync(cancellationToken);
    }

    public Task<Registro?> ObterParaAlteracaoAsync(Guid id, CancellationToken cancellationToken) =>
        contexto.Registros
            .Include(r => r.Referencias)
            .Include(r => r.ChavesUnicas)
            .Include(r => r.Valores)
            // Uma consulta por lista, em vez de um JOIN que multiplica as linhas.
            .AsSplitQuery()
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public void DefinirVersaoEsperada(Registro registro, byte[] versao) =>
        // O EF usa o valor ORIGINAL da Versao no WHERE do UPDATE. Trocá-lo pelo valor que o cliente leu
        // faz o UPDATE não alterar nada (e lançar DbUpdateConcurrencyException) se alguém gravou antes.
        contexto.Entry(registro).Property(r => r.Versao).OriginalValue = versao;

    public Task<bool> ExisteChaveUnicaAsync(
        Guid restricaoUnicaId, byte[] valorHash, Guid? ignorarRegistroId, CancellationToken cancellationToken) =>
        contexto.RegistrosChavesUnicas.AnyAsync(
            c => c.RestricaoUnicaId == restricaoUnicaId
                 && c.ValorHash == valorHash
                 && (ignorarRegistroId == null || c.RegistroId != ignorarRegistroId),
            cancellationToken);

    public void Adicionar(Registro registro) => contexto.Registros.Add(registro);

    public async Task<IReadOnlyDictionary<Guid, SituacaoAprovacao>> ObterSituacoesAsync(
        IReadOnlyCollection<Guid> registroIds, CancellationToken cancellationToken) =>
        await contexto.Registros.AsNoTracking()
            .Where(r => registroIds.Contains(r.Id))
            .Select(r => new { r.Id, r.SituacaoAprovacao })
            .ToDictionaryAsync(r => r.Id, r => r.SituacaoAprovacao, cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, string?>> ObterRotulosAsync(
        IReadOnlyCollection<Guid> registroIds, CancellationToken cancellationToken)
    {
        // Uma consulta para todos: o valor tipado do campo de exibição da entidade de cada registro.
        // Entidade sem campo de exibição, ou com campo de exibição REFERENCIA, não tem linha aqui (rótulo nulo).
        var valores = await (
                from valor in contexto.RegistrosValores
                join entidade in contexto.Entidades on valor.EntidadeId equals entidade.Id
                join campo in contexto.Campos on valor.CampoId equals campo.Id
                where registroIds.Contains(valor.RegistroId) && entidade.CampoExibicaoId == valor.CampoId
                select new { Valor = valor, campo.TipoDado, campo.Escala })
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return valores.ToDictionary(
            v => v.Valor.RegistroId,
            v => ConversorValorTipado.ParaTexto(v.TipoDado, v.Escala, v.Valor));
    }

    public async Task<PaginaDeIds> ListarIdsAsync(Guid entidadeId, ConsultaRegistros consulta, CancellationToken cancellationToken)
    {
        var filtrada = ConsultaRegistrosLinq.Filtrar(contexto, entidadeId, consulta);

        var total = await filtrada.CountAsync(cancellationToken);
        var ids = await ConsultaRegistrosLinq.Ordenar(contexto, filtrada, consulta.Ordenacao)
            .Skip(consulta.Paginacao.Pular)
            .Take(consulta.Paginacao.Tamanho)
            .Select(r => r.Id)
            .ToListAsync(cancellationToken);

        return new PaginaDeIds(ids, total);
    }

    public Task<int> ContarPendentesAsync(Guid entidadeId, Guid equipeId, CancellationToken cancellationToken) =>
        contexto.Registros.CountAsync(
            r => r.EntidadeId == entidadeId && r.EquipeId == equipeId && r.SituacaoAprovacao == SituacaoAprovacao.Pendente,
            cancellationToken);

    public async Task<PaginaDe<PendenteResumo>> ListarPendentesAsync(
        FiltroPendentes filtro, Paginacao paginacao, CancellationToken cancellationToken)
    {
        var consulta =
            from registro in contexto.Registros
            join entidade in contexto.Entidades on registro.EntidadeId equals entidade.Id
            join grupo in contexto.Grupos on entidade.GrupoId equals grupo.Id
            where registro.SituacaoAprovacao == SituacaoAprovacao.Pendente
                  // RN91: só equipes habilitadas na listagem do item (RN92: os itens de uma equipe removida ficam fora da fila).
                  && contexto.GruposEquipes.Any(v => v.GrupoId == grupo.Id && v.EquipeId == registro.EquipeId)
            select new { Registro = registro, Entidade = entidade, Grupo = grupo };

        if (!filtro.IncluirListagensDesativadas)
            consulta = consulta.Where(x => x.Grupo.Ativo);

        // Escopo de segurança: só as equipes da fila do usuário (RN36). Nulo = todas (ADMINISTRADOR).
        if (filtro.EquipesPermitidas is { } permitidas)
        {
            var ids = permitidas.ToList();
            consulta = consulta.Where(x => x.Registro.EquipeId != null && ids.Contains(x.Registro.EquipeId.Value));
        }

        if (filtro.EquipeId is Guid equipeId)
            consulta = consulta.Where(x => x.Registro.EquipeId == equipeId);
        if (filtro.GrupoChave is string grupoChave)
            consulta = consulta.Where(x => x.Grupo.Chave == grupoChave);
        if (filtro.EntidadeChave is string entidadeChave)
            consulta = consulta.Where(x => x.Entidade.Chave == entidadeChave);

        var total = await consulta.CountAsync(cancellationToken);
        var itens = await consulta
            // Dos mais antigos para os mais novos; o Id desempata.
            .OrderBy(x => x.Registro.CriadoEm).ThenBy(x => x.Registro.Id)
            .Skip(paginacao.Pular)
            .Take(paginacao.Tamanho)
            .Select(x => new PendenteResumo(
                x.Registro.Id, x.Grupo.Chave, x.Entidade.Chave, x.Entidade.Nome, x.Registro.EquipeId, x.Registro.CriadoPorId, x.Registro.CriadoEm))
            .ToListAsync(cancellationToken);

        return new PaginaDe<PendenteResumo>(itens, total);
    }

    public async Task<IReadOnlyList<ReferenciaEntrante>> ObterReferenciasQueApontamParaAsync(
        IReadOnlyCollection<Guid> registroIds, CancellationToken cancellationToken)
    {
        var linhas = await (
                from referencia in contexto.RegistrosReferencias
                join campo in contexto.Campos on referencia.CampoId equals campo.Id
                where registroIds.Contains(referencia.RegistroDestinoId)
                select new
                {
                    referencia.RegistroOrigemId,
                    referencia.EntidadeOrigemId,
                    referencia.CampoId,
                    referencia.RegistroDestinoId,
                    campo.AcaoExclusao,
                })
            .ToListAsync(cancellationToken);

        return linhas
            .Select(l => new ReferenciaEntrante(
                l.RegistroOrigemId, l.EntidadeOrigemId, l.CampoId, l.RegistroDestinoId,
                // O CHECK CK_Campo_Referencia garante AcaoExclusao em todo campo REFERENCIA.
                l.AcaoExclusao ?? AcaoExclusao.Restringir))
            .ToList();
    }

    public async Task<IReadOnlyDictionary<Guid, int>> ContarQueApontamParaAsync(
        Guid registroId, IReadOnlyList<ContagemPorCampo> contagens, CancellationToken cancellationToken)
    {
        if (contagens.Count == 0)
            return new Dictionary<Guid, int>();

        var linhas = await ConsultaRelacionadosLinq.Montar(contexto, registroId, contagens).ToListAsync(cancellationToken);
        return linhas.ToDictionary(l => l.CampoId, l => l.Quantidade);
    }

    public async Task<IReadOnlyList<ItemRecente>> ListarRecentesDoUsuarioAsync(
        Guid usuarioId, IReadOnlyList<EscopoDaEntidade> escopos, int limite, CancellationToken cancellationToken)
    {
        if (escopos.Count == 0 || limite <= 0)
            return [];

        return await ConsultaRecentesLinq.Montar(contexto, usuarioId, escopos, limite).ToListAsync(cancellationToken);
    }

    public async Task ExcluirAsync(PlanoExclusao plano, CancellationToken cancellationToken)
    {
        if (!plano.PodeExcluir)
            throw new InvalidOperationException("Plano de exclusão com bloqueios não pode ser executado.");

        await using var transacao = await contexto.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            // O que estiver pendente (o histórico da exclusão, RN32) é gravado na MESMA transação:
            // se a exclusão falhar, o histórico também é desfeito, e vice-versa.
            await contexto.SaveChangesAsync(cancellationToken);

            // Referências que fecham ciclos (A → B → A) saem antes, senão nenhuma ordem funcionaria.
            foreach (var referencia in plano.ReferenciasARemoverAntes)
            {
                await contexto.RegistrosReferencias
                    .Where(r => r.RegistroOrigemId == referencia.RegistroOrigemId && r.CampoId == referencia.CampoId)
                    .ExecuteDeleteAsync(cancellationToken);
            }

            // Dos filhos para o pai: a FK de destino de RegistroReferencia não tem CASCADE.
            // Cada DELETE de Registro apaga, por CASCADE no banco, as referências de origem,
            // as chaves únicas e os valores tipados (RegistroValor) dele.
            foreach (var registroId in plano.OrdemExclusao)
            {
                await contexto.Registros
                    .Where(r => r.Id == registroId)
                    .ExecuteDeleteAsync(cancellationToken);
            }

            await transacao.CommitAsync(cancellationToken);
        }
        catch (Exception ex) when (TradutorErrosSqlServer.EhViolacaoDeReferencia(ex))
        {
            // Outra requisição criou uma referência para um dos registros depois que o plano foi montado.
            // O rollback acontece ao descartar a transação: nada foi excluído.
            throw new ConflitoConcorrenciaException(
                "Outro registro passou a depender deste durante a exclusão. Tente de novo.", ex);
        }
    }
}
