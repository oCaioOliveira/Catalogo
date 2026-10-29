using Catalogo.Domain.Metadados;

namespace Catalogo.Infrastructure.Persistencia.Tabelas;

// Classes que espelham as tabelas de METADADOS, coluna por coluna (ver database/scripts/V001).
// Elas só existem para o EF ler o banco: o MetadadosProvider converte para o modelo de leitura
// imutável do Domain (Grupo, Entidade, Campo...), que é o que o resto da aplicação usa.
// Por isso ficam na Infrastructure e com o sufixo "Tabela".

/// <summary>catalogo.Grupo</summary>
public sealed class GrupoTabela
{
    public Guid Id { get; set; }
    public string Chave { get; set; } = "";
    public string Nome { get; set; } = "";
    public string? Descricao { get; set; }
    public DateTime CriadoEm { get; set; }

    /// <summary>V006: grupo desativado some para os não-administradores (RN57).</summary>
    public bool Ativo { get; set; } = true;

    public DateTime? AtualizadoEm { get; set; }

    /// <summary>rowversion (V006): concorrência otimista nas edições do grupo.</summary>
    public byte[] Versao { get; set; } = [];
}

/// <summary>catalogo.GrupoEquipe (V009): as equipes habilitadas numa listagem (RN90 a RN93).</summary>
public sealed class GrupoEquipeTabela
{
    public Guid GrupoId { get; set; }
    public Guid EquipeId { get; set; }

    /// <summary>Quem habilitou (nulo na carga inicial do V009).</summary>
    public Guid? CriadoPorId { get; set; }

    public DateTime CriadoEm { get; set; }
}

/// <summary>catalogo.Entidade</summary>
public sealed class EntidadeTabela
{
    public Guid Id { get; set; }
    public Guid GrupoId { get; set; }
    public string Chave { get; set; } = "";
    public string Nome { get; set; } = "";
    public string? Descricao { get; set; }

    /// <summary>V008: nome no plural (opcional).</summary>
    public string? NomePlural { get; set; }

    /// <summary>V010: nome de um ícone do PrimeIcons (opcional).</summary>
    public string? Icone { get; set; }
    public Guid? CampoExibicaoId { get; set; }

    /// <summary>1 = registros por equipe; 0 = registros globais (V003).</summary>
    public bool EscopoEquipe { get; set; } = true;

    public DateTime CriadoEm { get; set; }

    /// <summary>V006: entidade desativada some para os não-administradores e fica somente leitura (RN57).</summary>
    public bool Ativo { get; set; } = true;

    /// <summary>V006: ordem da entidade no menu.</summary>
    public int Ordem { get; set; }

    /// <summary>
    /// V006: atualizada em TODA alteração da entidade, inclusive de campo, restrição e dependência. Assim a
    /// <see cref="Versao"/> da entidade muda junto e a concorrência otimista vale para a estrutura inteira.
    /// </summary>
    public DateTime? AtualizadoEm { get; set; }

    /// <summary>rowversion (V006): concorrência otimista nas edições da entidade e dos seus campos.</summary>
    public byte[] Versao { get; set; } = [];
}

/// <summary>catalogo.Campo</summary>
public sealed class CampoTabela
{
    public Guid Id { get; set; }
    public Guid EntidadeId { get; set; }
    public string Chave { get; set; } = "";
    public string Nome { get; set; } = "";

    /// <summary>Convertido de/para o texto do banco ("TEXTO", "DATA_HORA"...) na configuração do EF.</summary>
    public TipoDado TipoDado { get; set; }

    public bool Obrigatorio { get; set; }
    public int? TamanhoMaximo { get; set; }
    public byte? Precisao { get; set; }
    public byte? Escala { get; set; }

    /// <summary>Array JSON de textos, ex.: ["GET","POST"]. Convertido em lista pelo MetadadosProvider.</summary>
    public string? Opcoes { get; set; }

    public Guid? EntidadeReferenciadaId { get; set; }
    public AcaoExclusao? AcaoExclusao { get; set; }
    public int Ordem { get; set; }
    public bool Ativo { get; set; }
    public DateTime CriadoEm { get; set; }
}

/// <summary>catalogo.CampoDependencia</summary>
public sealed class CampoDependenciaTabela
{
    public Guid CampoId { get; set; }
    public Guid? CampoViaId { get; set; }
    public Guid CampoOrigemId { get; set; }
    public Guid CampoAlvoId { get; set; }
}

/// <summary>catalogo.RestricaoUnica</summary>
public sealed class RestricaoUnicaTabela
{
    public Guid Id { get; set; }
    public Guid EntidadeId { get; set; }
    public string Nome { get; set; } = "";
}

/// <summary>catalogo.RestricaoUnicaCampo</summary>
public sealed class RestricaoUnicaCampoTabela
{
    public Guid RestricaoUnicaId { get; set; }
    public Guid EntidadeId { get; set; }
    public Guid CampoId { get; set; }
}
