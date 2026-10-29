namespace Catalogo.Domain.Acesso;

/// <summary>
/// Pessoa que acessa o sistema (tabela catalogo.Usuario). Nunca é excluída, só desativada (RN31).
/// As regras de login (bloqueio por tentativas, RN24) ficam aqui, como métodos puros.
/// </summary>
public sealed class Usuario
{
    /// <summary>Tentativas erradas seguidas que bloqueiam o login (RN24).</summary>
    public const int MaximoTentativasFalhas = 5;

    /// <summary>Quanto tempo o login fica bloqueado depois de <see cref="MaximoTentativasFalhas"/> erros (RN24).</summary>
    public static readonly TimeSpan TempoDeBloqueio = TimeSpan.FromMinutes(15);

    public Guid Id { get; set; }

    /// <summary>Identificador de login (único, até 20 caracteres).</summary>
    public string Matricula { get; set; } = "";

    public string Nome { get; set; } = "";

    /// <summary>Hash PBKDF2 da senha (docs/adr/0016). NUNCA sai da aplicação: nem na API, nem no log, nem no histórico.</summary>
    public string SenhaHash { get; set; } = "";

    public Perfil Perfil { get; set; }
    public bool Ativo { get; set; } = true;

    /// <summary>Senha provisória: só pode trocar a senha até definir uma nova (RN25).</summary>
    public bool DeveTrocarSenha { get; set; } = true;

    public int TentativasFalhas { get; set; }

    /// <summary>Até quando (UTC) o login está bloqueado. Nulo = não está.</summary>
    public DateTime? BloqueadoAte { get; set; }

    public DateTime? UltimoAcessoEm { get; set; }

    /// <summary>Preenchido pelo banco (UTC).</summary>
    public DateTime CriadoEm { get; set; }

    /// <summary>Vínculos com equipes (tabela catalogo.UsuarioEquipe). Vínculos com equipes inativas não dão permissão.</summary>
    public List<UsuarioEquipe> Equipes { get; set; } = [];

    public bool EhAdministrador => Perfil == Perfil.Administrador;

    public bool EstaBloqueado(DateTime agoraUtc) => BloqueadoAte is DateTime ate && ate > agoraUtc;

    /// <summary>
    /// Senha errada (RN24): conta mais uma tentativa e, na 5ª seguida, bloqueia por 15 minutos.
    /// Depois que um bloqueio vence, a contagem recomeça do zero.
    /// </summary>
    public void RegistrarFalhaDeLogin(DateTime agoraUtc)
    {
        if (BloqueadoAte is DateTime ate && ate <= agoraUtc)
        {
            BloqueadoAte = null;
            TentativasFalhas = 0;
        }

        TentativasFalhas++;
        if (TentativasFalhas >= MaximoTentativasFalhas)
        {
            BloqueadoAte = agoraUtc + TempoDeBloqueio;
            TentativasFalhas = 0;
        }
    }

    /// <summary>Login certo: zera as tentativas e registra o acesso.</summary>
    public void RegistrarLoginComSucesso(DateTime agoraUtc)
    {
        TentativasFalhas = 0;
        BloqueadoAte = null;
        UltimoAcessoEm = agoraUtc;
    }

    /// <summary>
    /// Troca o hash da senha. <paramref name="provisoria"/> = senha gerada pelo sistema, que obriga a
    /// troca no próximo acesso (RN25). Também desbloqueia o login.
    /// </summary>
    public void DefinirSenha(string senhaHash, bool provisoria)
    {
        SenhaHash = senhaHash;
        DeveTrocarSenha = provisoria;
        TentativasFalhas = 0;
        BloqueadoAte = null;
    }
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
