using Catalogo.Application.Abstracoes;
using Catalogo.Domain.Acesso;
using Catalogo.Domain.Metadados;
using Catalogo.Domain.Registros;

namespace Catalogo.Application.Comum;

/// <summary>
/// Monta o <see cref="RegistroAlvo"/> que a <see cref="PoliticaPermissao"/> precisa, com a informação
/// de aprovação ativa lida da configuração da entidade na equipe do registro (RN33).
/// A REGRA continua toda na política; aqui só se busca o dado.
/// </summary>
/// <remarks>
/// Serviço por requisição: a resposta de cada (entidade, equipe) é guardada até o fim da requisição, para a importação (RN97) não
/// consultar a mesma configuração a cada linha.
/// </remarks>
public sealed class ContextoAprovacao(IConfiguracaoRepository configuracoes)
{
    private readonly Dictionary<(Guid Entidade, Guid Equipe), bool> _lidas = [];

    /// <summary>Aprovação ativa para a entidade nesta equipe? Sempre falso em entidades globais.</summary>
    public async Task<bool> RequerAprovacaoAsync(Entidade entidade, Guid? equipeId, CancellationToken cancellationToken)
    {
        if (entidade.EhGlobal || equipeId is not Guid equipe)
            return false;
        if (!_lidas.TryGetValue((entidade.Id, equipe), out var requer))
            _lidas[(entidade.Id, equipe)] = requer = await configuracoes.RequerAprovacaoAsync(entidade.Id, equipe, cancellationToken);
        return requer;
    }

    public async Task<RegistroAlvo> NovoAsync(Entidade entidade, Guid? equipeId, CancellationToken cancellationToken) =>
        RegistroAlvo.Novo(entidade, equipeId, await RequerAprovacaoAsync(entidade, equipeId, cancellationToken));

    public async Task<RegistroAlvo> ExistenteAsync(Entidade entidade, Registro registro, CancellationToken cancellationToken) =>
        RegistroAlvo.De(entidade, registro, await RequerAprovacaoAsync(entidade, registro.EquipeId, cancellationToken));
}
