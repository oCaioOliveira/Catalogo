# Registros de decisão de arquitetura (ADR)

Cada ADR registra **uma** decisão: o contexto, o que foi decidido e as consequências.
ADR aceito não é editado. Se a decisão mudar, crie um ADR novo que **substitui** o antigo e
marque o antigo como "Substituído por NNNN".

**A numeração é uma só para o back-end e o front-end** ([ADR 0051](0051-repositorios-separados-back-e-front.md)). Os ADRs só do
front ficam no repositório do front-end (`docs/adr/`) e aparecem aqui sem link. Antes de criar um ADR, veja o último número nos dois
repositórios e use o próximo. Decisão que mexe nos dois lados fica aqui.

| Nº | Decisão | Status |
|---|---|---|
| [0001](0001-modelo-generico-eav-json.md) | Modelo genérico EAV com JSON e referências em tabela própria | Aceito |
| [0002](0002-clean-architecture-enxuta.md) | Clean Architecture enxuta | Aceito |
| [0003](0003-schema-por-scripts-sql.md) | Schema por scripts SQL versionados, sem migrations | Aceito |
| [0004](0004-sem-mediatr-automapper-fluentassertions.md) | Sem MediatR, AutoMapper e FluentAssertions 8+ | Aceito |
| [0005](0005-unicidade-por-hash.md) | Unicidade por hash em RegistroChaveUnica | Aceito |
| [0006](0006-dotnet-8.md) | .NET 8, com migração pendente para o .NET 10 | Aceito |
| [0007](0007-repositorio-por-aplicacao.md) | Repositório organizado em pastas por aplicação | Substituído por 0051 |
| [0008](0008-atualizacao-via-put.md) | Atualização via PUT com o registro inteiro | Aceito |
| [0009](0009-concorrencia-pela-versao-no-corpo.md) | Concorrência otimista pela `versao` no corpo do PUT | Aceito |
| [0010](0010-exclusao-fisica-com-plano.md) | Exclusão física com plano de exclusão | Aceito |
| [0011](0011-consultas-somente-linq.md) | Consultas ao banco somente via EF Core em LINQ | Aceito |
| [0012](0012-paginacao-por-pagina.md) | Paginação por página (`pagina` e `tamanho`) | Aceito |
| [0013](0013-registro-valor.md) | Tabela de valores tipados `RegistroValor` | Aceito |
| [0014](0014-perfil-unico-por-usuario.md) | Perfil único por usuário, válido em todas as equipes | Aceito |
| [0015](0015-jwt-com-provedor-de-identidade-isolado.md) | JWT com matrícula e senha local, atrás de interface para SSO | Aceito |
| [0016](0016-passwordhasher-sem-identity.md) | PasswordHasher sem o ASP.NET Core Identity completo | Aceito |
| [0017](0017-escopo-global-ou-por-equipe-na-entidade.md) | Escopo global ou por equipe definido na Entidade | Aceito |
| [0018](0018-historico-explicito-no-caso-de-uso.md) | Histórico explícito no caso de uso, sem interceptor | Aceito |
| [0019](0019-404-para-registro-de-outra-equipe.md) | 404, e não 403, para registros de outra equipe | Aceito |
| [0020](0020-aprovacao-por-entidade-e-equipe.md) | Aprovação de registros ativável por entidade e por equipe | Aceito |
| [0021](0021-avaliacao-configuravel-com-tabelas-tipadas.md) | Avaliação configurável por entidade e equipe, com tabelas relacionais tipadas | Aceito |
| [0022](0022-versionamento-por-prefixo-de-rota.md) | Versionamento da API por prefixo de rota (`/api/v1`) | Aceito |
| [0023](0023-codigos-de-erro-estaveis-no-problemdetails.md) | Códigos de erro estáveis no ProblemDetails | Aceito |
| [0024](0024-contrato-openapi-versionado-e-verificado.md) | Contrato OpenAPI versionado e verificado por teste | Aceito |
| [0025](0025-administracao-de-metadados-so-pelo-administrador.md) | Administração de metadados pela API, só pelo administrador | Aceito |
| [0026](0026-bloquear-alteracoes-incompativeis-com-os-dados.md) | Bloquear alterações de metadados incompatíveis com os dados, sem migração automática | Aceito |
| [0027](0027-chaves-de-metadados-imutaveis.md) | Chaves de metadados imutáveis | Aceito |
| [0028](0028-desativar-em-vez-de-excluir-metadados.md) | Desativar em vez de excluir metadados | Aceito |
| [0029](0029-simulacao-de-alteracoes-de-metadados.md) | Simulação de alterações de metadados com `?simular=true` | Aceito |
| [0030](0030-indicadores-genericos-pelos-metadados.md) | Indicadores genéricos, montados pelos metadados | Aceito (o limite de um nível de referência foi substituído pelo 0038) |
| [0031](0031-agregacao-no-banco-em-linq.md) | Agregação no banco, em LINQ, por LEFT JOIN com os valores tipados | Aceito |
| [0032](0032-painel-so-para-gestor-e-administrador.md) | Painel só para GESTOR e ADMINISTRADOR, com a visibilidade da listagem | Aceito |
| 0033 | Front-end em Angular 20 com PrimeNG (repositório do front-end) | Aceito |
| 0034 | Front-end dirigido por metadados (repositório do front-end) | Aceito |
| 0035 | Token de acesso em sessionStorage (repositório do front-end) | Aceito |
| 0036 | Cliente da API gerado a partir do OpenAPI (repositório do front-end) | Aceito (a origem do contrato foi alterada pelo 0051) |
| 0037 | Estado da listagem na URL (repositório do front-end) | Aceito |
| [0038](0038-caminhos-por-referencia.md) | Caminhos por referência em filtros, dimensões e métricas | Aceito |
| 0039 | Widgets do painel definidos por configuração em arquivo (repositório do front-end) | Aceito |
| 0040 | Gráficos com o p-chart do PrimeNG (Chart.js) (repositório do front-end) | Aceito |
| 0041 | Senha provisória exibida uma vez, sem armazenamento no front (repositório do front-end) | Aceito |
| [0042](0042-tipo-de-dado-url.md) | Tipo de dado URL e a conversão dos campos de endereço existentes | Aceito |
| [0043](0043-navegacao-entre-listagens-e-tela-inicial-pelos-metadados.md) | Navegação entre várias listagens, com a tela inicial calculada pelos metadados | Aceito |
| 0044 | Simulação obrigatória antes de gravar alterações de estrutura (na interface) (repositório do front-end) | Aceito |
| [0045](0045-telas-condicionais-por-recurso-ativo.md) | Telas de aprovação e avaliação condicionais ao recurso ativo, com contador por intervalo | Aceito |
| [0046](0046-listagens-habilitadas-por-equipe.md) | Listagens habilitadas por equipe, geridas pelo ADMINISTRADOR, com itens ocultos (não apagados) ao tirar uma equipe | Aceito |
| [0047](0047-itens-relacionados-descobertos-pelos-metadados.md) | Itens relacionados descobertos pelos metadados, com a contagem num único endpoint e os itens pela listagem filtrada | Aceito |
| [0048](0048-importacao-pelo-mesmo-caminho-do-cadastro.md) | Importação de planilhas pelo mesmo caminho de gravação do cadastro manual, com prévia por simulação e arquivo no banco | Aceito |
| [0049](0049-schema-catalogo-e-pacotes-de-entrega.md) | Schema próprio `catalogo` em todos os ambientes e pacotes de entrega consolidados para os DBAs | Aceito (em parte substituído pelo 0050) |
| [0050](0050-instalacao-inicial-so-estrutura-e-administrador.md) | Instalação inicial só com a estrutura e o administrador inicial, criado por script com o hash da senha | Aceito |
| [0051](0051-repositorios-separados-back-e-front.md) | Repositórios separados para o back-end e o front-end, com cópia do contrato no front | Aceito |

Modelo para um ADR novo (`NNNN-titulo-curto.md`):

```markdown
# NNNN. Título

- Status: Proposto | Aceito | Substituído por NNNN
- Data: aaaa-mm-dd

## Contexto
## Decisão
## Consequências
```
