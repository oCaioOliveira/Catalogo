# 0015. JWT com matrícula e senha local, atrás de uma interface para um futuro SSO

- Status: Aceito
- Data: 2026-10-21

## Contexto

A API precisa identificar as pessoas. Hoje não há um SSO corporativo disponível para a aplicação, mas os
arquitetos podem querer um no futuro. O front é uma aplicação separada e a API não guarda sessão.

## Decisão

- Login com **matrícula e senha** guardadas no próprio banco, devolvendo um **JWT** assinado
  (`Microsoft.AspNetCore.Authentication.JwtBearer` 8.x), com validade padrão de 8 horas e sem refresh token.
- O token leva só `sub`, `matricula` e `perfil`. Perfil, equipes e situação são relidos do banco a cada
  requisição (cache de 1 minuto), para mudanças valerem rápido.
- A conferência das credenciais fica atrás da interface **`IProvedorIdentidade`** (Application). A
  implementação de hoje é a `ProvedorIdentidadeLocal`. Os casos de uso não sabem de onde vem a identidade.
- Detalhes em [seguranca.md](../seguranca.md).

## Consequências

- ✅ Para adotar um SSO: criar outra implementação de `IProvedorIdentidade` (ou validar o token do SSO
  no `JwtBearer`) e trocar o registro na DI. A `PoliticaPermissao` e os casos de uso não mudam, porque
  continuam recebendo um `UsuarioAtual`.
- ⚠️ Sem refresh token, a sessão dura no máximo a validade do token.
- ⚠️ A chave de assinatura é um segredo crítico: se vazar, alguém pode emitir tokens. Ela é trocada por
  ambiente e, se necessário, a troca derruba todas as sessões.
