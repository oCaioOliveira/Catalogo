# 0022. Versionamento da API por prefixo de rota

- Status: Aceito
- Data: 2026-10-21

## Contexto

O front-end vai ser construído sobre a API, e depois dele podem vir outros clientes (integrações, scripts). Quando
o contrato precisar mudar de um jeito incompatível, os clientes antigos não podem quebrar sem aviso. Hoje só existe
uma versão, e o time é júnior: a solução precisa ser simples de entender e de seguir.

## Decisão

- Todas as rotas ficam sob **`/api/v1/...`**. O prefixo é a constante `RotasApi.V1`, usada em todo
  `[Route(RotasApi.V1 + "/...")]`. O `/health` fica **fora** da versão (é infraestrutura, não contrato).
- **Sem pacote de versionamento** (Asp.Versioning): com uma versão só, ele traria configuração e conceitos
  (versão por cabeçalho, por query, *sunset*) que ninguém usaria.
- O que **pode** mudar na v1: acrescentar campos na resposta, endpoints, parâmetros opcionais e códigos de erro novos.
  Os clientes devem ignorar campos que não conhecem.
- O que **quebra** o contrato e só pode ir para uma **v2**: remover ou renomear campo, mudar o tipo de um campo,
  mudar o status HTTP ou o `codigo` de um erro, tornar obrigatório algo que era opcional, mudar o significado de um valor.
- Quando existir a v2, ela convive com a v1 por um período combinado com os clientes (controllers em `RotasApi.V2`).

### Alternativas descartadas

- **Versão por cabeçalho** (`Api-Version: 1`) ou por *media type*: invisível na URL e nos logs, mais difícil de
  testar no navegador e no Swagger.
- **Pacote Asp.Versioning**: vale a pena quando houver mais de uma versão ao mesmo tempo. Pode entrar num ADR novo.
- **Sem versão**: qualquer mudança incompatível quebraria o front em produção.

## Consequências

- ✅ A versão aparece em toda URL, log e erro: fica claro o que o cliente está usando.
- ✅ A regra do que quebra o contrato está escrita, e o teste do contrato OpenAPI (ADR 0024) mostra a mudança no PR.
- ⚠️ Uma v2 exigirá duplicar os controllers que mudarem (ou uma convenção de rota). Fica para quando ela existir.
