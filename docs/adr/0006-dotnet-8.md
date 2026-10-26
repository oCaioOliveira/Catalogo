# 0006. .NET 8, com migração pendente para o .NET 10

- Status: Aceito
- Data: 2026-10-21

## Contexto

Os servidores on premise têm hoje o runtime do **.NET 8** (LTS). O suporte oficial do .NET 8
**termina em 10/11/2026**. O .NET 10 (LTS) já está disponível, mas ainda não foi instalado nos servidores.

## Decisão

- O projeto usa **.NET 8** (`net8.0`). O `global.json` fixa o SDK 8.0.x e os pacotes `Microsoft.*` ficam na linha 8.0.x.
- Swashbuckle gera o OpenAPI, porque a geração nativa do ASP.NET Core só chega no .NET 9.
- A **migração para o .NET 10 fica pendente** até o runtime estar disponível nos servidores.

## Consequências

- ⚠️ Depois de 10/11/2026 o .NET 8 deixa de receber correções de segurança. Na data deste ADR
  faltam **menos de três semanas**: a instalação do runtime .NET 10 nos servidores é urgente.
- A migração envolve:
  - trocar `TargetFramework` no `Directory.Build.props`;
  - atualizar o `global.json` e os pacotes `Microsoft.*` para 10.x;
  - avaliar trocar o Swashbuckle pelo `Microsoft.AspNetCore.OpenApi` nativo;
  - rodar `dotnet test`.
