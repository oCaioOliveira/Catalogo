# Administração de metadados: criar e alterar listagens pela API

Listagens novas (grupos, entidades, campos, restrições únicas, dependências e campo de exibição) são criadas **pela interface**
(Administração → Listagens, [guia](../guias/criar-uma-listagem.md)) ou **pela API**, sem script SQL ([ADR 0025](../adr/0025-administracao-de-metadados-so-pelo-administrador.md)). Scripts `V00x` ficam só para mudanças de schema.
As regras estão em [regras-de-negocio.md](../regras-de-negocio.md) (RN50 a RN77; equipes habilitadas, RN90 a RN93).

- Tudo fica sob **`/api/v1/admin/metadados`** e é **só do ADMINISTRADOR** (403 para os demais).
- Referências a outros metadados são sempre pela **chave**, que **não muda** depois de criada (RN51). Nome e descrição mudam.
- Toda escrita envia a **`versao`** do que altera (RN52): a do **grupo** para alterá-lo ou criar uma entidade nele; a da **entidade**
  para alterá-la e para tudo nos campos, restrições e dependências dela. Nos DELETE, `?versao=` (URL-encoded).
- Toda escrita responde `{ permitido, simulado, impactos, grupo }`, com o **grupo inteiro como ficou** (e as versões novas). Guarde
  esse `grupo`: é dele que saem as versões da próxima chamada.
- Toda escrita aceita **`?simular=true`** (RN56): valida tudo, inclusive contra os dados, e devolve o relatório **sem gravar**.

## Endpoints

| Método | Rota (sob `/api/v1/admin/metadados`) | O que faz |
|---|---|---|
| GET | `/grupos` | todos os grupos, ativos e inativos, com `quantidadeEntidades`, `quantidadeRegistros` (itens de todas as entidades) e `quantidadeEquipes` (habilitadas, RN90) |
| POST | `/grupos` | cria um grupo: `{ chave, nome, descricao }` (sem versão) |
| GET | `/grupos/{grupo}` | a estrutura inteira para edição (entidades e campos ativos e inativos, versões, registros por entidade, `quantidadeEquipes`) |
| GET | `/grupos/{grupo}/equipes` | **todas** as equipes (ativas e inativas), cada uma com `habilitada` e `quantidadeItens` na listagem, e a `versao` do grupo ([abaixo](#equipes-habilitadas)) |
| PUT | `/grupos/{grupo}/equipes` | `{ versao (do grupo), equipes: [ids] }`: a lista **completa** das habilitadas (vazia = nenhuma) (RN90, RN92) |
| PUT | `/grupos/{grupo}` | `{ versao, nome, descricao }` |
| PATCH | `/grupos/{grupo}/ativacao` | `{ versao, ativo }` (RN77) |
| DELETE | `/grupos/{grupo}?versao=` | só sem entidades (RN77) |
| GET | `/grupos/{grupo}/historico` | histórico `METADADO`, paginado. `?entidade=<chave>`: só o daquela entidade (ela, os campos, as restrições e as dependências dela). Cada item traz `alvo`: o que foi alterado ("Campo 'nome' de 'equipamento'") |
| POST | `/grupos/{grupo}/entidades` | `{ versao (do grupo), chave, nome, nomePlural, descricao, ordem, escopoEquipe }` |
| PUT | `/grupos/{grupo}/entidades/{entidade}` | `{ versao, nome, nomePlural, descricao, ordem, campoExibicao, escopoEquipe }` |
| PATCH | `.../entidades/{entidade}/ativacao` | `{ versao, ativo }` (RN75) |
| DELETE | `.../entidades/{entidade}?versao=` | só se nunca teve registros e não é referenciada (RN76) |
| POST | `.../entidades/{entidade}/campos` | cria um campo (abaixo) |
| PUT | `.../entidades/{entidade}/campos/{campo}` | altera um campo |
| PATCH | `.../campos/{campo}/ativacao` | `{ versao, ativo }` (RN69) |
| DELETE | `.../campos/{campo}?versao=` | só sem valores e sem usos (RN70) |
| PUT | `.../entidades/{entidade}/campos/ordem` | `{ versao, campos: [todas as chaves, na ordem nova] }` |
| POST | `.../entidades/{entidade}/restricoes` | `{ versao, nome, campos: [chaves] }` (RN71) |
| DELETE | `.../restricoes/{restricao}?versao=` | remove a restrição e as chaves dela (RN72) |
| PUT | `.../campos/{campo}/dependencia` | `{ versao, campoVia, campoOrigem, campoAlvo }` (RN73) |
| DELETE | `.../campos/{campo}/dependencia?versao=` | remove a dependência |

O corpo de um **campo**: `{ versao, chave, nome, tipoDado, obrigatorio, tamanhoMaximo, precisao, escala, opcoes,
entidadeReferenciada, acaoExclusao, ordem, valorPadrao }`. Só vão os atributos do tipo (RN53):

| `tipoDado` | Atributos |
|---|---|
| `TEXTO` | `tamanhoMaximo` (1 a 4000) |
| `TEXTO_LONGO`, `INTEIRO`, `BOOLEANO`, `DATA`, `DATA_HORA`, `URL` | nenhum |
| `DECIMAL` | `precisao` (1 a 38) e `escala` (0 a 10, até a precisão; parte inteira até 28 dígitos) |
| `LISTA` | `opcoes` (pelo menos uma, sem vazias nem repetidas) |
| `REFERENCIA` | `entidadeReferenciada` (chave de uma entidade ativa do mesmo grupo) e `acaoExclusao` (`RESTRINGIR` ou `CASCATA`) |

No **PUT do campo**, `chave`, `tipoDado`, `entidadeReferenciada`, `acaoExclusao` e `ordem` ausentes valem os atuais; os demais
atributos são substituídos (ausente = padrão: `obrigatorio` false). Chave, tipo (salvo as mudanças da RN66, abaixo) e entidade
referenciada não mudam (400).

O **nome no plural** da entidade (`nomePlural`, até 150 caracteres) é opcional: vai para os títulos das listagens, o menu e as seções
de filhos; sem ele, as telas usam o `nome`. No PUT, ausente ou vazio = sem plural.

## Equipes habilitadas

Cada listagem é usada por **algumas equipes** (RN90 a RN93, [ADR 0046](../adr/0046-listagens-habilitadas-por-equipe.md)). Só os membros
das equipes habilitadas a veem; dentro dela, "as suas equipes" são só as habilitadas (RN91). **Um grupo novo nasce sem equipes** (só o
administrador o vê, RN93) e uma **equipe nova não é habilitada em nenhum**: depois de montar a listagem, habilite as equipes.

```json
GET /api/v1/admin/metadados/grupos/inventario/equipes

200
{
  "versao": "AAAAAAAAC1E=",
  "equipes": [
    { "id": "…", "nome": "Equipe Alfa", "ativa": true, "habilitada": true, "quantidadeItens": 12 },
    { "id": "…", "nome": "Equipe Beta", "ativa": true, "habilitada": false, "quantidadeItens": 0 },
    { "id": "…", "nome": "Equipe Antiga", "ativa": false, "habilitada": false, "quantidadeItens": 0 }
  ]
}
```

O `PUT` recebe a lista **completa** e a `versao` do grupo, aceita `?simular=true` e responde como toda escrita
(`{ permitido, simulado, impactos, grupo }`). Ids repetidos, inválidos ou de equipes que não existem: **400** em `equipes[i]`.
Uma equipe inativa pode ficar habilitada (os membros voltam a ver a listagem quando ela é reativada).

**Tirar uma equipe que tem itens é permitido** (RN92): os itens **não são apagados**, ficam ocultos para os membros dela (só o
administrador os vê) e voltam ao habilitá-la de novo. A simulação diz quantos, com um impacto por equipe que sai:

```json
PUT /api/v1/admin/metadados/grupos/inventario/equipes?simular=true
{ "versao": "AAAAAAAAC1E=", "equipes": [] }

200
{
  "permitido": true, "simulado": true, "grupo": null,
  "impactos": [
    { "tipo": "ITENS_OCULTOS", "bloqueia": false, "quantidade": 12, "exemplos": [], "grupos": null, "usos": null,
      "descricao": "Os itens da equipe 'Equipe Alfa' ficam ocultos para os membros dela (só o administrador os vê). Nada é apagado: eles voltam ao habilitar a equipe de novo." }
  ]
}
```

A gravação muda a `versao` do grupo e gera **uma** linha `METADADO` no histórico, com as equipes antes e depois, pelo nome, no campo
`equipes`. O detalhe de cada equipe (`GET /api/v1/equipes/{id}`) traz `listagens`: em quais ela está habilitada.

## Passo a passo: a listagem "Inventário"

Uma listagem pequena com:

- `categoria`, **global** (as mesmas categorias para todas as equipes): `nome` TEXTO(50), obrigatório e único;
- `equipamento`, **por equipe**: `nome` TEXTO(100), obrigatório e único na equipe; `categoria` (REFERENCIA → categoria);
  `data_aquisicao` (DATA); `valor` (DECIMAL(12,2)).

A ordem é a natural: grupo, entidades, campos de cada entidade (as referências apontam para entidades que já existem), campo de
exibição e restrições. Em cada passo, a `versao` vem do `grupo` devolvido pelo passo anterior.

**1. O grupo** (sem versão: ele ainda não existe)

```json
POST /api/v1/admin/metadados/grupos
{ "chave": "inventario", "nome": "Inventário", "descricao": "Equipamentos de cada equipe" }
```

→ **201**, com `grupo.versao` (ex.: `"AAAAAAAAH0k="`).

**2. As entidades**, com a versão do **grupo** (que muda a cada entidade criada)

```json
POST /api/v1/admin/metadados/grupos/inventario/entidades
{ "versao": "AAAAAAAAH0k=", "chave": "categoria", "nome": "Categoria", "escopoEquipe": false, "ordem": 1 }

POST /api/v1/admin/metadados/grupos/inventario/entidades
{ "versao": "<grupo.versao devolvido>", "chave": "equipamento", "nome": "Equipamento", "escopoEquipe": true, "ordem": 2 }
```

**3. Os campos da categoria**, com a versão da **entidade** (`grupo.entidades[].versao`)

```json
POST .../grupos/inventario/entidades/categoria/campos
{ "versao": "<versão da categoria>", "chave": "nome", "nome": "Nome", "tipoDado": "TEXTO", "tamanhoMaximo": 50, "obrigatorio": true }
```

**4. O campo de exibição e a restrição da categoria** (o rótulo das referências, RN20, e a unicidade, RN09)

```json
PUT .../grupos/inventario/entidades/categoria
{ "versao": "<versão da categoria>", "nome": "Categoria", "campoExibicao": "nome" }

POST .../grupos/inventario/entidades/categoria/restricoes
{ "versao": "<versão da categoria>", "nome": "uq_categoria_nome", "campos": ["nome"] }
```

**5. Os campos do equipamento**

```json
POST .../entidades/equipamento/campos
{ "versao": "<v>", "chave": "nome", "nome": "Nome", "tipoDado": "TEXTO", "tamanhoMaximo": 100, "obrigatorio": true }

POST .../entidades/equipamento/campos
{ "versao": "<v>", "chave": "categoria", "nome": "Categoria", "tipoDado": "REFERENCIA", "obrigatorio": true,
  "entidadeReferenciada": "categoria", "acaoExclusao": "RESTRINGIR" }

POST .../entidades/equipamento/campos
{ "versao": "<v>", "chave": "data_aquisicao", "nome": "Data de aquisição", "tipoDado": "DATA" }

POST .../entidades/equipamento/campos
{ "versao": "<v>", "chave": "valor", "nome": "Valor", "tipoDado": "DECIMAL", "precisao": 12, "escala": 2 }
```

Uma entidade **por equipe** pode referenciar uma **global** (como aqui); o contrário não (RN54).

**6. Exibição e unicidade do equipamento**

```json
PUT .../entidades/equipamento
{ "versao": "<v>", "nome": "Equipamento", "campoExibicao": "nome" }

POST .../entidades/equipamento/restricoes
{ "versao": "<v>", "nome": "uq_equipamento_nome", "campos": ["nome"] }
```

**7. Pronto: os registros usam os endpoints normais** (`/api/v1/grupos/inventario/entidades/...`)

```json
POST /api/v1/grupos/inventario/entidades/categoria/registros
{ "nome": "Notebook" }

POST /api/v1/grupos/inventario/entidades/equipamento/registros
{ "equipeId": "<equipe>", "nome": "Notebook do Caio", "categoria": "<id da categoria>", "data_aquisicao": "2026-03-10", "valor": 7500.50 }
```

Antes de cadastrar itens **por equipe**, habilite as equipes (`PUT .../grupos/inventario/equipes`, [acima](#equipes-habilitadas)): sem
nenhuma, só o administrador vê a listagem, e nem ele cria itens por equipe nela (o `equipeId` precisa ser de uma equipe habilitada, RN91).

Para quem é das equipes habilitadas, a listagem aparece no menu (`GET /api/v1/grupos`), nos metadados (`GET .../grupos/inventario/entidades`), nos filtros
(`?valor.de=5000`), na exportação e na aprovação e avaliação por equipe, como qualquer outra.

## Alterar uma estrutura que já tem dados

Toda mudança é conferida contra os registros existentes (RN58, [ADR 0026](../adr/0026-bloquear-alteracoes-incompativeis-com-os-dados.md)).
Se algum registro ficaria inválido, a resposta é **409** `ALTERACAO_INCOMPATIVEL_COM_DADOS` com o relatório em `detalhes.impactos`;
se o que impede é um uso na configuração, **409** `METADADO_EM_USO`. Nada é migrado automaticamente.

| Mudança | Regra | RN |
|---|---|---|
| Adicionar campo opcional | sempre permitido | RN59 |
| Adicionar campo obrigatório | com registros, exige `valorPadrao`, gravado em todos | RN60 |
| Tornar obrigatório | bloqueado se há registros vazios, a menos que venha `valorPadrao` (só os vazios) | RN61 |
| Tornar opcional | sempre permitido | RN62 |
| Diminuir `tamanhoMaximo` | bloqueado se algum valor exceder | RN63 |
| Aumentar tamanho, precisão ou escala | permitido (aumentar **só a escala** reduz a parte inteira e é conferido) | RN64 |
| Diminuir precisão ou escala | bloqueado se algum valor não couber | RN65 |
| Mudar o tipo | não permitido (400), salvo `TEXTO` → `TEXTO_LONGO` e `URL` → `TEXTO_LONGO` (sempre) e `TEXTO`/`TEXTO_LONGO` → `URL` (**409** `VALOR_NAO_E_URL` se algum valor não for uma URL válida) | RN66 |
| LISTA: adicionar opção | sempre permitido | RN67 |
| LISTA: remover opção | bloqueado se algum registro a usa | RN68 |
| Desativar campo | se não é de exibição, de restrição nem de dependência ativa; valores mantidos | RN69 |
| Excluir campo | só sem nenhum valor e sem usos | RN70 |
| Adicionar restrição única | bloqueado com duplicatas; se passar, gera as chaves de todos | RN71 |
| Remover restrição única | sempre permitido | RN72 |
| Adicionar ou alterar dependência | bloqueado se algum registro viola a regra nova | RN73 |
| Mudar o escopo (global ↔ por equipe) | só sem registros e sem configuração de equipe | RN74 |
| Desativar entidade | permitido: 404 para não-administradores, somente leitura para o administrador | RN75 |
| Excluir entidade | só se nunca teve registros e não é referenciada | RN76 |
| Desativar / excluir grupo | desativar: some para os não-administradores; excluir: só sem entidades | RN77 |

**Exemplo**: diminuir o `nome` do equipamento para 20 caracteres, quando um registro tem 80. Simulando antes:

```json
PUT .../entidades/equipamento/campos/nome?simular=true
{ "versao": "<v>", "nome": "Nome", "obrigatorio": true, "tamanhoMaximo": 20 }

200
{
  "permitido": false, "simulado": true, "grupo": null,
  "impactos": [
    { "tipo": "TEXTO_ACIMA_DO_TAMANHO", "descricao": "Valores de 'nome' com mais de 20 caracteres (o limite era 100).",
      "bloqueia": true, "quantidade": 1, "exemplos": [{ "id": "…", "rotulo": "Notebook do setor de compras e licitações…" }],
      "grupos": null, "usos": null }
  ]
}
```

Sem `?simular=true`, a mesma chamada responde **409** com esses impactos em `detalhes.impactos`.

### Relatório de impacto

Cada impacto tem `tipo` (estável, para o front), `descricao`, `bloqueia` (false = só informa o efeito), `quantidade` de registros,
até 10 `exemplos` (`{ id, rotulo }`), `grupos` (só em duplicatas: até 10 grupos de registros repetidos) e `usos` (onde um campo ou
entidade é usado).

| `tipo` | Quando | Bloqueia |
|---|---|---|
| `CAMPO_OBRIGATORIO_SEM_VALOR` | campo obrigatório (novo, que vira obrigatório ou reativado) e registros sem valor | sim |
| `VALOR_PADRAO_APLICADO` | registros que recebem (ou receberiam) o `valorPadrao` | não |
| `TEXTO_ACIMA_DO_TAMANHO` | textos maiores que o tamanho novo | sim |
| `NUMERO_FORA_DA_PRECISAO` | números que não cabem na precisão ou escala nova | sim |
| `VALOR_NAO_E_URL` | mudar o tipo para `URL` com valores que não são URL (http/https, sem espaços, até 2000) | sim |
| `OPCAO_EM_USO` | registros com uma opção removida da LISTA | sim |
| `CAMPO_COM_VALORES` | excluir campo com valores | sim |
| `CAMPO_EM_USO` | o campo é de exibição, de restrição ou de dependência (`usos`) | sim |
| `DUPLICATAS_NA_RESTRICAO` | registros que já repetem os valores da restrição nova (`grupos`) | sim |
| `CHAVES_UNICAS_GERADAS` | registros que ganham a chave da restrição nova | não |
| `DEPENDENCIA_VIOLADA` | registros que violam a dependência nova | sim |
| `ENTIDADE_COM_REGISTROS` | mudar escopo ou excluir entidade com registros | sim |
| `ENTIDADE_COM_HISTORICO` | excluir entidade que já teve registros | sim |
| `ENTIDADE_COM_CONFIGURACOES` | mudar escopo ou excluir entidade com aprovação ou modelo de avaliação | sim |
| `ENTIDADE_REFERENCIADA` | excluir entidade referenciada por outros campos (`usos`) | sim |
| `GRUPO_COM_ENTIDADES` | excluir grupo com entidades (`usos`) | sim |
| `ITENS_OCULTOS` | tirar das equipes habilitadas uma equipe com itens na listagem: eles ficam ocultos para os membros (RN92) | não |

### Desativar um campo e reativar

Um campo desativado some dos formulários, da resposta dos registros, dos filtros e da exportação, mas **os valores ficam guardados**:
o PUT de um registro os preserva, e reativar o campo devolve tudo (RN69).

## Cuidados

- **Escolha bem as chaves**: elas não mudam (RN51). O nome pode ser corrigido a qualquer momento.
- **Simule** antes de alterar uma entidade com muitos registros: a alteração roda numa transação Serializable e trava gravações nela
  enquanto dura (segundos, em entidades grandes).
- Reordenar campos ou mudar a escala de um DECIMAL **recalcula as chaves únicas** das restrições envolvidas (a ordem dos campos e a
  escala fazem parte do hash, RN09).
- Toda mudança fica no histórico do grupo (`GET .../grupos/{grupo}/historico`), com antes, depois e `registrosAfetados`.
