using Catalogo.Application.Abstracoes;
using Catalogo.Application.Comum;
using Catalogo.Application.Dtos;
using Catalogo.Application.Mapeamentos;
using Catalogo.Domain.Acesso;
using Catalogo.Domain.Consultas;
using Catalogo.Domain.Validacao;

namespace Catalogo.Application.CasosDeUso.Administracao;

/// <summary>
/// Caso de uso: listar usuários (só ADMINISTRADOR), com filtros por equipe, perfil, ativo e "busca" (contido na matrícula ou no
/// nome, sem diferenciar maiúsculas nem acentos), e "ordenacao" (nome, matricula, ultimoAcessoEm ou criadoEm; "-" = decrescente).
/// </summary>
public sealed class ListarUsuarios(IUsuarioAtual usuarioAtual, IUsuarioRepository usuarios, IEquipeRepository equipes)
{
    /// <param name="parametros">Parâmetros da URL: equipeId, perfil, ativo, busca, ordenacao, pagina, tamanho.</param>
    public async Task<PaginaDto<UsuarioDto>> ExecutarAsync(
        IEnumerable<KeyValuePair<string, string>> parametros, CancellationToken cancellationToken)
    {
        Permissoes.ExigirAdministrador(await usuarioAtual.ObterAsync(cancellationToken));

        var lista = parametros.ToList();
        var erros = new ErrosPorCampo();
        var paginacao = InterpretadorConsulta.InterpretarPaginacao(lista, erros);

        Guid? equipeId = null;
        if (InterpretadorConsulta.LerUnico(lista, "equipeId", erros) is string textoEquipe)
        {
            if (Guid.TryParseExact(textoEquipe.Trim(), "D", out var id))
                equipeId = id;
            else
                erros.Adicionar("equipeId", "Deve ser o id (GUID) de uma equipe.");
        }

        Perfil? perfil = null;
        if (InterpretadorConsulta.LerUnico(lista, "perfil", erros) is string textoPerfil)
            perfil = ValidacaoUsuario.LerPerfil(textoPerfil, erros);

        bool? ativo = null;
        if (InterpretadorConsulta.LerUnico(lista, "ativo", erros) is string textoAtivo)
        {
            if (bool.TryParse(textoAtivo, out var valor))
                ativo = valor;
            else
                erros.Adicionar("ativo", "Deve ser true ou false.");
        }

        var busca = LerBusca(lista, erros);
        var ordenacao = LerOrdenacao(lista, erros);
        erros.LancarSeHouverErros();

        var pagina = await usuarios.ListarAsync(new FiltroUsuarios(equipeId, perfil, ativo, busca, ordenacao), paginacao, cancellationToken);
        var equipesPorId = await ValidacaoUsuario.EquipesDosUsuariosAsync(equipes, pagina.Itens, cancellationToken);

        return new PaginaDto<UsuarioDto>(
            pagina.Itens.Select(u => u.ParaDto(equipesPorId)).ToList(),
            paginacao.Pagina, paginacao.Tamanho, pagina.Total, paginacao.TotalPaginas(pagina.Total));
    }

    public const int TamanhoMaximoBusca = 100;

    /// <summary>"busca": texto a procurar (sem espaços nas pontas; vazio = sem filtro; até 100 caracteres).</summary>
    internal static string? LerBusca(IReadOnlyList<KeyValuePair<string, string>> parametros, ErrosPorCampo erros)
    {
        var busca = InterpretadorConsulta.LerUnico(parametros, "busca", erros)?.Trim();
        if (string.IsNullOrEmpty(busca))
            return null;
        if (busca.Length <= TamanhoMaximoBusca)
            return busca;

        erros.Adicionar("busca", $"Deve ter no máximo {TamanhoMaximoBusca} caracteres.");
        return null;
    }

    private static readonly IReadOnlyDictionary<string, CampoOrdenacaoUsuario> CamposOrdenacao =
        new Dictionary<string, CampoOrdenacaoUsuario>(StringComparer.Ordinal)
        {
            ["nome"] = CampoOrdenacaoUsuario.Nome,
            ["matricula"] = CampoOrdenacaoUsuario.Matricula,
            ["ultimoAcessoEm"] = CampoOrdenacaoUsuario.UltimoAcessoEm,
            ["criadoEm"] = CampoOrdenacaoUsuario.CriadoEm,
        };

    /// <summary>"ordenacao=[-]campo": nome (padrão), matricula, ultimoAcessoEm ou criadoEm.</summary>
    private static OrdenacaoUsuarios? LerOrdenacao(IReadOnlyList<KeyValuePair<string, string>> parametros, ErrosPorCampo erros)
    {
        var texto = InterpretadorConsulta.LerUnico(parametros, "ordenacao", erros)?.Trim();
        if (string.IsNullOrEmpty(texto))
            return null;

        var descendente = texto.StartsWith('-');
        if (CamposOrdenacao.TryGetValue(descendente ? texto[1..] : texto, out var campo))
            return new OrdenacaoUsuarios(campo, descendente);

        erros.Adicionar("ordenacao", "Use nome, matricula, ultimoAcessoEm ou criadoEm, com \"-\" na frente para decrescente.");
        return null;
    }
}
