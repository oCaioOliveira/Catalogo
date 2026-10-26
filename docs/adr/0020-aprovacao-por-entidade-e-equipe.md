# 0020. Aprovação de registros ativável por entidade e por equipe

- Status: Aceito
- Data: 2026-10-21

## Contexto

Equipes externas precisam cadastrar itens (por exemplo, relatórios de teste), mas o que elas cadastram só
deve valer depois que um gestor da equipe conferir. Nem toda equipe quer esse controle, e nem toda entidade
precisa dele: numa mesma equipe, relatórios podem exigir aprovação e projetos não.

## Decisão

- A aprovação é ligada **por entidade e por equipe**, na tabela `ConfiguracaoEntidadeEquipe` (V004). Sem linha,
  está desligada. Só entidades por equipe ([ADR 0017](0017-escopo-global-ou-por-equipe-na-entidade.md)) podem ser configuradas.
- O GESTOR configura as suas equipes e o ADMINISTRADOR, qualquer uma (RN33).
- Com a aprovação ativa, o EXTERNO cria registros **PENDENTES** e altera os seus enquanto pendentes ou
  rejeitados; GESTOR e ADMINISTRADOR aprovam ou rejeitam com justificativa (RN34 a RN36).
- A situação fica numa **coluna do `Registro`**, não numa tabela à parte: o filtro de visibilidade (RN37) vira
  um `WHERE` simples na mesma consulta LINQ do escopo de equipe.
- As regras de quem pode o quê entram na **mesma `PoliticaPermissao`** da parte 1: ela recebe um `RegistroAlvo`
  com situação, autor e se a aprovação está ativa. As transições ficam em `FluxoAprovacao` (Domain).
- Só a **última** decisão fica no registro; as anteriores ficam no histórico, com a justificativa (RN32).

### Alternativas descartadas

- **Configuração global por entidade** (a mesma regra para todas as equipes): mais simples, mas obrigaria todas
  as equipes a aprovar só porque uma precisa.
- **Aprovação "com os filhos"** (aprovar o relatório aprova os cenários dele): exigiria percorrer as referências e
  decidir o que fazer com filhos rejeitados ou de outros autores. Fica para depois, se o negócio pedir.

## Consequências

- ✅ Cada equipe decide o seu nível de controle sem afetar as outras, e entidades sem aprovação não mudam nada.
- ✅ Uma política só para permissões: não há um segundo lugar de decisão para revisar.
- ⚠️ **Cada registro é aprovado individualmente.** Um relatório com 30 cenários gera 31 pendências. Para o
  autor conseguir montar o conjunto antes da aprovação, ele pode referenciar os **seus** registros não aprovados (RN38).
- ⚠️ Pendentes e rejeitados ocupam as chaves únicas (RN39): um rejeitado esquecido pode impedir um cadastro novo
  até ser corrigido ou excluído pelo gestor.
- ⚠️ Desativar a aprovação exige decidir os pendentes antes (409 com a quantidade).
