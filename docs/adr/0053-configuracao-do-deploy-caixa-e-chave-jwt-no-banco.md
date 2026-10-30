# 0053. Configuração do deploy da CAIXA por seis chaves no `appsettings.json` e chave do JWT no banco

- Status: Aceito
- Data: 2026-10-30
- Substitui, em parte: a configuração por `ASPNETCORE_ENVIRONMENT`, user-secrets e variáveis de ambiente (`ConnectionStrings__Catalogo`,
  `Jwt__ChaveAssinatura`, `Cors__OrigensPermitidas__N`) descrita em `docs/seguranca.md` e no `src/Catalogo.Api/AGENTS.md`

## Contexto

O deploy da CAIXA não define variáveis de ambiente nem escolhe um `appsettings.<Ambiente>.json`: ele só substitui marcadores
(`__NOME__`) no `appsettings.json` publicado, e só estes seis: `ENVIRONMENT`, `CORS_ALLOW`, `DB_DATABASE`, `DB_SERVER`, `DB_USER` e
`DB_PASSWORD`. A API precisava de `ASPNETCORE_ENVIRONMENT`, da connection string e da chave do JWT por variável de ambiente, e de uma
lista de origens do CORS por variável indexada. O banco é o SQL Server 2014 da CAIXA, com certificado que não é de autoridade conhecida.

## Decisão

- **As seis chaves ficam na raiz do `appsettings.json`**, com o marcador do deploy como valor. O resto (JWT sem a chave, segurança,
  exportação, painel, log em JSON) fica fixo no mesmo arquivo. Não há mais `appsettings.Production.json` nem `appsettings.Homologacao.json`.
- **A API não sobe** se alguma das seis estiver vazia ou ainda com um marcador `__...__`, e a mensagem lista **todas** as que faltam
  (`ConfiguracaoDeploy.Validar`, no `Program.cs`, antes de abrir conexão com o banco).
- **Connection string montada** a partir de `DB_SERVER`, `DB_DATABASE`, `DB_USER` e `DB_PASSWORD`, com `TrustServerCertificate=true`
  (`ConfiguracaoInfraestrutura.ObterConnectionString`).
- **`CORS_ALLOW`**: origens separadas por vírgula; `*` sozinho libera qualquer origem (`AllowAnyOrigin`, sem credenciais, porque a
  autenticação é o JWT no cabeçalho). Curinga parcial, origem incompleta ou `*` junto de outras origens fazem a API não subir.
- **`ENVIRONMENT` (LOCAL, DES, TQS, HMP, PRD) substitui o `ASPNETCORE_ENVIRONMENT` na aplicação**: as decisões por ambiente usam a
  classe `AmbienteCatalogo`, nunca o `IHostEnvironment`. Swagger só em LOCAL e DES; detalhes da exceção no 500 só em LOCAL.
- **Chave do JWT sem variável**: na primeira subida, a API gera 64 bytes aleatórios (Base64) e grava em `cat.CATTB025_CHAVE_JWT`
  (V018, uma linha com id fixo, padrão de nomes do [ADR 0052](0052-padrao-de-nomes-caixa-e-sql-server-2014.md)); nas próximas, lê a
  mesma. Duas instâncias subindo juntas não gravam duas chaves (a chave primária barra a segunda, que passa a ler a da primeira). A
  leitura e a gravação são em LINQ ([ADR 0011](0011-consultas-somente-linq.md)). No banco da CAIXA, a tabela chega pelo pacote
  `database/entrega/2026-10-30-chave-jwt`.
- **Local**: as mesmas seis chaves num `appsettings.Development.json` fora do git (modelo versionado no `.exemplo`), carregado logo
  depois do `appsettings.json` mesmo sem `ASPNETCORE_ENVIRONMENT` e nunca publicado (`CopyToPublishDirectory=Never`). Os testes de
  integração definem as seis chaves por cima dele (ENVIRONMENT = TQS), a partir da connection string de testes de sempre.

## Consequências

- O pacote publicado é o mesmo em todos os ambientes; o deploy só troca os seis marcadores.
- A chave do JWT é um segredo guardado no banco: quem lê `cat.CATTB025_CHAVE_JWT` consegue emitir tokens. Trocar a chave é apagar a
  linha e reiniciar **todas** as instâncias (cada uma lê a chave só na subida); todos precisam entrar de novo.
- A API não sobe sem a tabela `cat.CATTB025_CHAVE_JWT`: o pacote de banco vai **antes** do deploy da aplicação.
- O schema de testes no banco da CAIXA (`cat_testes`, `estrutura_padrao_caixa.ps1 -Modo SchemaTestes`) precisa também da tabela
  `cat_testes.CATTB025_CHAVE_JWT` (o mesmo `CREATE TABLE` do V018, com o schema trocado).
