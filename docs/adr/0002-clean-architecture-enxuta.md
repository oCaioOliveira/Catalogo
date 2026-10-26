# 0002. Clean Architecture enxuta

- Status: Aceito
- Data: 2026-10-21

## Contexto

O time é júnior e o sistema tem um núcleo de regras (o motor genérico) que precisa ser bem testado
e isolado de banco e HTTP. As opções avaliadas foram:

- **Camadas tradicionais** (Controller → Service → Repository): simples, mas a regra tende a vazar
  para controllers e repositórios, e o domínio fica acoplado ao EF.
- **Vertical Slice**: ótima para muitas features independentes, mas aqui quase tudo passa pelo
  mesmo motor genérico, então as fatias repetiriam ou compartilhariam quase todo o código.
- **Clean Architecture "completa"**: com mediator, CQRS e muitos projetos, é cerimônia demais para o tamanho do sistema.

## Decisão

Clean Architecture **enxuta**, com quatro projetos: `Domain` (motor, sem dependências),
`Application` (casos de uso como classes simples e interfaces), `Infrastructure` (EF Core) e `Api`.
As dependências apontam para dentro e são verificadas por testes com NetArchTest.
Detalhes em [arquitetura.md](../arquitetura.md).

## Consequências

- ✅ O motor é testado sem banco e sem HTTP (`Domain.Tests` e `Application.Tests`).
- ✅ As regras de dependência quebram o build de testes se forem violadas.
- ⚠️ Existem dois modelos de metadados (Domain imutável e tabelas na Infrastructure), com conversão manual.
