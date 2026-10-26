# AGENTS.md — Catalogo.Domain

Complementa o [`AGENTS.md` da raiz](../../AGENTS.md).

## O que fica aqui

O **motor genérico**: tudo o que decide se um registro é válido, sem saber de banco nem de HTTP.

| Pasta | Conteúdo |
|---|---|
| `Metadados/` | modelo de leitura **imutável** (`Grupo`, `Entidade`, `Campo`, `RestricaoUnica`, `CampoDependencia`), os enums `TipoDado`/`AcaoExclusao` com conversores para o texto do banco, e `ChavesReservadas` (RN14) |
| `Registros/` | classes dos dados (`Registro`, `RegistroReferencia`, `RegistroChaveUnica`, `RegistroValor`), mapeadas direto pelo EF; `SincronizadorRegistro` (grava Dados, referências, valores tipados e chaves juntos) e `ConversorValorTipado` (JSON ↔ `RegistroValor`, texto do rótulo, valor tipado da exportação) |
| `Metadados/Administracao/` | administração de metadados (RN50 a RN77): `RegrasMetadados` (regras de **configuração**, sem dados: chaves, tipos iguais aos CHECKs, referências, exibição, restrições, dependências e ciclos), `PlanejadorAlteracoes` (para cada mudança, as **verificações contra os dados** e as **escritas em massa**), as `VerificacaoDados`/`EscritaRegistros`/`ImpactoDados`, as definições de entrada e `CopiasMetadados` |
| `Validacao/` | `ValidadorRegistro` (RN01 a RN05), `ValidadorDependencias` (RN08) e `ErrosPorCampo` |
| `Validacao/Tipos/` | uma estratégia por `TipoDado` (RN04) |
| `ChavesUnicas/` | `CalculadoraChaveUnica` (RN09, RN10). A normalização está documentada no comentário da classe |
| `Acesso/` | **`PoliticaPermissao`**: a ÚNICA regra de "quem pode o quê" (RN26 a RN29): perfil × operação × equipe × situação de aprovação (`RegistroAlvo`), escopo de listagem, equipe na criação, referências entre equipes e a registros não aprovados, configuração e fila de aprovação (RN33 a RN38), quem configura modelos de avaliação, quem avalia e quem vê as avaliações individuais (RN40, RN44, RN47), quem administra metadados e o uso de entidades desativadas (RN50, RN75, RN77), quem vê o painel (RN78), quem vê cada listagem e o
usuário como ele age dentro dela (`PodeVerListagem`, `EquipesNaListagem`, `NaListagem`, RN90 a RN93). Também `Usuario`/`Equipe` (bloqueio por tentativas, RN24), `UsuarioAtual`, `PoliticaSenha` (RN25) e `RegrasAdministrador` (RN30) |
| `Aprovacao/` | enums `SituacaoAprovacao`/`DecisaoAprovacao` com conversores, `ConfiguracaoEntidadeEquipe` e `FluxoAprovacao`: situação inicial, reenvio, decisão e limites da justificativa (RN34 a RN36). **Quem** pode cada transição fica na `PoliticaPermissao` |
| `Avaliacoes/` | entidades `ModeloAvaliacao`, `CriterioAvaliacao`, `Avaliacao` e `AvaliacaoResposta` (mapeadas direto pelo EF), `RegrasModeloAvaliacao` (validar o modelo e evoluí-lo sem perder avaliações, RN42 e RN43) e `RegrasAvaliacao` (validar e aplicar respostas, só itens APROVADOS, média com uma casa, RN44 a RN47). **Quem** avalia ou configura fica na `PoliticaPermissao` |
| `Historico/` | `HistoricoAlteracao` (linha do histórico) e `ComparadorAlteracoes` (mudanças por campo entre antes e depois, RN32) |
| `Exclusao/` | `PlanejadorExclusao`: o **plano de exclusão** (RN16), percurso do grafo, CASCATA/RESTRINGIR, ciclos e ordem filhos → pai |
| `Consultas/` | `InterpretadorConsulta`: transforma os parâmetros da URL em `ConsultaRegistros` tipada (RN17 a RN19); `CaminhoCampo`: o **único** intérprete de caminhos por referência (`relatorio_teste.ambiente`, até 2 saltos, RN89), usado pelos filtros e pelo painel |
| `Painel/` | painel gerencial: `InterpretadorIndicadores` (agruparPor, metricas, ordenarPor e limite → `ConsultaIndicadores` tipada, conferida contra os metadados, todos os erros juntos, RN85 a RN87) e `PeriodoPainel` ("de"/"ate" no fuso de negócio → limites UTC, RN80). **Quem** vê o painel fica na `PoliticaPermissao` (`PodeVerPainel`, RN78) |
| `Importacoes/` | importação de planilhas (RN97 a RN104), código puro: `Importacao`/`ImportacaoArquivo` (mapeadas pelo EF), opções, mapeamento e resultado de cada linha (`ResumoImportacao`), `LeitorCsv` e `PlanilhaImportada` (limites), `ConversorCelulas` (o que um usuário brasileiro digita → o JSON do cadastro, um por tipo, RN100), `SugestaoMapeamento`, `ResolvedorReferencias` (nome → item, RN99), `DuplicadosNoArquivo` (RN102) e `RegrasDesfazer` (RN103) |
| `Excecoes/` | exceções de domínio, cada uma com o seu **código**, e `CodigosErro` (todos os códigos estáveis de erro da API, ADR 0023). A tabela exceção → HTTP está em [`src/Catalogo.Api/AGENTS.md`](../Catalogo.Api/AGENTS.md); a do front, em [`docs/api/codigos-de-erro.md`](../../../docs/api/codigos-de-erro.md) |

Regras: [`docs/regras-de-negocio.md`](../../../docs/regras-de-negocio.md). Modelo: [`docs/modelo-de-dados.md`](../../../docs/modelo-de-dados.md).

## Regras deste projeto

- **Zero dependências**: o `.csproj` não pode ter `PackageReference` nem `ProjectReference`. Use só a
  BCL do .NET (`System.*`). Os testes `Domain_CsprojNaoTemPacotesNemProjetos`,
  `Domain_SoReferenciaAssembliesDoDotNet` e `Domain_NaoDependeDeOutrasCamadasNemDeFrameworks` falham se isso mudar.
- **Metadados são imutáveis** (`init`): eles ficam em cache e são compartilhados entre requisições.
  Nunca adicione `set` público nas classes de `Metadados/`.
- **Nenhum `switch` por tipo de dado fora dos validadores**, exceto em `TipoDadoConversor` e em
  `CalculadoraChaveUnica.Normalizar`. O resto do código chama a estratégia certa (padrão Strategy):
  o `ValidadorRegistro` guarda um dicionário `TipoDado → ITipoDadoValidador`.
- Validadores **não** recebem valor vazio: nulo e texto em branco já foram tratados pela obrigatoriedade (RN03).
- Regra de negócio quebrada → exceção de `Excecoes/` (nunca `Exception` genérica), com o código de `CodigosErro`. Erro de
  programação ou de configuração → `InvalidOperationException` (vira 500 `ERRO_INTERNO`).
- **Regras com grafo, filtros ou ordem ficam aqui, como código puro**. O `PlanejadorExclusao` não
  acessa banco: quem usa entrega as referências num laço (ver o comentário da classe). O
  `InterpretadorConsulta` só devolve objetos de metadados e valores tipados, nunca texto cru da URL.
  Ambos são testados sem banco em `tests/Catalogo.Domain.Tests/Exclusao` e `.../Consultas`.
- **Permissão só na `PoliticaPermissao`**. Precisa de uma regra nova de acesso? Adicione um método
  nela e um caso na matriz de `tests/Catalogo.Domain.Tests/Acesso/PoliticaPermissaoTests.cs`
  (regras de aprovação: `tests/Catalogo.Domain.Tests/Aprovacao/PoliticaAprovacaoTests.cs`; de avaliação:
  `tests/Catalogo.Domain.Tests/Avaliacoes/PoliticaAvaliacaoTests.cs`; os dois testam todas as combinações).
- Em classes do Domain mapeadas pelo EF, uma propriedade calculada que devolve uma coleção de outra entidade (ex.:
  `ModeloAvaliacao.CriteriosAtivos`) precisa de `Ignore` na configuração do EF, senão vira uma navegação.
  Não espalhe `if (perfil == ...)` nem `EhAdministrador` pelo resto do código.
- **Nunca altere `Registro.Dados` "na mão"**: use `SincronizadorRegistro.Aplicar`, que mantém
  `RegistroValor`, `RegistroReferencia` e `RegistroChaveUnica` coerentes ([ADR 0013](../../../docs/adr/0013-registro-valor.md)).

## Administração de metadados

- **Toda regra de alteração de metadados tem uma verificação contra dados correspondente e um teste.** Se uma mudança pode deixar
  algum registro inválido, o `PlanejadorAlteracoes` precisa devolver uma `VerificacaoDados` para ela (que a Infrastructure responde
  em LINQ, em `VerificadorDadosMetadados`), com um teste em `tests/Catalogo.Domain.Tests/Metadados/Administracao/PlanejadorAlteracoesTests.cs`
  e um de integração com dados em `tests/Catalogo.Api.IntegrationTests/AlteracoesComDadosApiTests.cs` (um caso que bloqueia e um que permite).
- Regra nova de configuração (sem dados): em `RegrasMetadados`, com um caso em `RegrasMetadadosTests`. Os erros vão por campo do corpo, todos juntos.
- Não existe migração automática ([ADR 0026](../../../docs/adr/0026-bloquear-alteracoes-incompativeis-com-os-dados.md)): a única escrita em
  massa de valores é o `valorPadrao` (`PreencherValorPadrao`). Mudanças que afetam o hash das chaves únicas (restrição nova, ordem dos campos,
  escala de DECIMAL) geram `RecalcularChavesUnicas`.
- Metadados são imutáveis: para descrever "como ficaria", use `CopiasMetadados` (nunca altere o objeto do cache).
- `TipoDado` novo: os atributos dele também entram em `RegrasMetadados.ValidarCampo` (o espelho dos CHECKs) e, se ele puder ter dados
  que deixem de caber, numa verificação do planejador.

## Painel (RN78 a RN88)

- Dimensão ou métrica nova: a regra (que tipos aceita, o nome, a chave) entra no `InterpretadorIndicadores`, com casos válidos e
  inválidos em `tests/Catalogo.Domain.Tests/Painel/InterpretadorIndicadoresTests.cs`; a tradução, em `ConsultaIndicadoresLinq`
  (Infrastructure, um método por tipo).
- Os parâmetros do painel são chaves reservadas (`ChavesReservadas.DaConsulta`, RN14): na listagem dão 400.
- Campo por caminho (`relatorio_teste.ambiente`, RN89): sempre por `CaminhoCampo.Resolver`, nunca com `Split('.')` próprio. Ele confere os
  saltos; quem chama confere o tipo do último campo ([ADR 0038](../../../docs/adr/0038-caminhos-por-referencia.md)).

## Como adicionar um novo `TipoDado`

Tipos hoje: `TEXTO`, `TEXTO_LONGO`, `INTEIRO`, `DECIMAL`, `BOOLEANO`, `DATA`, `DATA_HORA`, `LISTA`, `REFERENCIA` e `URL` (o exemplo mais
recente do passo a passo: `UrlValidador`, `V007`, [ADR 0042](../../../docs/adr/0042-tipo-de-dado-url.md)).

Exemplo: `EMAIL`.

1. **Script SQL**: crie `database/scripts/V00x__tipo_dado_email.sql` recriando o CHECK
   `CK_Campo_TipoDado` com o novo valor. Se o tipo tiver atributos próprios, inclua um CHECK como
   `CK_Campo_Texto`. Veja [`database/AGENTS.md`](../../../database/AGENTS.md).
2. **Enum**: adicione `Email` em `Metadados/TipoDado.cs` e os dois casos em `TipoDadoConversor`
   (`"EMAIL"` ↔ `TipoDado.Email`).
3. **Validador**: crie `Validacao/Tipos/EmailValidador.cs`, implementando `ITipoDadoValidador`, que
   valida **e normaliza** (ex.: trim e minúsculas). Registre em `ValidadoresPadrao.Todos()`.
4. **Chave única**: adicione o caso em `CalculadoraChaveUnica.Normalizar` e documente a normalização
   no comentário da classe.
5. **Administração**: inclua os atributos do tipo em `RegrasMetadados.ValidarCampo` (igual ao CHECK do passo 1) e, se diminuir um limite
   puder invalidar dados, a verificação no `PlanejadorAlteracoes` (ver a seção acima).
6. **Valor tipado**: escolha a coluna de `RegistroValor` em `ConversorValorTipado` (`ParaValor` e
   `ParaTexto`), atualize o `INSERT ... OPENJSON` de carga num script novo (para registros existentes, se houver)
   e decida em `InterpretadorConsulta` quais operadores de filtro e se a ordenação valem para o tipo.
7. **Documentação**: adicione a linha na tabela de RN04 e na lista de RN09 em
   [`docs/regras-de-negocio.md`](../../../docs/regras-de-negocio.md), nos operadores de RN18, e nas tabelas de tipos de
   [`docs/modelo-de-dados.md`](../../../docs/modelo-de-dados.md).
8. **Testes** (em `tests/Catalogo.Domain.Tests`):
   - `Validacao/Tipos/EmailValidadorTests.cs`: valores válidos, inválidos e limites;
   - um caso em `CalculadoraChaveUnicaTests.Normalizar_CadaTipo_GeraTextoInvariante`;
   - um caso em `TipoDadoTests.Conversor_TextoDoBanco_ConverteNosDoisSentidos`.
   O teste `ValidadoresPadrao_TodoTipoDado_TemExatamenteUmValidador` falha até o passo 3 estar feito.
9. Rode `dotnet test`.
