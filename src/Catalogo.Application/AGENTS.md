# AGENTS.md — Catalogo.Application

Complementa o [`AGENTS.md` da raiz](../../AGENTS.md). O fluxo completo de uma criação está em
[`docs/arquitetura.md`](../../../docs/arquitetura.md#fluxo-de-uma-criação-de-registro).

## O que fica aqui

| Pasta | Conteúdo |
|---|---|
| `CasosDeUso/` | um caso de uso por classe. Registros na raiz (`CriarRegistro`, `ListarRegistros`...); `Acesso/` (login, troca de senha, "eu", primeiro administrador); `Administracao/` (equipes e usuários); `Historico/`; `Aprovacao/` (configuração, decisão e fila de pendentes); `Avaliacoes/` (modelos de avaliação, avaliar, resumo, histórico); `Metadados/` (administração de grupos, entidades, campos, restrições e dependências, RN50 a RN77); `Painel/` (visão geral, indicadores e a exportação deles, com `OpcoesPainel`, RN78 a RN88); `Importacoes/` (modelo, envio, prévia, execução, relatório, desfazer, histórico e limpeza das importações de planilha, RN97 a RN104) |
| `Comum/` | peças usadas por mais de um caso de uso: `GravadorRegistro` (o **único** caminho de criar e alterar um registro: equipe, permissão, validação, dados, aprovação e histórico; usado pelo cadastro manual e pela importação), `PreparadorRegistro` (validação completa de criar **e** atualizar; aceita um `LotePreparacao` com os dados lidos em lote), `MontadorRegistroDto`, `ResolvedorEquipe` (equipe do registro, RN27), `Permissoes` (decisão → 404/403), `ServicoHistorico`, `MontadorHistorico`, `ContextoAprovacao` (monta o `RegistroAlvo` com a situação de aprovação), `ConfiguracaoDaEquipe` (quem configura uma equipe, usado pela aprovação e pela avaliação), `MontadorModeloAvaliacao`, `PreparadorConsultaRegistros` (parâmetros da URL + escopo do usuário, da listagem **e** da exportação), `ExecutorAlteracaoMetadados` (o caminho de toda escrita da administração de metadados), `ProtecaoFormula` (injeção de fórmula nas exportações), `SimuladorImportacao` (cada linha da planilha pelo `GravadorRegistro`, RN97 a RN102), `AcessoImportacao` (entrar no tipo de item para importar, achar a importação, montar a prévia) e `ProvedorIdentidadeLocal` |
| `Abstracoes/` | **interfaces** implementadas pela Infrastructure ou pela Api: `IMetadadosProvider`, `IRegistroRepository`, `IUnidadeDeTrabalho`, `IUsuarioRepository`, `IEquipeRepository`, `IHistoricoRepository`, `IConfiguracaoRepository`, `IModeloAvaliacaoRepository`, `IAvaliacaoRepository`, `IUsuarioAtual`, `ISituacaoUsuarios`, `IProvedorIdentidade`, `IHasherSenha`, `IGeradorToken`, `IGeradorArquivoExportacao` (CSV e XLSX), `IMetadadosAdminRepository`, `IVerificadorDadosMetadados` e `IEscritorRegistrosMetadados` (administração de metadados), `IPainelRepository` (as agregações do painel, todas no banco), `IImportacaoRepository`, `IConsultasImportacao` (as consultas em lote da importação) e `IArquivosImportacao` (ler e gerar as planilhas) |
| `Dtos/` | o que entra e sai da API (`record`s) |
| `Mapeamentos/` | conversão manual Domain → DTO, com métodos de extensão `ParaDto(...)` |

Este projeto depende **só** do Domain, sem nenhum pacote. Garantido por `Application_SoDependeDoDomain`.
Relógio: use `TimeProvider` (da BCL, injetado), nunca `DateTime.Now` direto.

## Como criar um caso de uso

1. Crie `CasosDeUso/NomeDoCasoDeUso.cs`: uma classe `sealed` com construtor primário recebendo as
   interfaces de que precisa e **um** método público `ExecutarAsync(..., CancellationToken)`.
   Use `ObterRegistro.cs` como modelo.
2. Precisa de algo do banco que ainda não existe? Adicione **só o método necessário** na interface
   de `Abstracoes/` (não crie repositório genérico) e implemente na Infrastructure.
3. Registre em `src/Catalogo.Api/Configuracao/ConfiguracaoAplicacao.cs` (`AddScoped<NomeDoCasoDeUso>()`).
4. Injete no controller pelo construtor. **Não use mediator** ([adr/0004](../../../docs/adr/0004-sem-mediatr-automapper-fluentassertions.md)).
5. Escreva os testes em `tests/Catalogo.Application.Tests/CasosDeUso/`, usando os dublês de `Dubles/`.

## Todo caso de uso que lê ou altera dados consulta a permissão e grava histórico

1. Pegue quem está fazendo: `var usuario = await usuarioAtual.ObterAsync(ct);`. **Dentro de uma listagem**, entre nela pelo
   `metadados.ObterGrupoOuFalharAsync`/`ObterEntidadeOuFalharAsync` (`MetadadosProviderExtensoes`): eles dão 404 se o usuário não vê a
   listagem (RN90) e devolvem o **usuário na listagem** (`PoliticaPermissao.NaListagem`: "suas equipes" = só as habilitadas, RN91).
   Use **esse** usuário daí em diante; nunca refaça a interseção no caso de uso.
2. Pergunte à política e traduza. Em registros, monte o alvo pelo `ContextoAprovacao`, que já considera a
   **situação de aprovação**, o autor e se a aprovação está ativa (RN33 a RN37):
   `var alvo = await aprovacao.ExistenteAsync(entidade, registro, ct);`
   `Permissoes.Exigir(PoliticaPermissao.Decidir(usuario, OperacaoRegistro.Alterar, alvo), descricao);`
   (404 se não vê, RN28 e RN37; 403 se o perfil não permite). Na criação, `aprovacao.NovoAsync(...)`.
   Administração: `Permissoes.ExigirAdministrador(usuario)`.
3. Listagens: passe o escopo para a consulta (`EquipesPermitidas = PoliticaPermissao.EquipesVisiveis(usuario)` e
   `NaoAprovadosSomenteDoAutor = PoliticaPermissao.NaoAprovadosSomenteDoAutor(usuario)`). Nunca filtre em memória.
4. Alterou dados? Chame o `ServicoHistorico` **antes** do `SalvarAlteracoesAsync`, com o objeto plano
   de antes e de depois (`ParaJsonPlano`, `ParaJsonHistorico`). Nunca coloque senha ou hash nesses objetos (RN32).
5. Mudou usuário ou equipe? Chame `ISituacaoUsuarios.Invalidar`/`InvalidarTodos` depois de salvar, para valer na hora.

Contagem de relacionados (RN94): `ListarRelacionados` descobre as relações pelos metadados (`Relacoes.QueApontamPara`) e manda o
escopo de cada entidade de origem (`PreparadorConsultaRegistros.Escopo`) para o repositório contar no banco. Escopo novo de
visibilidade? Ele entra no `Escopo`/`ConsultaRegistrosLinq.Filtrar`, e a listagem, a exportação, o painel e os relacionados seguem juntos.

Modelos: `AtualizarRegistro.cs` (registro, inclusive o reenvio), `DecidirAprovacao.cs` (decisão) e `AtualizarUsuario.cs` (administração).

Avaliação: `AvaliarRegistro.cs` (quem → modelo ativo → situação → respostas). Médias e contagens são do
repositório (no banco); o caso de uso só monta o DTO. Configurações de equipe começam por `ConfiguracaoDaEquipe.ValidarAsync`.

Casos de uso de escrita **consideram a situação de aprovação via política central**: nunca teste
`SituacaoAprovacao` ou perfil diretamente para decidir permissão. Transições de situação ficam em `FluxoAprovacao` (Domain).

## Entidade e grupo desativados (RN75, RN77)

Resolva a entidade sempre com `metadados.ObterEntidadeOuFalharAsync(usuario, grupo, entidade, UsoEntidade.Leitura|Escrita, ct)`:
o não-administrador recebe 404 numa entidade (ou grupo) desativada, e o administrador só lê (Escrita → 409 `ENTIDADE_INATIVA`).
Caso de uso que **grava** registro (criar, alterar, excluir, decidir, avaliar) usa `UsoEntidade.Escrita`. A regra está em
`PoliticaPermissao.PodeUsarEntidade`.

## Administração de metadados (RN50 a RN77)

Todo caso de uso de `CasosDeUso/Metadados/` faz só: `executor.ExigirAdministradorAsync` → lê o grupo **sem cache**
(`executor.ObterGrupoAsync`) → `ExecutorAlteracaoMetadados.ConferirVersao` → valida a configuração com `RegrasMetadados` (400) →
monta o plano com `PlanejadorAlteracoes` → `executor.ExecutarAsync(new AlteracaoMetadados(...), simular, ct)`. O executor faz o resto
(transação Serializable, verificações, simulação, 409 com o relatório, gravação, escritas em lotes, histórico `METADADO`, cache).
Toda gravação "toca" a entidade (`TocarEntidadeAsync`) ou o grupo, o que confere e muda a versão. Modelo: `AtualizarCampo.cs`.

## Painel (RN78 a RN88)

- Todo caso de uso de `CasosDeUso/Painel/` começa por `PermissoesPainel.Exigir(usuario)` (GESTOR e ADMINISTRADOR, RN78).
- **O painel não tem regra de visibilidade própria** (RN79): o escopo vem do `PreparadorConsultaRegistros` (`PrepararAsync` com os
  filtros, nos indicadores; `Escopo(usuario, entidade, equipeId)`, na visão geral). Número novo = começar desse escopo.
- **A conta é do banco** ([ADR 0031](../../../docs/adr/0031-agregacao-no-banco-em-linq.md)): o `IPainelRepository` devolve números já
  agregados. Aqui só se somam as entidades e se formatam as linhas (`CalcularIndicadores.Formatar`). Nunca traga registros para somar.
- Os erros de todos os parâmetros saem juntos: cada parte (filtros, indicadores, período, formato) valida o seu pedaço e
  `CalcularIndicadores.Juntar` reúne tudo num `ValidacaoRegistroException` só.

## Não duplique regra entre casos de uso

- **Criar ou alterar um registro** → `GravadorRegistro.CriarAsync` / `AlterarAsync`. Não chame o `SincronizadorRegistro` nem o
  `PreparadorRegistro` direto num caso de uso novo: o gravador já faz equipe, permissão, validação, aprovação e histórico, igual para
  o cadastro manual e para a importação (o `OrigemGravacao` diz o `CorrelacaoId` e a origem do histórico).
- Validar e preparar um registro (RN01 a RN09) → `PreparadorRegistro.PrepararAsync`.
- Gravar os dados num `Registro` → `SincronizadorRegistro.Aplicar` (Domain), que mantém `RegistroValor` junto.
- Devolver registros → `MontadorRegistroDto.MontarAsync` (busca rótulos, autores e médias das avaliações em lote, RN20 e RN48). Nunca busque por linha.
- Validar respostas de avaliação → `RegrasAvaliacao.ValidarRespostas` (Domain); validar modelo → `RegrasModeloAvaliacao`.
- Interpretar parâmetros de URL → `InterpretadorConsulta` (Domain).
- Montar a consulta de registros com o escopo do usuário (equipe, RN26; aprovação, RN37) → `PreparadorConsultaRegistros`.
  A listagem, a exportação (RN49) e o painel (RN79) usam o mesmo: um lugar só decide o que o usuário vê.
- Exportar (`ExportarRegistros`): os valores tipados vêm de `ConversorValorTipado.ParaPlanilha` (Domain), os textos passam
  por `ProtecaoFormula`, e o arquivo é escrito pelo `IGeradorArquivoExportacao` do formato (Infrastructure). Leia em lotes
  (`ExportarRegistros.TamanhoLote`), nunca o resultado inteiro de uma vez.

## Importação de planilhas (RN97 a RN104)

- Todo caso de uso de `CasosDeUso/Importacoes/` começa pelo `AcessoImportacao` (`EntrarParaImportarAsync`: 403 se o usuário não pode
  criar no tipo de item, `PoliticaPermissao.PodeImportar`; `ObterAsync`: 404 se ele não vê a importação).
- A prévia e a execução chamam o `SimuladorImportacao`, que monta o corpo de cada linha (células convertidas pelo `ConversorCelulas`,
  referências pelo nome no `ResolvedorReferencias`) e grava pelo `GravadorRegistro`. **Leituras em lote** (uma por tipo de item
  referenciado, as chaves únicas de uma vez) pelo `IConsultasImportacao` e pelo `LotePreparacao`; **nunca uma consulta por linha**.
- A prévia descarta tudo (`IUnidadeDeTrabalho.DescartarAlteracoes`, que limpa o rastreamento: leia de novo a importação antes de
  salvar o resumo). A execução simula de novo, compara com a prévia (`ResumoImportacao.MesmoResultadoQue`) e grava num SaveChanges só.
- O desfazer usa o mesmo `PlanejadorExclusao` da exclusão (com vários alvos) e o `IRegistroRepository.ExcluirAsync`.

## Mapeamento

- Sempre manual, num método de extensão em `Mapeamentos/` (ex.: `registro.ParaDto(entidade)`).
- Nunca devolva classes do Domain na API: sempre um DTO.
- Registros são **planos** para o cliente: `RegistroDto.Dados` junta campos simples e referências (RN07),
  e cada referência sai como `{ id, rotulo }` (RN20).

## Qual exceção lançar

Toda exceção de domínio tem um **código** de `CodigosErro` (Domain), que sai na extensão `codigo` do erro para o front
decidir o que fazer ([`docs/api/codigos-de-erro.md`](../../../docs/api/codigos-de-erro.md)). Nunca escreva o código como string solta.

| Situação | Exceção (de `Catalogo.Domain.Excecoes`) |
|---|---|
| Grupo, entidade ou registro não existe | `RecursoNaoEncontradoException` com o código do recurso (`CodigosErro.RegistroNaoEncontrado`...). Use `metadados.ObterEntidadeOuFalharAsync(...)` |
| Dados inválidos (um ou vários campos) | `ValidacaoRegistroException`. Junte com `ErrosPorCampo` e chame `LancarSeHouverErros()` (RN05). Código `VALIDACAO`, ou um específico no segundo parâmetro (ex.: `EXPORTACAO_MUITO_GRANDE`) |
| Valor já usado numa restrição única | `ConflitoUnicidadeException` (RN09) |
| `IUnidadeDeTrabalho` lançou `ViolacaoUnicidadeBancoException` | converta em `ConflitoUnicidadeException` (ver passo 7 de `CriarRegistro`) |
| Registro alterado por outra pessoa (`versao` diferente) | `ConflitoConcorrenciaException` (RN12). Também é lançada pela `IUnidadeDeTrabalho` |
| Exclusão impedida por RESTRINGIR | `ExclusaoBloqueadaException` com as `DependenciaBloqueante` (RN16) |
| Parâmetro de URL inválido (filtro, ordenação, página) | `ValidacaoRegistroException`, com a chave = nome do parâmetro (RN18) |
| Registro que o usuário não pode ver | `RecursoNaoEncontradoException` (RN28), via `Permissoes.Exigir` |
| Perfil não permite a operação | `AcessoNegadoException` (403), via `Permissoes.Exigir` |
| Credenciais inválidas, usuário desativado | `NaoAutenticadoException` (401). No login, mensagem sempre genérica (RN23) |
| Regra que impede a operação (último administrador, equipe com usuários, registro não pendente) | `RegraDeNegocioException` com o código (409, obrigatório). Dados extras (ex.: `quantidade`) vão no terceiro parâmetro e saem em `detalhes` no ProblemDetails |

## Regras deste projeto

- Grave tudo de um caso de uso com **uma** chamada a `IUnidadeDeTrabalho.SalvarAlteracoesAsync`.
  A exceção é a exclusão: `IRegistroRepository.ExcluirAsync` executa o plano na hora, na própria transação.
- Metadados vêm do `IMetadadosProvider` (com cache) e **não podem ser alterados**.
- `try/catch` aqui só para **traduzir** uma exceção (como no passo 7). Nunca para esconder erro.
