# Guia da API para quem consome (front-end e integrações)

A API do Catalogo é REST com JSON. Este guia mostra **como usar**; o porquê de cada regra está em
[`regras-de-negocio.md`](../regras-de-negocio.md) (os `RNxx` citados abaixo).

- **Contrato completo**: [`backend/openapi/v1.json`](../../backend/openapi/v1.json) (OpenAPI 3.0). Em Development, o Swagger UI
  fica em `http://localhost:5080/swagger` (botão *Authorize* para colar o token).
- **Erros**: [`codigos-de-erro.md`](codigos-de-erro.md).
- **Administração de metadados** (criar e alterar listagens, só o administrador): [`administracao-de-metadados.md`](administracao-de-metadados.md).
- **Painel gerencial** (visão geral e indicadores, GESTOR e ADMINISTRADOR): [`painel.md`](painel.md).
- **Importação por planilha** (XLSX e CSV, com prévia, execução, relatório e desfazer): [`importacao.md`](importacao.md).

## Versionamento

Todas as rotas começam com **`/api/v1`** (só o `/health` fica fora). Na v1 podem **aparecer** campos, endpoints,
parâmetros opcionais e códigos de erro novos: **ignore campos que não conhece**. Remover ou renomear campo, mudar tipo,
status HTTP ou código de erro só acontece numa v2, que vai conviver com a v1 por um tempo
([ADR 0022](../adr/0022-versionamento-por-prefixo-de-rota.md)).

## Autenticação

1. `POST /api/v1/auth/login` com `{ "matricula": "c123456", "senha": "..." }` devolve
   `{ "token": "...", "expiraEm": "2026-10-22T01:30:00Z", "deveTrocarSenha": false, "usuario": { id, matricula, nome, perfil, deveTrocarSenha, equipes } }`.
   Falha sempre com **401** `CREDENCIAIS_INVALIDAS` e a mesma mensagem (RN23); mais de 10 tentativas por minuto, **429**.
2. Envie o token em **todas** as requisições: `Authorization: Bearer <token>`. Ele vale 8 horas e não há refresh:
   com **401** (`NAO_AUTENTICADO`), volte ao login.
3. Se `deveTrocarSenha` for `true`, todos os endpoints respondem **403** `TROCA_DE_SENHA_OBRIGATORIA`, exceto
   `POST /api/v1/auth/trocar-senha` (`{ "senhaAtual": "...", "novaSenha": "..." }`, **204**) e `GET /api/v1/auth/eu` (RN25).
   Depois da troca, o mesmo token passa a funcionar.
4. `GET /api/v1/auth/eu` devolve o usuário logado, com o **perfil** e as **equipes ativas**: use-os para montar o menu e
   escolher a equipe. Perfil e equipes valem na hora em que mudam (RN23).

A API aceita chamadas de outra origem (CORS) só das origens configuradas no servidor, sem cookies
([`seguranca.md`](../seguranca.md#cors)).

## Convenções do JSON

| O quê | Como |
|---|---|
| Propriedades de sistema | camelCase (`criadoEm`, `situacaoAprovacao`) |
| Campos das entidades | a `chave` do metadado, como está (`nome_servico`, `data_execucao`) |
| Enums | texto em maiúsculas (`"APROVADO"`, `"ESTRELAS"`, `"GESTOR"`) |
| Datas de sistema | ISO 8601 em UTC com `Z`: `"2026-10-21T17:30:00.1234567Z"`. Converta para o fuso do usuário na tela |
| Nulos | **sempre presentes** como `null` (nunca omitidos), em toda a API |
| Ids | GUID em texto (`"6f1c0d5e-2b7a-4c11-9a3e-1d2c3b4a5f60"`) |

Status: **201** com o cabeçalho `Location` na criação, **204** quando não há corpo (exclusão, troca de senha), **200** nos demais.

## Montando as telas

| Para | Chame |
|---|---|
| O menu de listagens (seletor) | `GET /api/v1/grupos` → `[{ chave, nome, descricao, ativo, minhasEquipes: [{ id, nome }], possuiEquipes }]` (só as que você vê, RN90) |
| As entidades (tipos de item) e os seus campos | `GET /api/v1/grupos/{grupo}/entidades?equipeId=<equipe>` |
| Os itens relacionados a um item (seções do detalhe) | `GET /api/v1/grupos/{grupo}/entidades/{entidade}/registros/{id}/relacionados` (RN94, abaixo) |
| As opções de um campo REFERENCIA | `GET /api/v1/grupos/{grupo}/entidades/{entidade}/campos/{campo}/opcoes?busca=...&dependeDe=<id>&equipeId=<equipe do registro>` (RN21) |

### Listagens e equipes (RN90 a RN93)

Cada listagem é habilitada para **algumas equipes** (pelo administrador). `GET /api/v1/grupos` devolve **só as listagens que você vê**:
ativas e com pelo menos uma das suas equipes ativas habilitada (o administrador vê todas). Uma listagem que você não vê responde
**404** em tudo o que está sob ela (metadados, registros, opções, exportação, histórico, painel, configurações e fila).

Cada listagem traz:

- `minhasEquipes`: **as suas equipes nesta listagem** (RN91; para o administrador, as habilitadas e ativas). É o que vale dentro dela:
  o select de equipe ao criar (com uma só, `equipeId` é opcional), a coluna e o filtro de Equipe (só com mais de uma, ou para o
  administrador), as opções de equipe do painel, das configurações e da fila. Um `equipeId` fora dele: 403 (400 para o administrador).
- `possuiEquipes`: se há alguma equipe habilitada. Para o administrador, `false` = "só administradores veem esta listagem" (RN93).

Se a última listagem usada (guardada no navegador) não vier mais na lista, abra a primeira; lista vazia = nenhuma listagem para as
equipes do usuário. Itens de uma equipe que deixou de ser habilitada ficam ocultos para os membros dela e visíveis só para o
administrador (RN92).

As entidades vêm na ordem do menu (`ordem`, definida na administração). Grupos e entidades **desativados** não aparecem para quem
não é administrador (e as rotas deles dão 404); o administrador os vê com `"ativo": false` e só pode ler os registros: escrever dá
409 `ENTIDADE_INATIVA` (RN75, RN77). Campos **inativos** não aparecem em `campos` nem nos registros (RN69).

Cada entidade traz `nome` (singular: "Novo …", "Editar …"), `nomePlural` (títulos de listagem e menu; nulo = use o `nome`), `ordem`, `campos` (só os ativos, com `ativo`, `tipoDado`, `obrigatorio`, `ordem`, `tamanhoMaximo`, `opcoes`, `dependeDe`...),
`restricoesUnicas`, `escopoEquipe` (se o registro pertence a uma equipe, RN27) e, com `equipeId`, `requerAprovacao` (RN33) e
`modeloAvaliacao` (o formulário de avaliação, RN40). Sem `equipeId`, cada entidade traz `equipesComAprovacao` (as equipes que você vê
com a aprovação ligada, RN33) e `criteriosAvaliacao` (`[{ chave, nome }]`, os critérios de estrelas dos modelos ativos nessas equipes,
RN40/RN48): mostre situação, filtros e colunas de média só quando não estiverem vazios. As opções de um campo REFERENCIA trazem
`situacaoAprovacao` (RN38).

## Registros

Rotas sob `/api/v1/grupos/{grupo}/entidades/{entidade}/registros`: `GET` (lista), `POST`, `GET {id}`, `PUT {id}`, `DELETE {id}`,
`GET {id}/historico`, `GET {id}/relacionados` e `GET exportar`.

O registro é **plano**: os campos da entidade ficam em `dados`, e as colunas de sistema fora dele.

```json
{
  "id": "c3a4b5d6-...", "equipeId": "3d2b1a09-...", "criadoEm": "2026-10-21T17:30:00.1234567Z", "atualizadoEm": null,
  "versao": "AAAAAAAAB9I=", "criadoPor": { "id": "...", "matricula": "c123456", "nome": "Maria Souza" }, "atualizadoPor": null,
  "situacaoAprovacao": "APROVADO", "ultimaDecisao": null, "avaliacao": null,
  "dados": {
    "relatorio_teste": { "id": "6f1c0d5e-...", "rotulo": "Bateria 1" },
    "numero_cenario": 1,
    "vazao_media_tps": 35.50
  }
}
```

- **Referências**: na saída vêm como `{ id, rotulo }` (RN20); na entrada (POST e PUT) envie **só o id**:
  `{ "relatorio_teste": "6f1c0d5e-..." }`.
- **Chaves reservadas**: `id`, `versao`, `criadoEm`, `atualizadoEm`, `equipeId`, `criadoPor`, `atualizadoPor`,
  `situacaoAprovacao`, `ultimaDecisao`, `avaliacao`, `pagina`, `tamanho`, `ordenacao`, `busca`, `dependeDe` e `formato`
  nunca são campos (RN14). No corpo do POST, `equipeId` escolhe a equipe do registro (obrigatório para quem tem mais de
  uma equipe e para o administrador, RN27).
- **Formatos dos valores** (RN04): decimal com ponto (`35.5`), data `"aaaa-mm-dd"`, data e hora com fuso
  (`"2026-10-21T14:30:00-03:00"`; volta em UTC), booleano `true`/`false`, LISTA = uma das `opcoes`, URL = endereço absoluto
  `http`/`https`, sem espaços, até 2000 caracteres (`"https://grafana.interno/d/abc"`).
- **Validação**: todos os erros voltam juntos, por campo (400 `VALIDACAO`, RN05). Campos desconhecidos ou inativos (a estrutura mudou
  desde que a tela carregou os metadados) também saem em `detalhes.camposDesatualizados`: recarregue `GET .../entidades`.

### Itens relacionados

`GET .../registros/{id}/relacionados` (RN94) devolve uma entrada por campo REFERENCIA ativo, de entidade ativa do grupo, que aponta
para a entidade do item, na ordem dos metadados, com a quantidade de itens **que você vê** (a mesma regra da listagem; zero também vem):

```json
[
  { "entidade": { "chave": "relatorio_teste", "nome": "Relatório de Teste", "nomePlural": "Relatórios de Teste" },
    "campo": { "chave": "projeto", "nome": "Projeto" }, "filha": false, "quantidade": 12 },
  { "entidade": { "chave": "endpoint_servico", "nome": "Endpoint de Serviço", "nomePlural": "Endpoints de Serviço" },
    "campo": { "chave": "projeto", "nome": "Projeto" }, "filha": false, "quantidade": 2 }
]
```

- `filha`: o campo é obrigatório e `CASCATA` (o item "contém" esses itens, como os cenários de um relatório).
- Os itens de uma entrada vêm da listagem filtrada pelo campo: `GET .../entidades/relatorio_teste/registros?projeto=<id>`, com
  paginação, ordenação e filtros de sempre. Funciona igual quando a entidade de origem ou a do item é global.
- Item que você não vê: **404**, como o `GET {id}`.

### Concorrência com `versao`

O `GET` devolve a `versao`. O `PUT` envia o registro **inteiro** (campo opcional ausente = campo apagado, RN15) e a `versao`
lida, no corpo:

```json
PUT .../registros/{id}
{ "versao": "AAAAAAAAB9I=", "relatorio_teste": "6f1c0d5e-...", "numero_cenario": 2 }
```

Se outra pessoa alterou o registro depois da sua leitura: **409** `CONFLITO_CONCORRENCIA`, e nada é gravado (RN12).
Recarregue o registro e deixe o usuário refazer a alteração. A resposta do PUT traz a `versao` nova. O mesmo vale para o
PUT do modelo de avaliação e para a edição da própria avaliação.

## Paginação, filtros e ordenação

Toda listagem de dados usa o **mesmo envelope** e os mesmos parâmetros:

```json
{ "itens": [ ... ], "pagina": 1, "tamanho": 20, "total": 57, "totalPaginas": 3 }
```

- `pagina` a partir de 1 (padrão 1); `tamanho` de 1 a **100** (padrão 20). Fora disso, **400** no parâmetro (RN17).
- Usam o envelope: registros, opções de campo, históricos, avaliações individuais, fila de aprovação, usuários e equipes.
- Metadados e configurações pequenas voltam como **array simples**, sem paginação: grupos, entidades e modelos de avaliação.

Filtros dos registros (RN18), combinados com E:

```
GET .../cenario_teste/registros?numero_cenario.de=2&usuarios_virtuais.ate=500&relatorio_teste=6f1c0d5e-...
GET .../endpoint_servico/registros?nome_servico.contem=pix&ativo=true
GET .../relatorio_teste/registros?data_execucao=2026-10-21&equipeId=<equipe>&situacaoAprovacao=PENDENTE
GET .../relatorio_teste/registros?avaliacao.qualidade.de=4
```

| Forma | Vale para |
|---|---|
| `chave=valor` (igual) | todos os tipos (texto sem diferenciar maiúsculas nem acentos) |
| `chave.contem=valor` | TEXTO, TEXTO_LONGO, URL |
| `chave.de=valor`, `chave.ate=valor` (inclusivos) | INTEIRO, DECIMAL, DATA, DATA_HORA (só a data = o dia inteiro em UTC) |
| `equipeId=<id>` | só os registros dessa equipe (das suas) |
| `situacaoAprovacao=PENDENTE\|APROVADO\|REJEITADO` | registros nessa situação (RN37) |
| `avaliacao.<criterio>.de` / `.ate` | pela média exibida do critério (RN48) |
| `<referencia>.<chave>...` (até 2 saltos) | qualquer das formas acima num campo de um registro **apontado** (RN89): `relatorio_teste.tipo_teste=Carga`, `relatorio_teste.data_execucao.de=2026-01-01`, `relatorio_teste.ambiente.nome.contem=homolog` |

Ordenação (RN19): `ordenacao=nome` (crescente) ou `ordenacao=-nome` (decrescente); também `criadoEm`, `atualizadoEm`
e `avaliacao.<criterio>` (sem nota sempre por último). Padrão: `-criadoEm`. TEXTO_LONGO não ordena (400). Um campo **REFERENCIA**
(`ordenacao=projeto`) ordena pelo rótulo do item apontado (o campo de exibição dele, com o tipo dele); itens sem a referência ficam
sempre por último; se a entidade apontada não tiver campo de exibição, 400.

## Aprovação (do ponto de vista do front)

- Com `requerAprovacao: true` na entidade (para a equipe), o EXTERNO **cria** registros que nascem `PENDENTE`, e só altera
  os seus enquanto `PENDENTE` ou `REJEITADO` (alterar um rejeitado o reenvia, RN34, RN35).
- Mostre `situacaoAprovacao` e `ultimaDecisao` (`{ decisao, justificativa, decididoPor, decididoEm }`).
- GESTOR e ADMINISTRADOR: fila em `GET /api/v1/aprovacoes/pendentes` (mais antigos primeiro; só listagens que você vê e, em cada uma,
  as suas equipes nela, RN91) e decisão em
  `POST .../registros/{id}/aprovacao` com `{ "decisao": "APROVAR" | "REJEITAR", "justificativa": "..." }` (10 a 1000 caracteres, RN36).
- Configuração por equipe: `GET`/`PUT /api/v1/equipes/{equipeId}/configuracoes/grupos/{grupo}/entidades/{entidade}/aprovacao`
  com `{ "ativa": true }`, só para equipes habilitadas na listagem (404 nas outras, RN91). Desativar com pendentes: **409** `APROVACOES_PENDENTES` com `detalhes.quantidade`.

## Avaliação (do ponto de vista do front)

- O formulário vem em `modeloAvaliacao.criterios` de `GET .../entidades?equipeId=...` (só os critérios ativos, na ordem).
  Sem modelo, a entidade não é avaliada naquela equipe, e o registro traz `"avaliacao": null`.
- O registro traz `"avaliacao": { "medias": { "qualidade": 4.3 }, "quantidade": 3, "minha": true }` (RN48).
- Avaliar ou editar: `PUT .../registros/{id}/avaliacoes/minha` com
  `{ "versao": "<só ao editar>", "respostas": { "qualidade": { "estrelas": 4, "comentario": "..." }, "observacoes": { "texto": "..." } } }`
  (**201** ao criar, **200** ao editar). Remover: `DELETE .../avaliacoes/minha`.
- Resumo e avaliações: `GET .../registros/{id}/avaliacoes` → `resumo` (médias, quantidades, distribuição e a sua), `resumosAnteriores`
  (modelos desativados) e `avaliacoes` (paginadas; **nulo para o EXTERNO**, RN47).
- EXTERNO não avalia (403); item não aprovado: **409** `REGISTRO_NAO_APROVADO` (RN44).

## Usuários e equipes (administração)

Só o **ADMINISTRADOR** (403 para os demais, RN26). Usuários e equipes nunca são excluídos, só desativados (RN31).

| Para | Chame |
|---|---|
| Listar usuários | `GET /api/v1/usuarios?busca=&perfil=&equipeId=&ativo=&ordenacao=&pagina=&tamanho=` |
| Criar usuário | `POST /api/v1/usuarios` com `{ matricula, nome, perfil, equipes: [ids] }` → **201** com `{ usuario, senhaProvisoria }` |
| Alterar (nome, perfil, equipes; a matrícula não muda) | `PUT /api/v1/usuarios/{id}` |
| Ativar ou desativar | `PATCH /api/v1/usuarios/{id}/ativacao` com `{ "ativo": false }` |
| Nova senha provisória | `POST /api/v1/usuarios/{id}/redefinir-senha` → `{ senhaProvisoria }` |
| Equipes | `GET /api/v1/equipes?busca=&ativa=`, `GET {id}`, `POST`, `PUT {id}`, `PATCH {id}/ativacao` com `{ "ativa": false }` |
| Histórico | `GET /api/v1/usuarios/{id}/historico`, `GET /api/v1/equipes/{id}/historico` |

- `busca` (usuários: na matrícula **ou** no nome; equipes: no nome) é "contém", sem diferenciar maiúsculas nem acentos, até 100 caracteres.
- `ordenacao` de usuários: `nome` (padrão), `matricula`, `ultimoAcessoEm` ou `criadoEm`; `-` na frente = decrescente.
- Cada equipe traz `membrosAtivos`: quantos usuários **ativos** pertencem a ela. Os membros são `GET /api/v1/usuarios?equipeId=<id>`.
- Cada equipe traz também `listagens`: `[{ chave, nome, ativo }]`, as listagens em que ela está habilitada (RN90). Equipe nova não é
  habilitada em nenhuma (RN93): habilite-a em `PUT /api/v1/admin/metadados/grupos/{grupo}/equipes`
  ([administração de metadados](administracao-de-metadados.md#equipes-habilitadas)).
- A **senha provisória** vem uma única vez, na resposta da criação ou da redefinição (RN25). Mostre-a ao administrador e
  descarte-a: ela não pode ser recuperada depois (ADR 0041, no front).
- Recusas: 409 `ULTIMO_ADMINISTRADOR` (desativar ou rebaixar o último administrador ativo, RN30), 409 `USUARIOS_SEM_EQUIPE`
  (desativar a única equipe ativa de algum usuário ativo, com `detalhes.quantidade`, RN31), 409 `CONFLITO_UNICIDADE` com
  `detalhes.campos` (`["matricula"]` ou `["nome"]`).

## Exportação

`GET /api/v1/grupos/{grupo}/entidades/{entidade}/registros/exportar?formato=csv|xlsx&<os mesmos filtros e ordenação da listagem>`

- Exporta **o que o usuário vê na listagem**, com os mesmos filtros, ordenação e regras de visibilidade (equipe, aprovação e perfil),
  sem paginação (`pagina` e `tamanho` são ignorados).
- Colunas: os campos ativos na ordem dos metadados, com o **nome** do campo no cabeçalho (referências pelo rótulo) e, no fim,
  Equipe, Situação de aprovação (se a aprovação está ativa), "Média: &lt;critério&gt;" (se há modelo de avaliação ativo),
  Criado por, Criado em e Atualizado em. Datas e horas no fuso de São Paulo (configurável no servidor).
- **CSV**: UTF-8 com BOM, separador `;` e vírgula decimal (abre direto no Excel em português). **XLSX**: cabeçalho em negrito,
  filtro automático, números e datas com o tipo certo, URLs como hiperlink.
- Textos que começam com `=`, `+`, `-`, `@`, tab ou CR recebem um apóstrofo no início (proteção contra injeção de fórmula).
- O nome do arquivo vem no cabeçalho `Content-Disposition` (exposto no CORS): `<entidade>_<aaaa-mm-dd>.<formato>`.
- Mais de 10.000 linhas (configurável): **400** `EXPORTACAO_MUITO_GRANDE`. Peça mais filtros ao usuário.
- Para baixar no navegador, faça o GET com o token (via `fetch`), leia o corpo como `Blob` e use o nome do `Content-Disposition`.

## Importação por planilha

Cadastra muitos itens de um tipo de item a partir de um XLSX ou CSV, com as mesmas regras do cadastro manual (RN97 a RN104): baixar o
modelo, enviar o arquivo, ver a **prévia** linha a linha (nada é gravado), executar numa transação, baixar o relatório e, se preciso,
desfazer. Referências vão pelo **nome** do item (ou pela coluna `<campo> (ID)`). Rotas, corpos e respostas em [`importacao.md`](importacao.md).
O histórico dos registros traz `origem: "IMPORTACAO"` nas linhas gravadas por uma importação (nulo no cadastro manual).
