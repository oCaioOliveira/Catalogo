# AGENTS.md — Catalogo (back-end)

Instruções para agentes de IA e para pessoas que trabalham neste repositório. Este arquivo é só o **mapa**. Cada pasta tem o seu
próprio `AGENTS.md`, que **complementa** este: leia sempre o arquivo mais próximo do código que você vai mudar. O conhecimento do
projeto (o "porquê") fica em [`docs/`](docs/), e os AGENTS.md apontam para lá em vez de copiar.

## O que é o Catalogo

Sistema genérico de cadastro: qualquer tipo de item pode ser cadastrado a partir de **metadados**
guardados no banco (Grupo → Entidade → Campo; na tela, área de gestão → tipo de item → campo). Um motor genérico valida, grava, lista e exclui os
registros. O primeiro uso é a área de gestão Testes de Desempenho. O banco é SQL Server **externo** às
aplicações e o deploy é manual, on premise.

Este repositório tem a **API** e o **banco**. A aplicação web (Angular 20 + PrimeNG, telas montadas pelos metadados) fica no
**repositório do front-end**, com o seu próprio `AGENTS.md` e `docs/` ([ADR 0051](docs/adr/0051-repositorios-separados-back-e-front.md)).

## Mapa do repositório

| Pasta | O que tem | Leia antes de mexer |
|---|---|---|
| [`backend/`](backend/) | API .NET 8 (Clean Architecture enxuta) e os seus testes. O contrato da API fica em `backend/openapi/v1.json` | [`backend/AGENTS.md`](backend/AGENTS.md) e o AGENTS.md do projeto em `backend/src/` ou `backend/tests/` |
| [`database/`](database/) | scripts versionados do schema (`V00x`), SQL Server local em Docker, **pacotes de entrega para os DBAs** (`entrega/`) e o gerador de carga de áreas (`ferramentas/`). Tudo no schema **`cat`**, com os nomes do padrão da CAIXA (`CATTB0xx`, [ADR 0052](docs/adr/0052-padrao-de-nomes-caixa-e-sql-server-2014.md)) | [`database/AGENTS.md`](database/AGENTS.md) |
| [`docs/`](docs/) | o que vale para a API e para o front: arquitetura, modelo de dados, segurança, regras (RNxx), áreas de gestão, ADRs e a **fonte** do [glossário](docs/glossario.md) | [`docs/arquitetura.md`](docs/arquitetura.md) |
| [`docs/api/`](docs/api/) | guia para quem **consome** a API (front-end): autenticação, paginação, registros, exportação e códigos de erro. Contrato em `backend/openapi/v1.json` | [`docs/api/README.md`](docs/api/README.md) |

No repositório do front-end ficam: como a tela se monta (`docs/frontend.md`), o guia de interface e a revisão de usabilidade
(`docs/design/`), os guias do usuário com capturas de tela (`docs/guias/`), os ADRs só do front e as cópias do glossário e do
contrato (`openapi/v1.json`).

## Regras que valem para o repositório todo

1. **Regras de negócio têm id** (`RNxx`) em [`docs/regras-de-negocio.md`](docs/regras-de-negocio.md).
   Back-end e front-end citam o mesmo id. Regra nova recebe o próximo número; regra mudada é
   atualizada no mesmo PR.
2. **Decisões de arquitetura viram ADR** em [`docs/adr/`](docs/adr/README.md). ADR aceito não é editado: é substituído por um novo.
   A numeração é **única** com o repositório do front-end: veja o último número nos dois antes de criar um.
3. **Áreas de gestão (metadados) são criadas e alteradas pela interface** (Configurações → Áreas de gestão, guia
   `docs/guias/criar-uma-listagem.md` no repositório do front-end) ou pela API, pelo administrador
   ([`docs/api/administracao-de-metadados.md`](docs/api/administracao-de-metadados.md)); **o schema do banco só muda por script** em `database/scripts/` ([ADR 0003](docs/adr/0003-schema-por-scripts-sql.md)), sempre no schema `cat` com os nomes do padrão da CAIXA ([ADR 0052](docs/adr/0052-padrao-de-nomes-caixa-e-sql-server-2014.md)), e chega à produção como um **pacote de entrega** para os DBAs (`database/entrega/`, [ADR 0049](docs/adr/0049-schema-catalogo-e-pacotes-de-entrega.md)).
   Uma área de gestão nova só aparece para as equipes que o administrador habilitar nela (RN90 a RN93, [ADR 0046](docs/adr/0046-listagens-habilitadas-por-equipe.md)).
4. **Nunca coloque segredos em arquivos versionados**: senhas e connection strings ficam em
   user-secrets, em `database/.env` (fora do git) ou em variáveis de ambiente nos servidores.
5. **Domínio em português**: nomes, mensagens, documentação e testes.
6. **Na interface, sempre os termos da coluna "Na tela"** do [glossário](docs/glossario.md): "Área de gestão" (no código,
   `Grupo`), "Tipo de item" (`Entidade`), "Item" (`Registro`). O código, o banco e a API mantêm os nomes antigos. Vale para as
   mensagens da API que chegam à tela. O glossário daqui é a fonte; o front-end tem uma cópia, atualizada junto.
7. **Um caminho só para gravar itens**: a importação de planilhas (RN97 a RN104) grava pelo **mesmo** serviço do cadastro manual
   (`GravadorRegistro`), com as mesmas regras, sem atalhos ([ADR 0048](docs/adr/0048-importacao-pelo-mesmo-caminho-do-cadastro.md),
   guia do usuário em `docs/guias/importar-planilhas.md`, no repositório do front-end).
8. **Contexto para agentes de IA fica só em `AGENTS.md`** (padrão aberto agents.md). Não crie
   arquivos de contexto específicos de uma ferramenta.
9. **O contrato da API é consumido de outro repositório**: o front-end gera o cliente a partir de uma cópia do
   `backend/openapi/v1.json`. Mudança no contrato só aditiva na v1, e o front precisa ser avisado para atualizar a cópia.
