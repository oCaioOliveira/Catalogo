# 0003. Schema por scripts SQL versionados, sem migrations

- Status: Aceito
- Data: 2026-10-21

## Contexto

Produção é on premise, com deploy manual e banco **externo** à aplicação, administrado à parte.
Os scripts precisam ser revisados e aplicados à mão pelo DBA. Migrations do EF Core geram SQL
implícito, dependem da ferramenta `dotnet ef` e incentivam a aplicação a alterar o schema sozinha.

## Decisão

- O schema é definido por scripts numerados em `database/scripts/` (`V001__...sql`, `V002__...sql`).
  São os **mesmos** arquivos em desenvolvimento (aplicados pelo `db-init` do Docker) e em produção (aplicados manualmente).
- A tabela `dbo.__VersaoSchema` registra o que já foi aplicado.
- O EF Core **só mapeia** as tabelas existentes. É proibido usar migrations, `EnsureCreated`,
  `EnsureDeleted` e `Migrate` (o teste `SemMigrationsTests` falha se aparecerem).
- A aplicação usa o login `listagem_app`, que **não tem permissão** de alterar o schema.

## Consequências

- ✅ O que roda em produção é exatamente o SQL revisado.
- ✅ A aplicação não consegue alterar o schema, nem por engano.
- ⚠️ Mudança de schema tem dois passos: o script `V00x` e depois o mapeamento do EF, que precisa ser mantido em sincronia à mão.
- ⚠️ Script aplicado em produção é imutável: correção vira script novo.
