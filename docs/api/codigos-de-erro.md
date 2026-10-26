# Códigos de erro da API

Todo erro da API é um **ProblemDetails** ([RFC 9457](https://www.rfc-editor.org/rfc/rfc9457)), com
`Content-Type: application/problem+json`, e sempre traz:

| Extensão | O que é |
|---|---|
| `codigo` | código estável do erro, desta tabela. **Decida pelo código, nunca pelo texto** de `title` ou `detail`, que pode mudar |
| `traceId` | identificador da requisição (ex.: `00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-00`). O trecho do meio é o `TraceId` do log: mostre-o ao usuário para ele informar ao suporte |
| `detalhes` | só em alguns erros: dados para montar a mensagem ou decidir o que fazer (formatos abaixo) |
| `errors` | só nos erros de validação (400): mensagens por campo, `{ "campo": ["mensagem"] }` |

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.10",
  "title": "Exclusão bloqueada.",
  "status": 409,
  "detail": "O registro não pode ser excluído porque outros registros dependem dele. 2 registro(s) de 'endpoint_servico' pelo campo 'projeto'.",
  "codigo": "EXCLUSAO_BLOQUEADA",
  "detalhes": { "dependencias": [{ "entidade": "endpoint_servico", "campo": "projeto", "quantidade": 2, "exemplos": ["consulta", "pagamento"] }] },
  "traceId": "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-00"
}
```

Os códigos ficam em [`CodigosErro`](../../backend/src/Catalogo.Domain/Excecoes/CodigosErro.cs). Código novo pode entrar
na v1 (com uma linha nesta tabela); renomear ou remover um código só numa v2 ([ADR 0022](../adr/0022-versionamento-por-prefixo-de-rota.md),
[ADR 0023](../adr/0023-codigos-de-erro-estaveis-no-problemdetails.md)). Trate um código desconhecido pelo status HTTP.

## Tabela

| Código | Status | Quando ocorre | O que o front deve fazer |
|---|---|---|---|
| `VALIDACAO` | 400 | dados do corpo ou parâmetros da URL inválidos (RN05), ou corpo que não é JSON válido | mostrar cada mensagem de `errors` junto do campo (a chave é a do campo ou do parâmetro, ex.: `codigo_sigla`, `criterios[1].tipo`, `tamanho`). Chaves que não são campos do formulário (ex.: `$`, `modelo`) vão numa mensagem geral |
| `EXPORTACAO_MUITO_GRANDE` | 400 | a exportação passaria do limite de linhas (padrão 10.000) | pedir mais filtros; a mensagem está em `errors.exportacao` |
| `REQUISICAO_INVALIDA` | 400/4xx | requisição recusada antes da validação (ex.: corpo grande demais) | erro de programação do cliente: registrar e mostrar mensagem genérica |
| `NAO_AUTENTICADO` | 401 | sem token, token inválido ou expirado, ou usuário desativado depois do login (RN23) | descartar o token e ir para o login |
| `CREDENCIAIS_INVALIDAS` | 401 | login recusado: matrícula inexistente, senha errada, usuário inativo **ou bloqueado** (RN23, RN24). O motivo nunca é revelado | mostrar "Matrícula ou senha inválidas" (o `detail`). Não há código de "bloqueado" de propósito |
| `SEM_PERMISSAO` | 403 | o perfil não permite a operação (RN26), equipe de que o usuário não participa, painel para INTERNO ou EXTERNO (RN78) | esconder ou desabilitar a ação; mostrar o `detail` |
| `TROCA_DE_SENHA_OBRIGATORIA` | 403 | senha provisória: só `POST /api/v1/auth/trocar-senha` e `GET /api/v1/auth/eu` funcionam (RN25) | levar para a tela de troca de senha |
| `GRUPO_NAO_ENCONTRADO` | 404 | a chave do grupo na URL não existe | voltar ao menu (`GET /api/v1/grupos`) |
| `ENTIDADE_NAO_ENCONTRADA` | 404 | a chave da entidade não existe no grupo | voltar à lista de entidades do grupo |
| `CAMPO_NAO_ENCONTRADO` | 404 | o campo de `.../campos/{campo}/opcoes` não existe ou não é REFERENCIA | erro de programação do cliente |
| `REGISTRO_NAO_ENCONTRADO` | 404 | o registro não existe, foi excluído ou o usuário não pode vê-lo (outra equipe, pendente de outra pessoa; RN28, RN37) | avisar que o item não está disponível e voltar à listagem |
| `EQUIPE_NAO_ENCONTRADA` | 404 | a equipe não existe ou é de outro gestor (RN33, RN40) | voltar à tela anterior |
| `USUARIO_NAO_ENCONTRADO` | 404 | o usuário da URL não existe (administração) | voltar à lista de usuários |
| `MODELO_AVALIACAO_NAO_ENCONTRADO` | 404 | a entidade não tem modelo de avaliação ativo na equipe, ou o `modeloId` não é dela (RN40, RN41) | oferecer a criação do modelo (GESTOR e ADMINISTRADOR) |
| `AVALIACAO_NAO_ENCONTRADA` | 404 | `DELETE .../avaliacoes/minha` sem o usuário ter avaliado | atualizar a tela (não há o que remover) |
| `IMPORTACAO_NAO_ENCONTRADA` | 404 | importação inexistente, de outro tipo de item ou de alguém cujas importações o usuário não vê (RN97) | voltar ao histórico de importações |
| `ROTA_NAO_ENCONTRADA` | 404 | nenhuma rota da API corresponde à URL (ex.: sem o `/v1`) | erro de programação do cliente |
| `METODO_NAO_PERMITIDO` | 405 | a rota existe, mas não com esse método HTTP | erro de programação do cliente |
| `TIPO_DE_CONTEUDO_NAO_SUPORTADO` | 415 | corpo sem `Content-Type: application/json` | erro de programação do cliente |
| `CONFLITO_UNICIDADE` | 409 | valor já usado numa restrição única (RN09) | marcar os campos de `detalhes.campos` com a mensagem do `detail` |
| `CONFLITO_CONCORRENCIA` | 409 | alguém alterou o dado depois que ele foi lido (`versao` antiga, RN12) | avisar, recarregar o registro (GET) e deixar o usuário refazer a alteração |
| `EXCLUSAO_BLOQUEADA` | 409 | outros registros apontam para este com RESTRINGIR (RN16) | listar `detalhes.dependencias` (o que precisa ser excluído ou alterado antes) |
| `ULTIMO_ADMINISTRADOR` | 409 | desativar ou rebaixar o último administrador ativo (RN30) | mostrar o `detail` |
| `ADMINISTRADOR_JA_EXISTE` | 409 | o comando `criar-admin` com administrador ativo (RN30). Não ocorre na API HTTP | — |
| `USUARIOS_SEM_EQUIPE` | 409 | desativar a equipe deixaria usuários sem equipe ativa (RN31) | mostrar `detalhes.quantidade` e pedir para mover os usuários antes |
| `APROVACOES_PENDENTES` | 409 | desativar a aprovação com registros pendentes (RN33) | mostrar `detalhes.quantidade` e levar à fila de pendentes |
| `REGISTRO_NAO_PENDENTE` | 409 | aprovar ou rejeitar um registro que não está PENDENTE (RN36) | recarregar o registro (alguém já decidiu) |
| `MODELO_ATIVO_EXISTENTE` | 409 | criar ou reativar um modelo de avaliação com outro já ativo (RN41) | abrir o modelo `detalhes.modeloId` para edição |
| `REGISTRO_NAO_APROVADO` | 409 | avaliar um item que não está APROVADO (RN44) | esconder a avaliação até o item ser aprovado |
| `RESTRICAO_NAO_ENCONTRADA` | 404 | restrição única inexistente na administração de metadados | recarregar a estrutura do grupo |
| `DEPENDENCIA_NAO_ENCONTRADA` | 404 | o campo não tem dependência para remover | recarregar a estrutura do grupo |
| `ALTERACAO_INCOMPATIVEL_COM_DADOS` | 409 | alteração de metadados deixaria registros inválidos (RN58) | mostrar `detalhes.impactos` (quantidade e exemplos) |
| `METADADO_EM_USO` | 409 | campo ou entidade em uso (exibição, restrição, dependência, referência) (RN69, RN70, RN76) | mostrar `detalhes.impactos[].usos` |
| `ENTIDADE_INATIVA` | 409 | escrita em registro de entidade ou grupo desativado (RN75, RN77) | tratar a entidade como somente leitura |
| `IMPORTACAO_SITUACAO_INVALIDA` | 409 | a operação não vale na situação da importação: executar sem prévia ou duas vezes, executar "tudo ou nada" com erros ou sem nada a gravar, nova prévia de uma importação concluída, desfazer o que não foi concluído (RN97, RN101) | mostrar o `detail`; `detalhes.situacao` traz a situação atual |
| `IMPORTACAO_DADOS_MUDARAM` | 409 | os dados mudaram entre a prévia e a execução (ex.: um item referenciado foi excluído): nada foi gravado, e a prévia já foi refeita (RN101) | recarregar a prévia (`GET .../importacoes/{id}`) e pedir nova confirmação; `detalhes.totais` traz os totais novos |
| `IMPORTACAO_NAO_PODE_SER_DESFEITA` | 409 | itens criados pela importação foram alterados depois ou são referenciados por outros itens (RN103) | listar `detalhes.impedimentos` (motivo e itens) |
| `IMPORTACAO_ARQUIVO_EXPIRADO` | 409 | o arquivo foi apagado pela limpeza automática (7 dias, RN104): sem prévia nem relatório | pedir para enviar o arquivo de novo |
| `MUITAS_TENTATIVAS` | 429 | mais de 10 tentativas de login por minuto do mesmo endereço (RN23) | pedir para aguardar um minuto |
| `ERRO_INTERNO` | 500 | erro inesperado. Fora de Development, a resposta só traz o código, o `traceId` e uma mensagem genérica | mostrar mensagem genérica com o `traceId` para o suporte |

## Formatos de `detalhes`

| Código | `detalhes` |
|---|---|
| `CONFLITO_UNICIDADE` | `{ "restricao": "uq_comunidade_nome", "campos": ["nome"] }`: chaves dos campos que formam a restrição |
| `VALIDACAO` (registro) | só quando há campos desconhecidos ou inativos (RN01, RN02): `{ "camposDesatualizados": ["cor"] }`. O front usa metadados antigos: recarregue-os e peça para revisar o formulário |
| `EXCLUSAO_BLOQUEADA` | `{ "dependencias": [{ "entidade": "endpoint_servico", "campo": "projeto", "quantidade": 2, "exemplos": ["consulta"] }] }`: até 5 rótulos de exemplo por dependência |
| `APROVACOES_PENDENTES` | `{ "quantidade": 3 }` |
| `USUARIOS_SEM_EQUIPE` | `{ "quantidade": 2 }` |
| `MODELO_ATIVO_EXISTENTE` | `{ "modeloId": "<guid>" }` (nulo se dois pedidos simultâneos criaram o modelo ao mesmo tempo) |
| `IMPORTACAO_SITUACAO_INVALIDA` | `{ "situacao": "CONCLUIDA" }` |
| `IMPORTACAO_DADOS_MUDARAM` | `{ "totais": { "novos", "atualizacoes", "ignorados", "comErro", "total" } }` (a prévia nova) |
| `IMPORTACAO_NAO_PODE_SER_DESFEITA` | `{ "impedimentos": [{ "motivo": "Itens alterados depois da importação.", "quantidade": 1, "itens": [{ "id": "<guid>", "rotulo": "Bateria 1" }] }] }`: até 10 itens por motivo |

Os demais erros não têm `detalhes`.
