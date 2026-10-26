# Catalogo

Sistema genérico de cadastro guiado por **metadados**: grupos, entidades e campos ficam no banco,
e um motor genérico valida, grava, lista e exclui os registros. O primeiro uso é a listagem de
[Testes de Desempenho](docs/listagens/testes-desempenho.md). **Listagens novas são criadas pela interface**, pelo administrador
(Administração → Listagens), sem scripts: veja o guia [Criar uma listagem](docs/guias/criar-uma-listagem.md).

## Organização do repositório

| Pasta | Conteúdo | Como começar |
|---|---|---|
| [`backend/`](backend/) | API .NET 8 e testes | [`backend/README.md`](backend/README.md) |
| [`database/`](database/) | scripts do schema e SQL Server local em Docker | [`database/README.md`](database/README.md) |
| [`docs/`](docs/) | arquitetura, modelo de dados, regras de negócio, listagens e ADRs | [`docs/arquitetura.md`](docs/arquitetura.md) |
| [`frontend/`](frontend/) | aplicação web em Angular 20 + PrimeNG, com as telas montadas pelos metadados | [`frontend/README.md`](frontend/README.md) |

## Primeiros passos

1. Suba o banco local: [`database/README.md`](database/README.md).
2. Configure e rode a API: [`backend/README.md`](backend/README.md).
3. Rode o front: [`frontend/README.md`](frontend/README.md).

## Documentação

- [Arquitetura](docs/arquitetura.md)
- [Modelo de dados](docs/modelo-de-dados.md)
- [Regras de negócio (RNxx)](docs/regras-de-negocio.md)
- [Decisões de arquitetura (ADRs)](docs/adr/README.md)
- [Guia do administrador: criar uma listagem](docs/guias/criar-uma-listagem.md)
- 🤖 Instruções para agentes de IA (e para quem está chegando): [`AGENTS.md`](AGENTS.md), mais um `AGENTS.md` em cada pasta.
