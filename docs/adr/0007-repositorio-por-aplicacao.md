# 0007. Repositório organizado em pastas por aplicação

- Status: Substituído por 0051
- Data: 2026-10-21

## Contexto

O repositório vai receber um front-end. Com a solução .NET na raiz, os arquivos do back-end
(`Catalogo.sln`, `Directory.*.props`, `global.json`, `src/`, `tests/`) se misturariam com os do
front, e as regras específicas do .NET ficariam no `AGENTS.md` da raiz, valendo para todo mundo.

## Decisão

- Cada aplicação tem a sua pasta: `backend/` (solução .NET) e, no futuro, `frontend/`.
- Na raiz fica só o que é **compartilhado**: `docs/` (regras de negócio, modelo de dados, ADRs
  valem para back e front), `database/` (o banco é externo e independente das aplicações), o
  `README.md` e o `AGENTS.md`, que vira um mapa curto.
- As regras do .NET (dependência entre projetos, pacotes, comandos) ficam em `backend/AGENTS.md`.
- A mudança foi só de localização: namespaces, nomes de projeto e código não mudaram.

## Consequências

- ✅ O front entra sem mexer no back-end, com os seus próprios `AGENTS.md` e `README.md`.
- ✅ As regras de negócio continuam num lugar só (`docs/`), com os mesmos ids (RNxx) para os dois lados.
- ⚠️ Os comandos do back-end passam a ser rodados de dentro de `backend/`.
