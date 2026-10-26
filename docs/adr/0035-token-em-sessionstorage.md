# 0035. Token de acesso em sessionStorage

- Status: Aceito
- Data: 2026-10-22

## Contexto

A API autentica por JWT no cabeçalho `Authorization` (ADR 0015), válido por 8 horas e sem refresh; não usa cookies (CORS sem
credenciais, `docs/seguranca.md`). O front precisa guardar o token em algum lugar. Recarregar a página (F5) não deveria derrubar a
sessão, mas a sessão também não deveria sobreviver indefinidamente num computador compartilhado.

## Decisão

- O token fica em **`sessionStorage`** (chave `catalogo.token`) e é espelhado num signal do `AuthService`, que é o que o interceptor
  usa. Sobrevive ao recarregar a página e **some ao fechar a aba**. Cada aba tem a sua sessão.
- Ao abrir o app com um token guardado, um inicializador chama `GET /api/v1/auth/eu`; se falhar (expirado, usuário desativado), a
  sessão é limpa.
- Qualquer 401 limpa a sessão e leva ao login com `returnUrl`. O logout limpa o token, o usuário e o cache de metadados.
- Se o `sessionStorage` não estiver disponível, a sessão vive só em memória (some ao recarregar).

### Alternativas descartadas

- **localStorage**: a sessão sobreviveria a fechar o navegador e seria compartilhada entre abas; num computador compartilhado, o
  próximo usuário entraria logado.
- **Só em memória**: todo F5 pediria login de novo.
- **Cookie HttpOnly**: mais protegido contra XSS, mas exige mudar a API (emissão do cookie, proteção CSRF, CORS com credenciais).
  Fica como evolução se o risco justificar.

## Consequências

- ✅ F5 mantém a sessão; fechar a aba encerra.
- ⚠️ Um script injetado na página (XSS) conseguiria ler o token. A defesa é não ter XSS: o Angular escapa o conteúdo por padrão, e
  o front não usa `innerHTML` com dados da API.
- ⚠️ Abrir um link numa aba nova pede login de novo (o sessionStorage não é compartilhado entre abas).
