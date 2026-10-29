using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Catalogo.Application.Abstracoes;
using Catalogo.Application.Comum;
using Catalogo.Application.Mapeamentos;
using Catalogo.Domain.Acesso;
using Catalogo.Domain.Aprovacao;
using Catalogo.Domain.ChavesUnicas;
using Catalogo.Domain.Excecoes;
using Catalogo.Domain.Metadados;
using Catalogo.Domain.Registros;
using Catalogo.Domain.Validacao;

namespace Catalogo.Migracao.Montagem;

/// <summary>Um item pronto para o script: o registro (Dados, referências, valores e chaves únicas) e o JSON do histórico de criação.</summary>
public sealed record ItemMigrado(Entidade Entidade, Registro Registro, string HistoricoDepois, string Descricao);

/// <summary>Um item que os validadores do sistema recusaram (não entra no script).</summary>
public sealed record ItemRecusado(string Entidade, string Descricao, string Motivo);

/// <summary>
/// Monta os registros pelo MESMO caminho do sistema: <see cref="PreparadorRegistro"/> (validação dos campos com os validadores de tipo,
/// referências, equipes, dependências e unicidade, no modo em lote da importação, sem consultar o banco), depois
/// <see cref="SincronizadorRegistro"/> (Dados, referências, valores tipados e chaves únicas) e o JSON plano do histórico
/// (<see cref="RegistroMapeamentos.ParaJsonPlano"/>). Assim os dados são os que o sistema gravaria pela tela.
/// </summary>
public sealed class FabricaRegistros
{
    private static readonly JsonSerializerOptions OpcoesHistorico = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>Espaço de nomes dos GUIDs determinísticos da migração (gerar de novo dá os mesmos identificadores).</summary>
    private static readonly Guid EspacoDeNomes = new("5f0d6a52-3c1e-4b8a-9d27-6a1f0c2e8b41");

    private readonly Grupo _grupo;
    private readonly UsuarioAtual _usuario;
    private readonly PreparadorRegistro _preparador;
    private readonly Dictionary<Guid, Registro> _registros = [];
    private readonly Dictionary<(Guid RestricaoId, string Hash), Guid> _chaves = [];
    private readonly LotePreparacao _lote;

    /// <param name="usuarioId">
    /// Id provisório do autor: no script, o autor é o usuário procurado pela matrícula no banco de destino (variável @Usuario).
    /// </param>
    public FabricaRegistros(Grupo grupo, Guid usuarioId, string matricula)
    {
        _grupo = grupo;
        _usuario = new UsuarioAtual
        {
            Id = usuarioId,
            Matricula = matricula,
            Nome = matricula,
            Perfil = Perfil.Administrador,
            Equipes = new HashSet<Guid>(),
        };
        _preparador = new PreparadorRegistro(SemBanco<IRegistroRepository>.Criar(), new ValidadorRegistro(ValidadoresPadrao.Todos()), new CalculadoraChaveUnica());
        _lote = new LotePreparacao(_registros, _chaves);
    }

    public Guid UsuarioId => _usuario.Id;

    /// <summary>GUID determinístico (SHA-256 de um texto no espaço de nomes da migração, com os bits de versão 5 e variante RFC 4122).</summary>
    public static Guid GuidDe(string chave)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(EspacoDeNomes.ToString("D") + "|" + chave));
        var bytes = hash[..16];
        bytes[7] = (byte)((bytes[7] & 0x0F) | 0x50);
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);
        return new Guid(bytes);
    }

    public Entidade Entidade(string chave) =>
        _grupo.ObterEntidade(chave) ?? throw new InvalidOperationException($"A área não tem o tipo de item '{chave}': aplique antes a reconfiguração.");

    /// <summary>
    /// Valida e monta um item. <paramref name="dados"/> no formato do corpo do POST (referências pelo id). <paramref name="preservados"/>:
    /// valores de campos INATIVOS (como dados_originais), que o sistema preserva sem validar (RN69). Lança <see cref="FalhaItem"/> se o
    /// sistema recusaria o item.
    /// </summary>
    public ItemMigrado Criar(
        string entidadeChave, Guid id, JsonObject dados, Guid? equipeId, DateTime criadoEmUtc, string descricao,
        IReadOnlyDictionary<string, string>? preservados = null)
    {
        var entidade = Entidade(entidadeChave);
        RegistroPreparado preparado;
        try
        {
            preparado = _preparador
                .PrepararAsync(_grupo, entidade, dados, registroAtualId: null, equipeId, _usuario, _usuario.Id, [], CancellationToken.None, _lote)
                .GetAwaiter().GetResult();
        }
        catch (ValidacaoRegistroException erro)
        {
            throw new FalhaItem(string.Join("; ", erro.Erros.Select(e => $"{e.Key}: {string.Join(" ", e.Value)}")));
        }
        catch (ConflitoUnicidadeException erro)
        {
            throw new FalhaItem($"unicidade '{erro.NomeRestricao}': {erro.Message}");
        }

        var registro = new Registro
        {
            Id = id,
            EntidadeId = entidade.Id,
            EquipeId = entidade.EhGlobal ? null : equipeId,
            CriadoPorId = _usuario.Id,
            AtualizadoPorId = _usuario.Id,
            SituacaoAprovacao = SituacaoAprovacao.Aprovado,
            CriadoEm = criadoEmUtc,
            AtualizadoEm = criadoEmUtc,
        };

        if (preservados is { Count: > 0 })
        {
            var valoresInativos = new JsonObject();
            foreach (var (chave, valor) in preservados)
            {
                var campo = entidade.ObterCampo(chave) ?? throw new InvalidOperationException($"Campo '{chave}' não existe em '{entidade.Chave}'.");
                if (campo.Ativo)
                    throw new InvalidOperationException($"'{chave}' está ativo: vai pelos dados validados, não como preservado.");
                valoresInativos[chave] = valor;
            }

            registro.Dados = valoresInativos.ToJsonString();
        }

        SincronizadorRegistro.Aplicar(registro, entidade, preparado.Validado, preparado.ChavesUnicas);
        _registros[registro.Id] = registro;
        foreach (var chave in registro.ChavesUnicas)
            _chaves[(chave.RestricaoUnicaId, LotePreparacao.Hash(chave.ValorHash))] = registro.Id;

        var depois = registro.ParaJsonPlano(entidade).ToJsonString(OpcoesHistorico);
        return new ItemMigrado(entidade, registro, depois, descricao);
    }

    /// <summary>Um "repositório" que não pode ser usado: com o lote, o preparador não consulta o banco.</summary>
    public class SemBanco<T> : DispatchProxy where T : class
    {
        public static T Criar() => Create<T, SemBanco<T>>();

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
            throw new InvalidOperationException($"A migração não consulta o banco ({targetMethod?.Name}): os registros vêm do lote.");
    }
}

/// <summary>O sistema recusaria o item: a mensagem tem os motivos, como a API devolveria.</summary>
public sealed class FalhaItem(string motivo) : Exception(motivo);
