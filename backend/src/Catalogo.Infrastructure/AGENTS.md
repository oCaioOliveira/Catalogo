# AGENTS.md — Catalogo.Infrastructure

Complementa o [`AGENTS.md` da raiz](../../AGENTS.md).

## O que fica aqui

| Caminho | Conteúdo |
|---|---|
| `Persistencia/CatalogoDbContext.cs` | o `DbContext` |
| `Persistencia/Configuracoes/` | **uma classe `IEntityTypeConfiguration` por tabela**, espelhando o script SQL |
| `Persistencia/Tabelas/` | classes que espelham as tabelas de metadados (sufixo `Tabela`) |
| `Persistencia/UnidadeDeTrabalho.cs` | `SaveChanges` e tradução de erros do banco |
| `Persistencia/TradutorErrosSqlServer.cs` | reconhece os erros 2601/2627 (unicidade) e 547 (FK) |
| `Repositorios/RegistroRepository.cs` | implementação de `IRegistroRepository` |
| `Repositorios/ConsultaRegistrosLinq.cs` | tradução de `ConsultaRegistros` (filtros e ordenação) para LINQ |
| `Metadados/MetadadosProvider.cs` | leitura dos metadados com `IMemoryCache` |
| `Repositorios/UsuarioRepository.cs`, `EquipeRepository.cs`, `HistoricoRepository.cs` | usuários, equipes e histórico |
| `Repositorios/ConfiguracaoRepository.cs` | `ConfiguracaoEntidadeEquipe`: se a aprovação está ativa (RN33) |
| `Repositorios/ModeloAvaliacaoRepository.cs`, `AvaliacaoRepository.cs` | modelos e avaliações; médias, distribuição e contagens com `GroupBy`/`Average`/`Count` no banco (RN47, RN48) |
| `Exportacao/GeradorCsv.cs`, `GeradorXlsx.cs` | os `IGeradorArquivoExportacao` (RN49): CSV UTF-8 com BOM, `;` e vírgula decimal; XLSX com o **ClosedXML** (MIT), cabeçalho em negrito, filtro automático, números e datas tipados |
| `Repositorios/MetadadosAdminRepository.cs` | escrita dos metadados pela administração; `Tocar*` confere a `Versao` (UPDATE ... WHERE Versao = @lida) |
| `Repositorios/VerificadorDadosMetadados.cs` | as verificações contra os dados das alterações de metadados, em LINQ (RN58). Duplicatas de restrição nova: hash da `CalculadoraChaveUnica`, lendo os registros em lotes |
| `Repositorios/EscritorRegistrosMetadados.cs` | escritas em massa (valor padrão, chaves únicas) em lotes de 500, com `ChangeTracker.Clear()` entre os lotes |
| `Painel/PainelRepository.cs` | `IPainelRepository`: volume, aprovação (tempo até a primeira decisão, pelo histórico), avaliação (distribuição e ranking) e atividade, tudo com `GroupBy`/`Count`/`Average` no banco (RN81 a RN84) |
| `Painel/ConsultaIndicadoresLinq.cs`, `MontadorLinhas.cs` | a agregação configurável em LINQ (RN85 a RN87, [ADR 0031](../../../docs/adr/0031-agregacao-no-banco-em-linq.md)): um método por tipo de dimensão e de métrica, LEFT JOIN com `RegistroValor`/`RegistroReferencia` e um `GROUP BY` |
| `Repositorios/ImportacaoRepository.cs`, `ConsultasImportacao.cs` | as importações (RN97, RN104) e as consultas **em lote** da importação: itens pelo nome no escopo da listagem (uma por tipo de item, RN99), donos das chaves únicas (RN102), registros para alterar e alterações posteriores (RN103) |
| `Importacoes/ArquivosImportacao.cs` | leitura (valores das células, nunca fórmulas; tipo real pelo conteúdo; limite descompactado contra "zip bomb") e geração (modelo com listas suspensas e "Instruções"; relatório) das planilhas, com o ClosedXML (RN98) |
| `Acesso/HasherSenha.cs` | hash de senha com o `PasswordHasher` (PBKDF2), sem o Identity completo (ADR 0016) |
| `Acesso/SituacaoUsuarios.cs` | perfil, equipes ATIVAS e situação de cada usuário, com cache curto (RN23) |
| `ConfiguracaoInfraestrutura.cs` | `AdicionarInfraestrutura()`, chamado só pelo `Program.cs` |

## O EF só mapeia tabelas que já existem

Mudança de schema **sempre** nesta ordem:

1. Script novo `database/scripts/V00x__descricao.sql` (ver [`database/AGENTS.md`](../../../database/AGENTS.md)).
2. Aplique localmente: `cd database/docker && docker compose --env-file ../.env run --rm db-init`.
3. **Só então** atualize a classe de `Tabelas/` (ou do Domain, para dados) e a `...Configuracao.cs`
   da tabela, com os **mesmos** nomes de coluna, tipo (`HasColumnType`), PK, FK (`HasConstraintName`)
   e UNIQUE do script.
4. Rode `dotnet test`. Os testes de integração rodam contra o banco real e pegam mapeamento errado.
5. Mudou a estrutura? Ela também vai num **pacote de entrega** para os DBAs (`database/entrega/`, ver o AGENTS.md do database).

Todas as tabelas ficam no schema `catalogo` (V012). O nome do schema existe **só** em `Persistencia/SchemaBanco.cs`
(`SchemaBanco.Nome`), aplicado pelo `HasDefaultSchema` do `CatalogoDbContext`: as configurações usam `ToTable("Nome")` sem schema,
e nenhum código escreve `dbo.` ou `catalogo.` à mão.

Proibido: migrations, `EnsureCreated`, `EnsureDeleted`, `Migrate` ([adr/0003](../../../docs/adr/0003-schema-por-scripts-sql.md)).
As diferenças conscientes entre o script e o mapeamento estão em
[`docs/modelo-de-dados.md`](../../../docs/modelo-de-dados.md#mapeamento-no-ef-core). Mantenha essa lista atualizada.

## Toda consulta é EF Core em LINQ

- **Proibido SQL escrito à mão** na aplicação: `SqlQueryRaw`, `FromSql*`, `ExecuteSql*` e Dapper
  ([ADR 0011](../../../docs/adr/0011-consultas-somente-linq.md)). O teste `SemSqlManualTests` varre `src/` e falha se encontrar.
  `ExecuteDeleteAsync` (LINQ) é permitido.
- Filtros e ordenação usam subconsultas LINQ em `RegistroValor` (campos simples) e `RegistroReferencia`
  (referências), dentro de `ConsultaRegistrosLinq`. Os `CampoId` vêm sempre dos metadados; os valores, de
  `ConsultaRegistros` já tipada pelo Domain. Nunca use texto da URL para montar a consulta.
- Uma consulta por lista, nunca por linha (sem N+1): rótulos em `ObterRotulosAsync`, referências da
  exclusão em `ObterReferenciasQueApontamParaAsync`.

## Agregações do painel

- **Nunca agregue em memória**: nada de `ToList`/`AsEnumerable` antes do `GroupBy`. Toda consulta do painel parte de
  `ConsultaRegistrosLinq.Filtrar` (o escopo de segurança) e devolve só números.
- O SQL Server não aceita subconsulta dentro de `GROUP BY` nem de `AVG`: valores de campos entram por **LEFT JOIN** (veja
  `ConsultaIndicadoresLinq`), e períodos agrupam por números (`DATEPART`), não por `new DateTime(...)`.
- Dimensão nova: um método em `ConsultaIndicadoresLinq` que devolve um `ValorDoRegistro` por registro e diz as colunas que preenche
  (`ValoresDimensao`). Métrica nova: uma coluna em `LinhaIndicador`/`LinhaAgregada` e o caso em `MontadorLinhas.Agregacao`. O
  `MontadorLinhas` (expressões montadas à mão) raramente precisa mudar.
- Caminho por referência (RN89, [ADR 0038](../../../docs/adr/0038-caminhos-por-referencia.md)): um JOIN com `RegistroReferencia` por salto,
  do último para o primeiro (`AtravessarSaltos` no painel, `AplicarCondicaoPorCaminho` nos filtros). Não leia o registro apontado em memória.
- Confira o SQL com `ToQueryString()` (há um teste de integração que faz isso: `Indicadores_DuasDimensoes_UmaConsultaComGroupByNoBanco`;
  por isso o projeto tem `InternalsVisibleTo` para os testes de integração).

## Contagem de relacionados (RN94)

- `ConsultaRelacionadosLinq` agrupa `RegistroReferencia` por campo, só com as origens visíveis (o `Filtrar` de cada entidade de
  origem, juntado com `UNION ALL` dentro de um `IN`). Um SQL só, conferido com `ToQueryString` em `RelacionadosApiTests`
  ([ADR 0047](../../../docs/adr/0047-itens-relacionados-descobertos-pelos-metadados.md)). Não conte em memória nem uma consulta por campo.

## `RegistroValor` anda junto com `Registro.Dados`

- `RegistroValor` é a cópia tipada dos campos simples ([ADR 0013](../../../docs/adr/0013-registro-valor.md)).
  Ela é **sempre** gravada junto com `Registro.Dados`, no mesmo `SaveChanges`, pelo `SincronizadorRegistro`
  do Domain. Não grave nenhuma das duas sozinha. Na exclusão, o CASCADE do banco apaga os valores.
- Script que altera `Registro.Dados` direto no banco precisa atualizar `RegistroValor` também.
- `RegistroValorTests` (integração) confere a consistência depois de criar, atualizar e excluir.

## Administração de metadados (RN58)

- As alterações rodam em `IUnidadeDeTrabalho.ExecutarSerializavelAsync`: a verificação contra os dados e a gravação ficam na **mesma
  transação Serializable**. Deadlock (1205) vira 409.
- Dentro dela, `MetadadosProvider.CarregarGrupoAsync(contexto, ...)` lê a estrutura **sem cache**, já com o que a transação gravou.
- Exclusões de metadados usam `ExecuteDelete`/`ExecuteUpdate` com a condição de versão (`Where(e => e.Versao == lida)`): sem linha
  afetada, `ConflitoConcorrenciaException`. Não chame `SaveChanges` direto no repositório: use a `IUnidadeDeTrabalho` (ela traduz os erros).
- Colunas `bool` com padrão no banco (`Ativo`, `EscopoEquipe`, `Obrigatorio`) **não** têm `HasDefaultValue` no EF, pelo mesmo motivo da
  `SituacaoAprovacao`: o EF não enviaria `false` no INSERT.

## Cache da situação dos usuários

- `SituacaoUsuarios` guarda por usuário (perfil, equipes **ativas**, ativo, troca de senha) por
  `Seguranca:CacheUsuarioSegundos` (padrão 60 s). `Invalidar(id)` descarta um usuário; `InvalidarTodos()`
  descarta todos (ex.: equipe desativada), por meio de um sinal de cancelamento compartilhado.
- O cache é por servidor. Com mais de um servidor, uma mudança vale na hora só no servidor que a fez;
  nos outros, em até 60 s.

## Cache de metadados

- O `MetadadosProvider` guarda **o grupo inteiro** (entidades, campos, restrições, dependências) no
  `IMemoryCache` por `MetadadosProvider.TempoCache` (10 min). Grupo inexistente não é guardado.
- O cache é **por servidor**. Metadados alterados direto no banco aparecem em até 10 minutos, ou
  depois de reiniciar a aplicação.
- A administração de metadados chama `IMetadadosProvider.InvalidarCache(grupoChave)` depois de cada gravação (no
  `ExecutorAlteracaoMetadados`), o que também descarta a lista de grupos. Com mais de um servidor, os outros veem a mudança em até 10 minutos.

## Tradução dos erros do banco

| Erro do SQL Server | `UnidadeDeTrabalho` lança | Quem trata |
|---|---|---|
| 2601 (índice único) ou 2627 (UNIQUE/PK) | `ViolacaoUnicidadeBancoException`. Se a constraint for `UQ_RegistroChaveUnica_Valor`, o `RestricaoUnicaId` vem da mensagem | o caso de uso converte em `ConflitoUnicidadeException` → 409 |
| `DbUpdateConcurrencyException` (`Versao` mudou entre a leitura e o UPDATE, RN12) | `ConflitoConcorrenciaException` | Api → 409 |
| 547 (FK) durante `ExcluirAsync` (alguém criou uma referência no meio da exclusão) | `ConflitoConcorrenciaException`, com rollback | Api → 409 |

Para a concorrência do PUT, `DefinirVersaoEsperada` troca o valor **original** da `Versao` no EF pelo que
o cliente leu: assim o UPDATE sai com `WHERE Versao = @versaoLida`.

O teste `UnidadeDeTrabalhoTests` (integração) confere a tradução com um erro real do SQL Server.

## Regras deste projeto

- Leituras com `AsNoTracking()`. Não crie repositório genérico: só os métodos que os casos de uso usam.
- O escopo de equipes da listagem (`ConsultaRegistros.EquipesPermitidas`/`FiltroEquipe`) é aplicado em
  `ConsultaRegistrosLinq.Filtrar`, no `WHERE`. Não remova nem filtre depois em memória (RN26).
  O mesmo vale para a situação de aprovação (`NaoAprovadosSomenteDoAutor` e `FiltroSituacao`, RN37).
- Ordenação e filtro pela média de avaliação (RN48) são subconsultas correlacionadas em `ConsultaRegistrosLinq`
  (`NotasDoCriterio`). `Average` sobre `tinyint` precisa de conversão para `double` (senão o SQL Server faz média inteira).
- Média e contagem de avaliações: sempre no banco. Nunca traga as avaliações para a memória para calcular.
- `Registro.SituacaoAprovacao` é mapeada **sem** `HasDefaultValue`: o valor padrão do enum é `Pendente`, e o EF
  deixaria de enviá-lo no INSERT (o banco gravaria APROVADO).
- Classes de implementação são `internal sealed`. Só o `DbContext`, as classes de `Tabelas/` e o `ConfiguracaoInfraestrutura` são públicos.
- O ClosedXML **remove** um apóstrofo inicial dos textos (vira o "quote prefix" do Excel): o `GeradorXlsx` o dobra, para a
  proteção contra fórmula (RN49) ficar no valor, como no CSV. O `SaveAs` do ClosedXML é síncrono: grave num `MemoryStream`
  e copie para a resposta com `CopyToAsync` (o Kestrel não aceita escrita síncrona).
- `Contains` numa lista de `byte[]` (hashes) não vira parâmetro OPENJSON: use `EF.Constant(lista).Contains(...)`, em lotes de até 1.000
  (`ConsultasImportacao.ObterDonosDasChavesAsync`). Continua sendo LINQ.
- `Contains` em listas vira `OPENJSON` no EF 8, que exige nível de compatibilidade ≥ 130 (SQL Server 2016+).
  Se o banco de produção tiver nível menor, configure `UseCompatibilityLevel` no `UseSqlServer`.
