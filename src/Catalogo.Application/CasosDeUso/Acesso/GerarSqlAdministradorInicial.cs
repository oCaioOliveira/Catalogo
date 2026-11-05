using Catalogo.Application.Abstracoes;
using Catalogo.Application.Mapeamentos;
using Catalogo.Domain.Acesso;
using Catalogo.Domain.Historico;
using Catalogo.Domain.Validacao;

namespace Catalogo.Application.CasosDeUso.Acesso;

/// <summary>
/// Caso de uso: escrever o script SQL que cadastra o PRIMEIRO administrador num banco em que a aplicação ainda não rodou
/// (pacote de entrega dos DBAs, comando "criar-admin --gerar-sql"). Não acessa o banco. Faz o mesmo que
/// <see cref="CriarPrimeiroAdministrador"/>: valida a identificação, cria o usuário ADMINISTRADOR ativo e sem equipe, e a linha de
/// histórico de criação (RN32). Não há senha: o administrador entra pelo login do Windows, pela matrícula (RN23).
/// Exige a estrutura sem as colunas de senha (versão 19, docs/adr/0054).
/// </summary>
public sealed class GerarSqlAdministradorInicial
{
    /// <summary>A versão da estrutura que o script exige (V019: sem as colunas de senha).</summary>
    public const int VersaoMinimaEstrutura = 19;

    /// <returns>O script, pronto para o SSMS: lotes com GO, sem variáveis de SQLCMD, e para se a matrícula já existir.</returns>
    public string Executar(string? matricula, string? nome)
    {
        var erros = new ErrosPorCampo();
        CriarPrimeiroAdministrador.ValidarIdentificacao(matricula, nome, erros);
        erros.LancarSeHouverErros();

        var usuario = new Usuario
        {
            Id = Guid.NewGuid(),
            Matricula = matricula!.Trim(),
            Nome = nome!.Trim(),
            Perfil = Perfil.Administrador,
        };

        return Script(usuario, usuario.ParaJsonHistorico().ToJsonString(), Guid.NewGuid());
    }

    private static string Script(Usuario usuario, string dadosHistorico, Guid correlacaoId)
    {
        var matricula = Literal(usuario.Matricula);
        return $"""
            /* =====================================================================
               CATÁLOGO — Instalação inicial — Script 03 de 03: administrador inicial
               ---------------------------------------------------------------------
               Cadastra o primeiro administrador do Catálogo:
                 - matrícula {usuario.Matricula}, nome "{usuario.Nome}", perfil ADMINISTRADOR, ativo, sem equipe;
                 - sem senha: o administrador entra pelo login do Windows (AD), identificado pela matrícula.
               Também grava a linha de criação no histórico de alterações, como a aplicação faz.

               GERADO pelo comando "criar-admin --gerar-sql" da aplicação. Não edite à mão: gere de novo.
               Pré-requisito: a estrutura do Catálogo na versão {VersaoMinimaEstrutura} ou maior (sem as colunas de senha).
               Execução: SSMS (ou sqlcmd), conectado ao banco do Catálogo. Não usa variáveis de SQLCMD. Tempo: menos de 1 segundo.
               Proteção: se já existir um usuário com a matrícula {usuario.Matricula}, o script para, sem alterar nada.
               ===================================================================== */

            SET NOCOUNT ON;
            SET XACT_ABORT ON;
            GO

            PRINT '03_administrador_inicial: verificando pré-requisitos...';
            IF OBJECT_ID(N'cat.CATTB008_USUARIO', N'U') IS NULL OR OBJECT_ID(N'cat.CATTB015_HISTORICO_ALTERACAO', N'U') IS NULL
            BEGIN
                RAISERROR (N'03_administrador_inicial: a estrutura do Catálogo não está instalada (schema cat). Execute antes a instalação da estrutura. Nada foi alterado.', 16, 1);
                SET NOEXEC ON;
            END
            ELSE IF NOT EXISTS (SELECT 1 FROM cat.CATTB023_VERSAO_SCHEMA WHERE NU_NSU_VERSAO >= {VersaoMinimaEstrutura})
            BEGIN
                RAISERROR (N'03_administrador_inicial: este script exige a estrutura na versão {VersaoMinimaEstrutura} ou maior (sem as colunas de senha). Nada foi alterado.', 16, 1);
                SET NOEXEC ON;
            END
            ELSE IF EXISTS (SELECT 1 FROM cat.CATTB008_USUARIO WHERE CO_MATRICULA = {matricula})
            BEGIN
                RAISERROR (N'03_administrador_inicial: já existe um usuário com a matrícula {usuario.Matricula}. Nada foi alterado.', 16, 1);
                SET NOEXEC ON;
            END;
            GO

            BEGIN TRANSACTION;

            INSERT cat.CATTB008_USUARIO (NU_NSU_USUARIO, CO_MATRICULA, NO_USUARIO, NO_PERFIL, IC_ATIVO_USR)
            VALUES ('{usuario.Id:D}', {matricula}, {Literal(usuario.Nome)}, {Literal(PerfilConversor.ParaTexto(usuario.Perfil))}, {(usuario.Ativo ? 1 : 0)});
            PRINT '03_administrador_inicial: 1/2 usuário administrador cadastrado.';

            -- Histórico (RN32): a criação, com o próprio administrador como autor, como no comando criar-admin.
            INSERT cat.CATTB015_HISTORICO_ALTERACAO (NO_ALVO_TIPO, NU_ALVO, NO_OPERACAO, NU_NSU_CE_USUARIO_HA, CO_MATRI_USUARIO, NU_CORRELACAO, DE_DADO_DEPOIS)
            VALUES ({Literal(HistoricoConversor.ParaTexto(AlvoHistorico.Usuario))}, '{usuario.Id:D}', {Literal(HistoricoConversor.ParaTexto(OperacaoHistorico.Criacao))},
                    '{usuario.Id:D}', {matricula}, '{correlacaoId:D}', {Literal(dadosHistorico)});
            PRINT '03_administrador_inicial: 2/2 histórico de criação gravado.';

            COMMIT TRANSACTION;
            PRINT '03_administrador_inicial: concluído. O administrador {usuario.Matricula} entra pelo login do Windows.';
            GO

            SET NOEXEC OFF;
            GO

            """;
    }

    /// <summary>Literal SQL Unicode, com as aspas simples dobradas.</summary>
    private static string Literal(string valor) => "N'" + valor.Replace("'", "''", StringComparison.Ordinal) + "'";
}
