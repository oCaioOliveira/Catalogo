using Catalogo.Domain.Metadados;

namespace Catalogo.Domain.Tests.Metadados.Administracao;

/// <summary>
/// Um grupo pequeno para os testes da administração de metadados:
///   - fabricante (GLOBAL): nome, modelo_padrao → modelo
///   - modelo (GLOBAL): nome, fabricante → fabricante
///   - categoria (GLOBAL): nome, fabricante → fabricante
///   - equipamento (POR EQUIPE): nome TEXTO(100), categoria → categoria, fabricante → fabricante,
///     data_aquisicao DATA, valor DECIMAL(12,2), situacao LISTA, observacao TEXTO_LONGO, antigo (inativo), modelo → modelo
///   - local (POR EQUIPE): nome
/// Restrição "uq_equipamento_nome" (nome). Campo de exibição de equipamento: nome.
/// </summary>
internal sealed class GrupoInventario
{
    public static readonly Guid FabricanteId = Guid.NewGuid();
    public static readonly Guid ModeloId = Guid.NewGuid();
    public static readonly Guid CategoriaId = Guid.NewGuid();
    public static readonly Guid EquipamentoId = Guid.NewGuid();
    public static readonly Guid LocalId = Guid.NewGuid();

    public GrupoInventario(IReadOnlyList<CampoDependencia>? dependenciasDoEquipamento = null)
    {
        FabricanteNome = Campo(FabricanteId, "nome", TipoDado.Texto, 1, tamanho: 100);
        FabricanteModeloPadrao = Campo(FabricanteId, "modelo_padrao", TipoDado.Referencia, 2, referencia: ModeloId);
        ModeloNome = Campo(ModeloId, "nome", TipoDado.Texto, 1, tamanho: 100);
        ModeloFabricante = Campo(ModeloId, "fabricante", TipoDado.Referencia, 2, referencia: FabricanteId);
        Modelo = Campo(EquipamentoId, "modelo", TipoDado.Referencia, 9, referencia: ModeloId);
        CategoriaNome = Campo(CategoriaId, "nome", TipoDado.Texto, 1, tamanho: 50);
        CategoriaFabricante = Campo(CategoriaId, "fabricante", TipoDado.Referencia, 2, referencia: FabricanteId);
        Nome = Campo(EquipamentoId, "nome", TipoDado.Texto, 1, tamanho: 100, obrigatorio: true);
        Categoria = Campo(EquipamentoId, "categoria", TipoDado.Referencia, 2, referencia: CategoriaId);
        Fabricante = Campo(EquipamentoId, "fabricante", TipoDado.Referencia, 3, referencia: FabricanteId);
        DataAquisicao = Campo(EquipamentoId, "data_aquisicao", TipoDado.Data, 4);
        Valor = Campo(EquipamentoId, "valor", TipoDado.Decimal, 5, precisao: 12, escala: 2);
        Situacao = Campo(EquipamentoId, "situacao", TipoDado.Lista, 6, opcoes: ["EM_USO", "BAIXADO"]);
        Observacao = Campo(EquipamentoId, "observacao", TipoDado.TextoLongo, 7);
        Antigo = Campo(EquipamentoId, "antigo", TipoDado.Texto, 8, tamanho: 10, ativo: false);
        LocalNome = Campo(LocalId, "nome", TipoDado.Texto, 1, tamanho: 50);

        UqNome = new RestricaoUnica { Id = Guid.NewGuid(), EntidadeId = EquipamentoId, Nome = "uq_equipamento_nome", Campos = [Nome] };

        Grupo = new Grupo
        {
            Id = Guid.NewGuid(),
            Chave = "inventario",
            Nome = "Inventário",
            Entidades =
            [
                Entidade(FabricanteId, "fabricante", global: true, [FabricanteNome, FabricanteModeloPadrao]),
                Entidade(ModeloId, "modelo", global: true, [ModeloNome, ModeloFabricante]),
                Entidade(CategoriaId, "categoria", global: true, [CategoriaNome, CategoriaFabricante]),
                new Entidade
                {
                    Id = EquipamentoId, GrupoId = Guid.Empty, Chave = "equipamento", Nome = "Equipamento", EscopoEquipe = true,
                    CampoExibicaoId = Nome.Id,
                    Campos = [Nome, Categoria, Fabricante, DataAquisicao, Valor, Situacao, Observacao, Antigo, Modelo],
                    RestricoesUnicas = [UqNome],
                    Dependencias = dependenciasDoEquipamento ?? [],
                },
                Entidade(LocalId, "local", global: false, [LocalNome]),
            ],
        };
    }

    public Grupo Grupo { get; }
    public Entidade FabricanteEntidade => Grupo.ObterEntidade(FabricanteId)!;
    public Entidade ModeloEntidade => Grupo.ObterEntidade(ModeloId)!;
    public Entidade CategoriaEntidade => Grupo.ObterEntidade(CategoriaId)!;
    public Entidade Equipamento => Grupo.ObterEntidade(EquipamentoId)!;
    public Entidade Local => Grupo.ObterEntidade(LocalId)!;

    public Campo FabricanteNome { get; }
    public Campo FabricanteModeloPadrao { get; }
    public Campo ModeloNome { get; }
    public Campo ModeloFabricante { get; }
    public Campo Modelo { get; }
    public Campo CategoriaNome { get; }
    public Campo CategoriaFabricante { get; }
    public Campo Nome { get; }
    public Campo Categoria { get; }
    public Campo Fabricante { get; }
    public Campo DataAquisicao { get; }
    public Campo Valor { get; }
    public Campo Situacao { get; }
    public Campo Observacao { get; }
    public Campo Antigo { get; }
    public Campo LocalNome { get; }
    public RestricaoUnica UqNome { get; }

    private static Entidade Entidade(Guid id, string chave, bool global, IReadOnlyList<Campo> campos) => new()
    {
        Id = id, GrupoId = Guid.Empty, Chave = chave, Nome = chave, EscopoEquipe = !global, Campos = campos,
    };

    public static Campo Campo(
        Guid entidadeId, string chave, TipoDado tipo, int ordem, int? tamanho = null, bool obrigatorio = false,
        Guid? referencia = null, byte? precisao = null, byte? escala = null, IReadOnlyList<string>? opcoes = null, bool ativo = true) => new()
    {
        Id = Guid.NewGuid(),
        EntidadeId = entidadeId,
        Chave = chave,
        Nome = chave,
        TipoDado = tipo,
        Obrigatorio = obrigatorio,
        TamanhoMaximo = tamanho,
        Precisao = precisao,
        Escala = escala,
        Opcoes = opcoes ?? [],
        EntidadeReferenciadaId = referencia,
        AcaoExclusao = referencia is null ? null : AcaoExclusao.Restringir,
        Ordem = ordem,
        Ativo = ativo,
    };
}
