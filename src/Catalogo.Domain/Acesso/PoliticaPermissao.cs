using Catalogo.Domain.Aprovacao;
using Catalogo.Domain.Metadados;
using Catalogo.Domain.Registros;

namespace Catalogo.Domain.Acesso;

/// <summary>O que se quer fazer com um registro.</summary>
public enum OperacaoRegistro
{
    Ver,
    Criar,
    Alterar,
    Excluir,

    /// <summary>Aprovar ou rejeitar um registro pendente (RN36).</summary>
    Decidir,
}

/// <summary>Resultado de uma verificação de permissão.</summary>
public enum DecisaoPermissao
{
    Permitido,

    /// <summary>O usuário não pode nem VER o registro: responda 404, sem revelar que ele existe (RN28).</summary>
    NaoEncontrado,

    /// <summary>O usuário vê o registro, mas não pode fazer a operação: responda 403.</summary>
    Proibido,
}

/// <summary>
/// Tudo o que a política precisa saber sobre o registro (existente ou a criar).
/// </summary>
/// <param name="Global">É de uma entidade global (<see cref="Entidade.EhGlobal"/>).</param>
/// <param name="EquipeId">Equipe do registro (nula para globais).</param>
/// <param name="Situacao">Situação de aprovação (APROVADO para registros a criar).</param>
/// <param name="CriadoPorId">Quem criou (nulo para registros a criar ou antigos).</param>
/// <param name="RequerAprovacao">A aprovação está ativa para a entidade NA EQUIPE do registro (RN33).</param>
public sealed record RegistroAlvo(
    bool Global,
    Guid? EquipeId,
    SituacaoAprovacao Situacao = SituacaoAprovacao.Aprovado,
    Guid? CriadoPorId = null,
    bool RequerAprovacao = false)
{
    /// <summary>Registro que ainda vai ser criado.</summary>
    public static RegistroAlvo Novo(Entidade entidade, Guid? equipeId, bool requerAprovacao) =>
        new(entidade.EhGlobal, equipeId, SituacaoAprovacao.Aprovado, null, requerAprovacao);

    /// <summary>Registro existente.</summary>
    public static RegistroAlvo De(Entidade entidade, Registro registro, bool requerAprovacao) =>
        new(entidade.EhGlobal, registro.EquipeId, registro.SituacaoAprovacao, registro.CriadoPorId, requerAprovacao);
}

/// <summary>
/// ÚNICO lugar com as regras de permissão (RN26 a RN29, RN33 a RN38 e RN40, RN44 e RN47). Código puro: os casos de uso
/// perguntam aqui; ninguém mais faz "if (perfil == ...)".
///
/// Sem aprovação ativa (RN26):
/// | Perfil        | Ver | Criar | Alterar | Excluir | Decidir |
/// |---------------|-----|-------|---------|---------|---------|
/// | EXTERNO       | sim | não   | não     | não     | não     |
/// | INTERNO       | sim | sim   | sim     | não     | não     |
/// | GESTOR        | sim | sim   | sim     | sim     | sim     |
/// | ADMINISTRADOR | sim | sim   | sim     | sim     | sim     |
///
/// Com aprovação ativa para a entidade NA EQUIPE do registro, o EXTERNO também (RN34, RN35):
///   - CRIA (o registro nasce PENDENTE);
///   - ALTERA só os registros que ELE criou e que estão PENDENTE ou REJEITADO (reenvio).
///
/// Escopo: não-administradores só enxergam registros das SUAS equipes ativas e os globais. Registros
/// PENDENTE/REJEITADO só são vistos pelo autor e por INTERNO, GESTOR e ADMINISTRADOR (RN37).
/// Registros GLOBAIS são vistos por todos, mas só o ADMINISTRADOR cria, altera ou exclui.
/// </summary>
public static class PoliticaPermissao
{
    public static DecisaoPermissao Decidir(UsuarioAtual usuario, OperacaoRegistro operacao, RegistroAlvo alvo)
    {
        // RN28 e RN37: registro que o usuário não pode ver "não existe" para ele.
        // Na criação o registro ainda não existe: vale só o escopo de equipe.
        if (!PodeVerEquipe(usuario, alvo.Global, alvo.EquipeId)
            || (operacao != OperacaoRegistro.Criar && !PodeVerSituacao(usuario, alvo.Situacao, alvo.CriadoPorId)))
        {
            return DecisaoPermissao.NaoEncontrado;
        }

        if (operacao == OperacaoRegistro.Ver)
            return DecisaoPermissao.Permitido;

        // RN27: globais só o administrador altera.
        if (alvo.Global)
            return usuario.EhAdministrador ? DecisaoPermissao.Permitido : DecisaoPermissao.Proibido;

        // RN34 e RN35: o que a aprovação ativa libera para o EXTERNO.
        if (usuario.Perfil == Perfil.Externo && alvo.RequerAprovacao)
        {
            var permitido = operacao switch
            {
                OperacaoRegistro.Criar => true,
                OperacaoRegistro.Alterar => alvo.CriadoPorId == usuario.Id
                                            && alvo.Situacao is SituacaoAprovacao.Pendente or SituacaoAprovacao.Rejeitado,
                _ => false,
            };
            return permitido ? DecisaoPermissao.Permitido : DecisaoPermissao.Proibido;
        }

        return PerfilPermite(usuario.Perfil, operacao) ? DecisaoPermissao.Permitido : DecisaoPermissao.Proibido;
    }

    /// <summary>Sem aprovação (registro APROVADO, aprovação desativada): as regras da parte 1.</summary>
    public static DecisaoPermissao Decidir(UsuarioAtual usuario, OperacaoRegistro operacao, bool registroGlobal, Guid? equipeDoRegistro) =>
        Decidir(usuario, operacao, new RegistroAlvo(registroGlobal, equipeDoRegistro));

    /// <summary>Atalho para quando já se tem a entidade (sem aprovação).</summary>
    public static DecisaoPermissao Decidir(UsuarioAtual usuario, OperacaoRegistro operacao, Entidade entidade, Guid? equipeDoRegistro) =>
        Decidir(usuario, operacao, entidade.EhGlobal, equipeDoRegistro);

    /// <summary>Escopo de equipe (RN26): globais, administrador, ou uma das suas equipes ativas.</summary>
    public static bool PodeVer(UsuarioAtual usuario, bool registroGlobal, Guid? equipeDoRegistro) =>
        PodeVerEquipe(usuario, registroGlobal, equipeDoRegistro);

    /// <summary>Escopo de equipe E situação de aprovação (RN26 e RN37).</summary>
    public static bool PodeVer(UsuarioAtual usuario, bool registroGlobal, Registro registro) =>
        PodeVerEquipe(usuario, registroGlobal, registro.EquipeId)
        && PodeVerSituacao(usuario, registro.SituacaoAprovacao, registro.CriadoPorId);

    private static bool PodeVerEquipe(UsuarioAtual usuario, bool registroGlobal, Guid? equipeDoRegistro) =>
        registroGlobal
        || usuario.EhAdministrador
        || (equipeDoRegistro is Guid equipe && usuario.Equipes.Contains(equipe));

    /// <summary>RN37: PENDENTE e REJEITADO só para o autor e para quem não é EXTERNO.</summary>
    private static bool PodeVerSituacao(UsuarioAtual usuario, SituacaoAprovacao situacao, Guid? criadoPorId) =>
        situacao == SituacaoAprovacao.Aprovado
        || usuario.Perfil != Perfil.Externo
        || criadoPorId == usuario.Id;

    /// <summary>A coluna "perfil" da matriz, sem olhar equipe nem aprovação.</summary>
    public static bool PerfilPermite(Perfil perfil, OperacaoRegistro operacao) => operacao switch
    {
        OperacaoRegistro.Ver => true,
        OperacaoRegistro.Criar or OperacaoRegistro.Alterar => perfil is Perfil.Interno or Perfil.Gestor or Perfil.Administrador,
        OperacaoRegistro.Excluir or OperacaoRegistro.Decidir => perfil is Perfil.Gestor or Perfil.Administrador,
        _ => false,
    };

    /// <summary>Administrar usuários e equipes (e ver o histórico deles): só ADMINISTRADOR (RN26).</summary>
    public static bool PodeAdministrarAcessos(UsuarioAtual usuario) => usuario.EhAdministrador;

    /// <summary>
    /// RN50: administrar METADADOS (grupos, entidades, campos, restrições, dependências): só o ADMINISTRADOR. Os metadados
    /// são compartilhados por todas as equipes; o GESTOR configura só o que é da equipe dele (aprovação e avaliação).
    /// </summary>
    public static bool PodeAdministrarMetadados(UsuarioAtual usuario) => usuario.EhAdministrador;

    /// <summary>
    /// RN78: o painel gerencial (visão geral e indicadores) é do GESTOR (das equipes dele, mais os registros globais) e do
    /// ADMINISTRADOR (todas). INTERNO e EXTERNO: 403. O QUE cada um vê é o mesmo escopo da listagem (RN79).
    /// </summary>
    public static bool PodeVerPainel(UsuarioAtual usuario) => usuario.Perfil is Perfil.Gestor or Perfil.Administrador;

    /// <summary>
    /// RN75 e RN77: uso de uma entidade cujo grupo ou ela mesma está DESATIVADO. Para o não-administrador, não existe (404).
    /// O administrador a vê, mas só para leitura: criar, alterar e excluir registros (e avaliar) é bloqueado (409).
    /// </summary>
    public static DecisaoPermissao PodeUsarEntidade(UsuarioAtual usuario, Grupo grupo, Entidade entidade, bool escrita)
    {
        if (grupo.Ativo && entidade.Ativo)
            return DecisaoPermissao.Permitido;
        if (!usuario.EhAdministrador)
            return DecisaoPermissao.NaoEncontrado;
        return escrita ? DecisaoPermissao.Proibido : DecisaoPermissao.Permitido;
    }

    /// <summary>
    /// RN90: quem vê uma listagem (grupo). O ADMINISTRADOR vê todas (as desativadas, só para leitura, RN77). Os demais, só as ATIVAS
    /// em que pelo menos uma das suas equipes ATIVAS está habilitada. Listagem que o usuário não vê não existe para ele (404 em
    /// tudo o que está sob ela). Listagem sem nenhuma equipe habilitada: só o ADMINISTRADOR (RN93).
    /// </summary>
    public static bool PodeVerListagem(UsuarioAtual usuario, Grupo grupo) =>
        PodeVerListagem(usuario, grupo.Ativo, grupo.EquipesHabilitadas);

    /// <summary>RN90, para quando se tem só o resumo do grupo (a lista de listagens).</summary>
    public static bool PodeVerListagem(UsuarioAtual usuario, bool listagemAtiva, IReadOnlySet<Guid> equipesHabilitadas) =>
        VeListagensDesativadas(usuario) || (listagemAtiva && usuario.Equipes.Overlaps(equipesHabilitadas));

    /// <summary>RN77 e RN90: só o ADMINISTRADOR vê listagens desativadas e listagens sem equipe dele (todas).</summary>
    public static bool VeListagensDesativadas(UsuarioAtual usuario) => usuario.EhAdministrador;

    /// <summary>
    /// RN91: "as equipes do usuário nesta listagem" = as equipes ativas dele ∩ as habilitadas na listagem. Para o ADMINISTRADOR,
    /// todas as habilitadas (ele cria e configura em qualquer uma delas).
    /// </summary>
    public static IReadOnlySet<Guid> EquipesNaListagem(UsuarioAtual usuario, IReadOnlySet<Guid> equipesHabilitadas) =>
        usuario.EhAdministrador
            ? equipesHabilitadas
            : usuario.Equipes.Where(equipesHabilitadas.Contains).ToHashSet();

    /// <summary>
    /// RN91: o usuário como ele age DENTRO da listagem: as "suas equipes" passam a ser só as habilitadas nela. Todas as regras
    /// desta classe que falam em "suas equipes" (ver, criar, equipeId, configurar, avaliar) valem a partir daqui sem mudar.
    /// Quem resolve a listagem (IMetadadosProvider.ObterGrupoOuFalharAsync) devolve este usuário; use SEMPRE ele dentro da listagem.
    /// </summary>
    public static UsuarioAtual NaListagem(UsuarioAtual usuario, Grupo grupo) => new()
    {
        Id = usuario.Id,
        Matricula = usuario.Matricula,
        Nome = usuario.Nome,
        Perfil = usuario.Perfil,
        Ativo = usuario.Ativo,
        DeveTrocarSenha = usuario.DeveTrocarSenha,
        Equipes = EquipesNaListagem(usuario, grupo.EquipesHabilitadas),
        EquipesHabilitadasNaListagem = grupo.EquipesHabilitadas,
    };

    /// <summary>
    /// Configurar a aprovação (RN33) e os modelos de avaliação (RN40) de uma equipe: o GESTOR das suas equipes e
    /// o ADMINISTRADOR de qualquer uma. GESTOR de outra equipe → NaoEncontrado (404); INTERNO e
    /// EXTERNO → Proibido (403).
    /// </summary>
    /// <remarks>
    /// Dentro de uma listagem (<see cref="NaListagem"/>), só equipes HABILITADAS nela (RN91): para o GESTOR, as equipes dele já
    /// são só as habilitadas; o ADMINISTRADOR recebe NaoEncontrado numa equipe que não está habilitada.
    /// </remarks>
    public static DecisaoPermissao PodeConfigurarEquipe(UsuarioAtual usuario, Guid equipeId) => usuario.Perfil switch
    {
        Perfil.Administrador => usuario.EquipesHabilitadasNaListagem is { } habilitadas && !habilitadas.Contains(equipeId)
            ? DecisaoPermissao.NaoEncontrado
            : DecisaoPermissao.Permitido,
        Perfil.Gestor => usuario.Equipes.Contains(equipeId) ? DecisaoPermissao.Permitido : DecisaoPermissao.NaoEncontrado,
        _ => DecisaoPermissao.Proibido,
    };

    /// <summary>
    /// Fila de aprovação (RN36): equipes cujos pendentes o usuário decide. Nulo = todas (ADMINISTRADOR).
    /// Lança <see cref="InvalidOperationException"/> se o perfil não decide (use <see cref="PodeDecidirAlgo"/> antes).
    /// </summary>
    public static IReadOnlySet<Guid>? EquipesDaFilaDeAprovacao(UsuarioAtual usuario) => usuario.Perfil switch
    {
        Perfil.Administrador => null,
        Perfil.Gestor => usuario.Equipes,
        _ => throw new InvalidOperationException("Este perfil não decide aprovações."),
    };

    /// <summary>
    /// Avaliar um item (RN44): criar, editar e remover a PRÓPRIA avaliação. INTERNO, GESTOR e ADMINISTRADOR,
    /// em itens que eles VEEM (equipe e situação, RN28 e RN37); para os demais itens, NaoEncontrado (404).
    /// O EXTERNO nunca avalia (403). Se o item está APROVADO e tem modelo ativo é conferido pelo caso de uso.
    /// </summary>
    public static DecisaoPermissao PodeAvaliar(UsuarioAtual usuario, RegistroAlvo alvo)
    {
        if (Decidir(usuario, OperacaoRegistro.Ver, alvo) != DecisaoPermissao.Permitido)
            return DecisaoPermissao.NaoEncontrado;

        return usuario.Perfil is Perfil.Interno or Perfil.Gestor or Perfil.Administrador
            ? DecisaoPermissao.Permitido
            : DecisaoPermissao.Proibido;
    }

    /// <summary>
    /// RN47: avaliações INDIVIDUAIS (autor e textos) e o histórico delas só para INTERNO, GESTOR e ADMINISTRADOR.
    /// O EXTERNO vê só o resumo (médias e quantidades) dos itens que ele vê.
    /// </summary>
    public static bool PodeVerAvaliacoesIndividuais(UsuarioAtual usuario) => usuario.Perfil != Perfil.Externo;

    /// <summary>O perfil decide aprovações em alguma equipe (GESTOR ou ADMINISTRADOR)?</summary>
    public static bool PodeDecidirAlgo(UsuarioAtual usuario) => PerfilPermite(usuario.Perfil, OperacaoRegistro.Decidir);

    /// <summary>
    /// Equipes cujos registros o usuário enxerga numa listagem (além dos globais).
    /// Nulo = sem restrição (administrador: vê também os itens de equipes que deixaram de ser habilitadas, RN92).
    /// Com o usuário da listagem (<see cref="NaListagem"/>), são só as equipes dele habilitadas nela (RN91).
    /// </summary>
    public static IReadOnlySet<Guid>? EquipesVisiveis(UsuarioAtual usuario) =>
        usuario.EhAdministrador ? null : usuario.Equipes;

    /// <summary>
    /// RN37 na listagem: se não for nulo, registros não APROVADOS só aparecem se tiverem sido criados por
    /// este usuário (vale para o EXTERNO). Nulo = vê todas as situações.
    /// </summary>
    public static Guid? NaoAprovadosSomenteDoAutor(UsuarioAtual usuario) =>
        usuario.Perfil == Perfil.Externo ? usuario.Id : null;

    /// <summary>
    /// RN97: importar planilha = poder CRIAR itens no tipo de item. Cadastro de apoio (global): só o ADMINISTRADOR. Por equipe: em
    /// pelo menos uma das equipes do usuário nesta área de gestão (com o usuário de <see cref="NaListagem"/>), considerando a aprovação
    /// de cada equipe (<paramref name="equipesComAprovacao"/>): com ela ativa, o EXTERNO também importa, e os itens nascem PENDENTES
    /// (RN34). Cada linha passa depois pela mesma decisão do cadastro manual, na equipe dela.
    /// </summary>
    public static bool PodeImportar(UsuarioAtual usuario, Entidade entidade, IReadOnlySet<Guid> equipesComAprovacao) =>
        entidade.EhGlobal
            ? Decidir(usuario, OperacaoRegistro.Criar, RegistroAlvo.Novo(entidade, null, false)) == DecisaoPermissao.Permitido
            : usuario.Equipes.Any(equipe => Decidir(usuario, OperacaoRegistro.Criar,
                RegistroAlvo.Novo(entidade, equipe, equipesComAprovacao.Contains(equipe))) == DecisaoPermissao.Permitido);

    /// <summary>RN97: o ADMINISTRADOR vê as importações de planilha de todos.</summary>
    public static bool VeTodasAsImportacoes(UsuarioAtual usuario) => usuario.EhAdministrador;

    /// <summary>
    /// RN97: o GESTOR vê também as importações feitas por membros das suas equipes (nesta listagem, com o usuário de
    /// <see cref="NaListagem"/>). Nulo para os outros perfis: só as próprias.
    /// </summary>
    public static IReadOnlySet<Guid>? EquipesDasImportacoesVisiveis(UsuarioAtual usuario) =>
        usuario.Perfil == Perfil.Gestor ? usuario.Equipes : null;

    /// <summary>RN97: quem vê uma importação (e o relatório dela): o autor, o ADMINISTRADOR e o GESTOR de uma equipe do autor.</summary>
    public static bool PodeVerImportacao(UsuarioAtual usuario, Guid autorId, IReadOnlySet<Guid> equipesDoAutor) =>
        autorId == usuario.Id
        || VeTodasAsImportacoes(usuario)
        || (EquipesDasImportacoesVisiveis(usuario) is { } equipes && equipesDoAutor.Overlaps(equipes));

    /// <summary>
    /// RN29: um registro só referencia registros da MESMA equipe ou globais, mesmo que o usuário
    /// pertença às duas equipes. Um registro global só referencia globais.
    /// </summary>
    /// <param name="equipeOrigem">Equipe do registro que aponta (nula se global).</param>
    /// <param name="equipeDestino">Equipe do registro apontado (nula se global).</param>
    public static bool PodeReferenciar(Guid? equipeOrigem, Guid? equipeDestino) =>
        equipeDestino is null || equipeOrigem == equipeDestino;

    /// <summary>
    /// RN38: só se referenciam registros APROVADOS, exceto os PENDENTES ou REJEITADOS criados pelo
    /// AUTOR do registro que aponta (ex.: o externo monta o relatório e os cenários antes da aprovação).
    /// </summary>
    /// <param name="autorDaOrigem">Quem criou o registro que aponta (na criação, quem está criando).</param>
    public static bool PodeReferenciarSituacao(SituacaoAprovacao situacaoDestino, Guid? criadorDoDestino, Guid? autorDaOrigem) =>
        situacaoDestino == SituacaoAprovacao.Aprovado
        || (autorDaOrigem is Guid autor && criadorDoDestino == autor);

    /// <summary>
    /// RN27: equipe de um registro novo (ou do formulário, nas opções de campo) a partir do
    /// "equipeId" informado pelo cliente.
    ///   - Entidade global: equipeId não é aceito.
    ///   - Usuário de UMA equipe: equipeId opcional (assume a dele).
    ///   - Usuário de várias equipes ou ADMINISTRADOR: equipeId obrigatório.
    ///   - Não-administrador: equipeId precisa ser uma das suas equipes (senão, 403).
    /// Dentro de uma listagem, "suas equipes" são só as habilitadas nela (RN91), e o administrador só escolhe equipes habilitadas (400).
    /// A existência e a situação da equipe informada pelo administrador são conferidas pelo caso de uso.
    /// </summary>
    public static ResolucaoEquipe ResolverEquipe(UsuarioAtual usuario, Entidade entidade, Guid? equipeInformada)
    {
        if (entidade.EhGlobal)
        {
            return equipeInformada is null
                ? ResolucaoEquipe.Ok(null)
                : ResolucaoEquipe.Invalida($"'{entidade.Nome}' é global: os registros não têm equipe. Não informe equipeId.");
        }

        if (equipeInformada is Guid informada)
        {
            // RN91: o administrador escolhe qualquer equipe HABILITADA na listagem; uma equipe fora dela é um dado inválido.
            if (usuario.EhAdministrador)
            {
                return usuario.EquipesHabilitadasNaListagem is { } habilitadas && !habilitadas.Contains(informada)
                    ? ResolucaoEquipe.Invalida("Equipe não habilitada nesta listagem: habilite-a na administração da listagem.")
                    : ResolucaoEquipe.Ok(informada);
            }

            return usuario.Equipes.Contains(informada) ? ResolucaoEquipe.Ok(informada) : ResolucaoEquipe.Proibida();
        }

        if (!usuario.EhAdministrador && usuario.Equipes.Count == 1)
            return ResolucaoEquipe.Ok(usuario.Equipes.Single());

        if (!usuario.EhAdministrador && usuario.Equipes.Count == 0)
            return ResolucaoEquipe.Proibida();

        return ResolucaoEquipe.Invalida("Obrigatório: informe a equipe do registro em equipeId.");
    }
}

/// <summary>Resultado de <see cref="PoliticaPermissao.ResolverEquipe"/>.</summary>
public sealed record ResolucaoEquipe(Guid? EquipeId, string? ErroValidacao, bool Proibido)
{
    public static ResolucaoEquipe Ok(Guid? equipeId) => new(equipeId, null, false);
    public static ResolucaoEquipe Invalida(string erro) => new(null, erro, false);
    public static ResolucaoEquipe Proibida() => new(null, null, true);
}
