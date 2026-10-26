# Registros de decisão de arquitetura (ADR) do front-end

Cada ADR registra **uma** decisão: o contexto, o que foi decidido e as consequências.
ADR aceito não é editado. Se a decisão mudar, crie um ADR novo que **substitui** o antigo e
marque o antigo como "Substituído por NNNN".

**A numeração é uma só para o back-end e o front-end** (os ADRs vieram de um repositório único, e o código e os docs citam
"ADR 0045" sem dizer em que repositório ele está). Antes de criar um ADR, veja o último número **nos dois** repositórios
(`docs/adr/README.md` de cada um) e use o próximo. Decisão que mexe nos dois lados (um campo novo na API e o uso dele na tela)
fica no back-end.

## Aqui (decisões só do front-end)

| Nº | Decisão | Status |
|---|---|---|
| [0033](0033-angular-20-com-primeng.md) | Front-end em Angular 20 com PrimeNG | Aceito |
| [0034](0034-front-dirigido-por-metadados.md) | Front-end dirigido por metadados | Aceito |
| [0035](0035-token-em-sessionstorage.md) | Token de acesso em sessionStorage | Aceito |
| [0036](0036-cliente-gerado-do-openapi.md) | Cliente da API gerado a partir do OpenAPI | Aceito (o contrato passou a ser uma cópia em `openapi/v1.json`, ADR 0051) |
| [0037](0037-estado-da-listagem-na-url.md) | Estado da listagem na URL | Aceito |
| [0039](0039-widgets-por-configuracao-em-arquivo.md) | Widgets do painel definidos por configuração em arquivo | Aceito |
| [0040](0040-graficos-com-primeng-chartjs.md) | Gráficos com o p-chart do PrimeNG (Chart.js) | Aceito |
| [0041](0041-senha-provisoria-exibida-uma-vez-sem-armazenamento.md) | Senha provisória exibida uma vez, sem armazenamento no front | Aceito |
| [0044](0044-simulacao-obrigatoria-antes-de-gravar-estrutura.md) | Simulação obrigatória antes de gravar alterações de estrutura (na interface) | Aceito |

## No repositório do back-end (citados pelo front)

Ficam em `docs/adr/` do back-end porque também mudaram a API. Os que o front mais cita:

| Nº | Decisão |
|---|---|
| 0022 | Versionamento da API por prefixo de rota (`/api/v1`) |
| 0023 | Códigos de erro estáveis no ProblemDetails |
| 0024 | Contrato OpenAPI versionado e verificado por teste |
| 0038 | Caminhos por referência em filtros, dimensões e métricas |
| 0042 | Tipo de dado URL |
| 0043 | Navegação entre várias listagens, com a tela inicial calculada pelos metadados |
| 0045 | Telas de aprovação e avaliação condicionais ao recurso ativo, com contador por intervalo |
| 0046 | Listagens habilitadas por equipe |
| 0047 | Itens relacionados descobertos pelos metadados |
| 0048 | Importação de planilhas pelo mesmo caminho de gravação do cadastro manual |
| 0051 | Repositórios separados para o back-end e o front-end, com cópia do contrato no front |

Modelo para um ADR novo (`NNNN-titulo-curto.md`):

```markdown
# NNNN. Título

- Status: Proposto | Aceito | Substituído por NNNN
- Data: aaaa-mm-dd

## Contexto
## Decisão
## Consequências
```
