using Catalogo.Domain.Aprovacao;
using Catalogo.Domain.Consultas;
using Catalogo.Domain.Importacoes;
using Catalogo.Domain.Metadados;

namespace Catalogo.Application.Abstracoes;

/// <summary>As importações de planilha (tabelas catalogo.Importacao e catalogo.ImportacaoArquivo, V011).</summary>
public interface IImportacaoRepository
{
    /// <summary>Marca a importação e o arquivo para o próximo SaveChanges.</summary>
    void Adicionar(Importacao importacao, ImportacaoArquivo arquivo);

    /// <summary>A importação, rastreada (para alterar a situação e o resumo); nula se não existe.</summary>
    Task<Importacao?> ObterAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>O arquivo enviado; nulo se a limpeza já o apagou (RN104).</summary>
    Task<byte[]?> ObterConteudoAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>As importações de um tipo de item, das mais recentes para as mais antigas, no escopo de quem pergunta (RN97).</summary>
    Task<PaginaDe<Importacao>> ListarAsync(Guid entidadeId, EscopoImportacoes escopo, Paginacao paginacao, CancellationToken cancellationToken);

    /// <summary>As equipes ativas de cada autor (para decidir quem vê uma importação).</summary>
    Task<IReadOnlySet<Guid>> ObterEquipesDoUsuarioAsync(Guid usuarioId, CancellationToken cancellationToken);

    /// <summary>
    /// A limpeza (RN104): apaga os arquivos das importações criadas antes de <paramref name="limite"/> e marca como EXPIRADA as que
    /// nunca foram executadas. Direto no banco (ExecuteDelete/ExecuteUpdate).
    /// </summary>
    Task<(int ArquivosApagados, int Expiradas)> LimparAntigasAsync(DateTime limite, CancellationToken cancellationToken);
}

/// <summary>
/// Quem vê quais importações (RN97): as próprias; GESTOR e ADMINISTRADOR também as dos membros das suas equipes; nulo em
/// <see cref="EquipesDosAutores"/> e <see cref="Todas"/> verdadeiro: todas.
/// </summary>
public sealed record EscopoImportacoes(Guid UsuarioId, bool Todas, IReadOnlySet<Guid>? EquipesDosAutores);

/// <summary>Um item cujo campo de exibição tem um dos nomes procurados (a resolução de referências pelo nome, RN99).</summary>
public sealed record ItemComNome(Guid Id, string Nome, Guid? EquipeId, SituacaoAprovacao Situacao, Guid? CriadoPorId);

/// <summary>As consultas EM LOTE da importação (uma por tipo de item referenciado, nunca por linha), todas em LINQ.</summary>
public interface IConsultasImportacao
{
    /// <summary>
    /// Os itens VISÍVEIS (o escopo da listagem, <c>PreparadorConsultaRegistros.Escopo</c>) de uma entidade cujo campo de exibição tem
    /// um dos <paramref name="nomes"/>: em texto (TEXTO, TEXTO_LONGO, LISTA, URL), sem diferenciar maiúsculas nem acentos (a collation
    /// do banco, CI_AI); em número (INTEIRO, DECIMAL), pelo valor. <see cref="ItemComNome.Nome"/> é o rótulo do item (RN20).
    /// </summary>
    Task<IReadOnlyList<ItemComNome>> BuscarPorNomesAsync(
        Guid entidadeId, Campo campoExibicao, IReadOnlyCollection<string> nomes, ConsultaRegistros escopo, CancellationToken cancellationToken);

    /// <summary>
    /// Os nomes (campo de exibição de texto) visíveis de uma entidade, sem repetir, em ordem alfabética, até <paramref name="limite"/>
    /// (a lista suspensa do modelo). Campo de exibição que não é texto: vazio.
    /// </summary>
    Task<IReadOnlyList<string>> ListarNomesAsync(
        Guid entidadeId, Campo campoExibicao, ConsultaRegistros escopo, int limite, CancellationToken cancellationToken);

    /// <summary>O registro que já tem cada chave única (restrição + hash), para as chaves procuradas que existem no banco.</summary>
    Task<IReadOnlyDictionary<(Guid RestricaoId, string Hash), Guid>> ObterDonosDasChavesAsync(
        Guid entidadeId, IReadOnlyCollection<(Guid RestricaoId, byte[] Hash)> chaves, CancellationToken cancellationToken);

    /// <summary>Registros RASTREADOS para alteração (com referências, chaves e valores), como o PUT os carrega.</summary>
    Task<IReadOnlyList<Domain.Registros.Registro>> ObterParaAlteracaoAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken);

    /// <summary>Os registros (entre <paramref name="ids"/>) com alguma linha de histórico fora de <paramref name="correlacaoId"/> (RN103).</summary>
    Task<IReadOnlySet<Guid>> AlteradosForaDaCorrelacaoAsync(
        IReadOnlyCollection<Guid> ids, Guid correlacaoId, CancellationToken cancellationToken);
}

/// <summary>
/// Uma coluna do modelo XLSX (RN98): o cabeçalho, o formato das células, a lista suspensa (se houver) e a linha dela na aba
/// "Instruções".
/// </summary>
/// <param name="Tipo">Define o formato das células (data, número, texto). Nulo na coluna Equipe.</param>
/// <param name="Lista">Os valores da lista suspensa; nula = sem lista.</param>
/// <param name="ListaRestrita">Verdadeiro: o Excel recusa um valor fora da lista (LISTA, BOOLEANO, Equipe). Falso: só avisa (referências,
/// cuja lista pode não ter um item criado depois de baixar o modelo).</param>
public sealed record ColunaModelo(
    string Cabecalho,
    TipoDado? Tipo,
    byte? Escala,
    IReadOnlyList<string>? Lista,
    bool ListaRestrita,
    string DescricaoTipo,
    bool Obrigatorio,
    string ComoPreencher);

/// <summary>O modelo XLSX de um tipo de item (RN98): as colunas e as regras gerais (unicidade, referências, dependências).</summary>
public sealed record ModeloPlanilha(string NomeEntidade, IReadOnlyList<ColunaModelo> Colunas, IReadOnlyList<string> Regras);

/// <summary>
/// Leitura e geração das planilhas da importação (Infrastructure, com o ClosedXML). Toda regra de conteúdo fica na Application e no
/// Domain: aqui só se lê e se escreve o arquivo.
/// </summary>
public interface IArquivosImportacao
{
    /// <summary>
    /// O formato pelo CONTEÚDO (RN98), não pela extensão: XLSX é um pacote ZIP do Excel; CSV é texto. Lança
    /// <see cref="Domain.Excecoes.ValidacaoRegistroException"/> (chave "arquivo") se não for nenhum dos dois ou não bater com a extensão.
    /// </summary>
    FormatoImportacao DetectarFormato(byte[] conteudo, string nomeArquivo);

    /// <summary>Lê o arquivo (valores das células, nunca fórmulas; a primeira aba que não é "Instruções"), com os limites do RN98.</summary>
    PlanilhaImportada Ler(byte[] conteudo, FormatoImportacao formato);

    byte[] GerarModelo(ModeloPlanilha modelo);

    /// <summary>O relatório: as linhas originais e duas colunas a mais, "Resultado" e "Mensagens".</summary>
    byte[] GerarRelatorio(PlanilhaImportada planilha, IReadOnlyDictionary<int, (string Resultado, string Mensagens)> resultados);
}
