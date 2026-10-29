using Catalogo.Application.Abstracoes;
using Catalogo.Application.CasosDeUso;
using Catalogo.Application.CasosDeUso.Acesso;
using Catalogo.Application.CasosDeUso.Administracao;
using Catalogo.Application.CasosDeUso.Aprovacao;
using Catalogo.Application.CasosDeUso.Avaliacoes;
using Catalogo.Application.CasosDeUso.Historico;
using Catalogo.Application.CasosDeUso.Importacoes;
using Catalogo.Application.CasosDeUso.Metadados;
using Catalogo.Application.CasosDeUso.Painel;
using Catalogo.Application.Comum;
using Catalogo.Domain.ChavesUnicas;
using Catalogo.Domain.Validacao;

namespace Catalogo.Api.Configuracao;

/// <summary>Registro do Domain e da Application na injeção de dependência.</summary>
public static class ConfiguracaoAplicacao
{
    public static IServiceCollection AdicionarAplicacao(this IServiceCollection services)
    {
        // Limite de linhas e fuso da exportação ("Exportacao:*"), lidos na primeira exportação.
        services.AddSingleton(servicos => LerOpcoesExportacao(servicos.GetRequiredService<IConfiguration>()));

        // Fuso de negócio e limites do painel ("Painel:*", RN80, RN82, RN83).
        services.AddSingleton(servicos => LerOpcoesPainel(servicos.GetRequiredService<IConfiguration>()));

        // Domain: sem estado, uma instância para a aplicação toda.
        services.AddSingleton(new ValidadorRegistro(ValidadoresPadrao.Todos()));
        services.AddSingleton<CalculadoraChaveUnica>();
        // Relógio do sistema (nos testes pode ser trocado por um relógio fixo).
        services.AddSingleton(TimeProvider.System);

        // Peças compartilhadas pelos casos de uso.
        services.AddScoped<PreparadorRegistro>();
        services.AddScoped<GravadorRegistro>();
        services.AddScoped<PreparadorConsultaRegistros>();
        services.AddScoped<MontadorRegistroDto>();
        services.AddScoped<ResolvedorEquipe>();
        services.AddScoped<ServicoHistorico>();
        services.AddScoped<MontadorHistorico>();
        services.AddScoped<ContextoAprovacao>();
        services.AddScoped<MontadorModeloAvaliacao>();
        services.AddScoped<SimuladorImportacao>();
        services.AddScoped<AcessoImportacao>();

        // Fornecedor de identidade: matrícula e senha locais. Para um SSO, troque só esta linha (docs/adr/0015).
        services.AddScoped<IProvedorIdentidade, ProvedorIdentidadeLocal>();

        // Casos de uso: injetados direto nos controllers (sem mediator, ver docs/adr/0004).
        services.AddScoped<ListarGrupos>();
        services.AddScoped<ListarEntidades>();
        services.AddScoped<ListarRegistros>();
        services.AddScoped<ExportarRegistros>();
        services.AddScoped<CriarRegistro>();
        services.AddScoped<ObterRegistro>();
        services.AddScoped<ListarRelacionados>();
        services.AddScoped<ListarRecentes>();
        services.AddScoped<AtualizarRegistro>();
        services.AddScoped<ExcluirRegistro>();
        services.AddScoped<ListarOpcoes>();

        services.AddScoped<Login>();
        services.AddScoped<TrocarSenha>();
        services.AddScoped<ObterUsuarioLogado>();
        services.AddScoped<CriarPrimeiroAdministrador>();
        services.AddScoped<GerarSqlAdministradorInicial>();

        services.AddScoped<ListarEquipes>();
        services.AddScoped<ObterEquipe>();
        services.AddScoped<CriarEquipe>();
        services.AddScoped<AtualizarEquipe>();
        services.AddScoped<AlterarAtivacaoEquipe>();
        services.AddScoped<ListarUsuarios>();
        services.AddScoped<ObterUsuario>();
        services.AddScoped<CriarUsuario>();
        services.AddScoped<AtualizarUsuario>();
        services.AddScoped<AlterarAtivacaoUsuario>();
        services.AddScoped<RedefinirSenhaUsuario>();

        services.AddScoped<ListarHistoricoRegistro>();
        services.AddScoped<ListarHistoricoAcesso>();

        services.AddScoped<ObterConfiguracaoAprovacao>();
        services.AddScoped<DefinirConfiguracaoAprovacao>();
        services.AddScoped<ListarHistoricoConfiguracao>();
        services.AddScoped<DecidirAprovacao>();
        services.AddScoped<ListarPendentes>();

        services.AddScoped<ObterModeloAvaliacao>();
        services.AddScoped<ListarModelosAvaliacao>();
        services.AddScoped<CriarModeloAvaliacao>();
        services.AddScoped<AtualizarModeloAvaliacao>();
        services.AddScoped<AlterarAtivacaoModeloAvaliacao>();
        services.AddScoped<ListarHistoricoModeloAvaliacao>();
        services.AddScoped<AvaliarRegistro>();
        services.AddScoped<RemoverMinhaAvaliacao>();
        services.AddScoped<ListarAvaliacoesRegistro>();
        services.AddScoped<ListarHistoricoAvaliacoes>();

        // Administração de metadados (RN50 a RN77): todas as escritas passam pelo ExecutorAlteracaoMetadados.
        services.AddScoped<ExecutorAlteracaoMetadados>();
        services.AddScoped<ListarGruposAdmin>();
        services.AddScoped<ObterGrupoAdmin>();
        services.AddScoped<CriarGrupo>();
        services.AddScoped<AtualizarGrupo>();
        services.AddScoped<ObterEquipesListagem>();
        services.AddScoped<DefinirEquipesListagem>();
        services.AddScoped<AlterarAtivacaoGrupo>();
        services.AddScoped<ExcluirGrupo>();
        services.AddScoped<ListarHistoricoMetadados>();
        services.AddScoped<CriarEntidade>();
        services.AddScoped<AtualizarEntidade>();
        services.AddScoped<AlterarAtivacaoEntidade>();
        services.AddScoped<ExcluirEntidade>();
        services.AddScoped<CriarCampo>();
        services.AddScoped<AtualizarCampo>();
        services.AddScoped<AlterarAtivacaoCampo>();
        services.AddScoped<ExcluirCampo>();
        services.AddScoped<ReordenarCampos>();
        services.AddScoped<CriarRestricao>();
        services.AddScoped<ExcluirRestricao>();
        services.AddScoped<DefinirDependencia>();
        services.AddScoped<ExcluirDependencia>();

        // Importação de planilhas (RN97 a RN104): a gravação passa pelo GravadorRegistro, o mesmo do cadastro manual.
        services.AddScoped<ObterModeloImportacao>();
        services.AddScoped<EnviarImportacao>();
        services.AddScoped<ListarImportacoes>();
        services.AddScoped<ObterImportacao>();
        services.AddScoped<ValidarImportacao>();
        services.AddScoped<ExecutarImportacao>();
        services.AddScoped<BaixarRelatorioImportacao>();
        services.AddScoped<DesfazerImportacao>();
        services.AddScoped<LimparImportacoes>();
        // Limpeza dos arquivos com mais de 7 dias (RN104), em segundo plano.
        services.AddHostedService<LimpezaImportacoes>();

        // Painel gerencial (RN78 a RN88).
        services.AddScoped<ObterVisaoGeral>();
        services.AddScoped<CalcularIndicadores>();
        services.AddScoped<ExportarIndicadores>();

        return services;
    }

    /// <summary>"Exportacao:LimiteLinhas" (padrão 10.000) e "Exportacao:FusoHorario" (padrão America/Sao_Paulo).</summary>
    public static OpcoesExportacao LerOpcoesExportacao(IConfiguration configuracao)
    {
        var limite = configuracao.GetValue("Exportacao:LimiteLinhas", OpcoesExportacao.LimitePadrao);
        if (limite <= 0)
            throw new InvalidOperationException("Exportacao:LimiteLinhas deve ser maior que zero.");

        var fuso = configuracao["Exportacao:FusoHorario"] is { Length: > 0 } id ? id : OpcoesExportacao.FusoPadrao;
        return new OpcoesExportacao(limite, TimeZoneInfo.FindSystemTimeZoneById(fuso));
    }

    /// <summary>
    /// "Painel:FusoHorario" (padrão America/Sao_Paulo), "Painel:DiasPendenteAntigo" (padrão 7) e
    /// "Painel:MinimoAvaliacoesRanking" (padrão 3).
    /// </summary>
    public static OpcoesPainel LerOpcoesPainel(IConfiguration configuracao)
    {
        var fuso = configuracao["Painel:FusoHorario"] is { Length: > 0 } id ? id : OpcoesPainel.FusoPadrao;
        var dias = configuracao.GetValue("Painel:DiasPendenteAntigo", OpcoesPainel.DiasPendenteAntigoPadrao);
        var minimo = configuracao.GetValue("Painel:MinimoAvaliacoesRanking", OpcoesPainel.MinimoAvaliacoesRankingPadrao);
        if (dias < 0)
            throw new InvalidOperationException("Painel:DiasPendenteAntigo não pode ser negativo.");
        if (minimo < 1)
            throw new InvalidOperationException("Painel:MinimoAvaliacoesRanking deve ser pelo menos 1.");

        return new OpcoesPainel(TimeZoneInfo.FindSystemTimeZoneById(fuso), dias, minimo);
    }
}
