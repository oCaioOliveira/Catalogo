# 0014. Perfil único por usuário, válido em todas as equipes dele

- Status: Aceito
- Data: 2026-10-21

## Contexto

Um usuário pode pertencer a várias equipes. Seria possível ter um perfil por vínculo (GESTOR na
equipe A, EXTERNO na B), mas isso complica a regra de permissão, as telas de administração e a
explicação para o time, sem uma necessidade concreta hoje.

## Decisão

- O perfil fica em `Usuario.Perfil` e é **único**: vale em todas as equipes do usuário (RN26).
- `UsuarioEquipe` guarda só o vínculo (usuário, equipe).

## Consequências

- ✅ Uma regra simples: "o que o perfil permite, nas suas equipes".
- ⚠️ Quem precisa de papéis diferentes em equipes diferentes não é atendido hoje.
- **Evolução possível**: mover o perfil para `UsuarioEquipe` (perfil por equipe), mantendo o
  ADMINISTRADOR como perfil do usuário. A `PoliticaPermissao` passaria a receber o perfil da equipe
  do registro, e o resto do código não muda.
