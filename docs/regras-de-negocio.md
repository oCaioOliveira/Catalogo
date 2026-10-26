# Regras de negócio do motor

Cada regra tem um identificador (**RN01**, **RN02**...). Cite o identificador:

- no código, em comentários onde a regra é aplicada (ex.: `// RN09`);
- nos testes, no nome ou num comentário;
- nos PRs, quando a mudança mexer numa regra.

Mudou uma regra? Atualize **este arquivo no mesmo PR**. Nunca reaproveite um número: uma regra
removida fica marcada como *(removida)*.

| Id | Regra | Onde está |
|---|---|---|
| [RN01](#rn01--campos-desconhecidos) | Campos desconhecidos são recusados | `ValidadorRegistro` |
| [RN02](#rn02--campos-inativos) | Campos inativos não podem ser preenchidos | `ValidadorRegistro`, `EntidadeMapeamentos` |
| [RN03](#rn03--obrigatoriedade) | Obrigatoriedade (nulo e texto em branco = vazio) | `ValidadorRegistro` |
| [RN04](#rn04--validação-e-normalização-por-tipo) | Validação e normalização por tipo | `Validacao/Tipos/*` |
| [RN05](#rn05--todos-os-erros-de-uma-vez) | Todos os erros de uma vez, por campo | `ValidadorRegistro`, `ErrosPorCampo`, `PreparadorRegistro` |
| [RN06](#rn06--referências-existem-e-são-da-entidade-certa) | Referências existem e são da entidade certa | `PreparadorRegistro` |
| [RN07](#rn07--referências-fora-do-json) | Referências ficam fora do JSON | `ValidadorRegistro`, `SincronizadorRegistro`, `RegistroMapeamentos` |
| [RN08](#rn08--campos-dependentes) | Campos dependentes | `ValidadorDependencias` |
| [RN09](#rn09--unicidade) | Unicidade por hash normalizado | `CalculadoraChaveUnica`, `PreparadorRegistro` |
| [RN10](#rn10--nulo-não-gera-chave-única) | Nulo não gera chave única | `CalculadoraChaveUnica` |
| [RN11](#rn11--ações-de-exclusão-restringir-e-cascata) | Ações de exclusão: RESTRINGIR e CASCATA | `PlanejadorExclusao`, banco (FK) |
| [RN12](#rn12--concorrência-otimista) | Concorrência otimista | `AtualizarRegistro`, `UnidadeDeTrabalho` |
| [RN13](#rn13--colunas-de-sistema) | Colunas de sistema não são campos | `Registro`, `RegistroDto` |
| [RN14](#rn14--chaves-reservadas) | Chaves reservadas | `ChavesReservadas`, `MetadadosProvider` |
| [RN15](#rn15--atualização-substitui-o-registro-inteiro) | Atualização substitui o registro inteiro | `AtualizarRegistro`, `SincronizadorRegistro` |
| [RN16](#rn16--plano-de-exclusão) | Plano de exclusão | `PlanejadorExclusao`, `ExcluirRegistro` |
| [RN17](#rn17--paginação) | Paginação | `InterpretadorConsulta`, `Paginacao` |
| [RN18](#rn18--filtros) | Filtros | `InterpretadorConsulta`, `ConsultaRegistrosLinq` |
| [RN19](#rn19--ordenação) | Ordenação | `InterpretadorConsulta`, `ConsultaRegistrosLinq` |
| [RN20](#rn20--rótulo-das-referências) | Rótulo das referências | `MontadorRegistroDto`, `ConversorValorTipado` |
| [RN21](#rn21--opções-de-campo-dependente) | Opções de campo dependente | `ListarOpcoes` |
| [RN22](#rn22--limites-do-decimal) | Limites do DECIMAL | `CK_Campo_Decimal` (V002) |
| [RN23](#rn23--autenticação) | Autenticação por matrícula e senha | `Login`, `ProvedorIdentidadeLocal`, `VerificacaoUsuarioMiddleware` |
| [RN24](#rn24--bloqueio-por-tentativas) | Bloqueio por tentativas | `Usuario.RegistrarFalhaDeLogin` |
| [RN25](#rn25--senha-e-troca-obrigatória) | Senha e troca obrigatória | `PoliticaSenha`, `TrocarSenha`, `VerificacaoUsuarioMiddleware` |
| [RN26](#rn26--matriz-de-permissões) | Matriz de permissões | `PoliticaPermissao` |
| [RN27](#rn27--registros-globais-e-por-equipe) | Registros globais e por equipe | `Entidade.EscopoEquipe`, `PoliticaPermissao.ResolverEquipe` |
| [RN28](#rn28--registro-de-outra-equipe-é-404) | Registro de outra equipe é 404 | `PoliticaPermissao.Decidir`, `Permissoes.Exigir` |
| [RN29](#rn29--isolamento-de-referências-entre-equipes) | Isolamento de referências entre equipes | `PoliticaPermissao.PodeReferenciar`, `PreparadorRegistro` |
| [RN30](#rn30--último-administrador) | Último administrador | `RegrasAdministrador` |
| [RN31](#rn31--usuários-e-equipes-não-são-excluídos) | Usuários e equipes não são excluídos | `AlterarAtivacaoUsuario`, `AlterarAtivacaoEquipe` |
| [RN32](#rn32--histórico-de-alterações) | Histórico de alterações | `ServicoHistorico`, `ComparadorAlteracoes` |
| [RN33](#rn33--aprovação-ativável-por-entidade-e-equipe) | Aprovação ativável por entidade e equipe | `DefinirConfiguracaoAprovacao`, `PoliticaPermissao.PodeConfigurarEquipe` |
| [RN34](#rn34--situação-de-aprovação-e-criação) | Situação de aprovação e criação | `FluxoAprovacao.SituacaoInicial`, `PoliticaPermissao.Decidir` |
| [RN35](#rn35--alteração-e-reenvio-pelo-externo) | Alteração e reenvio pelo EXTERNO | `PoliticaPermissao.Decidir`, `FluxoAprovacao.SituacaoAposAlteracao` |
| [RN36](#rn36--decisão-aprovar-ou-rejeitar) | Decisão: aprovar ou rejeitar | `DecidirAprovacao`, `FluxoAprovacao.Decidir`, `ListarPendentes` |
| [RN37](#rn37--visibilidade-de-registros-não-aprovados) | Visibilidade de registros não aprovados | `PoliticaPermissao.PodeVer`, `ConsultaRegistrosLinq` |
| [RN38](#rn38--referências-a-registros-não-aprovados) | Referências a registros não aprovados | `PoliticaPermissao.PodeReferenciarSituacao`, `PreparadorRegistro`, `ListarOpcoes` |
| [RN39](#rn39--unicidade-e-exclusão-de-registros-não-aprovados) | Unicidade e exclusão de registros não aprovados | `PreparadorRegistro`, `PoliticaPermissao.Decidir` |
| [RN40](#rn40--quem-configura-os-modelos-de-avaliação) | Quem configura os modelos de avaliação | `PoliticaPermissao.PodeConfigurarEquipe`, `ConfiguracaoDaEquipe` |
| [RN41](#rn41--um-modelo-ativo-por-entidade-e-equipe) | Um modelo ativo por entidade e equipe | `CriarModeloAvaliacao`, `AlterarAtivacaoModeloAvaliacao`, `UX_ModeloAvaliacao_Ativo` (V005) |
| [RN42](#rn42--critérios-tipos-e-comentário) | Critérios: tipos e comentário | `RegrasModeloAvaliacao.Validar` |
| [RN43](#rn43--alterar-um-modelo-que-já-tem-avaliações) | Alterar um modelo que já tem avaliações | `RegrasModeloAvaliacao.ValidarAlteracao`, `RegrasAvaliacao.AplicarRespostas` |
| [RN44](#rn44--quem-avalia-e-o-que-pode-ser-avaliado) | Quem avalia e o que pode ser avaliado | `PoliticaPermissao.PodeAvaliar`, `RegrasAvaliacao.PodeSerAvaliado`, `AvaliarRegistro` |
| [RN45](#rn45--uma-avaliação-por-usuário-item-e-modelo) | Uma avaliação por usuário, item e modelo | `AvaliarRegistro`, `UQ_Avaliacao_Registro_Modelo_Usuario` (V005) |
| [RN46](#rn46--validação-das-respostas) | Validação das respostas | `RegrasAvaliacao.ValidarRespostas` |
| [RN47](#rn47--resumo-e-visibilidade-das-avaliações) | Resumo e visibilidade das avaliações | `ListarAvaliacoesRegistro`, `AvaliacaoRepository`, `PoliticaPermissao.PodeVerAvaliacoesIndividuais` |
| [RN48](#rn48--avaliação-no-registro-e-na-listagem) | Avaliação no registro e na listagem | `MontadorRegistroDto`, `InterpretadorConsulta`, `ConsultaRegistrosLinq` |
| [RN49](#rn49--exportação-da-listagem) | Exportação da listagem (CSV e XLSX) | `ExportarRegistros`, `PreparadorConsultaRegistros`, `ProtecaoFormula`, `GeradorCsv`, `GeradorXlsx` |
| [RN50](#rn50--quem-administra-os-metadados) | Quem administra os metadados | `PoliticaPermissao.PodeAdministrarMetadados`, `ExecutorAlteracaoMetadados` |
| [RN51](#rn51--chaves-imutáveis) | Chaves imutáveis | `RegrasMetadados.ConferirImutavel` |
| [RN52](#rn52--versão-histórico-e-cache-dos-metadados) | Versão, histórico e cache dos metadados | `ExecutorAlteracaoMetadados`, `MetadadosAdminRepository.Tocar*` |
| [RN53](#rn53--configuração-chaves-e-tipos) | Configuração: chaves e tipos | `RegrasMetadados.ValidarCampo` |
| [RN54](#rn54--configuração-referências-e-campo-de-exibição) | Configuração: referências e campo de exibição | `RegrasMetadados` |
| [RN55](#rn55--configuração-restrições-únicas-e-dependências) | Configuração: restrições únicas e dependências | `RegrasMetadados.ValidarRestricao`, `ValidarDependencia` |
| [RN56](#rn56--simulação) | Simulação (`?simular=true`) | `ExecutorAlteracaoMetadados` |
| [RN57](#rn57--desativar-e-excluir-campo-entidade-e-grupo) | Desativar e excluir campo, entidade e grupo | `PlanejadorAlteracoes`, `PoliticaPermissao.PodeUsarEntidade` |
| [RN58](#rn58--alterações-em-estruturas-com-dados) | Alterações em estruturas com dados | `PlanejadorAlteracoes`, `VerificadorDadosMetadados`, `EscritorRegistrosMetadados` |
| [RN59](#rn59--adicionar-campo-opcional) | Adicionar campo opcional | `PlanejadorAlteracoes.NovoCampo` |
| [RN60](#rn60--adicionar-campo-obrigatório) | Adicionar campo obrigatório | `PlanejadorAlteracoes.NovoCampo`, `RegrasMetadados.ValidarValorPadrao` |
| [RN61](#rn61--tornar-obrigatório-um-campo-existente) | Tornar obrigatório um campo existente | `PlanejadorAlteracoes.AlteracaoCampo` |
| [RN62](#rn62--tornar-opcional) | Tornar opcional | `PlanejadorAlteracoes.AlteracaoCampo` |
| [RN63](#rn63--diminuir-o-tamanho-máximo) | Diminuir o tamanho máximo | `TextosAcimaDoTamanho` |
| [RN64](#rn64--aumentar-tamanho-precisão-ou-escala) | Aumentar tamanho, precisão ou escala | `PlanejadorAlteracoes.AlteracaoCampo` |
| [RN65](#rn65--diminuir-precisão-ou-escala) | Diminuir precisão ou escala | `NumerosForaDaPrecisao` |
| [RN66](#rn66--mudar-o-tipo) | Mudar o tipo | `RegrasMetadados.ValidarCampo`, `PlanejadorAlteracoes` (URL) |
| [RN67](#rn67--lista-adicionar-opção) | LISTA: adicionar opção | `PlanejadorAlteracoes.AlteracaoCampo` |
| [RN68](#rn68--lista-remover-opção) | LISTA: remover opção | `ValoresComOpcoes` |
| [RN69](#rn69--desativar-campo) | Desativar campo | `PlanejadorAlteracoes.AtivacaoCampo`, `SincronizadorRegistro` |
| [RN70](#rn70--excluir-campo) | Excluir campo | `PlanejadorAlteracoes.ExcluirCampo` |
| [RN71](#rn71--adicionar-restrição-única) | Adicionar restrição única | `DuplicatasNaRestricao`, `RecalcularChavesUnicas` |
| [RN72](#rn72--remover-restrição-única) | Remover restrição única | `MetadadosAdminRepository.ExcluirRestricaoAsync` |
| [RN73](#rn73--adicionar-ou-alterar-dependência) | Adicionar ou alterar dependência | `ViolacoesDaDependencia` |
| [RN74](#rn74--mudar-o-escopo-da-entidade) | Mudar o escopo da entidade | `PlanejadorAlteracoes.MudarEscopo`, `RegrasMetadados.ValidarEntidade` |
| [RN75](#rn75--desativar-entidade) | Desativar entidade | `PoliticaPermissao.PodeUsarEntidade`, `PreparadorRegistro`, `ListarOpcoes` |
| [RN76](#rn76--excluir-entidade) | Excluir entidade | `PlanejadorAlteracoes.ExcluirEntidade` |
| [RN77](#rn77--desativar-ou-excluir-grupo) | Desativar ou excluir grupo | `PoliticaPermissao.PodeUsarEntidade`, `PlanejadorAlteracoes.ExcluirGrupo` |
| [RN78](#rn78--painel-acesso) | Painel: só GESTOR e ADMINISTRADOR | `PoliticaPermissao.PodeVerPainel` |
| [RN79](#rn79--painel-a-mesma-visibilidade-da-listagem) | Painel: a mesma visibilidade da listagem | `PreparadorConsultaRegistros.Escopo`, `ConsultaRegistrosLinq.Filtrar` |
| [RN80](#rn80--painel-período-e-fuso-de-negócio) | Painel: período e fuso de negócio | `PeriodoPainel`, `OpcoesPainel` |
| [RN81](#rn81--painel-volume) | Painel: volume | `PainelRepository.ResumirVolumeAsync`, `ObterVisaoGeral` |
| [RN82](#rn82--painel-aprovação) | Painel: aprovação | `PainelRepository.ResumirAprovacaoAsync`, `ObterVisaoGeral` |
| [RN83](#rn83--painel-avaliação-e-ranking) | Painel: avaliação e ranking | `PainelRepository.ResumirAvaliacaoAsync`, `ObterVisaoGeral` |
| [RN84](#rn84--painel-atividade) | Painel: atividade | `PainelRepository.ListarAtividadeAsync` |
| [RN85](#rn85--indicadores-dimensões) | Indicadores: dimensões | `InterpretadorIndicadores`, `ConsultaIndicadoresLinq` |
| [RN86](#rn86--indicadores-métricas) | Indicadores: métricas | `InterpretadorIndicadores`, `MontadorLinhas` |
| [RN87](#rn87--indicadores-ordenação-limite-e-truncado) | Indicadores: ordenação, limite e truncado | `InterpretadorIndicadores`, `MontadorLinhas` |
| [RN88](#rn88--indicadores-exportação) | Indicadores: exportação | `ExportarIndicadores` |
| [RN89](#rn89--caminhos-por-referência) | Caminhos por referência (filtros, dimensões e métricas) | `CaminhoCampo`, `ConsultaRegistrosLinq`, `ConsultaIndicadoresLinq` |
| [RN90](#rn90--visibilidade-da-listagem) | Visibilidade da listagem: equipes habilitadas | `PoliticaPermissao.PodeVerListagem`, `ListarGrupos`, `MetadadosProviderExtensoes` |
| [RN91](#rn91--as-equipes-do-usuário-nesta-listagem) | "As equipes do usuário nesta listagem" | `PoliticaPermissao.EquipesNaListagem`, `PoliticaPermissao.NaListagem`, `ResolverEquipe` |
| [RN92](#rn92--equipe-deixa-de-ser-habilitada-numa-listagem-com-itens) | Equipe deixa de ser habilitada numa listagem com itens | `PlanejadorAlteracoes.EquipesDaListagem`, `DefinirEquipesListagem` |
| [RN93](#rn93--listagem-nova-sem-equipes-equipe-nova-sem-listagens) | Listagem nova sem equipes; equipe nova sem listagens | `CriarGrupo`, `CriarEquipe`, `PoliticaPermissao.PodeVerListagem` |
| [RN94](#rn94--itens-relacionados) | Itens relacionados: descoberta, contagem e visibilidade | `Relacoes.QueApontamPara`, `ListarRelacionados`, `ConsultaRelacionadosLinq` |
| [RN95](#rn95--ícone-do-tipo-de-item) | Ícone do tipo de item | `RegrasMetadados.ValidarIcone`, `regras-metadados.ts → classeIcone`, `SeletorIconeComponent` |
| [RN96](#rn96--itens-recentes-do-usuário) | Itens recentes do usuário ("Continuar de onde parou") | `ListarRecentes`, `ConsultaRecentesLinq` |
| [RN97](#rn97--importação-de-planilhas-fluxo-e-permissão) | Importação de planilhas: fluxo e permissão | `AcessoImportacao`, `PoliticaPermissao.PodeImportar`, `GravadorRegistro` |
| [RN98](#rn98--arquivo-da-importação-formatos-limites-modelo-e-colunas) | Arquivo da importação: formatos, limites, modelo e colunas | `ArquivosImportacao`, `LeitorCsv`, `PlanilhaImportada`, `SugestaoMapeamento`, `ObterModeloImportacao` |
| [RN99](#rn99--referências-pelo-nome-na-importação) | Referências pelo nome na importação | `ResolvedorReferencias`, `SimuladorImportacao`, `ConsultasImportacao` |
| [RN100](#rn100--valores-das-células) | Valores das células | `ConversorCelulas` |
| [RN101](#rn101--prévia-e-execução-da-importação) | Prévia e execução da importação | `ValidarImportacao`, `ExecutarImportacao`, `SimuladorImportacao`, `ResumoImportacao` |
| [RN102](#rn102--duplicados-na-importação) | Duplicados na importação (no arquivo e no banco) | `DuplicadosNoArquivo`, `SimuladorImportacao.Classificar` |
| [RN103](#rn103--desfazer-uma-importação) | Desfazer uma importação | `DesfazerImportacao`, `RegrasDesfazer`, `PlanejadorExclusao` |
| [RN104](#rn104--histórico-de-importações-e-limpeza) | Histórico de importações e limpeza dos arquivos | `ListarImportacoes`, `LimparImportacoes`, `LimpezaImportacoes` |

---

## RN01 — Campos desconhecidos

Toda chave enviada no JSON precisa ser a `Chave` de um campo da entidade. A comparação é **exata**
(diferencia maiúsculas): `"Nome"` não é o campo `nome`.
Erro no campo enviado: *"Campo desconhecido na entidade 'X'."* As chaves recusadas aqui e na RN02 saem também em
`detalhes.camposDesatualizados`: quem enviou está com metadados antigos (o front recarrega e pede revisão, ADR 0043).

## RN02 — Campos inativos

Campo com `Ativo = 0` continua no banco (os dados antigos são preservados), mas:

- enviar um valor para ele gera o erro *"Campo inativo: não pode mais ser preenchido."*;
- ele não aparece em `GET /entidades`, e o front não deve mostrá-lo.

## RN03 — Obrigatoriedade

Um campo conta como **vazio** quando está ausente, é `null` ou é um texto só com espaços.

- Campo vazio com `Obrigatorio = 1` → *"Campo obrigatório."*
- Campo vazio opcional → ignorado, e **não** é gravado no JSON.

## RN04 — Validação e normalização por tipo

Cada `TipoDado` tem um validador (padrão Strategy, `backend/src/Catalogo.Domain/Validacao/Tipos`). Ele
confere o valor **e o normaliza**: o valor gravado é sempre o normalizado. Os tipos JSON são
estritos, então um número enviado como texto (`"42"`) é recusado.

| Tipo | Aceita | Grava (normalizado) |
|---|---|---|
| `TEXTO` | texto JSON com até `TamanhoMaximo` caracteres, contados **depois** do trim | texto sem espaços nas pontas |
| `TEXTO_LONGO` | texto JSON de qualquer tamanho | texto sem espaços nas pontas |
| `INTEIRO` | número inteiro de 32 bits (-2147483648 a 2147483647), sem casas decimais | o número |
| `DECIMAL` | número com até `Escala` casas decimais (zeros à direita não contam) e até `Precisao − Escala` dígitos antes da vírgula | o número com exatamente `Escala` casas (`12.5` → `12.50`) |
| `BOOLEANO` | `true` ou `false` (não aceita `"true"`, `0` nem `1`) | `true`/`false` |
| `DATA` | ISO 8601 `aaaa-mm-dd` (ex.: `"2026-10-21"`) | igual |
| `DATA_HORA` | ISO 8601 **com fuso** (ex.: `"2026-10-21T14:30:00-03:00"` ou `"...Z"`). Sem fuso é recusado, porque não dá para saber o instante | em UTC: `"2026-10-21T17:30:00Z"` |
| `LISTA` | exatamente uma das `Opcoes` (diferencia maiúsculas) | igual |
| `REFERENCIA` | GUID no formato `D` (`3f2504e0-4f89-11d3-9a0c-0305e82c3301`). Aqui só se confere o formato; existência é RN06 | GUID em minúsculas |
| `URL` | texto JSON com um endereço **absoluto**, esquema `http` ou `https`, com host, **sem espaços** e com até **2000** caracteres (contados depois do trim). Ex.: `"https://grafana.interno/d/abc"`. `exemplo.com`, `ftp://...` e `javascript:...` são recusados ([ADR 0042](adr/0042-tipo-de-dado-url.md)) | texto sem espaços nas pontas (caminho e consulta como vieram) |

## RN05 — Todos os erros de uma vez

A validação **não para no primeiro erro**. A resposta 400 traz todos os erros, agrupados por chave de campo:

```json
{
  "title": "Um ou mais campos são inválidos.",
  "status": 400,
  "codigo": "VALIDACAO",
  "traceId": "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-00",
  "errors": {
    "campo_inexistente": ["Campo desconhecido na entidade 'Sigla'."],
    "codigo_sigla": ["Deve ter no máximo 20 caracteres (foram informados 30)."],
    "comunidade": ["Campo obrigatório."]
  }
}
```

São duas etapas: primeiro RN01 a RN04 (sem banco); se passarem, RN06 e RN08 (com banco), cujos erros também voltam juntos.

Todo erro da API (não só a validação) traz `codigo` e `traceId`; a tabela de códigos está em
[api/codigos-de-erro.md](api/codigos-de-erro.md) ([ADR 0023](adr/0023-codigos-de-erro-estaveis-no-problemdetails.md)).

## RN06 — Referências existem e são da entidade certa

Cada GUID num campo `REFERENCIA` precisa ser o id de um registro **existente** e da entidade
indicada em `Campo.EntidadeReferenciadaId`. Caso contrário: *"Registro {id} não encontrado."* ou
*"O registro {id} não é da entidade 'Projeto'."*. O banco garante o mesmo com FKs compostas. A
aplicação confere antes para dar uma mensagem amigável.

## RN07 — Referências fora do JSON

Campos `REFERENCIA` **nunca** são gravados em `Registro.Dados`: vão para `RegistroReferencia`, com
integridade garantida pelo banco. Para o cliente o registro é plano: as referências aparecem em
`dados` junto com os campos simples, como GUID. Ver [modelo-de-dados.md](modelo-de-dados.md#como-um-campo-vira-dado).

## RN08 — Campos dependentes

Uma `CampoDependencia` diz: **"em `Campo`, só vale escolher registros cujo `CampoAlvo` seja igual
ao valor de `CampoOrigem`"**.

- `CampoVia` nulo: `CampoOrigem` é um campo do **próprio** registro.
- `CampoVia` preenchido: segue essa referência (um salto) e lê `CampoOrigem` no registro apontado.

Exemplo do seed: em `cenario_teste`, o `endpoint_servico` escolhido precisa ser do mesmo `projeto`
do `relatorio_teste` escolhido.

| Coluna | Valor |
|---|---|
| `CampoId` | `cenario_teste.endpoint_servico` |
| `CampoViaId` | `cenario_teste.relatorio_teste` |
| `CampoOrigemId` | `relatorio_teste.projeto` |
| `CampoAlvoId` | `endpoint_servico.projeto` |

Regras de aplicação:

- campo dependente vazio → nada a conferir;
- valor de origem vazio → sem filtro, aceita qualquer registro;
- origem preenchida e alvo diferente (ou vazio) → erro no **campo dependente**.

A mesma regra servirá para o front filtrar as opções do select (endpoint fora do escopo desta etapa).

## RN09 — Unicidade

Uma `RestricaoUnica` (simples ou composta) impede dois registros da mesma entidade com os mesmos
valores normalizados. Funcionamento ([adr/0005](adr/0005-unicidade-por-hash.md)):

1. Cada valor é normalizado para texto:
   - **TEXTO, TEXTO_LONGO e LISTA**: trim, sem acentos e em maiúsculas (cultura invariante). Assim `"Comunidade Ágil"` = `"  COMUNIDADE AGIL"`, coerente com a collation `Latin1_General_CI_AI`;
   - **INTEIRO**: cultura invariante;
   - **DECIMAL**: cultura invariante, com a escala do campo (`12.5` → `"12.50"`);
   - **BOOLEANO**: `"true"`/`"false"`;
   - **DATA**: `aaaa-mm-dd`;
   - **DATA_HORA**: ISO 8601 em UTC;
   - **REFERENCIA**: GUID em minúsculas, formato `D`;
   - **URL**: trim, com o **esquema e o host** (e a porta) em minúsculas; caminho, consulta e fragmento como vieram
     (`HTTPS://Exemplo.com/a` = `https://exemplo.com/a`, mas `.../A` é outra).
2. Os textos são unidos, **na ordem dos campos da restrição** (`Campo.Ordem`, depois `Chave`),
   pelo separador `\u001f`.
3. O resultado em UTF-8 vai para o SHA-256, gravado em `RegistroChaveUnica`.

**Nas entidades por equipe (RN27), a unicidade vale dentro de cada equipe**: o id da equipe entra
como primeira parte do texto do hash. Duas equipes podem ter um projeto "Portal" cada, e um 409 nunca
revela que existe um registro de outra equipe (RN28). Nas entidades globais, vale para o sistema todo.

A aplicação confere antes de gravar (409 `CONFLITO_UNICIDADE` com mensagem amigável e
`"detalhes": { "restricao": "uq_comunidade_nome", "campos": ["nome"] }`), e o UNIQUE
(`RestricaoUnicaId`, `ValorHash`) do banco garante a regra mesmo com gravações simultâneas. Se
o banco barrar (erros 2601/2627), a resposta também é 409.

> ⚠️ Mudar a normalização muda os hashes. Os registros já gravados precisariam de um script para recalcular.

## RN10 — Nulo não gera chave única

Se **qualquer** campo de uma restrição estiver vazio, a chave dessa restrição não é gerada. É a
mesma semântica do UNIQUE do SQL: nulo não conflita com nada. Exemplo: `uq_link_rtc` só vale para
links que têm RTC.

## RN11 — Ações de exclusão: RESTRINGIR e CASCATA

Todo campo `REFERENCIA` tem uma `AcaoExclusao`, que diz o que acontece ao excluir o registro **apontado**:

- **RESTRINGIR**: a exclusão é bloqueada enquanto houver registros apontando para ele. O banco já
  garante isso: `FK_RegistroReferencia_Destino` não tem cascade.
- **CASCATA**: os registros que apontam para ele são excluídos junto. **A aplicação** faz isso,
  excluindo os filhos antes, na mesma transação. Ex.: excluir um `relatorio_teste` exclui os seus
  `cenario_teste` e `link`.

Ao excluir um registro, o banco apaga em cascata as **suas próprias** linhas de `RegistroReferencia`
e `RegistroChaveUnica` (`ON DELETE CASCADE`).

Como a exclusão é montada, passo a passo, está em [RN16](#rn16--plano-de-exclusão).

## RN12 — Concorrência otimista

`Registro.Versao` é um `rowversion`: o SQL Server muda o valor a cada UPDATE. O GET devolve a
`versao` (em Base64), e o PUT a envia de volta **no corpo**, junto com os campos
([ADR 0009](adr/0009-concorrencia-pela-versao-no-corpo.md)):

```json
PUT .../registros/{id}
{ "versao": "AAAAAAAAB9I=", "nome": "Comunidade Ágil" }
```

- `versao` ausente ou inválida → **400** na chave `versao`.
- `versao` diferente da atual → **409** (`ConflitoConcorrenciaException`), sem gravar nada.
- A proteção continua até a gravação: o UPDATE é `... WHERE Versao = @versaoLida`. Se alguém
  gravar entre a leitura e o UPDATE, a resposta também é **409**.

## RN13 — Colunas de sistema

`id`, `criadoEm`, `atualizadoEm` e `versao` são colunas do `Registro`, preenchidas pelo sistema, e
não são cadastradas como `Campo` (RN14). Na resposta elas ficam fora do objeto `dados`. No corpo do
POST e do PUT, que é plano, são ignoradas (o PUT lê a `versao`). Datas de sistema são sempre UTC.

## RN14 — Chaves reservadas

Nenhum `Campo` pode ter como `Chave` (sem diferenciar maiúsculas):

| Chaves | Por quê |
|---|---|
| `id`, `versao`, `criadoEm`, `atualizadoEm`, `equipeId`, `criadoPor`, `atualizadoPor`, `situacaoAprovacao`, `ultimaDecisao`, `avaliacao` | colunas de sistema do registro: o corpo traz a `versao` (PUT) e o `equipeId` (POST) junto com os campos |
| `pagina`, `tamanho`, `ordenacao`, `busca`, `dependeDe`, `formato` | parâmetros da listagem, das opções e da exportação (RN49): um campo com esse nome não poderia ser filtrado na URL |
| `agruparPor`, `metricas`, `ordenarPor`, `limite`, `de`, `ate` | parâmetros do painel (RN80, RN85 a RN87), que aceita os filtros da listagem na mesma URL. Na listagem, dão **400** ("parâmetro válido só no painel") |

Um metadado assim é erro de configuração: o carregamento dos metadados do grupo falha com uma
mensagem que lista os campos inválidos (o endpoint responde 500 e o log mostra a mensagem).
No corpo do POST/PUT, as colunas de sistema não são campos: o PUT lê a `versao`, o POST lê o `equipeId`
(RN27) e as demais são ignoradas (a situação só muda pela decisão, RN36, e a avaliação tem endpoints próprios, RN45). Na listagem, `equipeId` e `situacaoAprovacao`
(RN37) são filtros, e `avaliacao.<critério>` é prefixo de filtro e de ordenação (RN48); as demais chaves reservadas como filtro dão **400**.

## RN15 — Atualização substitui o registro inteiro

`PUT .../registros/{id}` recebe **todos** os campos ([ADR 0008](adr/0008-atualizacao-via-put.md)):

- campo opcional **ausente** (ou nulo, ou texto em branco) = campo **removido**, do JSON, das
  referências, dos valores tipados e, se fizer parte de uma restrição, da chave única (RN10);
- vale a **mesma validação do criar** (RN01 a RN09);
- o registro **não conflita consigo mesmo** na unicidade: manter o próprio valor é permitido;
- `atualizadoEm` recebe a data e hora atual (UTC) e a resposta traz a nova `versao`.
- Campos **inativos** são a exceção: não aparecem na resposta nem são enviados, e o PUT **preserva** os valores deles
  (em `Dados`, `RegistroValor` e `RegistroReferencia`). Reativado o campo, o valor volta (RN69).

## RN16 — Plano de exclusão

`DELETE .../registros/{id}` faz exclusão **física** ([ADR 0010](adr/0010-exclusao-fisica-com-plano.md)).
Antes de apagar qualquer coisa, monta um **plano**:

1. Busca quem aponta para o registro (`RegistroReferencia.RegistroDestinoId`).
2. Para cada referência, olha a `AcaoExclusao` do campo:
   - **CASCATA**: quem aponta entra no plano, e o passo 1 se repete para ele;
   - **RESTRINGIR**: **bloqueia**, a menos que quem aponta também já esteja no plano.
3. Cada registro é visitado uma vez só (ciclos não travam o processo).
4. **Tudo ou nada**: um bloqueio em qualquer nível impede a exclusão de tudo. A resposta é **409**
   com a lista do que impede:
   ```json
   { "title": "Exclusão bloqueada.", "status": 409, "codigo": "EXCLUSAO_BLOQUEADA",
     "detalhes": { "dependencias": [ { "entidade": "endpoint_servico", "campo": "projeto", "quantidade": 2,
                                       "exemplos": ["cadastro", "consulta"] } ] } }
   ```
   `exemplos` traz até 5 rótulos (RN20); sem rótulo, o id.
5. Plano válido: exclui **dos filhos para o pai**, numa única transação, e responde **204**. Ao
   excluir um registro, o banco apaga junto as referências que **saem** dele, as chaves únicas e os
   valores tipados (CASCADE). As referências que **chegam** nele não têm CASCADE, por isso a ordem
   importa. Num ciclo (A → B → A), a referência que fecha o ciclo é apagada antes.

Exemplo: excluir um `relatorio_teste` exclui os seus `cenario_teste` e `link` (CASCATA), mas não o
`rtc` nem o `endpoint_servico` para os quais eles apontam.

## RN17 — Paginação

`GET .../registros` devolve uma página ([ADR 0012](adr/0012-paginacao-por-pagina.md)):

```json
{ "itens": [ ... ], "pagina": 1, "tamanho": 20, "total": 57, "totalPaginas": 3 }
```

- `pagina`: a partir de 1 (padrão 1). Página além da última devolve `itens` vazio.
- `tamanho`: de 1 a **100** (padrão 20). Fora disso, **400**.
- `total` conta todos os registros que passam nos filtros.
- O mesmo envelope e os mesmos parâmetros (e limites) valem para **toda** listagem de dados da API: opções de campo, históricos,
  avaliações individuais, fila de aprovação, usuários e equipes. Metadados e configurações pequenas (grupos, entidades,
  modelos de avaliação) voltam como array simples.

## RN18 — Filtros

Os demais parâmetros da URL são filtros, no formato `<chave>` ou `<chave>.<operador>`, e se
combinam com **E**:

| Operador | Tipos | Exemplo |
|---|---|---|
| (nenhum) = igual | todos | `metodo=POST`, `projeto=<guid>`, `ativo=true` |
| `contem` | TEXTO, TEXTO_LONGO, URL | `nome_servico.contem=pix`, `url.contem=grafana` |
| `de` / `ate` (inclusivos) | INTEIRO, DECIMAL, DATA, DATA_HORA | `data_execucao.de=2026-10-01` |

- Os valores seguem os formatos de RN04 (decimal com ponto, data `aaaa-mm-dd`, GUID formato `D`,
  `true`/`false`). LISTA precisa ser uma das opções.
- **DATA_HORA** aceita data e hora com fuso (instante exato) **ou só a data**. Só a data = o **dia
  inteiro em UTC**: `data_execucao=2026-10-21` pega o dia todo, e `ate=2026-10-31` vai até 23:59:59 de 31/10.
- Igualdade e `contem` em texto **não diferenciam maiúsculas nem acentos** (collation do banco).
  `%`, `_` e `[` são buscados literalmente.
- REFERENCIA filtra pela tabela `RegistroReferencia`; os demais tipos, pela `RegistroValor` (ADR 0013).
- Erros (todos juntos, **400**, no formato de RN05, com a chave = nome do parâmetro):
  - chave desconhecida ou de campo inativo;
  - chave reservada (RN14);
  - operador desconhecido ou inválido para o tipo;
  - valor que não converte para o tipo.
- Filtro pela média de um critério de avaliação: `avaliacao.<criterio>.de` / `.ate` (RN48).
- A chave pode ser um **caminho por referência**, até 2 saltos (RN89): `relatorio_teste.tipo_teste=Carga`,
  `relatorio_teste.data_execucao.de=2026-01-01`, `relatorio_teste.ambiente.nome.contem=homolog`. Os operadores seguem o tipo do último campo.

## RN19 — Ordenação

`ordenacao=<chave>` (crescente) ou `ordenacao=-<chave>` (decrescente). A chave é de um campo, ou
`criadoEm`/`atualizadoEm`. O padrão é `-criadoEm` (mais recentes primeiro).

- A ordem respeita o tipo: números como números (9.5 antes de 10), datas como datas.
- Não é possível ordenar por TEXTO_LONGO (**400**). URL ordena como texto.
- **REFERENCIA** ordena pelo **valor do campo de exibição** do registro apontado (o rótulo, RN20), com o tipo dele: texto em ordem
  alfabética, número como número, data como data. Registros **sem referência** (ou com o rótulo vazio) ficam **sempre por último**,
  nos dois sentidos. Se a entidade apontada não tiver campo de exibição: **400**. Em LINQ, por subconsulta em `RegistroReferencia` →
  `RegistroValor` (`ConsultaRegistrosLinq.OrdenarPorReferencia`).
- Registros sem valor no campo vêm primeiro na ordem crescente. Empates são desfeitos pelo `id`,
  para a paginação ser estável.
- `ordenacao=[-]avaliacao.<criterio>` ordena pela média de um critério de avaliação, com os itens sem nota
  **sempre por último** (RN48).

## RN20 — Rótulo das referências

Em **todo** retorno de registro (GET por id, listagem, POST e PUT), cada referência vem como
`{ "id": "<guid>", "rotulo": "<texto>" }`. Na entrada (POST e PUT), a referência continua sendo só o GUID.

- O rótulo é o valor do **campo de exibição** (`Entidade.CampoExibicaoId`) do registro apontado,
  convertido em texto no mesmo formato do JSON (ex.: `35.50`, `2026-10-21T17:30:00Z`, `true`).
- `rotulo` é **nulo** se a entidade apontada não tem campo de exibição, se ele é REFERENCIA, ou se está vazio no registro.
- Os rótulos são buscados **em lote**: uma consulta para todos os registros da resposta, nunca uma por linha.

## RN21 — Opções de campo dependente

`GET .../entidades/{entidade}/campos/{campo}/opcoes` devolve, no formato de RN17, itens
`{ "id", "rotulo" }` ordenados por rótulo, para o front montar o select de um campo **REFERENCIA**
(outro tipo: **400**).

| Parâmetro | Uso |
|---|---|
| `busca` | trecho do rótulo (`contem`, sem diferenciar maiúsculas nem acentos). Exige rótulo texto. |
| `pagina`, `tamanho` | como em RN17 |
| `dependeDe` | o valor de que o campo depende |
| `equipeId` | a equipe do registro do formulário (que pode ainda não existir). Mesma regra do POST (RN27): obrigatório para o administrador e para quem está em várias equipes |

- **Sem `CampoDependencia`**: todos os registros da entidade referenciada.
- **Com `CampoDependencia`** (RN08), `dependeDe` é **obrigatório** (400 se faltar):
  - com `CampoVia`: `dependeDe` é o id do registro escolhido **no campo via** (ex.: o relatório do
    cenário). O valor de origem é lido em `CampoOrigem` desse registro;
  - sem `CampoVia`: `dependeDe` já é o valor de `CampoOrigem` no formulário;
  - as opções são os registros cujo `CampoAlvo` aponta para esse valor de origem. Origem vazia = sem filtro (RN08).
- O `GET .../entidades` informa, em cada campo, `dependeDe`: a chave do campo do formulário cujo
  valor o front envia. Ex.: em `cenario_teste.endpoint_servico`, `dependeDe = "relatorio_teste"`.
- **Limitações desta versão** (respondem 400 com a explicação): `CampoOrigem` e `CampoAlvo`
  precisam ser REFERENCIA, e a entidade referenciada precisa ser do mesmo grupo.
- As opções são só as **visíveis para a equipe do registro**: os registros dessa equipe ou globais (RN29).
- É só conveniência: o POST e o PUT continuam validando a dependência no servidor (RN08).

## RN22 — Limites do DECIMAL

Um campo DECIMAL pode ter no máximo **10 casas decimais** (`Escala <= 10`) e **28 dígitos antes da
vírgula** (`Precisao - Escala <= 28`), para caber em `RegistroValor.ValorNumero decimal(38,10)`.
Garantido pelo CHECK `CK_Campo_Decimal`, recriado no script `V002`.

## RN23 — Autenticação

Todo endpoint exige login, exceto `POST /api/v1/auth/login` e `GET /health`. Detalhes técnicos em
[seguranca.md](seguranca.md).

- `POST /api/v1/auth/login` com `{ matricula, senha }` devolve um **JWT** (8 horas por padrão, sem refresh token).
- A matrícula não diferencia maiúsculas.
- Qualquer falha (matrícula inexistente, senha errada, usuário inativo ou bloqueado) responde
  **401** com a **mesma** mensagem: *"Matrícula ou senha inválidas."*. Nunca se revela se a matrícula existe.
- No máximo 10 tentativas de login por minuto por endereço IP (configurável). Acima disso, **429**.
- O token só identifica a pessoa. Perfil, equipes e situação são lidos do banco a cada requisição,
  com cache de até 1 minuto. Desativar um usuário, trocar o perfil ou as equipes vale **na hora**
  quando feito pela API (o cache é descartado), ou em até 1 minuto se feito direto no banco.
- Usuário desativado com token ainda válido recebe **401** na próxima requisição.
- Sem token ou com token inválido/expirado: **401**.

## RN24 — Bloqueio por tentativas

- **5** senhas erradas seguidas bloqueiam o login por **15 minutos**, mesmo com a senha certa.
- Login certo zera a contagem. Depois que o bloqueio vence, a contagem recomeça do zero.
- A redefinição de senha pelo administrador também desbloqueia.

## RN25 — Senha e troca obrigatória

- **Política**: pelo menos 10 caracteres, com letra e número. A nova senha tem que ser diferente da atual.
- Senha nunca é registrada em log, devolvida pela API ou gravada no histórico. No banco fica só o hash (PBKDF2).
- Usuário criado pelo administrador, ou com senha redefinida, recebe uma **senha provisória** gerada pelo
  sistema (16 caracteres), devolvida **uma única vez**, e `DeveTrocarSenha = 1`.
- Com `DeveTrocarSenha = 1`, o login funciona, mas todos os outros endpoints respondem **403** com
  `"codigo": "TROCA_DE_SENHA_OBRIGATORIA"`. As exceções são `POST /api/v1/auth/trocar-senha` (`{ senhaAtual, novaSenha }`)
  e `GET /api/v1/auth/eu`, que o front usa para saber que precisa pedir a troca.

## RN26 — Matriz de permissões

Cada usuário tem **um** perfil, que vale em todas as equipes dele ([ADR 0014](adr/0014-perfil-unico-por-usuario.md)).
"Suas equipes" são as equipes **ativas** às quais ele pertence. **Dentro de uma listagem**, "suas equipes" são só as habilitadas
nela: "as equipes do usuário nesta listagem" ([RN91](#rn91--as-equipes-do-usuário-nesta-listagem)). Uma listagem sem nenhuma equipe
dele nem aparece para ele ([RN90](#rn90--visibilidade-da-listagem)).

| Perfil | Ver | Criar | Alterar | Excluir | Escopo |
|---|---|---|---|---|---|
| EXTERNO | ✅ | ❌ | ❌ | ❌ | suas equipes e registros globais |
| INTERNO | ✅ | ✅ | ✅ | ❌ | ver: suas equipes e globais; escrever: só nas suas equipes |
| GESTOR | ✅ | ✅ | ✅ | ✅ | ver: suas equipes e globais; escrever e excluir: só nas suas equipes |
| ADMINISTRADOR | ✅ | ✅ | ✅ | ✅ | qualquer equipe e registros globais, sem precisar pertencer a elas (para criar, só equipes habilitadas na listagem, RN91) |

- A regra fica num lugar só: `PoliticaPermissao` (Domain). Os casos de uso perguntam a ela.
- Perfil sem permissão para a operação num registro que ele **vê**: **403**.
- Listagem e opções aplicam o escopo **dentro da consulta**: o não-administrador vê a união das suas
  equipes **nesta listagem** (RN91) mais os globais. Todos podem filtrar por `equipeId`; o não-administrador, só entre as suas
  nesta listagem (senão, 403).
- Usuários e equipes (`/api/v1/usuarios`, `/api/v1/equipes`) são administrados só pelo ADMINISTRADOR (403 para os demais).
- Na exclusão em cascata (RN16), **todos** os registros do plano precisam estar dentro do que o usuário
  pode excluir (senão, 403 e nada é excluído).
- Avaliar itens e configurar modelos de avaliação (RN40, RN44, RN47) também são regras da `PoliticaPermissao`.
- A tabela acima vale para as entidades **sem aprovação**. Com a aprovação ativa numa entidade e equipe
  (RN33), o EXTERNO passa a criar e a alterar o que criou (RN34, RN35), e GESTOR e ADMINISTRADOR decidem
  (RN36). Tudo isso está na **mesma** `PoliticaPermissao`, que recebe a situação e o autor do registro.

## RN27 — Registros globais e por equipe

A `Entidade` define o escopo dos seus registros ([ADR 0017](adr/0017-escopo-global-ou-por-equipe-na-entidade.md)):

- **Por equipe** (`EscopoEquipe = 1`, o padrão): cada registro pertence a uma equipe (`Registro.EquipeId`),
  que **não muda** depois de criado. Um `equipeId` diferente no PUT dá 400.
- **Global** (`EscopoEquipe = 0`): tabelas de apoio compartilhadas. Todos os perfis veem; **só o
  ADMINISTRADOR** cria, altera ou exclui.

Equipe na criação, pela chave reservada `equipeId` no corpo do POST:

| Situação | `equipeId` |
|---|---|
| Entidade global | não aceito (400) |
| Usuário com **uma** equipe **nesta listagem** (RN91) | opcional: se ausente, é ela |
| Usuário com **várias** equipes nesta listagem, ou ADMINISTRADOR | **obrigatório** (400 se faltar) |
| Não-administrador informando equipe da qual não participa, ou que é dele mas não está habilitada na listagem (RN91) | 403 |
| Administrador informando equipe inexistente, inativa ou não habilitada na listagem (RN91) | 400 |

A classificação das entidades de Testes de Desempenho está em
[listagens/testes-desempenho.md](listagens/testes-desempenho.md#escopo-das-entidades-a-confirmar) e **ainda precisa ser confirmada** com o negócio.

## RN28 — Registro de outra equipe é 404

Um registro que o usuário **não pode ver** é tratado como **inexistente**: **404**, com a mesma
mensagem de um id que não existe. Isso vale para GET, PUT, DELETE e histórico, e também quando um
POST/PUT aponta para ele (*"Registro ... não encontrado."*). Assim a API não revela que o registro
existe ([ADR 0019](adr/0019-404-para-registro-de-outra-equipe.md)).

## RN29 — Isolamento de referências entre equipes

- Um registro de uma equipe só referencia registros **da mesma equipe** ou **globais**.
- Um registro global só referencia registros globais.
- A regra é sobre os **registros**, não sobre o usuário: vale **mesmo que ele pertença às duas equipes**.
  O POST e o PUT respondem 400 no campo (*"O registro ... é de outra equipe"*).
- As opções de campo (RN21) listam só o que o registro do formulário pode referenciar.
- Consequência: uma exclusão em cascata nunca atravessa equipes.

## RN30 — Último administrador

Não é possível desativar nem rebaixar o **último** administrador ativo: **409** com
`"codigo": "ULTIMO_ADMINISTRADOR"`. O primeiro administrador é criado pelo comando `criar-admin`, que só
funciona se não houver nenhum administrador ativo (ver o [README do back-end](../backend/README.md)).

## RN31 — Usuários e equipes não são excluídos

- Usuários e equipes **nunca são excluídos**, só desativados (`PATCH .../ativacao`), para preservar o histórico.
- Usuário **não-administrador** precisa de pelo menos **uma equipe ativa** (400 ao criar ou alterar sem
  ela). O administrador não precisa de equipe.
- Não é possível desativar uma equipe que é a **única equipe ativa** de algum usuário ativo não-administrador:
  **409** com `"codigo": "USUARIOS_SEM_EQUIPE"` e `"detalhes": { "quantidade" }`. Mova esses usuários para outra equipe antes.
- Vínculos com equipes desativadas são ignorados nas permissões.

## RN32 — Histórico de alterações

Toda criação, alteração e exclusão de **registro**, **usuário** e **equipe**, toda **decisão** de aprovação
(operações `APROVACAO` e `REJEICAO`, RN36), toda mudança de **configuração** de aprovação (alvo `CONFIGURACAO`, RN33),
de **modelo de avaliação** (alvo `MODELO_AVALIACAO`, RN40) e de **avaliação** (alvo `AVALIACAO`, RN45) grava uma linha em
`HistoricoAlteracao` ([ADR 0018](adr/0018-historico-explicito-no-caso-de-uso.md)):

- no **mesmo SaveChanges** da operação: se o histórico falhar, a operação inteira falha;
- uma linha por alvo. Numa exclusão em cascata, uma linha por registro excluído, todas com o mesmo `CorrelacaoId`;
- com `DadosAntes` e `DadosDepois`, o objeto **plano**:
  - para registros, os campos no formato do corpo do POST, com referências como GUID;
  - para usuários, `matricula`, `nome`, `perfil`, `ativo` e `equipes`;
  - para equipes, `nome` e `ativa`;
  - para decisões, `situacaoAprovacao` antes e depois, e a **justificativa** numa coluna própria;
  - para configurações, `requerAprovacao`;
  - para modelos de avaliação, `modelo` (id), `nome`, `descricao`, `ativo` e uma chave por critério (`criterios.qualidade`);
  - para avaliações, `avaliacao` (id), `modelo` (nome) e uma chave por resposta (`qualidade.estrelas`, `qualidade.comentario`, `observacoes.texto`);
  - na criação, `DadosAntes` é nulo; na exclusão, `DadosDepois` é nulo;
- para usuários, **nunca** contém senha nem hash. Troca e redefinição de senha aparecem como o campo
  `senha` com `"alterada"` ou `"redefinida"`, sem valor;
- o registro guarda `criadoPor` e `atualizadoPor`, devolvidos como `{ id, matricula, nome }`;
- a linha diz a **origem** da gravação: nula no cadastro manual (tela ou API) e `IMPORTACAO` quando veio de uma importação de planilha,
  com o `CorrelacaoId` da importação (RN97). Sai em `origem` no histórico do registro.

Consulta, do mais recente para o mais antigo, paginada:

| Rota | Quem pode |
|---|---|
| `GET .../registros/{id}/historico` | quem pode **ver** o registro. Se ele já foi excluído, vale a equipe gravada no histórico |
| `GET /api/v1/usuarios/{id}/historico`, `GET /api/v1/equipes/{id}/historico` | só ADMINISTRADOR |
| `GET /api/v1/equipes/{equipeId}/configuracoes/grupos/{grupo}/entidades/{entidade}/aprovacao/historico` | quem pode configurar (RN33) |
| `GET /api/v1/equipes/{equipeId}/configuracoes/grupos/{grupo}/entidades/{entidade}/avaliacao/historico` | quem pode configurar (RN40) |
| `GET .../registros/{id}/avaliacoes/historico` | INTERNO, GESTOR e ADMINISTRADOR que veem o registro (RN47); funciona para registro excluído |

Cada item traz quem, quando, a operação, a `justificativa` (só nas decisões) e a lista de **mudanças por campo** (`campo`, `de`, `para`),
calculada a partir do antes e do depois. Em campos REFERENCIA, `de` e `para` vêm como `{ id, rotulo }`,
com o rótulo **atual** (nulo se o registro apontado não existe mais).

Não geram histórico: login, contagem de tentativas e último acesso.

## RN33 — Aprovação ativável por entidade e equipe

A aprovação é ligada **por entidade e por equipe**, na tabela `ConfiguracaoEntidadeEquipe`
([ADR 0020](adr/0020-aprovacao-por-entidade-e-equipe.md)). Sem linha na tabela, a aprovação está **desligada**,
e tudo funciona como na RN26.

`GET` e `PUT /api/v1/equipes/{equipeId}/configuracoes/grupos/{grupo}/entidades/{entidade}/aprovacao`, com `{ "ativa": true }`:

| Quem | Resultado |
|---|---|
| ADMINISTRADOR | configura qualquer equipe |
| GESTOR | configura as suas equipes; nas outras, **404** (como a RN28) |
| INTERNO, EXTERNO | **403** |

- Só equipes **habilitadas na listagem** (RN91), para todos, inclusive o ADMINISTRADOR: equipe não habilitada, **404**; listagem que
  o usuário não vê (RN90), **404**.
- Só entidades **por equipe** (RN27) têm aprovação. Entidade global: **400** no campo `entidade`.
- **Desativar** com registros PENDENTES nessa entidade e equipe: **409** com `"codigo": "APROVACOES_PENDENTES"`
  e `"detalhes": { "quantidade" }`. Decida os pendentes antes. Registros REJEITADOS não impedem a desativação: continuam
  rejeitados e, sem aprovação, o EXTERNO não os altera mais.
- Toda mudança gera histórico (RN32). Um PUT que não muda nada não gera.
- `GET .../grupos/{grupo}/entidades?equipeId=...` devolve `requerAprovacao` por entidade (sem `equipeId`, vem nulo;
  nas globais, `false`). O não-administrador só consulta as suas equipes nesta listagem (RN91; 403 nas outras).
- Sem `equipeId`, cada entidade traz `equipesComAprovacao`: as equipes **que o usuário vê** nesta listagem (RN91; o ADMINISTRADOR, as habilitadas)
  com a aprovação ligada para ela. Vazia = nenhuma; sempre vazia nas globais. A tela usa para decidir se mostra a situação
  e o filtro (ADR 0045).

## RN34 — Situação de aprovação e criação

Todo registro tem uma `situacaoAprovacao`: **PENDENTE**, **APROVADO** ou **REJEITADO**. Os registros
anteriores à aprovação e os de entidades sem aprovação são APROVADO.

| Quem cria, com aprovação ativa | Situação inicial |
|---|---|
| EXTERNO | **PENDENTE** |
| INTERNO, GESTOR, ADMINISTRADOR | **APROVADO** |

- Sem aprovação ativa, o EXTERNO continua sem criar (403, RN26).
- A resposta do registro traz `situacaoAprovacao` e `ultimaDecisao` (`{ decisao, justificativa, decididoPor, decididoEm }`,
  ou nulo se nunca houve decisão). `decisao` é `APROVAR` ou `REJEITAR`.

## RN35 — Alteração e reenvio pelo EXTERNO

Com aprovação ativa, o EXTERNO altera **só os registros que ele criou**, e só enquanto estão **PENDENTE** ou
**REJEITADO** (403 nos demais). Alterar um REJEITADO é um **reenvio**: o registro volta para **PENDENTE**, e a
alteração no histórico mostra a mudança de `situacaoAprovacao`. O EXTERNO **nunca exclui**.

INTERNO, GESTOR e ADMINISTRADOR alteram como na RN26, sem mudar a situação.

## RN36 — Decisão: aprovar ou rejeitar

`POST .../registros/{id}/aprovacao` com `{ "decisao": "APROVAR" | "REJEITAR", "justificativa": "..." }`:

- Decide o **GESTOR** da equipe do registro ou o **ADMINISTRADOR**. INTERNO e EXTERNO: **403**. GESTOR de outra equipe: **404**.
- `justificativa` é obrigatória, de **10 a 1000** caracteres (sem os espaços das pontas): 400 no campo.
- Só registros **PENDENTES**: nos demais, **409** com `"codigo": "REGISTRO_NAO_PENDENTE"`.
- A decisão grava quem, quando e a justificativa no registro (a `versao` muda, RN12) e gera histórico
  (`APROVACAO` ou `REJEICAO`) com a justificativa.
- Fila: `GET /api/v1/aprovacoes/pendentes`, do **mais antigo** para o mais novo, paginada, com filtros `grupo`,
  `entidade` e `equipeId`. GESTOR vê as suas equipes (filtrar por outra: 403); ADMINISTRADOR vê todas; os demais, 403. Só entram
  listagens que o usuário vê e, em cada uma, as equipes habilitadas nela (RN90, RN91): os pendentes de uma equipe que deixou de ser
  habilitada saem da fila (RN92). Filtrar por uma listagem que o usuário não vê: 404.
- Cada registro é aprovado **individualmente**: aprovar um relatório não aprova os cenários que apontam para ele.

## RN37 — Visibilidade de registros não aprovados

- O **EXTERNO** vê os registros **APROVADOS** e os **seus** (de qualquer situação). PENDENTES e REJEITADOS
  de outras pessoas são tratados como inexistentes (**404**, RN28) e não aparecem na listagem nem nas opções.
- INTERNO, GESTOR e ADMINISTRADOR veem todas as situações, dentro do seu escopo de equipe.
- O filtro fica **dentro da consulta LINQ**, como o escopo de equipe (RN26).
- A listagem aceita `situacaoAprovacao=PENDENTE|APROVADO|REJEITADO` (400 para outro valor).

## RN38 — Referências a registros não aprovados

Um registro só referencia registros **APROVADOS** ou registros não aprovados **do mesmo autor**: o autor do
registro de origem (na criação, quem cria) tem de ser quem criou o destino. Senão, **400** no campo
(*"O registro ... ainda não foi aprovado."*). Assim o EXTERNO monta um relatório e os seus cenários antes da aprovação.

As opções de campo (RN21) seguem a mesma regra, com quem está preenchendo o formulário como autor. A RN29
(mesma equipe) continua valendo. Cada opção traz a `situacaoAprovacao` do registro apontado (`"APROVADO"` quando a
aprovação não se aplica), para a tela marcar os não aprovados ("Relatório X (pendente)").

## RN39 — Unicidade e exclusão de registros não aprovados

- Registros PENDENTES e REJEITADOS **ocupam** as chaves únicas (RN09): um segundo registro com a mesma chave
  dá 400, mesmo que o primeiro ainda não tenha sido aprovado.
- GESTOR e ADMINISTRADOR excluem registros em qualquer situação (RN16). A exclusão em cascata não olha a situação.

## RN40 — Quem configura os modelos de avaliação

Os itens podem ser **avaliados** pelos usuários, sempre o **item inteiro** (um registro). O formulário de
avaliação é um **modelo**, configurado **por entidade e por equipe**, como a aprovação (RN33)
([ADR 0021](adr/0021-avaliacao-configuravel-com-tabelas-tipadas.md)).

Rotas sob `/api/v1/equipes/{equipeId}/configuracoes/grupos/{grupo}/entidades/{entidade}/avaliacao`:

| Método | Rota | O que faz |
|---|---|---|
| GET | `.../avaliacao` | o modelo **ativo**, com todos os critérios em ordem (404 se não há) |
| GET | `.../avaliacao/modelos` | todos os modelos, ativo e inativos (para achar o id de um inativo) |
| POST | `.../avaliacao` | cria o modelo com a lista de critérios (**201**; **409** se já há um ativo, RN41) |
| PUT | `.../avaliacao` | altera nome, descrição e a lista **inteira** de critérios, com a `versao` do GET (RN12, RN43) |
| PATCH | `.../avaliacao/ativacao` | `{ "ativo": false }` desativa o ativo; `{ "ativo": true, "modeloId": "..." }` reativa um modelo |
| GET | `.../avaliacao/historico` | histórico dos modelos (RN32) |

- Quem configura: o **GESTOR** das suas equipes (**404** nas outras) e o **ADMINISTRADOR** (qualquer equipe), sempre só nas
  equipes **habilitadas na listagem** (RN91; não habilitada: 404). INTERNO e EXTERNO: **403**. É a mesma regra da aprovação
  (`PoliticaPermissao.PodeConfigurarEquipe`).
- Só entidades **por equipe** (RN27) têm modelo. Entidade global: **400** no campo `entidade`.
- `GET .../entidades?equipeId=...` devolve, por entidade, o `modeloAvaliacao` ativo (só os critérios ativos), para
  o front montar o formulário. Sem `equipeId` ou sem modelo, vem nulo.
- Sem `equipeId`, cada entidade traz `criteriosAvaliacao`: os critérios de **estrelas** ativos dos modelos ativos nas
  equipes que o usuário vê, como `{ "chave", "nome" }` (sem repetir a chave, na ordem dos critérios). São as colunas,
  a ordenação e o filtro por média da listagem (RN48). Vazia nas globais e sem modelo ativo.
- Toda criação, alteração, ativação e desativação gera histórico (alvo `MODELO_AVALIACAO`).

## RN41 — Um modelo ativo por entidade e equipe

- No máximo **um** modelo **ativo** por entidade e equipe (índice único filtrado no banco). Criar ou reativar
  com outro ativo: **409** com `"codigo": "MODELO_ATIVO_EXISTENTE"` e `"detalhes": { "modeloId" }` do ativo.
- Modelos **nunca são excluídos**. **Desativado**, o modelo para de aceitar avaliações (RN44), mas as que ele
  tem continuam visíveis, **separadas**, com o nome do modelo (RN47). A equipe pode então criar outro modelo.
- A listagem de Testes de Desempenho **não usa avaliação** por enquanto: o seed não cria nenhum modelo.

## RN42 — Critérios: tipos e comentário

Cada critério tem `chave`, `nome`, `descricao`, `tipo`, `obrigatorio`, `comentario`, `tamanhoMaximoTexto`,
`ordem` e `ativo`:

| `tipo` | Resposta | `comentario` |
|---|---|---|
| `ESTRELAS` | nota inteira de **1 a 5**, mais um comentário que justifica a nota | `NENHUM` (não aceita), `OPCIONAL` ou `OBRIGATORIO` |
| `TEXTO` | texto livre | sempre `NENHUM` |

- `chave`: obrigatória, padrão `^[a-z0-9_]+$`, até 100 caracteres, **única no modelo**. Identifica o critério
  nas respostas, nas médias e na listagem (`avaliacao.qualidade`).
- `nome` obrigatório (até 150), `descricao` até 500.
- `tamanhoMaximoTexto` (padrão 2000) vale para o texto livre e para o comentário: de **1 a 4000**.
- Padrões: `obrigatorio` false, `comentario` NENHUM, `ordem` = posição na lista, `ativo` true.
- O modelo precisa de `nome` (até 150) e de **pelo menos um critério ativo**.
- Todos os erros voltam juntos (RN05), com chaves como `nome`, `criterios` e `criterios[1].tipo`.

## RN43 — Alterar um modelo que já tem avaliações

O PUT envia a lista **inteira** de critérios; eles são casados pela `chave`. Para não corromper as notas já dadas:

| Pode | Não pode (com respostas no critério) |
|---|---|
| mudar nome e descrição do modelo e dos critérios, ordem, obrigatoriedade, comentário e tamanho | **excluir** o critério (tirá-lo da lista) |
| **adicionar** critérios | **mudar o tipo** do critério |
| **desativar** um critério (`"ativo": false`) | |

- O que não pode: **400**, explicando que o critério deve ser **desativado**. Um critério **sem** respostas pode ser excluído
  ou mudar de tipo.
- Critério desativado: some do formulário e das médias; as respostas antigas ficam (e aparecem no histórico).
- Regras novas (critério novo, obrigatoriedade nova) valem só para avaliações **criadas ou editadas depois**: as antigas
  não ficam inválidas. Ao editar, a avaliação é validada pelo modelo **atual**.
- `GET .../avaliacao` informa, por critério, `possuiRespostas`, e o modelo traz `quantidadeAvaliacoes`.

## RN44 — Quem avalia e o que pode ser avaliado

- Avaliam **INTERNO, GESTOR e ADMINISTRADOR**, em itens que eles **veem** (INTERNO e GESTOR: das suas equipes nesta listagem, RN91;
  ADMINISTRADOR: qualquer um). Item que o usuário não vê: **404** (RN28). **EXTERNO não avalia: 403**.
- O item precisa ter um modelo **ativo** para a entidade **na equipe dele**. Senão: **400** no campo `modelo`.
- Só itens **APROVADOS** são avaliados: com a aprovação ativa (RN33), PENDENTES e REJEITADOS dão **409** com
  `"codigo": "REGISTRO_NAO_APROVADO"`. Sem aprovação ativa, os registros já são APROVADOS.
- Ordem das verificações: quem (404/403), modelo (400), situação (409), respostas (400, RN46).

## RN45 — Uma avaliação por usuário, item e modelo

- `PUT /api/v1/grupos/{grupo}/entidades/{entidade}/registros/{id}/avaliacoes/minha` **cria** (201) ou **edita** (200) a
  avaliação do usuário logado: `{ "versao": "...", "respostas": { "qualidade": { "estrelas": 4, "comentario": "..." },
  "observacoes": { "texto": "..." } } }`. A `versao` é obrigatória **só ao editar** (RN12: versão antiga, 409).
- Cada usuário tem **uma** avaliação por item e por modelo (UNIQUE no banco). Com um modelo novo, ele pode avaliar
  de novo; a avaliação do modelo antigo fica.
- Editar substitui as respostas dos critérios **ativos**: critério ativo não enviado = resposta removida. Respostas a
  critérios desativados ficam como estão. Uma edição que não muda nada não grava nem gera histórico.
- `DELETE .../avaliacoes/minha` remove a avaliação do usuário no modelo ativo (404 se ele não avaliou).
- **Excluir o item exclui as avaliações** (CASCADE no banco). O histórico guarda o que existia: uma linha
  `EXCLUSAO` por avaliação, com o mesmo `CorrelacaoId` da exclusão do registro.
- Toda criação, edição e remoção gera histórico (alvo `AVALIACAO`, com o registro como alvo), com as respostas
  antes e depois.

## RN46 — Validação das respostas

Todos os erros juntos (RN05), com chaves `respostas.<criterio>` e `respostas.<criterio>.<propriedade>`:

| Situação | Erro |
|---|---|
| critério desconhecido ou inativo | `respostas.inexistente` |
| critério obrigatório sem resposta (ausente ou nulo) | `respostas.qualidade` |
| resposta que não é objeto, ou com propriedade desconhecida | `respostas.qualidade`, `respostas.qualidade.nota` |
| `estrelas` ausente, fora de 1 a 5, não inteira (4.5) ou não numérica ("4") | `respostas.qualidade.estrelas` |
| `comentario` num critério com `comentario = NENHUM` | `respostas.clareza.comentario` |
| `comentario` ausente (ou em branco) num critério com `comentario = OBRIGATORIO` | `respostas.qualidade.comentario` |
| `texto` vazio, só espaços, ausente ou acima de `tamanhoMaximoTexto` (o mesmo vale para o comentário) | `respostas.observacoes.texto` |

Critério opcional não enviado (ou nulo) = não respondido. Textos são gravados sem os espaços das pontas; comentário em
branco = sem comentário.

## RN47 — Resumo e visibilidade das avaliações

`GET .../registros/{id}/avaliacoes` devolve:

- `resumo`: o do modelo **ativo** (nulo se não há): `quantidadeAvaliadores` e, para cada critério `ESTRELAS` **ativo**,
  `media` (uma casa decimal, arredondada "metade para cima"; nula sem notas), `quantidade` e `distribuicao`
  (`{ "1": n, ..., "5": n }`), e `minha` (a avaliação do usuário logado, se houver);
- `resumosAnteriores`: os mesmos dados para cada modelo **inativo** que avaliou o item, identificado pelo nome;
- `avaliacoes`: as avaliações **individuais** (autor, respostas, modelo), paginadas, das mais recentes (criadas ou
  editadas) para as mais antigas.

Visibilidade:

- **Todos que veem o item** (inclusive o EXTERNO) veem os resumos.
- As avaliações individuais e o histórico delas são só para **INTERNO, GESTOR e ADMINISTRADOR**. Para o EXTERNO,
  `avaliacoes` vem **nulo** e o histórico dá **403**.
- Médias e contagens são calculadas **no banco**, em LINQ (`GroupBy`, `Average`, `Count`), sem trazer as avaliações para a memória.

## RN48 — Avaliação no registro e na listagem

- Com modelo ativo para a entidade na equipe do registro, o retorno do registro ganha
  `"avaliacao": { "medias": { "qualidade": 4.3 }, "quantidade": 3, "minha": true }`: média de cada critério `ESTRELAS`
  ativo (nula sem notas), quantas avaliações e se o usuário logado avaliou. **Sem modelo ativo, `avaliacao` é nulo** e
  nada mais muda. `avaliacao` é chave reservada (RN14).
- Listagem: `ordenacao=[-]avaliacao.<criterio>` ordena pela média, com os itens **sem nota sempre por último**.
- Listagem: `avaliacao.<criterio>.de=4` e `.ate=4` filtram pela média **como ela é exibida** (uma casa decimal): `de=4`
  inclui uma média 3.96 (exibida 4.0). Itens sem nota não passam no filtro.
- Vale para critérios `ESTRELAS` **ativos** dos modelos **ativos** nas equipes que o usuário vê. Outro critério:
  **400** no parâmetro, com a lista dos aceitos.

## RN49 — Exportação da listagem

`GET .../registros/exportar?formato=csv|xlsx` exporta **exatamente o que a listagem mostra ao usuário**: os mesmos
filtros (RN18) e ordenação (RN19) e as mesmas regras de visibilidade (equipe, RN26; aprovação, RN37). A consulta é montada
pelo mesmo `PreparadorConsultaRegistros` da listagem. `pagina` e `tamanho` são ignorados; `formato` é obrigatório (400 no parâmetro).

- **Colunas**: os campos **ativos**, na `Ordem` dos metadados, com o `Nome` do campo no cabeçalho; referências pelo **rótulo**
  (RN20; o id, se não houver rótulo); BOOLEANO como "Sim"/"Não". No fim: **Equipe** (nas entidades por equipe), **Situação de
  aprovação** (se a aprovação está ativa para a entidade em alguma equipe que o usuário vê), **Média: &lt;critério&gt;** para
  cada critério ESTRELAS ativo dos modelos ativos (RN48), **Criado por**, **Criado em** e **Atualizado em**.
- Datas e horas saem no fuso de `Exportacao:FusoHorario` (padrão `America/Sao_Paulo`): o Excel não guarda fuso.
- **Limite**: `Exportacao:LimiteLinhas` (padrão **10.000**). Acima disso, **400** com `"codigo": "EXPORTACAO_MUITO_GRANDE"`
  (mensagem em `errors.exportacao`), pedindo mais filtros. Nada é gerado.
- **CSV**: UTF-8 com BOM, separador `;`, vírgula decimal, datas `dd/MM/aaaa` e `dd/MM/aaaa HH:mm:ss`, aspas no padrão RFC 4180.
  **XLSX**: cabeçalho em negrito e congelado, filtro automático, números como número e datas como data.
- **Injeção de fórmula**: textos que começam com `=`, `+`, `-`, `@`, tab ou carriage return recebem um **apóstrofo** no início,
  no CSV e no XLSX. Números e datas não mudam.
- **Arquivo**: `<entidade>_<aaaa-mm-dd>.<formato>` (data no fuso da exportação), no cabeçalho `Content-Disposition`.
- **Em lotes**: os registros são lidos em páginas de 500 da mesma consulta; o primeiro lote antes de responder (para conferir
  o limite), os demais enquanto o arquivo é escrito. O CSV sai linha a linha; o XLSX é montado na memória antes de sair.
- A exportação não gera histórico (é uma leitura).

## RN50 — Quem administra os metadados

Grupos (listagens), entidades (tipos de item), campos, restrições únicas, dependências e campo de exibição são criados e
alterados **pela API**, em `/api/v1/admin/metadados` ([ADR 0025](adr/0025-administracao-de-metadados-so-pelo-administrador.md)),
e não mais por script. Passo a passo em [api/administracao-de-metadados.md](api/administracao-de-metadados.md).

- **Só o ADMINISTRADOR.** Os metadados são compartilhados por todas as equipes: a mudança de um gestor afetaria as outras.
  GESTOR, INTERNO e EXTERNO recebem **403** em toda a rota `/admin/metadados` (inclusive nas leituras).
- O GESTOR continua configurando o que é da equipe dele: aprovação (RN33) e avaliação (RN40).
- A leitura dos metadados de uso (`GET .../grupos/{grupo}/entidades`) continua como está para todos.

## RN51 — Chaves imutáveis

As chaves de grupo, entidade, campo e restrição **não mudam** depois de criadas
([ADR 0027](adr/0027-chaves-de-metadados-imutaveis.md)): elas fazem parte das URLs, do JSON dos registros, dos filtros e do
contrato com o front. Nome e descrição podem mudar.

- No PUT, a chave pode vir (igual) ou não vir; diferente, **400** na chave `chave`.
- Num campo REFERENCIA, a **entidade referenciada** também não muda (400 em `entidadeReferenciada`). A `acaoExclusao` muda.
- Para "renomear" de verdade: crie o campo novo e desative o antigo (RN69).

## RN52 — Versão, histórico e cache dos metadados

- **Concorrência** (como RN12): toda escrita envia a `versao` do que altera: a do **grupo** para alterar o grupo ou criar uma
  entidade nele; a da **entidade** para alterá-la e para tudo nos campos, restrições e dependências dela. Toda alteração de
  campo, restrição ou dependência também muda a versão da entidade. Versão ausente ou inválida: **400** em `versao`;
  diferente da atual: **409** `CONFLITO_CONCORRENCIA`. Nos DELETE, a versão vai na query (`?versao=`, URL-encoded).
- **Histórico**: **uma** linha `METADADO` por mudança, com antes, depois e, se registros foram reescritos (valor padrão, chaves
  únicas), `registrosAfetados` (não uma linha por registro). `AlvoId` = grupo; a entidade e o campo vão nos dados.
  `GET /api/v1/admin/metadados/grupos/{grupo}/historico`; com `?entidade=<chave>`, só as linhas daquela entidade (ela, os campos,
  as restrições e as dependências dela). Cada linha traz `alvo`, a descrição do que mudou ("Campo 'nome' de 'equipamento'"),
  porque as mudanças só listam o que mudou.
- **Cache**: toda gravação invalida o cache de metadados do grupo, e a mudança vale na hora (no servidor que a fez).

## RN53 — Configuração: chaves e tipos

Validadas no Domain (`RegrasMetadados`), sem olhar os dados, com todos os erros juntos (RN05):

- **Chaves** de grupo, entidade, campo e restrição: padrão `^[a-z0-9_]+$`, até 100 caracteres, únicas no escopo (sem
  diferenciar maiúsculas, como a collation). Campo não pode usar chave reservada (RN14) nem `ordem` (a rota de reordenação).
- **Nome** obrigatório (até 150) e descrição até 500. Entidade: **nome no plural** (`nomePlural`) opcional, até 150 (sem ele, as telas
  usam o nome).
- **Tipos**, as mesmas regras dos CHECKs do banco:

| Tipo | Atributos |
|---|---|
| `TEXTO` | `tamanhoMaximo` de 1 a 4000 (obrigatório) |
| `DECIMAL` | `precisao` de 1 a 38, `escala` de 0 a 10 e até a precisão, parte inteira (precisão − escala) até 28 dígitos (RN22) |
| `LISTA` | `opcoes`: pelo menos uma, sem vazias nem repetidas (sem os espaços das pontas) |
| `REFERENCIA` | `entidadeReferenciada` e `acaoExclusao` (RESTRINGIR ou CASCATA) |
| os demais (inclusive `URL`) | nenhum: atributo de outro tipo é erro |

- **Ordem dos campos**: `PUT .../campos/ordem` com **todas** as chaves (ativas e inativas), cada uma uma vez. A ordem dos campos é a
  ordem do hash das restrições compostas (RN09): as chaves dessas restrições são recalculadas na mesma transação.

## RN54 — Configuração: referências e campo de exibição

- A entidade referenciada é do **mesmo grupo** e está **ativa**.
- **Uma entidade global só tem REFERENCIAs para entidades globais**: um registro global não aponta para registros de equipe (RN29).
- **Campo de exibição**: campo **ativo** da própria entidade, de tipo diferente de REFERENCIA e TEXTO_LONGO. Só depois de criar
  os campos (no PUT da entidade).

## RN55 — Configuração: restrições únicas e dependências

- **Restrição única**: de **1 a 5** campos **ativos** da entidade, sem TEXTO_LONGO nem repetidos; nome no padrão das chaves, único
  na entidade.
- **Dependência** (semântica da RN08), `PUT .../campos/{campo}/dependencia` com `{ campoVia, campoOrigem, campoAlvo }` (chaves):
  - o campo dependente é REFERENCIA;
  - o via (opcional) é REFERENCIA da mesma entidade;
  - a origem é da entidade apontada pelo via, ou da própria entidade sem via;
  - o alvo é da entidade referenciada pelo campo dependente;
  - origem e alvo são REFERENCIA e apontam para a **mesma** entidade;
  - todos ativos, e **sem ciclos**: o valor de um campo não pode depender, por outras dependências, dele mesmo.

## RN56 — Simulação

Toda escrita da administração aceita **`?simular=true`** ([ADR 0029](adr/0029-simulacao-de-alteracoes-de-metadados.md)):
executa **todas** as validações, inclusive contra os dados existentes, e devolve `{ "permitido", "simulado": true, "impactos": [...] }`
**sem gravar nada** (a transação é desfeita).

- Os impactos são o **mesmo** relatório que o bloqueio traria (RN58), e nos casos permitidos mostram o efeito (ex.: quantos
  registros vão receber o valor padrão).
- Erros de configuração (RN53 a RN55) continuam sendo **400**, também na simulação. A versão é conferida (409).

## RN57 — Desativar e excluir campo, entidade e grupo

**Desativar em vez de excluir** ([ADR 0028](adr/0028-desativar-em-vez-de-excluir-metadados.md)): a exclusão só existe para
o que nunca foi usado; o resto é desativado e pode ser reativado.

| | Desativar | Excluir |
|---|---|---|
| Campo | RN69: se não é de exibição, de restrição ou de dependência ativa; os valores ficam | RN70: só sem nenhum valor e sem usos |
| Entidade | RN75: some para os não-administradores; somente leitura para o administrador | RN76: só se nunca teve registros e não é referenciada |
| Grupo | RN77: como a entidade, para todas as entidades dele | RN77: só sem entidades |

## RN58 — Alterações em estruturas com dados

Princípio ([ADR 0026](adr/0026-bloquear-alteracoes-incompativeis-com-os-dados.md)): **toda mudança é validada contra os dados
existentes; se algum registro ficaria inválido, ela é bloqueada**, e nada é migrado automaticamente. A única escrita em massa
pedida pelo administrador é o **valor padrão** (RN60, RN61); a outra é técnica: gerar e recalcular as chaves únicas (RN71, RN53).

- Bloqueio: **409** com `"codigo": "ALTERACAO_INCOMPATIVEL_COM_DADOS"` e o **relatório de impacto** em `detalhes.impactos`:
  `[{ tipo, descricao, bloqueia, quantidade, exemplos: [{ id, rotulo }], grupos, usos }]`, com **até 10 exemplos** (e até 10 grupos
  de duplicados). Quando o que impede é um uso na configuração (o campo é de exibição, a entidade é referenciada), o código é
  `METADADO_EM_USO` e os lugares vêm em `usos`.
- As verificações são LINQ sobre `Registro`, `RegistroValor` e `RegistroReferencia`, numa **transação Serializable** que abrange a
  verificação e a gravação: um registro inserido no meio não escapa.
- As escritas em massa são em **lotes de 500**, mantendo `Dados`, `RegistroValor` e `RegistroChaveUnica` coerentes, na mesma
  transação. A `versao` de cada registro reescrito muda: quem estava editando um deles recebe 409 (RN12).
- Os códigos de `tipo` dos impactos estão em [api/administracao-de-metadados.md](api/administracao-de-metadados.md#relatório-de-impacto).

## RN59 — Adicionar campo opcional

Sempre permitido. Os registros existentes ficam sem valor nele.

## RN60 — Adicionar campo obrigatório

- Entidade **sem registros**: permitido, sem valor padrão.
- Entidade **com registros**: exige `valorPadrao`, gravado em **todos** os registros existentes (em `Dados` e `RegistroValor`: o
  valor aparece nos filtros da listagem). Sem ele: 409, impacto `CAMPO_OBRIGATORIO_SEM_VALOR`.
- O `valorPadrao` é validado e normalizado como o valor de um registro (RN04: ex.: texto sem espaços nas pontas). Não vale para
  REFERENCIA (cada equipe aponta para os seus registros, RN29): crie o campo opcional, preencha e depois torne-o obrigatório.

## RN61 — Tornar obrigatório um campo existente

Bloqueado se houver registros **sem valor**, a menos que venha um `valorPadrao`, que preenche **só os vazios**. O `valorPadrao` não
vale para campo de restrição única (os registros vazios ficariam duplicados) nem para REFERENCIA.
Reativar um campo obrigatório é conferido do mesmo jeito (os registros criados enquanto ele estava inativo não têm valor).

## RN62 — Tornar opcional

Sempre permitido.

## RN63 — Diminuir o tamanho máximo

TEXTO: bloqueado se algum valor tiver mais caracteres que o tamanho novo (impacto `TEXTO_ACIMA_DO_TAMANHO`).

## RN64 — Aumentar tamanho, precisão ou escala

Sempre permitido, dentro dos limites do tipo (RN53). **Atenção**: aumentar **só a escala** diminui a parte inteira (DECIMAL(12,2)
→ (12,4) passa de 10 para 8 dígitos inteiros), e por isso é conferido como a RN65.

## RN65 — Diminuir precisão ou escala

DECIMAL: bloqueado se algum valor não couber, isto é, se tiver mais casas decimais que a escala nova (`Math.Round(valor, escala) != valor`)
ou mais dígitos na parte inteira que (precisão − escala) (impacto `NUMERO_FORA_DA_PRECISAO`). Como a escala entra no hash da chave
única (RN09), as chaves das restrições com o campo são recalculadas.

## RN66 — Mudar o tipo

**Não permitido** (400 em `tipoDado`), com estas exceções:

| De → para | Quando |
|---|---|
| `TEXTO` → `TEXTO_LONGO` | sempre, quanto aos dados (o TEXTO_LONGO aceita tudo). Não vale se o campo for de exibição ou de restrição única (400) |
| `TEXTO` ou `TEXTO_LONGO` → `URL` | só se **todo valor existente** for uma URL válida (RN04). Senão, **409** `ALTERACAO_INCOMPATIVEL_COM_DADOS` com o impacto `VALOR_NAO_E_URL` (quantidade e exemplos). As chaves únicas do campo são recalculadas (a normalização muda, RN09). [ADR 0042](adr/0042-tipo-de-dado-url.md) |
| `URL` → `TEXTO_LONGO` | sempre, quanto aos dados. Não vale se o campo for de exibição ou de restrição única (400) |

Para outros casos: crie um campo novo e desative o antigo.

## RN67 — LISTA: adicionar opção

Sempre permitido.

## RN68 — LISTA: remover opção

Bloqueado se algum registro usa a opção removida (impacto `OPCAO_EM_USO`).

## RN69 — Desativar campo

- Permitido se o campo **não** é campo de exibição nem participa de restrição única ou de dependência ativa (como campo dependente,
  via, origem ou alvo). Senão, 409 `METADADO_EM_USO`, com os usos.
- **Os valores são mantidos**, mas o campo some dos formulários, da resposta dos registros, dos filtros, da ordenação e da exportação.
- Um PUT de registro **preserva** os valores dos campos inativos, em vez de apagá-los (RN15). **Reativar devolve tudo.**

## RN70 — Excluir campo

Só se **nenhum** registro tem valor nele e ele não é usado em restrição, dependência (ativa ou não) ou exibição. Senão, 409: a
orientação é desativar (RN69).

## RN71 — Adicionar restrição única

- Bloqueado se os registros existentes já têm duplicatas (impacto `DUPLICATAS_NA_RESTRICAO`, com os **grupos** de duplicados). A
  comparação usa o **mesmo hash** da unicidade (RN09: sem acentos e maiúsculas, dentro da equipe); valor vazio não conflita (RN10).
- Se passar, gera as chaves (`RegistroChaveUnica`) de todos os registros existentes, na mesma transação, com a mesma
  `CalculadoraChaveUnica`: a partir daí, duplicatas novas são barradas.

## RN72 — Remover restrição única

Sempre permitido. As chaves dela saem junto.

## RN73 — Adicionar ou alterar dependência

Bloqueado se algum registro existente viola a regra nova (semântica da RN08: origem preenchida e alvo diferente ou vazio; impacto
`DEPENDENCIA_VIOLADA`). Remover a dependência é sempre permitido.

## RN74 — Mudar o escopo da entidade

`escopoEquipe` (global ↔ por equipe) só muda se a entidade **não tem registros** nem configuração de equipe (aprovação ou modelo de
avaliação). Nos dois sentidos, respeita a RN54: a que vira global só referencia globais; a que vira por equipe não pode ser referenciada
por entidades globais.

## RN75 — Desativar entidade

Permitido. Depois:

- **Não-administradores**: a entidade some (de `GET .../entidades`) e responde **404**.
- **Administrador**: vê, lista e exporta, mas é **somente leitura**: criar, alterar e excluir registros, decidir aprovação e avaliar
  dão **409** `ENTIDADE_INATIVA`.
- Campos de outras entidades que a referenciam **continuam funcionando**, mas as opções dela não aparecem (`.../opcoes` vazio) e um
  vínculo **novo** com um registro dela é recusado (400); os vínculos existentes ficam.

## RN76 — Excluir entidade

Só se ela **nunca teve registros** (o histórico deles aponta para ela e é permanente, RN32), não tem configuração de equipe e não é
referenciada por campos de outras entidades. Os campos, restrições e dependências dela saem junto. Senão: desative-a.

## RN77 — Desativar ou excluir grupo

- **Desativar**: o grupo some para os não-administradores (de `GET /grupos`; 404 nas rotas dele); as entidades ficam somente leitura
  para o administrador, como na RN75.
- **Excluir**: só sem entidades (409 `METADADO_EM_USO`). Um grupo com entidades é desativado.

---

# Painel gerencial

Guia da API, com exemplos: [`docs/api/painel.md`](api/painel.md). Decisões: [ADR 0030](adr/0030-indicadores-genericos-pelos-metadados.md),
[ADR 0031](adr/0031-agregacao-no-banco-em-linq.md) e [ADR 0032](adr/0032-painel-so-para-gestor-e-administrador.md).

## RN78 — Painel: acesso

O painel (`/api/v1/painel/...`: visão geral, indicadores e exportação dos indicadores) é do **GESTOR** e do **ADMINISTRADOR**.
INTERNO e EXTERNO recebem **403** `SEM_PERMISSAO`. A regra está em `PoliticaPermissao.PodeVerPainel`.

## RN79 — Painel: a mesma visibilidade da listagem

Os números contam **exatamente** os registros que o usuário veria na listagem (RN26, RN27, RN37): o escopo é montado pelo mesmo
`PreparadorConsultaRegistros` e aplicado pela mesma `ConsultaRegistrosLinq.Filtrar`, dentro do SQL.

- **GESTOR**: as entidades globais inteiras e, nas por equipe, só as equipes dele **nesta listagem** (RN91). Um registro de outra
  equipe (ou de uma equipe dele não habilitada na listagem) não muda nenhum número. Listagem que ele não vê (RN90): 404.
- **ADMINISTRADOR**: tudo.
- `equipeId` (opcional) restringe as entidades **por equipe** a uma equipe; as **globais continuam inteiras** (não são de equipe nenhuma).
  O GESTOR só pode informar uma equipe dele nesta listagem (senão, 403).
- Entidades e grupos desativados seguem a RN75 e a RN77 (somem para o GESTOR; o ADMINISTRADOR vê).

## RN80 — Painel: período e fuso de negócio

- `de` e `ate` são **datas** `aaaa-mm-dd` no **fuso de negócio** (`Painel:FusoHorario`, padrão `America/Sao_Paulo`), inclusivas. Sem elas,
  o período não tem limite daquele lado. `de` depois de `ate`: 400.
- As datas de sistema (`CriadoEm`, histórico) estão em UTC. Os limites são convertidos no código (00:00 local de `de` até 00:00 local do
  dia seguinte a `ate`) e o agrupamento por período (`criadoEm:mes`, a série por mês) soma o deslocamento do fuso **na consulta**
  (`DATEADD(minute, ...)`). Ex.: um registro criado em 2026-04-01 01:00 UTC é de **março** (31/03 22:00 em São Paulo).
- O deslocamento é **fixo** (`TimeZoneInfo.BaseUtcOffset`): não há horário de verão (o Brasil não tem desde 2019). Se voltar a ter, a
  conversão precisa mudar.
- Campos **DATA** e **DATA_HORA** dos usuários são usados **como estão gravados**, sem conversão (a DATA_HORA é gravada em UTC, RN04).
- Na visão geral, o período vale para o que é "no período" (criados, alterados, decisões, atividade); totais, situações e avaliações são
  o **estado atual**. Nos indicadores, `de` e `ate` filtram a data de **criação**.

## RN81 — Painel: volume

Por entidade do grupo (as que o usuário vê): **total** de registros (estado atual), **por equipe**, **criados no período**, **alterados no
período** (registros com pelo menos uma ALTERACAO no histórico dentro do período) e a **série de criados por mês** (mês do fuso de negócio).
O bloco traz também a soma de tudo.

## RN82 — Painel: aprovação

Só onde a aprovação está **ativa** (entidade por equipe e equipe com `RequerAprovacao`, RN33); sem nenhuma, o bloco é `null`.

- **Por situação** (pendentes, aprovados, rejeitados) e **pendentes antigos**: pendentes criados há mais de `Painel:DiasPendenteAntigo`
  dias (padrão 7). Estado atual.
- **Tempo médio até a primeira decisão**: para cada registro cuja **primeira** APROVACAO ou REJEICAO (do histórico, RN36) caiu no
  período, `primeira decisão − CriadoEm`; a média, em horas (2 casas). A base vem em `primeirasDecisoes`. No total do grupo, é a média de
  todos esses tempos juntos (a média por entidade ponderada pela quantidade).
- **Taxa de rejeição**: `rejeições / (aprovações + rejeições)` de **todas** as decisões do período (um registro rejeitado, corrigido e
  aprovado conta as duas), de 0 a 1. Sem decisões: `null`.

## RN83 — Painel: avaliação e ranking

Só nas entidades com **modelo de avaliação ativo** nas equipes vistas (RN40); sem nenhuma, o bloco é `null`. Por entidade e por critério
de **estrelas** ativo (pela chave, juntando os modelos das equipes):

- **média** de todas as notas (2 casas), **quantidade** de notas e **distribuição** (quantas de 1 a 5 estrelas);
- **ranking**: a média de **cada item** no critério; entram só os itens com pelo menos `Painel:MinimoAvaliacoesRanking` notas (padrão
  3); os **5 melhores** (maior média; empate: mais notas) e os **5 piores** (menor média; empate: mais notas), com o rótulo do item.

Só contam notas de modelos e critérios ativos (como o filtro da RN48) e de itens visíveis (RN79).

## RN84 — Painel: atividade

Os **10 usuários** que mais criaram e alteraram registros do grupo no período, pelo histórico (CRIACAO e ALTERACAO, RN32), com matrícula
e nome. A visibilidade é a da RN79, pela equipe gravada no histórico. Ordem: total (criações + alterações), depois matrícula.

## RN85 — Indicadores: dimensões

`GET /api/v1/painel/grupos/{grupo}/entidades/{entidade}/indicadores?agruparPor=...` aceita **até 2** dimensões, separadas por vírgula:

| Dimensão | Valor em cada linha |
|---|---|
| campo **LISTA** ou **BOOLEANO** | o valor |
| campo **REFERENCIA** | `{ "id", "rotulo" }` (o rótulo da listagem, RN20) |
| campo **TEXTO** | o valor; só os **50 mais frequentes** (nos registros da consulta) viram grupos, o resto vai para `"Outros"` |
| campo **DATA** ou **DATA_HORA** com `:dia`, `:mes`, `:trimestre` ou `:ano` | `"2026-03-15"`, `"2026-03"`, `"2026-T1"`, `"2026"` |
| `equipe` (só em entidade por equipe) | `{ "id", "rotulo" }` (nome da equipe) |
| `situacaoAprovacao` | `"PENDENTE"`, `"APROVADO"`, `"REJEITADO"` |
| `criadoEm:<período>` | como as datas, no fuso de negócio (RN80) |
| caminho `<referencia>.<campo>` ou `<referencia>.<referencia>.<campo>` | um campo (dos tipos acima) de um registro **apontado**, até 2 saltos (RN89). Ex.: em `cenario_teste`, `relatorio_teste.ambiente` |

Registros sem valor na dimensão formam um grupo com valor `null`. Sem `agruparPor`: **sempre** uma linha só, com o total (sem registros:
`contagem` 0). TEXTO_LONGO, INTEIRO,
DECIMAL e datas sem período não são dimensões (400).

## RN86 — Indicadores: métricas

`metricas=...` aceita **até 5**: `contagem` (o padrão), `media:<campo>`, `soma:<campo>`, `minimo:<campo>` e `maximo:<campo>` (só
**INTEIRO** e **DECIMAL**) e `media_avaliacao:<criterio>` (critério de estrelas ativo, RN48).

- **`contagem` conta registros** e está em **toda** linha, pedida ou não.
- **média, mínimo e máximo ignoram os registros sem valor**; sem nenhum valor no grupo, `null`. A **soma** sem valores é `0`.
- `media_avaliacao` é a **média das médias dos itens** do grupo (cada item pesa igual, com 1 ou 50 notas); itens sem nota não entram.
- Números com até 4 casas, sem zeros à direita.
- O campo da métrica pode ser um **caminho por referência** (RN89): `media:relatorio_teste.duracao`. A métrica é calculada **por registro
  consultado**: dois cenários do mesmo relatório contam o valor do relatório duas vezes.
- Sem `agruparPor` e sem nenhum registro: a linha vem com `contagem` 0, `soma` 0 e as demais métricas `null`.

## RN87 — Indicadores: ordenação, limite e truncado

- `ordenarPor=<chave>` ordena por uma dimensão ou métrica da consulta (inclusive `contagem`); `-` na frente é decrescente. Dimensões
  REFERENCIA e `equipe` ordenam pelo rótulo; TEXTO deixa `"Outros"` por último. Sem `ordenarPor`: pelas dimensões, crescente. Nulos vêm
  primeiro no crescente.
- `limite` (padrão **100**, máximo **500**): se havia mais linhas, `"truncado": true`.
- Os **filtros** são os da listagem (RN18, RN37, RN48), com o mesmo interpretador. `pagina`, `tamanho` e `ordenacao` não se aplicam (400).
- Todos os erros de todos os parâmetros vêm juntos num 400 (RN05).
- A conta é toda no banco (ADR 0031).

## RN88 — Indicadores: exportação

`GET .../indicadores/exportar?formato=csv|xlsx` com os **mesmos** parâmetros devolve as **mesmas** linhas (inclusive o limite) num arquivo
`<entidade>_indicadores_<aaaa-mm-dd>.<formato>`. Colunas: as dimensões (REFERENCIA e equipe pelo rótulo), `Contagem` e as métricas, com os
nomes da resposta JSON. Usa os geradores e a proteção contra fórmula da exportação da listagem (RN49).

## RN89 — Caminhos por referência

Filtros (da listagem, da exportação e do painel), dimensões e métricas aceitam, no lugar da chave de um campo, um **caminho** que atravessa
campos REFERENCIA até um campo de um registro **apontado**: `<referencia>.<campo>` (1 salto) ou `<referencia>.<referencia>.<campo>`
(2 saltos, o máximo). Em `cenario_teste`: `relatorio_teste.ambiente`, `relatorio_teste.data_execucao:mes`, `relatorio_teste.projeto.sigla`.

- **Validação**: cada salto é um campo **REFERENCIA ativo** da entidade em que se está, e a entidade apontada precisa estar disponível no
  grupo; o último trecho é um campo **ativo** da entidade a que se chegou e segue as regras do tipo de sempre (operadores de RN18, dimensões
  de RN85, métricas de RN86). Mais de 2 saltos, trecho vazio, campo desconhecido ou inativo e salto que não é REFERENCIA são 400, todos
  juntos (RN05), na chave do parâmetro.
- **Filtro com operador**: se a chave inteira é um caminho até um campo, é igualdade; senão, o último trecho é o operador
  (`relatorio_teste.data_execucao.de`).
- **Um valor por registro**: cada campo REFERENCIA aponta para no máximo um registro, então um caminho dá no máximo um valor por registro
  consultado. Registro sem o registro apontado (ou sem valor nele) fica sem valor: grupo `null` na dimensão, fora da média.
- **Visibilidade**: o escopo de equipe e de aprovação (RN26, RN37, RN79) continua sendo aplicado aos registros **consultados**. O caminho só
  lê os registros que eles apontam, que pelo isolamento de referências (RN29) são da mesma equipe ou globais. Um caminho nunca leva a dado
  de outra equipe (teste `Caminho_NuncaExpoeDadosDeOutraEquipe`).
- **No banco**: um JOIN com `RegistroReferencia` por salto, em LINQ, no mesmo SQL da consulta ([ADR 0038](adr/0038-caminhos-por-referencia.md)).

## RN90 — Visibilidade da listagem

Cada listagem (grupo) tem as suas **equipes habilitadas** (`dbo.GrupoEquipe`, V009, [ADR 0046](adr/0046-listagens-habilitadas-por-equipe.md)).
Quem as define é **só o ADMINISTRADOR** (RN50), pela aba **Equipes** da listagem ou pela API
(`GET`/`PUT /api/v1/admin/metadados/grupos/{grupo}/equipes`, [api/administracao-de-metadados.md](api/administracao-de-metadados.md#equipes-habilitadas)).

- Um usuário **não-administrador** vê uma listagem se ela está **ativa** e se **pelo menos uma das suas equipes ativas** está habilitada
  nela. Equipe inativa do usuário não conta.
- O **ADMINISTRADOR** vê todas (as desativadas, só para leitura, RN77).
- Uma listagem que o usuário não vê responde **404** em **todos** os endpoints sob ela: metadados, registros, opções, exportação,
  histórico, painel, configurações da equipe e o filtro `grupo` da fila de aprovação. A API não revela que ela existe (como a RN28).
- `GET /api/v1/grupos` só devolve as listagens que o usuário vê. Cada uma traz `minhasEquipes` (RN91, com id e nome) e
  `possuiEquipes` (se há alguma equipe habilitada; para o ADMINISTRADOR, `false` mostra a faixa de aviso da RN93).
- Uma equipe **inativa** pode continuar habilitada: ao ser reativada, os membros voltam a ver a listagem.
- O detalhe da equipe (`GET /api/v1/equipes/{id}`) informa as listagens em que ela está habilitada (`listagens`).

## RN91 — As equipes do usuário nesta listagem

"As equipes do usuário nesta listagem" = as **equipes ativas** do usuário **∩** as **equipes habilitadas** na listagem. Para o
ADMINISTRADOR, são todas as equipes **ativas habilitadas** nela. Esse conjunto substitui "suas equipes" (RN26) em **todas** as regras
dentro da listagem (`PoliticaPermissao.NaListagem`):

- **Itens por equipe**: visíveis só se a equipe do item está no conjunto. O ADMINISTRADOR vê os itens de qualquer equipe, inclusive os
  de equipes que deixaram de ser habilitadas (RN92). Item fora do conjunto: **404** (RN28).
- **Criação**: o `equipeId` precisa ser do conjunto. Não-administrador com outra equipe (até uma dele que não está habilitada): **403**;
  ADMINISTRADOR com equipe não habilitada: **400** no `equipeId`. Com **uma só** equipe no conjunto, o `equipeId` é opcional (RN27).
- **Itens globais da listagem**: visíveis para quem vê a listagem; só o ADMINISTRADOR altera (RN27).
- **Opções de campo REFERENCIA**, **filtro `equipeId`** da listagem e da exportação: só equipes do conjunto (403 nas outras).
- **Aprovação, avaliação e configurações da equipe** (RN33, RN36, RN40, RN44): só equipes habilitadas na listagem.
- **Fila de aprovação e painel** (RN36, RN79): só listagens visíveis e, em cada uma, as equipes do conjunto.
- No front, o conjunto (`minhasEquipes`) decide o select de equipe do formulário (com uma só, ela é usada sozinha), a **coluna e o
  filtro de Equipe** (só com mais de uma equipe no conjunto, ou para o ADMINISTRADOR) e as opções de equipe do painel, das
  configurações da equipe e da fila.

## RN92 — Equipe deixa de ser habilitada numa listagem com itens

Tirar uma equipe de uma listagem em que ela tem itens é **permitido, com aviso**:

- A simulação (`?simular=true`, RN56) e a resposta trazem, por equipe que sai, o impacto **`ITENS_OCULTOS`** (não bloqueia) com a
  quantidade de itens dela na listagem. A tela pede confirmação mostrando a quantidade.
- Os itens **não são apagados**: ficam **invisíveis para os membros** da equipe (fora da listagem, das opções, da fila e do painel) e
  visíveis só para o ADMINISTRADOR. A aprovação e os modelos de avaliação da equipe ficam guardados.
- Ao **habilitar a equipe de novo**, tudo volta como estava.
- Habilitar uma equipe não tem efeito nos dados (sem impacto).
- A mudança gera **uma** entrada de histórico `METADADO` no grupo (RN32), com as equipes antes e depois, pelo nome, no campo
  `equipes`, e muda a `versao` do grupo (RN52).

## RN93 — Listagem nova sem equipes; equipe nova sem listagens

- Uma **listagem nova** nasce **sem equipes**: só o ADMINISTRADOR a vê, para montá-la e testá-la antes de liberar. A tela da listagem
  (administração e uso) mostra a faixa "Nenhuma equipe habilitada: só administradores veem esta listagem", com o atalho para a aba
  Equipes. Sem equipe habilitada, nem o ADMINISTRADOR cria itens **por equipe** nela (RN91); os globais, sim.
- Uma **equipe nova** não é habilitada automaticamente em nenhuma listagem.
- A carga do `V009` manteve tudo como era: `testes_desempenho` ficou habilitada para **todas** as equipes que existiam, e cada outra
  listagem, para as equipes que já tinham itens nela.

## RN94 — Itens relacionados

O detalhe de um item mostra **todos os itens que apontam para ele**: os de qualquer entidade do mesmo grupo com um campo REFERENCIA para a
entidade do item ([ADR 0047](adr/0047-itens-relacionados-descobertos-pelos-metadados.md)). Nada é configurado por tipo de item: as
relações são **descobertas pelos metadados** e valem para qualquer listagem, inclusive as criadas pela administração.

- `GET /api/v1/grupos/{grupo}/entidades/{entidade}/registros/{id}/relacionados` devolve **uma entrada por campo REFERENCIA ativo**, de
  **entidade ativa** do grupo, que aponta para a entidade do item, na ordem dos metadados (a da entidade de origem e, dentro dela, a
  dos campos). Campos e entidades inativos ficam de fora, para todos os perfis.
- Cada entrada traz a entidade de origem (`chave`, `nome`, `nomePlural`), o campo (`chave`, `nome`), `filha` (o campo é obrigatório e
  `CASCATA`: a mesma regra de "entidade filha" do front) e a `quantidade`. Entradas com **zero** também vêm.
- A quantidade conta **exatamente** o que o usuário veria na listagem da entidade de origem filtrada por esse campo
  (`?<campo>=<id>`): o mesmo escopo (`PreparadorConsultaRegistros.Escopo`: equipes do usuário nesta listagem, RN91; registros não
  aprovados só do próprio autor para o EXTERNO, RN37; itens globais inteiros). É contada no banco, num SQL só, agrupando
  `RegistroReferencia` por campo.
- O item precisa ser visível para o usuário: senão, **404** (RN28), como o detalhe. Listagem que o usuário não vê: **404** (RN90).
- Os itens de cada entrada vêm da **listagem filtrada** (`GET .../entidades/{origem}/registros?{campo}={id}`), com paginação,
  ordenação e as mesmas regras de sempre. Não há outro endpoint de listagem.
- Só **um nível**: os cenários de um projeto através dos relatórios não aparecem no projeto.

## RN95 — Ícone do tipo de item

Cada entidade (na tela, **tipo de item**) pode ter um **ícone**, usado no menu da área de gestão, no título das telas e nos atalhos da
página Início.

- Opcional: é o nome de um ícone do PrimeIcons, como `pi-chart-line` (`pi-`, depois letras minúsculas, números e hífens, até 50
  caracteres; coluna `Entidade.Icone`, `V010`). Fora desse formato: **400** com o erro no campo `icone`.
- Vazio = sem ícone: as telas usam um ícone **genérico**. Um nome válido que não existe no PrimeIcons não quebra nada (só não aparece);
  o seletor visual da administração (aba Geral do tipo de item) só oferece os que existem, com busca em português.
- Vem nos metadados (`EntidadeDto.icone`) e é gravado com o resto da entidade (PUT completo: ausente = apagado).

## RN96 — Itens recentes do usuário

A página Início mostra, em "Continuar de onde parou", os itens que o usuário **criou ou alterou por último** na área de gestão atual.

- `GET /api/v1/grupos/{grupo}/eu/recentes`: no máximo **8** itens, do mais recente para o mais antigo, cada um com o tipo de item, o
  rótulo e quando foi a última criação ou alteração **dele**.
- Vem do **histórico** (RN32): é o que o próprio usuário fez, não o que outros fizeram nos itens dele.
- **Visibilidade**: só entram os itens que o usuário **ainda vê**, com o mesmo escopo da listagem de cada tipo de item
  (`PreparadorConsultaRegistros.Escopo`: equipes do usuário nesta área, RN91; itens não aprovados de outros autores para o EXTERNO,
  RN37; cadastros de apoio inteiros). Item excluído, de equipe que deixou de ser habilitada (RN92) ou de tipo de item desativado
  **não aparece**. Área de gestão que o usuário não vê: **404** (RN90).
- Calculado no banco, numa consulta só (índice `IX_HistoricoAlteracao_Usuario`, `V010`).

## RN97 — Importação de planilhas: fluxo e permissão

Um tipo de item pode receber **muitos itens de uma vez** a partir de uma planilha (XLSX ou CSV), em `.../entidades/{entidade}/importacoes`
([api/importacao.md](api/importacao.md)). A importação passa pelas **mesmas regras do cadastro manual**: cada linha vira o corpo que a tela
enviaria e é gravada pelo `GravadorRegistro`, o mesmo do `POST` e do `PUT` (RN01 a RN09, RN26 a RN29, RN34, RN35, RN38). Nenhuma regra é
contornada e não há caminho paralelo de gravação.

- **Um tipo de item por importação.** Pai e filho vão em duas etapas: primeiro os relatórios, depois os cenários, que apontam para o
  relatório pelo nome (RN99).
- Fluxo: enviar o arquivo → **prévia** (simulação completa, nada é gravado) → executar → (opcional) desfazer (RN101, RN103).
- **Importar = poder criar** no tipo de item (`PoliticaPermissao.PodeImportar`), em pelo menos uma das equipes do usuário nesta área
  de gestão (RN91): cadastros de apoio (globais), só o ADMINISTRADOR; o EXTERNO, só onde a aprovação está ativa, e os itens nascem
  **PENDENTES** (RN34). Sem permissão: **403**, inclusive para baixar o modelo. Cada linha passa depois pela decisão da equipe dela.
- A prévia e a execução são **de quem enviou o arquivo**; quem só vê a importação (RN104) recebe 403.
- Equipe de cada linha: a coluna **Equipe** (pelo nome, entre as equipes do usuário nesta área de gestão) ou a **equipe padrão** escolhida
  nas opções. Valem as regras do `equipeId` (RN27): o ADMINISTRADOR e quem tem várias equipes informam; com uma só, ela é usada.
- **Histórico**: os itens criados ou alterados por uma importação geram as linhas de sempre (RN32), com o `CorrelacaoId` **da importação**
  e `origem = IMPORTACAO` (coluna `HistoricoAlteracao.Origem`, `V011`; nula no cadastro manual). O histórico do item mostra a origem.

## RN98 — Arquivo da importação: formatos, limites, modelo e colunas

- **XLSX** (lido com o ClosedXML) ou **CSV** em UTF-8, com ou sem BOM, separador `;` ou `,` detectado pela linha de cabeçalho (fora das
  aspas; empate = `;`). No XLSX vale o **valor** das células (nunca a fórmula), da primeira aba visível que não se chama "Instruções".
- **Limites**, com mensagens claras (400 em `errors.arquivo`): até **5 MB**, **5.000 linhas** de dados e **100 colunas**. Linhas
  totalmente vazias não contam.
- O tipo é conferido pelo **conteúdo**: `.xlsx` precisa ser um pacote ZIP do Excel (com `[Content_Types].xml` e `xl/workbook.xml`); `.csv`
  precisa ser texto. `.xls` antigo e arquivos protegidos por senha são recusados com a explicação de como salvar. Contra arquivos
  compactados maliciosos, a soma dos tamanhos descompactados das partes do XLSX é limitada a **100 MB** (e a 10.000 partes).
- O arquivo enviado fica **no banco** (`dbo.ImportacaoArquivo`, `V011`), não em disco, para funcionar igual em qualquer servidor.
- **Modelo** (`GET .../importacao/modelo`): gerado a partir dos metadados, com o **nome** dos campos ativos na ordem do formulário (`*`
  nos obrigatórios); a coluna "Equipe" no fim, se o tipo de item é por equipe e o usuário tem mais de uma equipe na área de gestão; lista
  suspensa em LISTA, BOOLEANO e Equipe (o Excel recusa outro valor) e nas referências com até **1.000** itens visíveis (só avisa: um item
  criado depois de baixar o modelo continua valendo), numa aba oculta "Listas"; formato de data, de número e de texto nas células; e a aba
  **"Instruções"** com cada coluna (tipo, obrigatoriedade, tamanho, opções, a referência e como ela é resolvida, as dependências e a
  unicidade) e as regras gerais. A coluna "(ID)" não vem no modelo: as instruções explicam como acrescentá-la.
- **Sugestão de mapeamento**: cada coluna vai para o campo ativo de mesmo **nome** ou **chave**, sem diferenciar maiúsculas e acentos e
  ignorando o `*`; "&lt;campo&gt; (ID)" vai para o identificador de uma referência; "Equipe", para a equipe (só por equipe). Cada destino
  recebe no máximo uma coluna; o resto é ignorado. O usuário confere e muda antes da prévia.

## RN99 — Referências pelo nome na importação

A coluna de um campo REFERENCIA traz o **nome** do item: o valor do **campo de exibição** dele (RN20), ex.: "Transferências Alfa" para Projeto.

- O nome é comparado **sem diferenciar maiúsculas e acentos** (campo de exibição de texto; número pelo valor), e só entre os itens que o
  usuário **vê** (o escopo da listagem: equipes, RN26 e RN91; aprovação, RN37) e que **a linha pode referenciar**: da mesma equipe ou
  cadastros de apoio (RN29), aprovados ou do próprio autor (RN38). Um item de outra equipe nunca é resolvido.
- **Não encontrou**: erro na célula. **Mais de um**: erro na célula, sugerindo a coluna `<campo> (ID)`.
- A coluna opcional `<campo> (ID)` traz o id do item e vale mais que o nome (é ela que desfaz nomes repetidos). O id passa pelas mesmas
  regras (RN06, RN28): um item que o usuário não vê é "não encontrado", e o nome dele não aparece na prévia.
- **Dependências** (RN08): as referências de que o campo depende (o campo via, ou a origem na própria linha) são resolvidas antes; entre
  itens de mesmo nome, vale o que atende à dependência. A dependência continua validada linha a linha, como no cadastro manual.
- Tipo de item sem campo de exibição de texto ou número: só pela coluna `(ID)`.
- **Em lote**: uma consulta por tipo de item referenciado (todos os nomes do arquivo de uma vez) e uma carga dos itens encontrados, em LINQ.

## RN100 — Valores das células

Cada célula vira o valor JSON que o cadastro manual envia; depois vale a validação de sempre (RN04: tamanho, casas decimais, opções...).
Célula vazia (ou só com espaços) = **não informado** (RN03).

| Tipo | Aceita |
|---|---|
| INTEIRO, DECIMAL | número do Excel; texto com vírgula ou ponto decimal (`1234,56`, `1234.56`) e milhar com ponto quando há vírgula decimal (`1.234,56`). Nos inteiros, o ponto é sempre milhar (`1.200` = 1200). `1,234.56` (americano) é recusado |
| DATA | `dd/mm/aaaa` (ou `d/m/aaaa`), `aaaa-mm-dd` ou a data do Excel (inclusive o número de série) |
| DATA_HORA | `dd/mm/aaaa hh:mm[:ss]`, ISO com ou sem fuso (sem fuso = o fuso de negócio, `Exportacao:FusoHorario`) ou a data e hora do Excel (hora local do negócio) |
| BOOLEANO | Sim/Não, Verdadeiro/Falso, true/false, 1/0 (sem diferenciar maiúsculas e acentos) ou o verdadeiro/falso do Excel |
| LISTA | a opção sem diferenciar maiúsculas e acentos, gravando o texto **oficial** da opção |
| TEXTO, TEXTO_LONGO, URL | o texto (número ou data do Excel viram o texto como aparecem); URL validada como no cadastro (RN04) |

## RN101 — Prévia e execução da importação

- **Nada é gravado sem prévia.** `POST .../importacoes/{id}/validar` recebe o mapeamento e as opções e **simula** cada linha pelo mesmo
  caminho do cadastro manual, descartando tudo no fim. Mostra, linha a linha, o que será **criado**, **atualizado**, **ignorado** ou
  **recusado**, os erros por coluna (a célula) e as referências resolvidas com o nome, e os totais. Os erros de cada linha vêm juntos (RN05).
- **Opções**: duplicados "pular" ou "atualizar" (RN102); linhas com erro "importar só as válidas" ou "tudo ou nada"; equipe padrão (RN97).
- **Execução**: executa exatamente o que foi validado. Simula de novo e, se a situação de cada linha (e o item que cada atualização altera)
  for a mesma da prévia, grava **todos** os itens e o histórico num **único SaveChanges** (uma transação). Se algo mudou desde a prévia
  (um nome foi apagado, outro item igual foi criado), **nada é gravado**: a prévia nova é guardada e a resposta é **409**
  `IMPORTACAO_DADOS_MUDARAM`. Uma corrida no próprio SaveChanges (unicidade no banco) tem o mesmo resultado.
- "Tudo ou nada" com erro, ou nada a gravar: a execução é recusada (409 `IMPORTACAO_SITUACAO_INVALIDA`). Executar duas vezes, também.
- Para corrigir, o usuário ajusta a planilha e **envia de novo** (uma importação nova); a tela reaproveita o mapeamento.
- **Relatório** (`GET .../importacoes/{id}/relatorio`): XLSX com as linhas originais e as colunas "Resultado" (Criado, Atualizado,
  Ignorado, Erro; antes de executar, "Será criado"...) e "Mensagens" (cada erro com a coluna).

## RN102 — Duplicados na importação

O "mesmo item" é o que tem o mesmo valor numa **restrição única** do tipo de item (RN09, com a equipe nas entidades por equipe; RN10: vazio
não conta).

- **Dentro do arquivo**: duas (ou mais) linhas com a mesma chave são **erro em todas elas**, dizendo quais linhas se repetem.
- **Contra o banco**: segue a opção. "Pular": a linha é **ignorada**. "Atualizar": o item que já existe é **alterado** com os valores da
  linha, pelo mesmo caminho do `PUT` (exige permissão de alterar, RN26 e RN35; sem ela, erro na linha). As colunas **mapeadas** mandam
  (célula vazia apaga o valor, como no PUT, RN15); os campos sem coluna ficam como estão; a equipe não muda (RN27). Uma linha que bate com
  dois itens diferentes, ou duas linhas que atualizariam o mesmo item, são erro.
- A verificação é **em lote**: as chaves de todas as linhas numa consulta (em LINQ), e o UNIQUE do banco continua garantindo a regra.

## RN103 — Desfazer uma importação

`POST .../importacoes/{id}/desfazer` remove os itens **criados** pela importação; as **atualizações não são desfeitas**.

- Só uma importação **concluída**, e só se **nenhum** item criado foi alterado depois (qualquer linha de histórico fora da importação:
  alteração, aprovação, rejeição, avaliação) **nem** é referenciado por um item que não veio da importação: o mesmo plano de exclusão
  da exclusão manual (RN16) não pode ser bloqueado por RESTRINGIR nem levar junto, por CASCATA, itens de fora. Referências entre os próprios
  itens da importação não impedem. Senão: **409** `IMPORTACAO_NAO_PODE_SER_DESFEITA` com o motivo e os itens que impedem.
- Desfazer é **excluir**: precisa poder excluir cada item (RN26: GESTOR e ADMINISTRADOR; 403 para os demais).
- Exclui numa transação, com uma linha `EXCLUSAO` por item no histórico (o `CorrelacaoId` e a origem da importação), e a importação fica
  **DESFEITA**. Itens que alguém já excluiu à mão são ignorados.
- Ex.: importados relatórios e depois os cenários deles, a importação dos relatórios só pode ser desfeita depois da dos cenários.

## RN104 — Histórico de importações e limpeza

- Cada importação fica registrada (`dbo.Importacao`, `V011`) com o tipo de item, quem importou, o arquivo, o formato, a situação
  (`ENVIADA`, `VALIDADA`, `CONCLUIDA`, `DESFEITA`, `EXPIRADA`), as opções, o mapeamento, o resultado de cada linha e as datas.
- `GET .../importacoes` ("Importações anteriores"): as recentes do tipo de item, paginadas. Cada usuário vê **as próprias**; o **GESTOR**,
  também as de quem é das suas equipes nesta área de gestão; o **ADMINISTRADOR**, todas. A mesma regra vale para abrir a prévia e baixar o
  relatório (`PoliticaPermissao.PodeVerImportacao`; 404 `IMPORTACAO_NAO_ENCONTRADA` para quem não vê).
- **Limpeza**: um serviço em segundo plano (a cada `Importacao:IntervaloLimpezaMinutos`, padrão 60) apaga os arquivos com mais de **7 dias**
  (o registro da importação fica, com o resumo) e marca como **EXPIRADA** as importações nunca executadas. Sem o arquivo, a prévia e o
  relatório respondem 409 `IMPORTACAO_ARQUIVO_EXPIRADO`.

