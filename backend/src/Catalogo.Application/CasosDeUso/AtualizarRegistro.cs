using System.Text.Json.Nodes;
using Catalogo.Application.Abstracoes;
using Catalogo.Application.Comum;
using Catalogo.Application.Dtos;
using Catalogo.Domain.Excecoes;
using Catalogo.Domain.Metadados;
using Catalogo.Domain.Validacao;

namespace Catalogo.Application.CasosDeUso;

/// <summary>
/// Caso de uso: substituir um registro inteiro (PUT, RN15). O corpo traz TODOS os campos e a
/// "versao" lida no GET (RN12). Campo opcional ausente = campo removido. A equipe não muda.
/// Lança <see cref="RecursoNaoEncontradoException"/>, <see cref="AcessoNegadoException"/>,
/// <see cref="ValidacaoRegistroException"/>, <see cref="ConflitoConcorrenciaException"/> ou
/// <see cref="ConflitoUnicidadeException"/>.
/// </summary>
public sealed class AtualizarRegistro(
    IUsuarioAtual usuarioAtual,
    IMetadadosProvider metadados,
    IRegistroRepository registros,
    IUnidadeDeTrabalho unidadeDeTrabalho,
    GravadorRegistro gravador,
    MontadorRegistroDto montador)
{
    public async Task<RegistroDto> ExecutarAsync(
        string grupoChave, string entidadeChave, Guid id, JsonObject dados, CancellationToken cancellationToken)
    {
        var usuario = await usuarioAtual.ObterAsync(cancellationToken);
        (var grupo, var entidade, usuario) = await metadados.ObterEntidadeOuFalharAsync(usuario, grupoChave, entidadeChave, UsoEntidade.Escrita, cancellationToken);

        // 1. O registro precisa existir, ser desta entidade e visível (404) e o perfil precisa permitir (403).
        var registro = await registros.ObterParaAlteracaoAsync(id, cancellationToken);
        var descricao = $"Registro {id} em '{entidade.Nome}'";
        if (registro is null || registro.EntidadeId != entidade.Id)
            throw new RecursoNaoEncontradoException($"{descricao} não encontrado.", CodigosErro.RegistroNaoEncontrado);
        await gravador.ExigirPermissaoDeAlterarAsync(entidade, usuario, registro, cancellationToken);

        // 2. Concorrência (RN12): a versão lida pelo cliente precisa ser a atual...
        var versaoLida = LerVersao(dados);
        if (!versaoLida.AsSpan().SequenceEqual(registro.Versao))
        {
            throw new ConflitoConcorrenciaException(
                "O registro foi alterado por outra pessoa depois que você o leu. Recarregue e tente de novo.");
        }

        // ... e continua protegida até a gravação: o UPDATE só acontece se a Versao no banco ainda for essa.
        registros.DefinirVersaoEsperada(registro, versaoLida);

        // A equipe de um registro não muda (RN27).
        ConferirEquipeInalterada(dados, registro.EquipeId);

        // 3 a 5. A mesma validação do criar, dados, reenvio (RN35) e histórico: o mesmo caminho da importação (GravadorRegistro).
        await gravador.AlterarAsync(grupo, entidade, usuario, registro, dados, OrigemGravacao.Manual(), cancellationToken);

        try
        {
            await unidadeDeTrabalho.SalvarAlteracoesAsync(cancellationToken);
        }
        catch (ViolacaoUnicidadeBancoException ex)
        {
            throw PreparadorRegistro.ConverterViolacao(entidade, ex);
        }

        return await montador.MontarAsync(registro, entidade, cancellationToken);
    }

    /// <summary>A "versao" vem no corpo, em Base64, igual ao que o GET devolve.</summary>
    private static byte[] LerVersao(JsonObject dados)
    {
        var erros = new ErrosPorCampo();
        byte[]? versao = null;

        if (!dados.TryGetPropertyValue(ChavesReservadas.Versao, out var no) || no is not JsonValue valor
            || !valor.TryGetValue<string>(out var texto) || string.IsNullOrWhiteSpace(texto))
        {
            erros.Adicionar(ChavesReservadas.Versao, "Obrigatória: envie a \"versao\" devolvida pelo GET do registro.");
        }
        else
        {
            try
            {
                versao = Convert.FromBase64String(texto);
            }
            catch (FormatException)
            {
                erros.Adicionar(ChavesReservadas.Versao, "Inválida: envie a \"versao\" exatamente como o GET devolveu.");
            }
        }

        erros.LancarSeHouverErros();
        return versao!;
    }

    /// <summary>"equipeId" no corpo do PUT é aceito só se for a mesma equipe (o front pode reenviar o que leu).</summary>
    private static void ConferirEquipeInalterada(JsonObject dados, Guid? equipeAtual)
    {
        var informada = ResolvedorEquipe.LerDoCorpo(dados);
        if (informada is null || (Guid.TryParse(informada, out var id) && id == equipeAtual))
            return;

        var erros = new ErrosPorCampo();
        erros.Adicionar(ChavesReservadas.EquipeId, "A equipe de um registro não pode ser alterada.");
        erros.LancarSeHouverErros();
    }
}
