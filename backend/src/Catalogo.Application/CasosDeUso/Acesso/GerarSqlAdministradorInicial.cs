using Catalogo.Application.Abstracoes;
using Catalogo.Application.Mapeamentos;
using Catalogo.Domain.Acesso;
using Catalogo.Domain.Historico;
using Catalogo.Domain.Validacao;

namespace Catalogo.Application.CasosDeUso.Acesso;

/// <summary>
/// Caso de uso: escrever o script SQL que cadastra o PRIMEIRO administrador num banco em que a aplicação ainda não rodou
/// (pacote de entrega dos DBAs, comando "criar-admin --gerar-sql"). Não acessa o banco. Faz o mesmo que
/// <see cref="CriarPrimeiroAdministrador"/>: valida a identificação, gera o hash com o mesmo <see cref="IHasherSenha"/>,
/// cria o usuário ADMINISTRADOR ativo, sem equipe e com troca de senha obrigatória (RN25), e a linha de histórico de criação
/// (RN32). No script vai SOMENTE o hash: a senha não aparece em lugar nenhum.
/// <para>
/// A política de senha (<see cref="PoliticaSenha"/>) vale aqui como em toda a aplicação. A única exceção é explícita
/// (<paramref name="aceitarSenhaForaDaPolitica"/> em <see cref="Executar"/>): só para esse usuário inicial, porque a senha é
/// provisória e a troca no primeiro acesso exige uma senha dentro da política. Nada muda para os demais casos.
/// </para>
/// </summary>
public sealed class GerarSqlAdministradorInicial(IHasherSenha hasher)
{
    /// <returns>O script, pronto para o SSMS: lotes com GO, sem variáveis de SQLCMD, e para se a matrícula já existir.</returns>
    public string Executar(string? matricula, string? nome, string? senha, bool aceitarSenhaForaDaPolitica)
    {
        var erros = new ErrosPorCampo();
        CriarPrimeiroAdministrador.ValidarIdentificacao(matricula, nome, erros);
        if (string.IsNullOrEmpty(senha))
            erros.Adicionar("senha", "Campo obrigatório.");
        else if (!aceitarSenhaForaDaPolitica)
        {
            foreach (var erro in PoliticaSenha.Validar(senha))
                erros.Adicionar("senha", erro);
        }
        erros.LancarSeHouverErros();

        var usuario = new Usuario
        {
            Id = Guid.NewGuid(),
            Matricula = matricula!.Trim(),
            Nome = nome!.Trim(),
            Perfil = Perfil.Administrador,
        };
        usuario.DefinirSenha(hasher.GerarHash(senha!), provisoria: true);

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
                 - senha PROVISÓRIA, gravada SOMENTE como hash (PBKDF2, o mesmo da aplicação): a senha não está neste script;
                 - DeveTrocarSenha = 1: no primeiro acesso a aplicação exige a troca da senha, antes de qualquer outra ação.
               Também grava a linha de criação no histórico de alterações, como a aplicação faz.

               GERADO pelo comando "criar-admin --gerar-sql" da aplicação. Não edite à mão: gere de novo.
               Pré-requisito: os scripts 01 e 02 já executados.
               Execução: SSMS (ou sqlcmd), conectado ao banco do Catálogo. Não usa variáveis de SQLCMD. Tempo: menos de 1 segundo.
               Proteção: se já existir um usuário com a matrícula {usuario.Matricula}, o script para, sem alterar nada.
               ===================================================================== */

            SET NOCOUNT ON;
            SET XACT_ABORT ON;
            GO

            PRINT '03_administrador_inicial: verificando pré-requisitos...';
            IF OBJECT_ID(N'catalogo.Usuario', N'U') IS NULL OR OBJECT_ID(N'catalogo.HistoricoAlteracao', N'U') IS NULL
            BEGIN
                RAISERROR (N'03_administrador_inicial: a estrutura do Catálogo não está instalada (schema catalogo). Execute antes o 01 e o 02. Nada foi alterado.', 16, 1);
                SET NOEXEC ON;
            END
            ELSE IF EXISTS (SELECT 1 FROM catalogo.Usuario WHERE Matricula = {matricula})
            BEGIN
                RAISERROR (N'03_administrador_inicial: já existe um usuário com a matrícula {usuario.Matricula}. Nada foi alterado.', 16, 1);
                SET NOEXEC ON;
            END;
            GO

            BEGIN TRANSACTION;

            INSERT catalogo.Usuario (Id, Matricula, Nome, SenhaHash, Perfil, Ativo, DeveTrocarSenha)
            VALUES ('{usuario.Id:D}', {matricula}, {Literal(usuario.Nome)}, {Literal(usuario.SenhaHash)},
                    {Literal(PerfilConversor.ParaTexto(usuario.Perfil))}, {(usuario.Ativo ? 1 : 0)}, {(usuario.DeveTrocarSenha ? 1 : 0)});
            PRINT '03_administrador_inicial: 1/2 usuário administrador cadastrado.';

            -- Histórico (RN32): a criação, com o próprio administrador como autor, como no comando criar-admin.
            INSERT catalogo.HistoricoAlteracao (AlvoTipo, AlvoId, Operacao, UsuarioId, UsuarioMatricula, CorrelacaoId, DadosDepois)
            VALUES ({Literal(HistoricoConversor.ParaTexto(AlvoHistorico.Usuario))}, '{usuario.Id:D}', {Literal(HistoricoConversor.ParaTexto(OperacaoHistorico.Criacao))},
                    '{usuario.Id:D}', {matricula}, '{correlacaoId:D}', {Literal(dadosHistorico)});
            PRINT '03_administrador_inicial: 2/2 histórico de criação gravado.';

            COMMIT TRANSACTION;
            PRINT '03_administrador_inicial: concluído. No primeiro acesso, o administrador {usuario.Matricula} precisa trocar a senha provisória.';
            GO

            SET NOEXEC OFF;
            GO

            """;
    }

    /// <summary>Literal SQL Unicode, com as aspas simples dobradas.</summary>
    private static string Literal(string valor) => "N'" + valor.Replace("'", "''", StringComparison.Ordinal) + "'";
}
