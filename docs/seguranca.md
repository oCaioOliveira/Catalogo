# Segurança: login pelo Windows e token

Como a API identifica as pessoas. As regras de negócio estão em
[regras-de-negocio.md](regras-de-negocio.md#rn23--autenticação) (RN23, RN26 e RN105). A decisão está no
[ADR 0054](adr/0054-login-pela-autenticacao-integrada-do-windows.md); o token, no [ADR 0015](adr/0015-jwt-com-provedor-de-identidade-isolado.md).

## Visão geral

```mermaid
sequenceDiagram
    autonumber
    participant F as Front (navegador)
    participant I as IIS (Autenticação do Windows)
    participant A as AuthController + Login
    participant G as GeradorTokenJwt
    participant M as VerificacaoUsuarioMiddleware
    participant S as ISituacaoUsuarios (cache 1 min)

    F->>A: POST /api/v1/auth/login (sem corpo, withCredentials)
    A-->>F: 401 WWW-Authenticate: Negotiate
    F->>I: de novo, com o usuário do Windows (Kerberos/NTLM)
    I->>A: usuário autenticado: DOMINIO\c161587
    Note over A: matrícula C161587; nova → EXTERNO sem equipe (RN105)<br/>desativado → 403
    A->>G: Gerar(usuario)
    A-->>F: { token, expiraEm, usuario }
    F->>M: GET /api/v1/... Authorization: Bearer token
    Note over M: JwtBearer validou assinatura, emissor, audiência e validade
    M->>S: situação atual do usuário (perfil, equipes ativas, ativo)
    Note over M: desativado → 401
    M-->>F: segue para o controller (permissões na PoliticaPermissao)
```

## Login

- `POST /api/v1/auth/login`, **sem corpo**. É o único endpoint que exige o **usuário do Windows** (política
  `ConfiguracaoSeguranca.PoliticaLoginWindows`, esquema Negotiate do pacote `Microsoft.AspNetCore.Authentication.Negotiate`); todos os
  outros exigem o JWT, menos o `/health`. O token **não** abre o login: só o Windows emite token.
- No **IIS**, o Negotiate repassa a autenticação para o IIS (é o mesmo mecanismo das aplicações GIC). No Kestrel em Windows, ele faz
  Kerberos/NTLM sozinho. No Linux/WSL do desenvolvedor não há Kerberos: o login não funciona lá (ver "Desenvolvimento e testes").
- A matrícula vem do nome do Windows (`MatriculaWindows`, Domain): `DOMINIO\c161587` → `C161587` (também `c161587@dominio`).
  Matrícula ainda não cadastrada: cria o usuário EXTERNO, ativo e sem equipe, com histórico (RN105). Usuário desativado: 403.
- Não há senha, troca obrigatória, bloqueio por tentativas nem limite de logins por minuto.

### O que configurar no servidor (IIS)

No **site (ou aplicação) da API** no IIS, em *Autenticação*:

| Item | Valor | Por quê |
|---|---|---|
| **Autenticação do Windows** | **Habilitada** (provedores `Negotiate` e `NTLM`, nessa ordem) | o login; sem ela a API **não sobe** no IIS (o Negotiate exige que o servidor a tenha ligada) |
| **Autenticação Anônima** | **Habilitada** | o *preflight* do CORS (`OPTIONS`) e todas as chamadas com o JWT chegam sem credencial do Windows |
| Modelo de hospedagem | `inprocess` (o `web.config` gerado pelo publish) | o mesmo das aplicações GIC |

Pelo `appcmd` (equivalente ao Gerenciador do IIS; `<site>` é o nome do site ou `site/aplicação`):

```bat
%windir%\system32\inetsrv\appcmd set config "<site>" -section:system.webServer/security/authentication/windowsAuthentication /enabled:true /commit:apphost
%windir%\system32\inetsrv\appcmd set config "<site>" -section:system.webServer/security/authentication/anonymousAuthentication /enabled:true /commit:apphost
```

- O endereço da API precisa estar na **zona Intranet** do navegador (política da CAIXA para os sites internos): só assim o Edge/Chrome
  mandam o usuário do Windows sem perguntar. Se o navegador abrir uma janela pedindo usuário e senha, é essa configuração que falta.
- Com Kerberos (e não só NTLM) e a API num nome DNS próprio, o SPN `HTTP/<nome-da-api>` precisa estar registrado na conta do pool de
  aplicativos (ou na conta do servidor, se o pool usa `ApplicationPoolIdentity`). Sem SPN, o navegador cai para NTLM, que também funciona.
- O `CORS_ALLOW` do deploy precisa ter a origem **exata** do front (ver [CORS](#cors)).

## Token (JWT)

- Assinado com HMAC-SHA256 (chave simétrica), com os claims `sub` (id do usuário), `matricula` e `perfil`.
- Validade em `Jwt:ValidadeHoras` (padrão 8). Não há refresh token: depois de expirar, o front chama o login de novo (sem nada para a pessoa digitar).
- **As equipes não vão no token.** Perfil, equipes ativas e situação são lidos do banco a cada requisição
  (`ISituacaoUsuarios`, cache de `Seguranca:CacheUsuarioSegundos`, padrão 60). Os casos de uso de administração
  descartam o cache na hora; mudanças feitas direto no banco valem em até 1 minuto.
- O front envia `Authorization: Bearer <token>` em todas as chamadas. No Swagger UI (LOCAL e DES), use o botão **Authorize**.

## Configuração por ambiente

A API sobe só com as seis chaves que o deploy da CAIXA substitui na raiz do `appsettings.json` ([ADR 0053](adr/0053-configuracao-do-deploy-caixa-e-chave-jwt-no-banco.md)):

| Chave | O que é |
|---|---|
| `ENVIRONMENT` | `LOCAL`, `DES`, `TQS`, `HMP` ou `PRD`. Substitui o `ASPNETCORE_ENVIRONMENT`: Swagger só em LOCAL e DES; detalhes da exceção no 500 só em LOCAL |
| `CORS_ALLOW` | origens **exatas** do front, separadas por vírgula; `*` não é aceito (ver [CORS](#cors)) |
| `DB_SERVER`, `DB_DATABASE`, `DB_USER`, `DB_PASSWORD` | o banco; a connection string é montada com `TrustServerCertificate=true` |

Se alguma estiver vazia ou ainda com o marcador (`__DB_PASSWORD__`), a API **não sobe** e diz quais faltam. No computador do
desenvolvedor, os valores ficam no `src/Catalogo.Api/appsettings.Development.json` (fora do git; modelo no `.exemplo`).

O resto é fixo no `appsettings.json`:

| Chave | O que é | Padrão |
|---|---|---|
| `Jwt:Emissor` / `Jwt:Audiencia` | `iss` e `aud` do token | `catalogo-api` / `catalogo` |
| `Jwt:ValidadeHoras` | validade do token | 8 |
| `Seguranca:CacheUsuarioSegundos` | cache da situação do usuário | 60 |

### Chave de assinatura do JWT

A chave **não fica em arquivo nem em variável**: fica no banco, em `cat.CATTB025_CHAVE_JWT` (V018). Na primeira subida, a API gera
64 bytes aleatórios (Base64) e grava; nas próximas, lê a mesma. Todas as instâncias usam a mesma chave, e duas instâncias subindo
juntas não gravam duas (a chave primária barra a segunda, que passa a ler a da primeira). Sem a tabela, a API não sobe e diz qual
script aplicar. Nos testes de integração, a chave é a do banco de testes.

A coluna é um **segredo**: quem a lê consegue emitir tokens. **Trocar a chave invalida todos os tokens emitidos** (todo mundo precisa
logar de novo): apague a linha (`DELETE FROM cat.CATTB025_CHAVE_JWT`) e reinicie **todas** as instâncias, que geram e passam a usar
uma chave nova. É o jeito de "derrubar" todas as sessões se a chave vazar.

## CORS

O front-end roda em outra origem (outro servidor ou porta), e o navegador só deixa ele chamar a API se ela autorizar
essa origem (CORS). A configuração fica em `ConfiguracaoCors` (Api):

- **Origens**: as de **`CORS_ALLOW`**, separadas por vírgula (`https://a.caixa,https://b.caixa`), sempre **exatas**. `*` (sozinho ou
  junto de outras) não é aceito: o login vai com credenciais, e o navegador recusa credenciais com `Access-Control-Allow-Origin: *`.
- **Com credenciais** (`AllowCredentials`): o `POST /auth/login` é chamado com `withCredentials`, para o navegador mandar o usuário do
  Windows. As outras chamadas levam o JWT no cabeçalho `Authorization`, sem credenciais.
- **Métodos**: GET, POST, PUT, PATCH e DELETE. **Cabeçalhos aceitos**: `Authorization` e `Content-Type`.
- **Cabeçalhos expostos**: `Location` (criação, 201) e `Content-Disposition` (nome do arquivo da exportação).
- `CORS_ALLOW` vazio, `*`, uma origem com curinga parcial (`https://*.caixa`), sem `http://`/`https://` ou com caminho fazem a API
  **não subir**, com mensagem clara. Continua sendo uma das seis chaves do deploy: nenhuma chave nova.
- O CORS vem antes da autenticação: o *preflight* (`OPTIONS`) não leva token, e os erros (401, 403...) também levam os
  cabeçalhos de CORS, inclusive o desafio `401 WWW-Authenticate: Negotiate` do login.

| Ambiente | `CORS_ALLOW` |
|---|---|
| LOCAL | `http://localhost:4200,http://localhost:5173`, no `appsettings.Development.json` |
| DES / TQS / HMP / PRD | o marcador `__CORS_ALLOW__` no arquivo versionado, substituído pelo deploy pela origem exata do front |

## Desenvolvimento e testes

- **Testes de integração**: o TestServer não tem Windows. O `AutenticacaoWindowsDeTeste` (tests, `Apoio/`) troca o handler do esquema
  Negotiate: o usuário do Windows vem no cabeçalho `X-Teste-Usuario-Windows` (atalho `TesteDeApi.LoginWindows`). Sem o cabeçalho, 401
  com `WWW-Authenticate: Negotiate`, como o servidor real.
- **E2E do front**: o `POST /auth/login` é simulado no navegador com um JWT do banco local (`tools/token-local.mjs`, no front); o resto
  da API é o real.
- **API local no Linux/WSL**: o login responde 401 (sem Kerberos). Para usar a tela, gere um token com `node tools/token-local.mjs
  <matrícula>` (no front) e use-o no Swagger (**Authorize**). Com a API rodando no Windows, o login funciona com o seu usuário.

## Primeiro administrador

Não existe cadastro público. O primeiro administrador é criado pelo comando `criar-admin --matricula <matrícula do AD> --nome "..."`
(ver [backend/README.md](../backend/README.md#primeiro-administrador)), que só funciona se não houver nenhum administrador ativo e não
pede senha: a pessoa entra pelo Windows. Se a matrícula já entrou antes (e virou EXTERNO, RN105), o comando a promove a ADMINISTRADOR.
Num banco instalado pelo pacote dos DBAs, ele vem num script gerado pelo mesmo comando (`criar-admin --gerar-sql --saida <arquivo>`),
que exige a estrutura na versão 19 (sem as colunas de senha). Os demais usuários são criados pela API, por um administrador, só pela
matrícula — ou nascem EXTERNO no primeiro acesso.
