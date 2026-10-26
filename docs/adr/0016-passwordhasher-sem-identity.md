# 0016. PasswordHasher sem o ASP.NET Core Identity completo

- Status: Aceito
- Data: 2026-10-21

## Contexto

Senhas precisam de um hash lento, com sal, e de um algoritmo testado. O ASP.NET Core Identity
completo resolve isso, mas traz as próprias tabelas (`AspNetUsers`...), migrations do EF (que não usamos,
[ADR 0003](0003-schema-por-scripts-sql.md)) e muitos conceitos que o time não precisa agora.
Escrever o próprio PBKDF2 seria arriscado.

## Decisão

- Usar **só** o `PasswordHasher<T>` do pacote `Microsoft.Extensions.Identity.Core` (PBKDF2 com HMAC-SHA512,
  sal aleatório, 100 mil iterações), dentro da classe `HasherSenha` da Infrastructure, atrás da interface `IHasherSenha`.
- Usuários ficam na nossa tabela `dbo.Usuario`, criada por script.

## Consequências

- ✅ Algoritmo mantido pela Microsoft. Formatos antigos de hash continuam sendo aceitos quando os parâmetros mudam.
- ✅ Nenhuma tabela ou migration extra.
- ⚠️ Recursos do Identity (confirmação de e-mail, 2FA, recuperação de senha) não existem. Se forem necessários, precisam de um ADR novo.
