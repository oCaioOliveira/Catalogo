# 0004. Sem MediatR, AutoMapper e FluentAssertions 8+

- Status: Aceito
- Data: 2026-10-21

## Contexto

- **MediatR** (a partir da v13), **AutoMapper** (a partir da v15) e **FluentAssertions** (a partir da v8)
  passaram a ter **licença comercial**. **MassTransit** também anunciou licença comercial para a v9.
- Para um time júnior, mediator e mapeamento automático escondem o fluxo: não dá para seguir o
  código com "ir para definição", e erros de mapeamento só aparecem em tempo de execução.

## Decisão

- Casos de uso são **classes simples** injetadas direto no controller (ex.: `CriarRegistro`).
- O mapeamento entidade ↔ DTO é **manual**, com métodos de extensão (`Application/Mapeamentos`).
- Asserts com o próprio **xUnit**. Shouldly é permitido se um dia for útil.
- Proibidos: `MediatR`, `AutoMapper`, `MassTransit` e `FluentAssertions` ≥ 8. O teste
  `PacotesTests` falha se algum aparecer em `Directory.Packages.props` ou num `.csproj`.

## Consequências

- ✅ O fluxo é explícito e navegável, e sem custo de licença.
- ✅ Erros de mapeamento aparecem na compilação.
- ⚠️ Um pouco mais de código repetitivo nos mapeamentos.
- ⚠️ Comportamentos transversais (log, transação) ficam explícitos no caso de uso ou em middleware, em vez de pipeline behaviors.
