# Catálogo — Migração dos relatórios de testes de desempenho (SharePoint)

**Pacote:** `2026-10-28-migracao-sharepoint` · **Banco:** `CATDB001`, SQL Server 2014, schema `cat` ·
**Pré-requisitos:** área `testes_desempenho` carregada e **vazia** (pacote `2026-10-28-area-testes-desempenho`) e o usuário
**C161587** cadastrado. Detalhes da migração em [`docs/migracao-sharepoint.md`](../../../docs/migracao-sharepoint.md).

## Ordem de execução

| # | Arquivo | Tamanho | O que faz |
|---|---|---|---|
| 1 | `01_reconfigurar_area_testes_desempenho.sql` | 42 KB | Reconfigura a área (campos novos, tipos, unicidades), mantendo os GUIDs. Para se a área tiver algum item |
| 2 | `02_migracao_parte_01.sql` | 3,7 MB | Tabela de controle `cat.CATTB024_MIGRACAO_CONTROLE`, origem `MIGRACAO_SHAREPOINT` no histórico, equipe GEQTI habilitada na área, cadastros de apoio, projetos, endpoints e RTCs (2.635 itens) |
| 3 | `02_migracao_parte_02.sql` a `_08.sql` | 4,5 a 4,6 MB cada | Relatórios completos, com cenários e links, do mais antigo ao mais recente |
| 4 | `02_migracao_parte_09.sql` | 0,5 MB | Últimos relatórios |
| 5 | `03_conferencia.sql` | 6 KB | Só consultas: compara o banco com o CSV e mostra `OK` ou `DIVERGÊNCIA` por linha |

As partes `02_*` ficam **fora do git** (dados de produção); gere de novo pelo gerador se precisar (seção "Gerar de novo").

- Cada parte roda numa **transação própria**: em caso de erro, nada daquela parte fica gravado.
- Rodar de novo uma parte já aplicada não faz nada (`já aplicada. Nada a fazer.`). Rodar fora de ordem para com uma mensagem
  dizendo qual parte falta.
- A última mensagem de cada parte é `parte NN/09: concluída (...)`. A do `03` é `OK: o banco confere com o CSV.` (0 divergências).

## Como executar

No SSMS: abrir cada arquivo, conectado ao `CATDB001`, e executar inteiro (F5), na ordem acima.

**Se o SSMS travar nas partes de 4 a 5 MB**, use o `sqlcmd` (uma parte por vez, na ordem):

```bat
sqlcmd -S <servidor> -d CATDB001 -E -b -f 65001 -i 01_reconfigurar_area_testes_desempenho.sql
sqlcmd -S <servidor> -d CATDB001 -E -b -f 65001 -i 02_migracao_parte_01.sql
...
sqlcmd -S <servidor> -d CATDB001 -E -b -f 65001 -i 02_migracao_parte_09.sql
sqlcmd -S <servidor> -d CATDB001 -E -W -f 65001 -i 03_conferencia.sql
```

`-f 65001` é obrigatório (os arquivos são UTF-8, com acentos). `-b` faz o `sqlcmd` sair com erro se a parte falhar.
Use `-i <arquivo>`: passar o arquivo pela entrada padrão (`< arquivo`) quebra no BOM do UTF-8.
Na simulação local, cada parte levou de 0,5 a 10 segundos.

## O que conferir no sistema depois

Entrando como C161587:

1. **Testes de Desempenho → Relatórios de Teste**: 687 itens, com títulos como "API Consulta Campanha por Conta — 1ª bateria".
   Filtre por ambiente PRD e status Insatisfatório: 26 itens.
2. Abra um relatório e compare com o SharePoint: cenários, links, apontamentos e as observações dos cenários.
3. **Painel**, período personalizado de 01/07/2022 até hoje: relatórios por mês desde 2022 e vazão média por ambiente.
4. **Configurações → Equipes**: a equipe GEQTI existe e está habilitada na área. **Inclua nela os usuários** que precisam ver os
   relatórios (a migração não cria usuários nem associa ninguém à equipe).

## Gerar de novo

```bash
dotnet run --project backend/tools/Catalogo.Migracao -- gerar \
  --csv "$PWD/database/migracao/origem/<arquivo>.csv" \
  --scripts "$PWD/database/entrega/2026-10-28-migracao-sharepoint" \
  --relatorios "$PWD/database/migracao/saida" [--base-sharepoint https://caixa.sharepoint.com]
```

O gerador lê a configuração da área do banco local (já com o `V016`) e é determinístico: o mesmo CSV gera os mesmos GUIDs e os
mesmos scripts. O `01` é gerado por `database/ferramentas/gerar_carga_area.sh` (ver `docs/migracao-sharepoint.md`).

## Simulação local (`Simulacao_Migracao`)

`./database/migracao/simular_migracao.sh` recria o banco `Simulacao_Migracao` no Docker local com a estrutura atual da CAIXA (V014 +
V015 + C161587 gerado por `criar-admin --gerar-sql`), roda `01`, `02` (todas as partes) e `03` pelo `sqlcmd -i`, roda tudo de novo e
confere que nada mudou. Com `--manter`, não recria o banco.

Para apontar a API para a simulação (de dentro de `backend/`, com `<senha>` = `APP_DB_PASSWORD` do `database/.env`):

```bash
ConnectionStrings__Catalogo="Server=localhost,1433;Database=Simulacao_Migracao;User Id=listagem_app;Password=<senha>;Encrypt=True;TrustServerCertificate=True" \
  dotnet run --project src/Catalogo.Api --launch-profile Development
```

e o front como sempre (`npm start` no repositório do front). Entre como **C161587**, com a senha em
`database/.estado/senha-admin-simulacao-migracao` (fora do git). Para voltar ao banco de desenvolvimento, basta rodar a API sem a
variável.
