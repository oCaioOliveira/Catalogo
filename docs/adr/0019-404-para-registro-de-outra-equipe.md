# 0019. 404, e não 403, para registros de outra equipe

- Status: Aceito
- Data: 2026-10-21

## Contexto

Quando alguém pede um registro de uma equipe da qual não participa, a API pode responder 403 ("existe,
mas você não pode") ou 404 ("não existe"). O 403 revela que o registro existe, e com ids previsíveis
alguém poderia mapear o que outras equipes cadastram.

## Decisão

- Registro que o usuário **não pode ver** → **404**, com a mesma mensagem de um id inexistente (RN28).
  Vale para GET, PUT, DELETE, histórico e referências em POST/PUT.
- Registro que o usuário **vê**, mas o perfil não permite a operação → **403** (ex.: EXTERNO tentando alterar).
- A decisão está em `PoliticaPermissao.Decidir` (`NaoEncontrado` × `Proibido`).

## Consequências

- ✅ A API não revela a existência de dados de outras equipes. A unicidade por equipe (RN09) evita que um 409 revele.
- ⚠️ Para quem está depurando, um 404 pode ser "não existe" ou "não é seu". O log e o administrador (que vê tudo) ajudam a diferenciar.
