# Importação de itens por planilha

Cadastra muitos itens de **um tipo de item** de uma vez, a partir de um XLSX ou CSV, com **as mesmas regras do cadastro manual**:
tipos, obrigatórios, opções, referências, dependências, unicidade, equipes, permissões e aprovação. Cada linha passa pelo mesmo
`GravadorRegistro` do `POST`/`PUT` de registros: não há caminho paralelo de gravação. Regras: RN97 a RN104 em
[`regras-de-negocio.md`](../regras-de-negocio.md#rn97--importação-de-planilhas-fluxo-e-permissão). Guia para quem usa a tela:
[`guias/importar-planilhas.md`](../guias/importar-planilhas.md).

Todas as rotas ficam sob `/api/v1/grupos/{grupo}/entidades/{entidade}`:

| Método | Rota | O quê |
|---|---|---|
| GET | `importacao/modelo` | o modelo XLSX do tipo de item (só para quem pode importar) |
| POST | `importacoes` | envia o arquivo (`multipart/form-data`, campo `arquivo`): **201** com as colunas e a sugestão de mapeamento |
| POST | `importacoes/{id}/validar?pagina=&tamanho=&somenteComErro=` | a **prévia**: simula tudo, sem gravar nenhum item |
| GET | `importacoes/{id}?pagina=&tamanho=&somenteComErro=` | a prévia (ou o resultado) já calculada, para paginar e filtrar |
| POST | `importacoes/{id}/executar` | grava exatamente o que a prévia mostrou, numa transação |
| GET | `importacoes/{id}/relatorio` | XLSX com as linhas originais e as colunas "Resultado" e "Mensagens" |
| POST | `importacoes/{id}/desfazer` | remove os itens **criados** pela importação |
| GET | `importacoes?pagina=&tamanho=` | o histórico de importações do tipo de item, das mais recentes para as mais antigas |

## Fluxo

1. **Modelo** (opcional, mas recomendado): `GET .../importacao/modelo` → `modelo_<entidade>.xlsx`. Aba de dados com o **nome** dos campos
   ativos na ordem do formulário (`*` nos obrigatórios), coluna `Equipe` no fim se o tipo de item é por equipe e o usuário tem mais de
   uma equipe na área de gestão, listas suspensas (LISTA, BOOLEANO e referências com até 1.000 itens visíveis, numa aba oculta),
   formatos de data e número e a aba **Instruções**.
2. **Envio**: `POST .../importacoes` com o arquivo. Limites: **5 MB**, **5.000 linhas**, **100 colunas** (400 em `errors.arquivo`, com a
   mensagem para mostrar). O tipo é conferido pelo **conteúdo** (`.xlsx` precisa ser um pacote do Excel; `.xls` antigo e arquivos com
   senha são recusados com explicação), e um XLSX que descompactado passa de 100 MB é recusado. Resposta:
   ```json
   { "id": "…", "nomeArquivo": "relatorios.xlsx", "formato": "XLSX", "situacao": "ENVIADA", "totalLinhas": 20,
     "colunas": [{ "indice": 0, "cabecalho": "Projeto *", "exemplos": ["Transferências Alfa", "…"], "destino": { "tipo": "CAMPO", "campo": "projeto" } }],
     "destinos": [{ "tipo": "CAMPO", "campo": "projeto", "rotulo": "Projeto", "obrigatorio": true },
                  { "tipo": "CAMPO_ID", "campo": "projeto", "rotulo": "Projeto (ID)", "obrigatorio": false },
                  { "tipo": "EQUIPE", "campo": null, "rotulo": "Equipe", "obrigatorio": false }] }
   ```
   A sugestão compara o cabeçalho com o **nome** e a **chave** dos campos, sem diferenciar maiúsculas e acentos e ignorando o `*`.
   `destinos` é o que o select de cada coluna oferece (mais "Ignorar esta coluna", `tipo: "IGNORAR"`).
3. **Prévia**: `POST .../importacoes/{id}/validar`:
   ```json
   { "mapeamento": [{ "indice": 0, "destino": { "tipo": "CAMPO", "campo": "projeto" } }, { "indice": 5, "destino": { "tipo": "IGNORAR" } }],
     "opcoes": { "duplicados": "PULAR", "linhasComErro": "SO_VALIDAS", "equipePadraoId": "…" } }
   ```
   - `duplicados`: `PULAR` (a linha é ignorada) ou `ATUALIZAR` (altera o item que já existe, achado pelas restrições únicas do tipo de
     item; exige permissão de alterar, conferida linha a linha). Ao atualizar, as colunas **mapeadas** substituem os valores (célula vazia
     apaga, como no `PUT`); os campos sem coluna ficam como estão.
   - `linhasComErro`: `SO_VALIDAS` ou `TUDO_OU_NADA` (com erro, nada é importado).
   - `equipePadraoId`: a equipe das linhas sem a coluna Equipe. Obrigatória para o administrador e para quem tem várias equipes na área
     de gestão (RN27), como o `equipeId` do `POST`.

   Resposta (também a do `GET .../importacoes/{id}`):
   ```json
   { "importacao": { "id": "…", "situacao": "VALIDADA", "totais": { … }, "podeDesfazer": false, … },
     "totais": { "novos": 16, "atualizacoes": 0, "ignorados": 1, "comErro": 3, "total": 20 },
     "executavel": true,
     "cabecalhos": ["Projeto *", "RTC *", "…"],
     "mapeamento": [ … ], "opcoes": { … },
     "linhas": { "itens": [{ "numero": 3, "situacao": "ERRO", "valores": ["Projeto X", "…"],
                             "erros": [{ "coluna": 0, "campo": "projeto", "mensagem": "Nenhum item de 'Projeto' com o nome 'Projeto X' …" }],
                             "referencias": [{ "coluna": 2, "campo": "ambiente", "id": "…", "nome": "Homologação" }],
                             "registroId": null }],
                 "pagina": 1, "tamanho": 20, "total": 20, "totalPaginas": 1 } }
   ```
   `numero` é a linha da planilha (o cabeçalho é a 1); `coluna` é o `indice` da célula (nula = erro da linha inteira, como "repetida no
   arquivo", ou de um campo sem coluna, como um obrigatório não mapeado). `situacao`: `NOVA`, `ATUALIZACAO`, `IGNORADA` ou `ERRO`.
   `executavel` é falso quando não há nada a gravar ou quando a opção é `TUDO_OU_NADA` e há erros. Mapeamento inválido (coluna que não
   existe, campo inativo, dois destinos iguais, Equipe num cadastro de apoio): 400 com `errors` por `mapeamento[i]...` e `opcoes...`.
4. **Execução**: `POST .../importacoes/{id}/executar` → 200 com o resumo (`situacao: "CONCLUIDA"`). Simula de novo e, se o resultado de
   cada linha for o mesmo da prévia, grava todos os itens e o histórico (o `CorrelacaoId` da importação, com `origem: "IMPORTACAO"`)
   numa transação. Se algo mudou (um item referenciado foi excluído, outro igual foi criado…): **409** `IMPORTACAO_DADOS_MUDARAM`, nada é
   gravado e a prévia guardada já é a nova: recarregue-a com o `GET` e peça outra confirmação.
5. **Desfazer** (opcional): `POST .../importacoes/{id}/desfazer` → 200 (`situacao: "DESFEITA"`). Remove só os itens **criados** (as
   atualizações ficam), pelo mesmo plano de exclusão da exclusão manual (RN16). Precisa poder excluir cada item (GESTOR e ADMINISTRADOR).
   Se algum item criado foi alterado depois (inclusive aprovado, rejeitado ou avaliado) ou se um item de fora aponta para eles: **409**
   `IMPORTACAO_NAO_PODE_SER_DESFEITA`, com `detalhes.impedimentos`.

## Referências pelo nome (RN99)

A coluna de um campo de referência traz o **nome** do item: o valor do **campo de exibição** dele (ex.: "Transferências Alfa" para Projeto).

- O nome é comparado sem diferenciar maiúsculas e acentos (números pelo valor), **só entre os itens que o usuário vê** e que a linha pode
  referenciar: da mesma equipe da linha ou cadastros de apoio (RN29), aprovados ou do próprio autor (RN38).
- Não encontrou: erro na célula. Encontrou mais de um: erro pedindo a coluna `<campo> (ID)`, que traz o id do item (vale mais que o nome).
- Com dependência (RN08), as referências de que o campo depende são resolvidas antes, e entre itens de mesmo nome vale o que atende à
  dependência (ex.: o endpoint do projeto do relatório).
- As consultas são **em lote**: uma por tipo de item referenciado, não uma por linha.

## Valores das células (RN100)

| Tipo | Aceita |
|---|---|
| Número | vírgula ou ponto decimal; ponto como milhar quando há vírgula decimal (`1.234,56`); nos inteiros o ponto é sempre milhar (`1.200`) |
| DATA | `dd/mm/aaaa`, `aaaa-mm-dd` ou a data do Excel |
| DATA_HORA | `dd/mm/aaaa hh:mm[:ss]`, ISO (sem fuso = o fuso de negócio, `Exportacao:FusoHorario`) ou a data e hora do Excel |
| BOOLEANO | Sim/Não, Verdadeiro/Falso, true/false, 1/0 |
| LISTA | a opção sem diferenciar maiúsculas e acentos; grava o texto oficial |
| URL, textos | o texto (validado como no cadastro manual) |

Célula vazia = não informado. Do XLSX vale o **valor** da célula (o resultado de uma fórmula, nunca a fórmula), da primeira aba que não
se chama "Instruções". O CSV precisa estar em UTF-8 (com ou sem BOM); o separador `;` ou `,` é detectado pelo cabeçalho.

## Quem pode (RN97)

- **Importar** (modelo, envio, prévia, execução) = poder **criar** itens no tipo de item: cadastros de apoio, só o ADMINISTRADOR; o
  EXTERNO só onde a aprovação está ativa, e os itens nascem PENDENTES. Sem permissão: 403.
- A prévia e a execução são de quem enviou o arquivo (403 para quem só vê a importação).
- **Ver** (histórico, prévia, relatório): cada um as próprias; o GESTOR também as de quem é das suas equipes; o ADMINISTRADOR todas.
  Importação que o usuário não vê: 404 `IMPORTACAO_NAO_ENCONTRADA`.

## Histórico de importações e limpeza (RN104)

`GET .../importacoes` → `PaginaDto` de `{ id, nomeArquivo, formato, situacao, criadoEm, concluidaEm, desfeitaEm, usuario: { id, matricula, nome },
totais, arquivoDisponivel, podeDesfazer }`. O arquivo enviado fica no banco por **7 dias**; depois a limpeza automática o apaga (o registro
da importação fica) e marca como `EXPIRADA` as que nunca foram executadas. Sem o arquivo, a prévia e o relatório respondem 409
`IMPORTACAO_ARQUIVO_EXPIRADO`. A limpeza roda a cada `Importacao:IntervaloLimpezaMinutos` (padrão 60; 0 desliga).

## Downloads

O modelo e o relatório saem como `ArquivoExportadoResult` (como a exportação): faça o GET com o token via `fetch`, leia como `Blob` e use o
nome do `Content-Disposition`.
