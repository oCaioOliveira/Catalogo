# 0024. Contrato OpenAPI versionado e verificado por teste

- Status: Aceito
- Data: 2026-10-21

## Contexto

O front vai depender do formato exato das respostas. Com o código gerando o OpenAPI (Swashbuckle), uma mudança no
contrato (um campo renomeado num DTO, um status trocado num controller) passa despercebida na revisão: o diff mostra
C#, não o efeito na API. Também queremos um arquivo estável para gerar tipos e clientes no front.

## Decisão

- O documento OpenAPI gerado fica **salvo no repositório**, em [`backend/openapi/v1.json`](../../backend/openapi/v1.json).
- O teste `ContratoOpenApiTests` gera o documento (pelo `ISwaggerProvider`, sem subir HTTP) e **falha se ele for diferente
  do arquivo salvo**, com a instrução de como atualizar:
  `ATUALIZAR_OPENAPI=1 dotnet test --filter "FullyQualifiedName~ContratoOpenApiTests"`. A atualização é sempre
  consciente: quem muda o contrato regenera o arquivo e o diff dele vai no PR, para o revisor ver.
- O mesmo teste confere que **todo endpoint declara** as respostas: sucesso, 401 e 500 (e 400 e 404 quando cabem),
  e que todo erro é um ProblemDetails em `application/problem+json`. As respostas comuns (401, 403, 500) são declaradas
  uma vez, como filtros globais (`ConfiguracaoSwagger.DeclararRespostasComuns`); as demais, em cada ação.
- O documento descreve o JWT (esquema Bearer), os campos `codigo`, `traceId` e `detalhes` dos erros, e o corpo
  dinâmico dos registros como objeto com propriedades adicionais, com exemplos da listagem de Testes de Desempenho.

### Alternativas descartadas

- **Escrever o OpenAPI à mão** (design-first): o documento e o código divergiriam, e o time teria dois lugares para manter.
- **Gerar o arquivo no build** (CLI do Swashbuckle): adiciona ferramenta e passo de build; o teste já gera e confere.
- **Não salvar o arquivo**: o front teria que subir a API para ler o contrato, e as mudanças não apareceriam no PR.

## Consequências

- ✅ Toda mudança de contrato aparece no PR, no diff de `openapi/v1.json`.
- ✅ O front pode gerar tipos a partir de um arquivo versionado junto com o código.
- ⚠️ Qualquer mudança em comentário XML de controller (que vira descrição no OpenAPI) também exige regenerar o arquivo.
- ⚠️ O documento depende da ordem em que o Swashbuckle encontra os controllers: se uma atualização do pacote mudar a
  ordem, o arquivo muda sem mudança de contrato. Basta regenerar e conferir.
