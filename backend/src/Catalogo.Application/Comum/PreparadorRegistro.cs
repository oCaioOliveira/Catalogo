using System.Text.Json.Nodes;
using Catalogo.Application.Abstracoes;
using Catalogo.Domain.Acesso;
using Catalogo.Domain.ChavesUnicas;
using Catalogo.Domain.Excecoes;
using Catalogo.Domain.Metadados;
using Catalogo.Domain.Registros;
using Catalogo.Domain.Validacao;

namespace Catalogo.Application.Comum;

/// <summary>
/// Validação COMPLETA dos dados de um registro, igual para criar e para atualizar:
///   1. campos: desconhecido, inativo, obrigatório e tipo (RN01 a RN05);
///   2. referências: existem, o usuário as vê, são da entidade certa (RN06), da mesma equipe ou globais (RN29)
///      e aprovadas, salvo as do próprio autor (RN38);
///   3. campos dependentes (RN08). Os erros de 2 e 3 voltam juntos;
///   4. unicidade, com verificação prévia para mensagem amigável (RN09).
/// Lança <see cref="ValidacaoRegistroException"/> ou <see cref="ConflitoUnicidadeException"/>.
/// </summary>
public sealed class PreparadorRegistro(
    IRegistroRepository registros,
    ValidadorRegistro validador,
    CalculadoraChaveUnica calculadoraChaveUnica)
{
    /// <param name="registroAtualId">Na atualização, o próprio registro: ele não conflita consigo mesmo (RN15).</param>
    /// <param name="equipeDoRegistro">Equipe do registro (nula se global): define o que ele pode referenciar (RN29).</param>
    /// <param name="usuario">Quem está gravando: registros que ele não vê são tratados como inexistentes (RN28).</param>
    /// <param name="autorDoRegistro">
    /// Quem criou o registro que está sendo gravado (na criação, o próprio usuário). Ele pode apontar para
    /// registros PENDENTES ou REJEITADOS que ele mesmo criou; os de outros, só se APROVADOS (RN38).
    /// </param>
    /// <param name="referenciasAtuais">
    /// Na atualização, as referências que o registro já tem: elas continuam valendo mesmo que a entidade apontada tenha sido
    /// desativada; só os vínculos NOVOS com entidade desativada são recusados (RN75). Na criação, vazio.
    /// </param>
    /// <param name="lote">
    /// Na importação (RN97), os registros referenciados e as chaves únicas que já existem, carregados de uma vez para todas as linhas.
    /// Com ele, nada é consultado aqui; as REGRAS são exatamente as mesmas. Nulo no cadastro manual.
    /// </param>
    public async Task<RegistroPreparado> PrepararAsync(
        Grupo grupo, Entidade entidade, JsonObject dados, Guid? registroAtualId, Guid? equipeDoRegistro,
        UsuarioAtual usuario, Guid? autorDoRegistro, IReadOnlyCollection<RegistroReferencia> referenciasAtuais,
        CancellationToken cancellationToken, LotePreparacao? lote = null)
    {
        var resultado = validador.Validar(entidade, dados);
        if (!resultado.Valido)
            throw new ValidacaoRegistroException(resultado.Erros, camposDesatualizados: resultado.CamposDesatualizados);

        var registroValidado = resultado.Registro!;

        var erros = new ErrosPorCampo();
        var registrosReferenciados = await CarregarReferenciasValidasAsync(
            grupo, registroValidado, equipeDoRegistro, usuario, autorDoRegistro, referenciasAtuais, erros, lote, cancellationToken);
        ValidadorDependencias.Validar(entidade, registroValidado, registrosReferenciados, erros);
        erros.LancarSeHouverErros();

        // Nas entidades por equipe, a unicidade vale dentro da equipe (RN09).
        var chavesUnicas = calculadoraChaveUnica.Calcular(entidade, registroValidado, entidade.EhGlobal ? null : equipeDoRegistro);
        foreach (var chave in chavesUnicas)
        {
            var existe = lote is not null
                ? lote.DonoDaChave(chave) is Guid dono && dono != registroAtualId
                : await registros.ExisteChaveUnicaAsync(chave.Restricao.Id, chave.ValorHash, registroAtualId, cancellationToken);
            if (existe)
                throw CriarConflito(entidade, chave.Restricao);
        }

        return new RegistroPreparado(registroValidado, chavesUnicas);
    }

    /// <summary>
    /// O UNIQUE do banco barrou (2601/2627) porque outra requisição gravou o mesmo valor entre a
    /// verificação prévia e o SaveChanges. Vira o mesmo 409 da verificação prévia.
    /// </summary>
    public static ConflitoUnicidadeException ConverterViolacao(Entidade entidade, ViolacaoUnicidadeBancoException violacao)
    {
        var restricao = entidade.RestricoesUnicas.FirstOrDefault(r => r.Id == violacao.RestricaoUnicaId);
        return restricao is not null
            ? CriarConflito(entidade, restricao)
            : new ConflitoUnicidadeException("(não identificada)", [], "Já existe um registro com estes valores.");
    }

    private static ConflitoUnicidadeException CriarConflito(Entidade entidade, RestricaoUnica restricao)
    {
        var nomes = string.Join(", ", restricao.Campos.Select(c => c.Nome));
        return new ConflitoUnicidadeException(
            restricao.Nome,
            restricao.Campos.Select(c => c.Chave).ToList(),
            $"Já existe um registro de '{entidade.Nome}' com o mesmo valor em: {nomes}.");
    }

    private async Task<IReadOnlyDictionary<Guid, Registro>> CarregarReferenciasValidasAsync(
        Grupo grupo, RegistroValidado registroValidado, Guid? equipeDoRegistro, UsuarioAtual usuario,
        Guid? autorDoRegistro, IReadOnlyCollection<RegistroReferencia> referenciasAtuais, ErrosPorCampo erros,
        LotePreparacao? lote, CancellationToken cancellationToken)
    {
        var ids = registroValidado.Referencias.Select(r => r.RegistroDestinoId).Distinct().ToList();
        if (ids.Count == 0)
            return new Dictionary<Guid, Registro>();

        var encontrados = lote is not null
            ? lote.Registros(ids)
            : (await registros.ObterPorIdsAsync(ids, cancellationToken)).ToDictionary(r => r.Id);
        var validos = new Dictionary<Guid, Registro>();

        foreach (var referencia in registroValidado.Referencias)
        {
            var campo = referencia.Campo;

            var entidadeDestino = encontrados.TryGetValue(referencia.RegistroDestinoId, out var achado)
                ? grupo.ObterEntidade(achado.EntidadeId)
                : null;
            var destinoGlobal = entidadeDestino?.EhGlobal ?? false;

            // RN28 e RN37: registro que o usuário não pode ver é tratado como inexistente.
            if (achado is not { } destino || !PoliticaPermissao.PodeVer(usuario, destinoGlobal, destino))
            {
                erros.Adicionar(campo.Chave, $"Registro {referencia.RegistroDestinoId} não encontrado.");
            }
            else if (!PoliticaPermissao.PodeReferenciar(equipeDoRegistro, destinoGlobal ? null : destino.EquipeId))
            {
                // RN29: a regra é sobre os REGISTROS, mesmo que o usuário pertença às duas equipes.
                erros.Adicionar(campo.Chave, equipeDoRegistro is null
                    ? $"O registro {referencia.RegistroDestinoId} é de uma equipe: um registro global só aponta para registros globais."
                    : $"O registro {referencia.RegistroDestinoId} é de outra equipe: só é possível apontar para registros da mesma equipe ou globais.");
            }
            else if (!PoliticaPermissao.PodeReferenciarSituacao(destino.SituacaoAprovacao, destino.CriadoPorId, autorDoRegistro))
            {
                // RN38: só APROVADOS, exceto os não aprovados do próprio autor.
                erros.Adicionar(campo.Chave,
                    $"O registro {referencia.RegistroDestinoId} ainda não foi aprovado: só o autor dele pode apontar para ele.");
            }
            else if (entidadeDestino is { Ativo: false } && !referenciasAtuais.Any(r => r.CampoId == campo.Id && r.RegistroDestinoId == destino.Id))
            {
                // RN75: as referências existentes continuam; um vínculo NOVO com entidade desativada, não.
                erros.Adicionar(campo.Chave, $"'{entidadeDestino.Nome}' está desativada: não aceita vínculos novos.");
            }
            else if (destino.EntidadeId != campo.EntidadeReferenciadaId)
            {
                var esperada = campo.EntidadeReferenciadaId is Guid id ? grupo.ObterEntidade(id)?.Nome : null;
                erros.Adicionar(campo.Chave,
                    $"O registro {referencia.RegistroDestinoId} não é da entidade '{esperada ?? "esperada"}'.");
            }
            else
            {
                validos[destino.Id] = destino;
            }
        }

        return validos;
    }
}

/// <summary>Dados prontos para gravar com o <see cref="SincronizadorRegistro"/>.</summary>
public sealed record RegistroPreparado(RegistroValidado Validado, IReadOnlyList<ChaveUnicaCalculada> ChavesUnicas);

/// <summary>
/// Dados de outros registros carregados de uma vez para preparar MUITOS registros (a importação, RN97): os registros que podem ser
/// referenciados (com as referências deles, para as dependências) e o dono de cada chave única que já existe no banco. O
/// <see cref="PreparadorRegistro"/> aplica as mesmas regras, só sem consultar o banco a cada registro.
/// </summary>
public sealed class LotePreparacao(
    IReadOnlyDictionary<Guid, Registro> registros,
    IReadOnlyDictionary<(Guid RestricaoId, string Hash), Guid> chavesExistentes)
{
    /// <summary>Os registros pedidos que existem (os que não existem ficam de fora, como numa consulta).</summary>
    public IReadOnlyDictionary<Guid, Registro> Registros(IEnumerable<Guid> ids) =>
        ids.Where(registros.ContainsKey).Distinct().ToDictionary(id => id, id => registros[id]);

    /// <summary>O registro do banco que já tem esta chave única (nulo se ninguém tem).</summary>
    public Guid? DonoDaChave(ChaveUnicaCalculada chave) =>
        chavesExistentes.TryGetValue((chave.Restricao.Id, Convert.ToHexString(chave.ValorHash)), out var dono) ? dono : null;

    public static string Hash(byte[] valorHash) => Convert.ToHexString(valorHash);
}
