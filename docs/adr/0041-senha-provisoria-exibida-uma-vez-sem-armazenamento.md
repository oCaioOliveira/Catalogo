# 0041. Senha provisória exibida uma vez, sem armazenamento no front

- Status: Aceito
- Data: 2026-10-22

## Contexto

Ao criar um usuário ou redefinir a senha dele, a API gera uma **senha provisória** e a devolve uma única vez (RN25): no banco fica só o
hash, e nenhum endpoint a devolve de novo. Agora a administração de usuários tem tela no front, e o administrador precisa ver a senha
para enviá-la à pessoa. Qualquer cópia da senha no navegador (estado global, `localStorage`, `sessionStorage`, URL, log, histórico do
navegador) poderia vazar depois, por exemplo numa máquina compartilhada, numa extensão do navegador ou num print do console.

## Decisão

- A senha vai da resposta da API **direto** para um diálogo (`SenhaProvisoriaDialogComponent`), que a guarda num **signal local** do
  componente, e só enquanto está aberto. O serviço só a devolve a quem chamou; nada a guarda no caminho.
- O diálogo mostra a senha, um botão **Copiar** (área de transferência) e o aviso: "Esta senha não será exibida novamente. Envie ao
  usuário por um canal seguro. Ele deverá trocá-la no primeiro acesso."
- O diálogo **não fecha** clicando fora nem com Esc: só por **"Já copiei"**, que apaga a senha do signal e fecha.
- A senha **nunca** vai para estado global (serviços `providedIn: 'root'`, signals de página), `localStorage`, `sessionStorage`, URL,
  `console` ou toast. Testes unitários conferem o descarte ao fechar e a ausência nos armazenamentos; o E2E confere que a senha não
  fica na página nem aparece no histórico.

### Alternativas descartadas

- **Mostrar num toast ou na página de detalhe**: o toast some sozinho (o administrador pode não copiar a tempo); a página de detalhe
  manteria a senha visível e no estado da tela.
- **Guardar para "mostrar de novo"**: contraria a RN25 (uma vez) e cria uma cópia que pode vazar.
- **Enviar por e-mail pelo sistema**: fora do escopo (não há serviço de e-mail); fica para quando houver pedido real.

## Consequências

- ✅ A senha existe no front só enquanto o diálogo está aberto; fechar é descartar.
- ✅ Se o administrador perder a senha antes de enviar, basta redefinir de novo (a anterior deixa de valer).
- ⚠️ Fora de `localhost`/HTTPS o navegador pode negar a área de transferência: o botão Copiar não funciona, e o texto continua
  selecionável no diálogo.
