# 0054. Login pela autenticação integrada do Windows (AD), sem senha no Catálogo

- Status: Aceito
- Data: 2026-11-05
- Substitui: [0016](0016-passwordhasher-sem-identity.md) (hash de senha) e, em parte, [0015](0015-jwt-com-provedor-de-identidade-isolado.md)
  (o provedor de identidade local por matrícula e senha) e [0053](0053-configuracao-do-deploy-caixa-e-chave-jwt-no-banco.md) (o `*` no `CORS_ALLOW`).
  No front-end, substitui o ADR 0041 (senha provisória exibida uma vez).

## Contexto

Na CAIXA, as aplicações internas identificam a pessoa pelo login do Windows (Active Directory), sem pedir senha: é o que as aplicações
GIC já fazem, hospedadas no IIS com a Autenticação do Windows ligada. O Catálogo tinha login próprio por matrícula e senha (hash
PBKDF2, senha provisória com troca obrigatória, bloqueio por tentativas e limite de logins por minuto). O resto da API já usava um JWT.

## Decisão

- **Um endpoint de login com autenticação Windows**: `POST /api/v1/auth/login`, sem corpo, exige o esquema **Negotiate**
  (`Microsoft.AspNetCore.Authentication.Negotiate`, política `ConfiguracaoSeguranca.PoliticaLoginWindows`). No IIS, o Negotiate
  repassa a autenticação para o IIS; no Kestrel (Windows), faz Kerberos/NTLM ele mesmo. O resto da API continua só com o JWT.
- **A matrícula vem do nome do Windows**: `DOMINIO\c161587` → `C161587` (`MatriculaWindows`, Domain; também aceita `c161587@dominio`).
  O endpoint devolve **o mesmo JWT de antes** (claims `sub`, `matricula`, `perfil`; chave do banco, ADR 0053).
- **Matrícula ainda não cadastrada vira um usuário EXTERNO, ativo e sem equipe** (RN105), com a linha de criação no histórico. Perfil e
  equipes continuam dados pelo administrador do Catálogo. Usuário desativado recebe 403 no login.
- **Sem senha**: saem o hash, a troca obrigatória, o bloqueio por tentativas, o limite de logins por minuto, a troca de senha em
  "Minha conta" e os endpoints `POST /auth/trocar-senha` e `POST /usuarios/{id}/redefinir-senha`. O cadastro pela administração é só
  pela matrícula. O comando `criar-admin` não pede senha (e promove a ADMINISTRADOR uma matrícula que já entrou como EXTERNO).
  As colunas de senha saem do banco no `V019` (pacote `entrega/2026-11-05-login-windows`).
- **CORS com credenciais**: o login é chamado com `withCredentials`, e o navegador só aceita credenciais de uma origem exata. O
  `CORS_ALLOW` passa a exigir origens exatas (`*` faz a API não subir) e a política tem `AllowCredentials`. Nenhuma chave nova no deploy.
- **Testes sem Windows**: os de integração trocam o handler do esquema Negotiate por um de teste (`AutenticacaoWindowsDeTeste`,
  usuário num cabeçalho); os E2E do front simulam a resposta do login com um JWT do banco local.

## Consequências

- Mudança que quebra a v1 do contrato (feita de propósito, com o front atualizado junto): `LoginEntrada`, `TrocaSenhaEntrada`,
  `SenhaProvisoriaDto` e `UsuarioCriadoDto` saem; `deveTrocarSenha` e `bloqueadoAte` saem dos DTOs; `POST /usuarios` devolve o
  `UsuarioDto`; os códigos `CREDENCIAIS_INVALIDAS`, `TROCA_DE_SENHA_OBRIGATORIA` e `MUITAS_TENTATIVAS` deixam de existir.
- No servidor, o site da API precisa da **Autenticação do Windows e da Autenticação Anônima ligadas** (a anônima para o preflight
  `OPTIONS` e para as chamadas com JWT); sem a do Windows, a API não sobe no IIS (o Negotiate exige que o servidor a tenha ligada).
- No computador do desenvolvedor (WSL/Linux) não há Kerberos: o login só funciona com a API rodando no Windows. Para os E2E e o
  `npm run dados-dev`, o front gera o token com a chave do banco local (`tools/token-local.mjs`).
- O `NegotiateHandler` olha toda requisição (o handshake pode ter várias etapas na mesma conexão); é o comportamento normal do pacote.
