# 0023. Códigos de erro estáveis no ProblemDetails

- Status: Aceito
- Data: 2026-10-21

## Contexto

A API já devolvia erros como ProblemDetails (RFC 9457), com mensagens em português. O front precisa decidir o que
fazer com cada erro (mandar para o login, pedir a troca de senha, recarregar o registro, mostrar o campo inválido),
e decidir pelo **texto** da mensagem quebraria a cada revisão de redação. Só alguns erros tinham `codigo`, e os dados
extras (dependências de uma exclusão, quantidade de pendentes) vinham soltos no objeto, cada um com um formato.

## Decisão

- **Todo** ProblemDetails tem duas extensões:
  - `codigo`: MAIÚSCULAS com sublinhado (ex.: `REGISTRO_NAO_ENCONTRADO`), de uma lista fechada;
  - `traceId`: o identificador da requisição, o mesmo que sai no log (TraceId), para o suporte achar o erro.
- Os códigos ficam **numa classe de constantes**, `CodigosErro` (Domain, para o Domain e a Application também
  usarem), e nunca como string solta. Toda exceção de domínio carrega o seu código (`ExcecaoDominio.Codigo`).
- Erros com dados para o front os trazem em **`detalhes`**, com formato documentado (ex.: `EXCLUSAO_BLOQUEADA` →
  `{ dependencias: [...] }`). Validação continua com `errors` por campo e `codigo = VALIDACAO`.
- Um ponto central (`ExtensoesProblema.Completar`, em `AddProblemDetails`) completa **qualquer** ProblemDetails, inclusive
  os do próprio ASP.NET (rota inexistente, 405, 415, JSON malformado, 401 sem token), para nenhum sair sem `codigo`.
- 404 tem um código **por tipo de recurso** (`GRUPO_NAO_ENCONTRADO`, `REGISTRO_NAO_ENCONTRADO`...), e `ROTA_NAO_ENCONTRADA`
  quando a URL não existe: o front distingue "o item sumiu" de "a URL está errada".
- Fora de Development, o 500 só traz `codigo = ERRO_INTERNO`, o `traceId` e uma mensagem genérica.
- O login recusado é sempre `CREDENCIAIS_INVALIDAS`, inclusive para usuário bloqueado ou inativo: um código
  `USUARIO_BLOQUEADO` revelaria que a matrícula existe, o que a RN23 proíbe.
- A tabela código → status → o que o front faz está em [`docs/api/codigos-de-erro.md`](../api/codigos-de-erro.md).

### Alternativas descartadas

- **Só o status HTTP**: 409 cobre unicidade, concorrência, exclusão bloqueada e várias regras; o front não teria como distinguir.
- **O `type` do ProblemDetails como URI** (`https://catalogo/erros/registro-nao-encontrado`): é o previsto na RFC, mas
  exige publicar páginas para cada URI; um código curto é mais simples para o front e para o time. O `type` continua o padrão.

## Consequências

- ✅ O front trata erros por código, e o texto pode melhorar sem quebrar nada.
- ✅ Um teste de integração percorre os principais cenários e falha se algum erro sair sem `codigo` ou `traceId`.
- ⚠️ Renomear ou remover um código quebra o contrato (só numa v2, ADR 0022). Código novo exige uma linha na tabela.
- ⚠️ Os dados de erros que antes vinham soltos (`quantidade`, `dependencias`, `restricao`, `campos`, `modeloId`) passaram para
  `detalhes`. Como o front ainda não existia, a mudança entrou junto com a v1.
