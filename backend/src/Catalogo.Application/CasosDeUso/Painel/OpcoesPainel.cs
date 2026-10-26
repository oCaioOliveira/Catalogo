using Catalogo.Application.Dtos;
using Catalogo.Domain.Acesso;
using Catalogo.Domain.Consultas;
using Catalogo.Domain.Excecoes;
using Catalogo.Domain.Metadados;
using Catalogo.Domain.Painel;
using Catalogo.Domain.Validacao;

namespace Catalogo.Application.CasosDeUso.Painel;

/// <summary>Configuração do painel ("Painel:FusoHorario", "Painel:DiasPendenteAntigo", "Painel:MinimoAvaliacoesRanking").</summary>
/// <param name="Fuso">Fuso de negócio (RN80): o de "de", "ate" e dos meses. Usa-se o deslocamento FIXO dele.</param>
/// <param name="DiasPendenteAntigo">Pendente criado há mais que isso é "pendente antigo" (RN82).</param>
/// <param name="MinimoAvaliacoesRanking">Notas mínimas para um item entrar no ranking (RN83).</param>
public sealed record OpcoesPainel(TimeZoneInfo Fuso, int DiasPendenteAntigo = OpcoesPainel.DiasPendenteAntigoPadrao, int MinimoAvaliacoesRanking = OpcoesPainel.MinimoAvaliacoesRankingPadrao)
{
    public const string FusoPadrao = "America/Sao_Paulo";
    public const int DiasPendenteAntigoPadrao = 7;
    public const int MinimoAvaliacoesRankingPadrao = 3;
}

/// <summary>O que os casos de uso do painel têm em comum: o acesso (RN78), o "equipeId" e o período devolvido.</summary>
internal static class PermissoesPainel
{
    /// <exception cref="AcessoNegadoException">INTERNO ou EXTERNO (403 SEM_PERMISSAO).</exception>
    public static void Exigir(UsuarioAtual usuario)
    {
        if (!PoliticaPermissao.PodeVerPainel(usuario))
            throw new AcessoNegadoException("O painel gerencial é só para gestores e administradores.");
    }

    public static Guid? LerEquipe(IReadOnlyList<KeyValuePair<string, string>> parametros, ErrosPorCampo erros)
    {
        if (InterpretadorConsulta.LerUnico(parametros, ChavesReservadas.EquipeId, erros) is not string texto)
            return null;
        if (Guid.TryParseExact(texto.Trim(), "D", out var id))
            return id;

        erros.Adicionar(ChavesReservadas.EquipeId, "Deve ser o id (GUID) de uma equipe.");
        return null;
    }

    /// <summary>O GESTOR só filtra pelas equipes dele (RN79); o ADMINISTRADOR, por qualquer uma.</summary>
    /// <exception cref="AcessoNegadoException">Equipe de que o usuário não participa.</exception>
    public static void ExigirEquipeVisivel(UsuarioAtual usuario, Guid? equipeId)
    {
        if (equipeId is Guid id && PoliticaPermissao.EquipesVisiveis(usuario) is { } visiveis && !visiveis.Contains(id))
            throw new AcessoNegadoException("Você não pertence a essa equipe.");
    }

    public static PeriodoDto Periodo(PeriodoPainel periodo, OpcoesPainel opcoes) => new(periodo.De, periodo.Ate, opcoes.Fuso.Id);
}
