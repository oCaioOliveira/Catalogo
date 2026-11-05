namespace Catalogo.Domain.Acesso;

/// <summary>
/// Pessoa que acessa o sistema (tabela cat.CATTB008_USUARIO). Nunca é excluída, só desativada (RN31).
/// Não há senha: quem identifica a pessoa é a autenticação integrada do Windows (AD), pela matrícula (RN23, docs/adr/0054).
/// </summary>
public sealed class Usuario
{
    public Guid Id { get; set; }

    /// <summary>Matrícula do AD (única, até 20 caracteres, ex.: C161587). É o que liga o login do Windows ao usuário.</summary>
    public string Matricula { get; set; } = "";

    public string Nome { get; set; } = "";

    public Perfil Perfil { get; set; }
    public bool Ativo { get; set; } = true;

    public DateTime? UltimoAcessoEm { get; set; }

    /// <summary>Preenchido pelo banco (UTC).</summary>
    public DateTime CriadoEm { get; set; }

    /// <summary>Vínculos com equipes (tabela catalogo.UsuarioEquipe). Vínculos com equipes inativas não dão permissão.</summary>
    public List<UsuarioEquipe> Equipes { get; set; } = [];

    public bool EhAdministrador => Perfil == Perfil.Administrador;

    /// <summary>Login pelo Windows aceito: registra o acesso.</summary>
    public void RegistrarAcesso(DateTime agoraUtc) => UltimoAcessoEm = agoraUtc;
}

/// <summary>Vínculo usuário ↔ equipe (tabela catalogo.UsuarioEquipe).</summary>
public sealed class UsuarioEquipe
{
    public Guid UsuarioId { get; set; }
    public Guid EquipeId { get; set; }

    /// <summary>Preenchido pelo banco (UTC).</summary>
    public DateTime CriadoEm { get; set; }
}

/// <summary>Equipe (tabela catalogo.Equipe). Nunca é excluída, só desativada (RN31).</summary>
public sealed class Equipe
{
    public Guid Id { get; set; }

    /// <summary>Único (sem diferenciar maiúsculas nem acentos, pela collation).</summary>
    public string Nome { get; set; } = "";

    public bool Ativa { get; set; } = true;

    /// <summary>Preenchido pelo banco (UTC).</summary>
    public DateTime CriadoEm { get; set; }
}
