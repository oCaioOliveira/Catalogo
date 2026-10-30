# AGENTS.md — tests

Complementa o [`AGENTS.md` da raiz](../AGENTS.md). Rode `dotnet test` na raiz antes de concluir qualquer mudança.

## O que testar em cada projeto

| Projeto | Testa | Banco? | Exemplo |
|---|---|---|---|
| `Catalogo.Domain.Tests` | cada validador de tipo (válido, inválido, **limites**), `ValidadorRegistro`, `ValidadorDependencias`, `CalculadoraChaveUnica`, `SincronizadorRegistro`, **plano de exclusão**, **interpretação de filtros** e dos **parâmetros do painel** (`Painel/`) | não | `Exclusao/PlanejadorExclusaoTests.cs` |
| `Catalogo.Application.Tests` | fluxo dos casos de uso com **dublês** escritos à mão (`Dubles/`), sem biblioteca de mock | não | `CasosDeUso/CriarRegistroTests.cs` |
| `Catalogo.ArchitectureTests` | regra de dependência (NetArchTest), pacotes proibidos, Central Package Management, ausência de migrations e de SQL escrito à mão | não | `DependenciasEntreCamadasTests.cs` |
| `Catalogo.Api.IntegrationTests` | HTTP de ponta a ponta com `WebApplicationFactory`, mapeamento do EF e erros reais do SQL Server | **sim** | `RegistrosApiTests.cs` |

Use os atalhos de `Apoio/TesteDeApi` (`Criar`, `Ler`, `Atualizar`, `Excluir`, `ListarOk`, `ComVersao`...)
em vez de montar requisições à mão. Para outros perfis: `var (usuario, cliente) = await CriarUsuarioAsync("m", Perfil.Interno, [EquipePadrao]);`
(token gerado direto, sem login). `EquipeDosAtalhos` define em que equipe os atalhos criam registros por equipe.
Testes de permissão usam `Postar(cliente, entidade, corpo)`, que envia o corpo exatamente como está.
Nos testes de Application, troque `_ambiente.Usuario` para testar outros perfis.

Regra prática: a regra é do motor? Teste no Domain. A regra depende de outro registro (referência,
dependência, unicidade)? Teste na Application **e** cubra o caminho feliz e um erro na integração.

## Contrato da API (OpenAPI e erros)

- `ContratoOpenApiTests` gera o OpenAPI e **falha se ele for diferente de `backend/openapi/v1.json`**. Mudou o contrato de
  propósito? Regenere e revise o diff: `ATUALIZAR_OPENAPI=1 dotnet test --filter "FullyQualifiedName~ContratoOpenApiTests"`.
  O mesmo teste confere que todo endpoint declara sucesso, 401, 500, 400 e 404 quando cabem ([ADR 0024](../../docs/adr/0024-contrato-openapi-versionado-e-verificado.md)).
- `ErrosApiTests` percorre os principais erros (401, 403, 404, 400, 409, 405, 415, troca de senha) e confere `codigo` e
  `traceId` em todos. Endpoint novo com um erro novo? Inclua o cenário lá.
- Teste que precisa de outra configuração (CORS, limite da exportação) ou de um serviço trocado (ex.: um que falha, para o 500):
  `FabricaCom(configuracoes, servicos)` cria outra instância da API sobre o mesmo banco; use `ClienteDe(usuario, fabrica)`.
  A instância é descartada no fim do teste.
- Os testes de integração rodam com `ENVIRONMENT` = `TQS` (a `BancoDeTestesFixture` define as seis chaves do deploy, por cima do
  `appsettings.Development.json` do desenvolvedor): o 500 sai sem detalhes internos e sem Swagger, como em produção. Para testar outro
  ambiente ou outro CORS, `FabricaCom(new Dictionary<string, string> { ["ENVIRONMENT"] = "LOCAL" })`.
- A chave do JWT é a do banco de testes (`cat.CATTB025_CHAVE_JWT`): todas as fábricas leem a mesma. Teste que apaga a linha a devolve
  no fim (`ChaveJwtApiTests`).

## Banco de testes e Respawn

- Os testes de integração usam o banco **`ListagemGenerica_Testes`**, no mesmo container do banco
  de desenvolvimento. Ele é criado e atualizado pelo `db-init` (`database/docker`) com os mesmos scripts e o mesmo seed.
- A connection string vem de `ConnectionStrings:CatalogoTestes`, no user-secrets deste projeto ou na
  variável de ambiente `ConnectionStrings__CatalogoTestes` (ver [README](../README.md#testes)).
- Por segurança, a `BancoDeTestesFixture` **recusa** qualquer banco cujo nome não termine em `_Testes`.
- As tabelas ficam no schema `catalogo` (V012): SQL escrito nos testes usa `{SchemaBanco.Nome}.Tabela`, nunca `dbo.`.
  `OutroSistemaNoMesmoBancoTests` cria `dbo.Usuario` e `dbo.Registro` "de outro sistema" e confere que o Catálogo funciona sem
  tocá-las e que o login da aplicação nem as enxerga. Ele usa um segundo user-secret, `ConnectionStrings:CatalogoTestesAdmin`
  (o `sa` local, também só em banco `_Testes`), porque o login da aplicação não tem DDL.
- Antes de **cada** teste, o Respawn apaga os dados: `Registro`, `RegistroReferencia`, `RegistroValor`,
  `RegistroChaveUnica`, `HistoricoAlteracao`, `ImportacaoArquivo`, `Importacao`, `ConfiguracaoEntidadeEquipe`, as quatro tabelas de avaliação (`ModeloAvaliacao`,
  `CriterioAvaliacao`, `Avaliacao`, `AvaliacaoResposta`), `UsuarioEquipe`, `Usuario` e `Equipe`.
  Por isso todo teste começa com a aprovação **desligada** e **sem modelo de avaliação** em todas as entidades. Depois, a `TesteDeApi`
  cria a `EquipePadrao` e um ADMINISTRADOR, que é o `Cliente` padrão.
  Tabela de dados nova? Inclua em `BancoDeTestesFixture.TabelasDeDados`.
  Os metadados do seed ficam. Por isso os testes podem contar com o grupo `testes_desempenho`. Os **grupos criados pelos testes**
  (todos os que não são o do seed) são apagados com as entidades, campos, restrições e dependências deles, e o cache de metadados é limpo.
- Todos os testes de integração estão na coleção `"Api"` (herdam de `Apoio/TesteDeApi`) e rodam um de cada vez.
- **Nunca altere os metadados do seed** (`testes_desempenho`) num teste. Teste de administração de metadados (RN50 a RN77) herda de
  `Apoio/TesteDeMetadados` e cria o **próprio** grupo (chave `inv_<aleatório>`), pela API; `CriarInventarioAsync` monta a listagem
  "Inventário" inteira. Os atalhos guardam o estado devolvido por cada escrita (`Estado`, `VersaoEntidade`...), para mandar sempre a
  versão atual, e `Bloqueio(resposta, codigo)` confere um 409 e devolve o relatório de impacto.
- Regra de alteração de metadados nova: um caso que bloqueia e um que permite em `AlteracoesComDadosApiTests` (e o teste do planejador no Domain).

## Painel e testes lentos

- O painel (RN78 a RN88) é testado com **dados controlados** em `PainelApiTests` (duas equipes; aprovação e avaliação ativas na A): os
  números esperados estão calculados à mão no comentário de `PrepararAsync`. Datas de criação conhecidas são gravadas direto no banco
  (`DefinirCriadoEmAsync`), porque a API usa o relógio do servidor. Casos que o seed não tem (DECIMAL opcional, TEXTO com mais de 50
  valores) ficam em `IndicadoresInventarioApiTests`, sobre a listagem Inventário.
- **Testes lentos** usam `[FatoLento]` (em vez de `[Fact]`) e ficam **fora** do `dotnet test` padrão. Para rodar:

  ```bash
  RODAR_TESTES_LENTOS=1 dotnet test --filter "FullyQualifiedName~DesempenhoPainelTests" --logger "console;verbosity=detailed"
  ```

  `DesempenhoPainelTests` gera 20 mil relatórios e 20 mil cenários direto nas tabelas (uns 40 s) e imprime o tempo de três agregações
  do painel. Se passar de 2 s, proponha índices em `database/propostas/` (sem aplicar) — ver [ADR 0031](../../docs/adr/0031-agregacao-no-banco-em-linq.md).

## Importação de planilhas (RN97 a RN104)

- `ImportacaoApiTests` monta as planilhas no próprio teste (`Apoio/PlanilhasDeTeste`: `Xlsx(...)`, `Csv(...)`, `EnviarEValidarAsync`,
  `Linha`/`Situacao`/`Mensagens` da prévia). O administrador sempre informa a equipe (RN27): o atalho `Previa` passa a `EquipePadrao`.
- A limpeza automática fica desligada nos testes (`Importacao:IntervaloLimpezaMinutos=0`); o teste da expiração chama `LimparImportacoes`.
- Desempenho com 5.000 linhas: `RODAR_TESTES_LENTOS=1 dotnet test --filter "FullyQualifiedName~DesempenhoImportacaoTests" --logger "console;verbosity=detailed"`.

## Nomenclatura

- Classe: `<ClasseTestada>Tests` (ex.: `CriarRegistroTests`), na mesma pasta relativa da classe testada.
- Método: `Metodo_Cenario_ResultadoEsperado`, em português. Ex.:
  `Validar_TextoUmCaractereAcimaDoLimite_DevolveErroComTamanho`,
  `Post_ComunidadeComMesmoNomeComAcentoECaixaDiferentes_Devolve409`.
- Teste que cobre uma regra de negócio cita o id num comentário (`// RN09`) ou no nome.
  Os ids estão em [`docs/regras-de-negocio.md`](../../docs/regras-de-negocio.md).
- Asserts do próprio xUnit (`Assert.Equal`, `Assert.Single`...). FluentAssertions ≥ 8 é proibido.
- Um comportamento por teste. Use `[Theory]` + `[InlineData]` para variações do mesmo comportamento (ex.: limites).
