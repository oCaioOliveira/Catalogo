# Segurança: login, token e senhas

Como a API identifica as pessoas e protege o login. As regras de negócio estão em
[regras-de-negocio.md](regras-de-negocio.md#rn23--autenticação) (RN23 a RN26). As decisões estão nos
ADRs [0015](adr/0015-jwt-com-provedor-de-identidade-isolado.md) e [0016](adr/0016-passwordhasher-sem-identity.md).

## Visão geral

```mermaid
sequenceDiagram
    autonumber
    participant F as Front
    participant A as AuthController
    participant P as IProvedorIdentidade (local)
    participant G as GeradorTokenJwt
    participant M as VerificacaoUsuarioMiddleware
    participant S as ISituacaoUsuarios (cache 1 min)

    F->>A: POST /api/v1/auth/login { matricula, senha }
    Note over A: rate limiting: 10 por minuto por IP (429)
    A->>P: AutenticarAsync
    Note over P: inativo, bloqueado ou senha errada → null<br/>(5 erros = 15 min bloqueado)
    A->>G: Gerar(usuario)
    A-->>F: { token, expiraEm, deveTrocarSenha, usuario }
    F->>M: GET /api/v1/... Authorization: Bearer token
    Note over M: JwtBearer validou assinatura, emissor, audiência e validade
    M->>S: situação atual do usuário (perfil, equipes ativas, ativo, troca de senha)
    Note over M: desativado → 401; troca pendente → 403 TROCA_DE_SENHA_OBRIGATORIA
    M-->>F: segue para o controller (permissões na PoliticaPermissao)
```

## Login

- `POST /api/v1/auth/login` com `{ "matricula": "...", "senha": "..." }`. É o único endpoint de negócio sem
  autenticação (o outro é `/health`).
- A resposta de erro é **sempre** 401 *"Matrícula ou senha inválidas."*. Quando a matrícula não existe, a API
  ainda calcula um hash, para o tempo de resposta não revelar quais matrículas existem.
- **Bloqueio** (RN24): 5 senhas erradas seguidas bloqueiam por 15 minutos (`Usuario.TentativasFalhas`,
  `Usuario.BloqueadoAte`). Enquanto bloqueado, nem a senha certa entra.
- **Rate limiting**: `RateLimiter` nativo do ASP.NET Core, janela fixa de 1 minuto por endereço IP, limite em
  `Seguranca:LoginLimitePorMinuto` (padrão 10). Acima do limite: 429.
  > ⚠️ Atrás de um proxy reverso ou balanceador, todos os pedidos chegam com o IP do proxy. Nesse caso,
  > configure o `ForwardedHeaders` no `Program.cs` para usar o IP real do cliente. Hoje isso **não** está configurado.

## Token (JWT)

- Assinado com HMAC-SHA256 (chave simétrica), com os claims `sub` (id do usuário), `matricula` e `perfil`.
- Validade em `Jwt:ValidadeHoras` (padrão 8). Não há refresh token: depois de expirar, é preciso fazer login de novo.
- **As equipes não vão no token.** Perfil, equipes ativas e situação são lidos do banco a cada requisição
  (`ISituacaoUsuarios`, cache de `Seguranca:CacheUsuarioSegundos`, padrão 60). Os casos de uso de administração
  descartam o cache na hora; mudanças feitas direto no banco valem em até 1 minuto.
- O front envia `Authorization: Bearer <token>` em todas as chamadas. No Swagger UI (Development), use o botão **Authorize**.

## Senhas

- Hash com o `PasswordHasher<T>` do .NET (pacote `Microsoft.Extensions.Identity.Core`): PBKDF2 com
  HMAC-SHA512, sal aleatório e 100 mil iterações. O hash guarda o algoritmo e o sal. **Não** usamos o
  ASP.NET Core Identity completo ([ADR 0016](adr/0016-passwordhasher-sem-identity.md)).
- Política (RN25): mínimo de 10 caracteres, com letra e número; a nova diferente da atual.
- Senhas provisórias são geradas pelo sistema (`PoliticaSenha.GerarProvisoria`, 16 caracteres aleatórios
  com `RandomNumberGenerator`) e devolvidas **uma única vez** na resposta.
- Senha nunca aparece em log, resposta (exceto a provisória, uma vez) ou histórico. O hash também não sai do banco.

## Configuração por ambiente

| Chave | O que é | Padrão |
|---|---|---|
| `Jwt:ChaveAssinatura` | **segredo** para assinar os tokens, com pelo menos 32 caracteres aleatórios | — (obrigatória) |
| `Jwt:Emissor` / `Jwt:Audiencia` | `iss` e `aud` do token | `catalogo-api` / `catalogo` |
| `Jwt:ValidadeHoras` | validade do token | 8 |
| `Seguranca:LoginLimitePorMinuto` | tentativas de login por minuto por IP | 10 |
| `Seguranca:CacheUsuarioSegundos` | cache da situação do usuário | 60 |

Onde configurar a **chave de assinatura** (nunca em arquivo versionado):

| Ambiente | Onde |
|---|---|
| Development | user-secrets, de dentro de `backend/`: `dotnet user-secrets set "Jwt:ChaveAssinatura" "$(openssl rand -base64 48)" --project src/Catalogo.Api` |
| Homologacao / Production | variável de ambiente **`Jwt__ChaveAssinatura`** no servidor, com dois sublinhados. Use uma chave diferente por ambiente |
| Testes de integração | gerada aleatoriamente a cada execução pela `BancoDeTestesFixture` |

Sem a chave, a API não sobe e explica o que falta. **Trocar a chave invalida todos os tokens emitidos**
(todo mundo precisa logar de novo): é o jeito de "derrubar" todas as sessões se a chave vazar.

## CORS

O front-end roda em outra origem (outro servidor ou porta), e o navegador só deixa ele chamar a API se ela autorizar
essa origem (CORS). A configuração fica em `ConfiguracaoCors` (Api):

- **Origens**: só as listadas em **`Cors:OrigensPermitidas`**. Lista vazia = nenhuma origem de outro site é aceita
  (chamadas do mesmo site e de fora do navegador, como scripts, não são afetadas).
- **Métodos**: GET, POST, PUT, PATCH e DELETE. **Cabeçalhos aceitos**: `Authorization` e `Content-Type`.
- **Cabeçalhos expostos**: `Location` (criação, 201) e `Content-Disposition` (nome do arquivo da exportação).
- **Sem credenciais** (`AllowCredentials`): a autenticação é o JWT no cabeçalho `Authorization`, sem cookies.
- **Nunca `AllowAnyOrigin`**: "*" (ou uma origem com curinga, ou sem `http://`/`https://`) em `Cors:OrigensPermitidas`
  faz a API **não subir**, com mensagem clara.
- O CORS vem antes da autenticação: o *preflight* (`OPTIONS`) não leva token, e os erros (401, 403...) também levam os
  cabeçalhos de CORS, para o front conseguir ler o `codigo`.

| Ambiente | `Cors:OrigensPermitidas` |
|---|---|
| Development | `http://localhost:5173` e `http://localhost:4200` (servidores de desenvolvimento mais comuns), em `appsettings.Development.json` |
| Homologacao / Production | **vazia** no arquivo versionado. Preencha no servidor, por variável de ambiente: `Cors__OrigensPermitidas__0=https://catalogo.empresa.com.br` (e `__1`, `__2`... para outras) |

## Primeiro administrador

Não existe cadastro público. O primeiro administrador é criado pelo comando `criar-admin` (ver
[backend/README.md](../backend/README.md#primeiro-administrador)), que só funciona se não houver nenhum
administrador ativo. Num banco instalado pelo pacote dos DBAs, ele vem no script `03_administrador_inicial.sql`, gerado pelo
mesmo comando (`criar-admin --gerar-sql`): o script tem só o hash PBKDF2 da senha provisória, nunca a senha, e o usuário nasce com
troca obrigatória no primeiro acesso (RN25). A política de senha tem uma única exceção, explícita
(`--aceitar-senha-fora-da-politica`), só para essa senha provisória: a senha definida na troca segue a política. Os demais
usuários são criados pela API, por um administrador.
