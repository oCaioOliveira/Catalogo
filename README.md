# Catalogo — back-end

Sistema genérico de cadastro guiado por **metadados**: grupos, entidades e campos ficam no banco,
e um motor genérico valida, grava, lista e exclui os registros. O primeiro uso é a listagem de
[Testes de Desempenho](docs/listagens/testes-desempenho.md). **Listagens novas são criadas pela interface**, pelo administrador
(Administração → Listagens), sem scripts: o guia "Criar uma listagem" fica no repositório do front-end
(`docs/guias/criar-uma-listagem.md`).

Este repositório tem a API e o banco. A aplicação web (Angular 20 + PrimeNG) fica no **repositório do front-end**
([ADR 0051](docs/adr/0051-repositorios-separados-back-e-front.md)).

## Organização do repositório

| Pasta | Conteúdo | Como começar |
|---|---|---|
| [`backend/`](backend/) | API .NET 8 e testes | [`backend/README.md`](backend/README.md) |
| [`database/`](database/) | scripts do schema e SQL Server local em Docker | [`database/README.md`](database/README.md) |
| [`docs/`](docs/) | arquitetura, modelo de dados, regras de negócio, guia da API, listagens e ADRs | [`docs/arquitetura.md`](docs/arquitetura.md) |

## Primeiros passos

1. Suba o banco local: [`database/README.md`](database/README.md).
2. Configure e rode a API: [`backend/README.md`](backend/README.md).
3. Rode o front: `README.md` do repositório do front-end.

## Documentação

- [Arquitetura](docs/arquitetura.md)
- [Modelo de dados](docs/modelo-de-dados.md)
- [Regras de negócio (RNxx)](docs/regras-de-negocio.md)
- [Guia da API (para quem consome)](docs/api/README.md)
- [Decisões de arquitetura (ADRs)](docs/adr/README.md)
- [Glossário](docs/glossario.md)
- 🤖 Instruções para agentes de IA (e para quem está chegando): [`AGENTS.md`](AGENTS.md), mais um `AGENTS.md` em cada pasta.
