# Migração dos relatórios de testes de desempenho (SharePoint → Catálogo)

Os 687 relatórios da lista "Relatórios de Testes de Desempenho" do SharePoint (julho de 2022 a outubro de 2026) migram para a área
`testes_desempenho`. São **dados de produção**: o CSV (`database/migracao/origem/`) e as saídas (`database/migracao/saida/`) ficam
fora do git, assim como as partes `02_*` do pacote. A regra principal é **não perder nenhuma informação**.

| Peça | Onde |
|---|---|
| Gerador (console .NET, reaproveita o código da aplicação) | `backend/tools/Catalogo.Migracao/` e testes em `backend/tools/Catalogo.Migracao.Tests/` |
| Reconfiguração da área pela API (local) | `database/migracao/reconfigurar_area_pela_api.py`, depois `V016` e o `01` do pacote, gerados por `ferramentas/gerar_carga_area.sh` (`MODO=reconfigurar`) |
| Origem `MIGRACAO_SHAREPOINT` no histórico | `V017` (local) e parte 01 do pacote (CAIXA) |
| Pacote para o banco da CAIXA | `database/entrega/2026-10-28-migracao-sharepoint/` (README com a ordem de execução) |
| Simulação local de ponta a ponta | `database/migracao/simular_migracao.sh` → banco `Simulacao_Migracao` |
| Relatório por linha, anomalias, quantidades | `database/migracao/saida/relatorio_migracao.csv`, `anomalias.md`, `quantidades.md` |

## Como o gerador garante que os dados são os do sistema

O gerador **não escreve SQL de dados à mão**. Para cada item ele usa o código da aplicação: os metadados da área lidos do banco local
pelo `IMetadadosProvider` (os GUIDs dos campos), os validadores de tipo e a normalização (o mesmo `ConversorCelulas` da importação de
planilhas), a montagem do JSON `Dados`, as linhas de valores tipados, as referências, a `CalculadoraChaveUnica`, o histórico e os
nomes físicos do `NomesBanco`. **Todo item passa pelos validadores antes de entrar no script**: o recusado vai para as anomalias
(nesta execução, 0). Os GUIDs são determinísticos (UUID v5 a partir da linha do CSV): o mesmo CSV gera os mesmos scripts.

As partes conferem a impressão digital da configuração da área antes de gravar: se o `01` não tiver rodado, nada é gravado.

## Estrutura da área (antes da migração)

Feita **localmente pela API de administração de metadados** (mesmas validações da tela), sem mudar nenhuma chave. Pela RN66 a API não
muda o tipo de um campo: os 4 campos que mudaram de tipo (`bateria_testes`, `tipo_teste`, `tempo_medio_resposta_ms`,
`percentil_95_ms`) foram excluídos e recriados **com a mesma chave** (RN70, a área estava vazia) e a unicidade do cenário foi
recriada com o endpoint; esses 5 ganharam GUID novo. Todo o resto manteve o GUID. Widgets e filtros usam as chaves, que não mudaram.

| Tipo de item | Mudança |
|---|---|
| Relatório de Teste | novo `titulo` (TEXTO 200, obrigatório, **campo de exibição**: "&lt;Contexto&gt; — &lt;N&gt;ª bateria"); `bateria_testes` → INTEIRO "Nº da bateria"; `tipo_teste` → LISTA "Tecnologia" (API, WEB, Fila MQ); `rtc` opcional; novos opcionais `comunidade_na_execucao` (TEXTO 100), `host_gerenciador_filas` (TEXTO 255), `endpoints_testados`, `configuracoes_ambiente`, `observacoes_cenarios` (TEXTO_LONGO), `script` (URL), `infraestrutura` (TEXTO 500) e `dados_originais` (TEXTO_LONGO, **inativo**: oculto nas telas e na API) |
| Cenário de Teste | `tempo_medio_resposta_ms` e `percentil_95_ms` → DECIMAL(18,2), percentil opcional; novos `descricao_cenario` (TEXTO 150) e `amostras` (INTEIRO); unicidade **relatório + número do cenário + endpoint**; `taxa_erro_percentual` DECIMAL(5,2) basta (máximo nos dados: 100%, 2 casas) |
| Endpoint de Serviço | `metodo` opcional; `endpoint_url_fila` e `nome_servico` com 500 caracteres (o maior endpoint migrado tem 187: 255 bastaria, 500 dá margem para os próximos) |
| RTC | novo `url` (URL, opcional) |
| Ambiente | sem mudança |

O `tools/dados-dev` do front, os testes e os widgets do painel foram ajustados (tecnologia em vez de carga/estresse, percentil opcional).

## Coluna do CSV → destino

Todas as colunas também ficam, **sem nenhuma limpeza**, em `dados_originais` do relatório (a linha inteira, com o nome de cada coluna).

| Coluna do CSV | Destino | Regra |
|---|---|---|
| SIGLA | Sigla (`codigo_sigla`), via Projeto | Maiúsculas; uma sigla por código, ligada à comunidade do relatório **mais recente** |
| Bateria de testes | Relatório `bateria_testes` e `titulo` | "18ª" → 18; título "&lt;Contexto&gt; — 18ª bateria" |
| Contexto | Projeto (`nome`) e Relatório `contexto` | Um projeto por sigla + contexto (contextos que só diferem por maiúsculas/acentos são um projeto só); invisíveis removidos |
| Comunidade | Comunidade (`nome`) e Relatório `comunidade_na_execucao` | Trim e espaços colapsados; iguais sem diferenciar maiúsculas, acentos e espaços viram uma; o nome da época fica no relatório |
| Tipo de Teste | Relatório `tipo_teste` | Array JSON → API, WEB ou Fila MQ |
| Ambiente | Ambiente (`nome`), referenciado no relatório | 9 valores; "Contigência PRD" → "Contingência PRD" |
| Data da execução | Relatório `data_execucao` e `CriadoEm` de todos os itens do relatório | `dd/MM/aaaa HH:mm` lido pelo mesmo conversor da importação (hora de Brasília, gravada como o sistema grava `DATA_HORA`): a tela mostra a hora do CSV |
| Executado por | Relatório `executado_por` | Nomes separados por "; " |
| Host / Queue Manager | Relatório `host_gerenciador_filas` | Texto limpo |
| Endpoints / Fila | Relatório `endpoints_testados` | A lista original, um por linha; também usada para achar o endpoint de tabelas sem caminho |
| Objetivo, Conclusão | Relatório `objetivo`, `conclusao` | Invisíveis (zero-width) removidos, espaços normalizados |
| Apontamentos | Relatório `apontamentos` | HTML → texto com estrutura: parágrafos em linhas, listas com "- " ou "1. ", tabelas com " \| ", entidades decodificadas, estilos removidos, `<img>` → `[imagem: src]`, links → `texto (url)` |
| Cenários de testes executados | Cenários de Teste + Endpoints de Serviço; o resto em `observacoes_cenarios` | Seção abaixo |
| Resultado | Status do Resultado, referenciado no relatório | Satisfatório, Insatisfatório |
| RTC | RTC (`rtc_externo_id`, `url`), referenciado no relatório | Número de `id=<n>` ou `/WorkItem/<n>`; o mesmo número é o mesmo RTC. Valor que não é work item (link de commit) vira **Link** do relatório |
| Script | Relatório `script` | URL; relativo completado com a base do SharePoint |
| Configurações do Ambiente | Relatório `configuracoes_ambiente` | Texto limpo, com as quebras de linha |
| Infraestrutura | Relatório `infraestrutura` | Array JSON → "Java, Quarkus, Azure Devops Cloud", com a grafia mais frequente de cada tecnologia |
| Link | Link do relatório (`texto_exibicao`, `url`) | Texto descritivo ("Script no Azure DevOps (commit)", "SharePoint", "Link"); relativo completado com a base |

### Cenários (tabelas HTML)

- **Colunas pelo sentido**: o cabeçalho (às vezes em duas linhas, com `rowspan`) é comparado sem acentos, maiúsculas, unidades e
  invisíveis. As 91 variações caem em: Cenário, Nome do Serviço, Método, Endpoint(s), Usuários Virtuais, Vazão, Tempo médio,
  Percentil 95, Amostras e Erros %. Cabeçalho em segundos converte para ms.
- **Células mescladas** (`rowspan`/`colspan`) são propagadas. Uma célula com um valor por linha ao lado de vários endpoints dá um
  cenário por endpoint, cada um com o seu valor.
- **Números brasileiros**: milhar com ponto, decimal com vírgula, unidades ("ms", "seg", "TPS", "req/min", "%", "VUs", "mil").
  O ponto sozinho ("1.178") é decidido pelos outros valores da coluna. Segundos na célula de uma coluna em ms convertem (×1000), por
  minuto converte para por segundo (÷60).
- **Ambíguos**: faixas, textos, vários valores numa célula, "1191 seg" (≈ 20 minutos, com média de 274 ms) e "14 seg ms" (duas
  unidades) não são inequívocos. Campo opcional fica vazio; campo obrigatório faz a linha não virar cenário. A célula original vai
  para `observacoes_cenarios` e para as anomalias.
- **Endpoint**: por nome do serviço, método e endpoint; sem nome, o nome é o próprio endpoint; iguais no mesmo projeto são
  reaproveitados (`ativo` = verdadeiro). Numa célula com caminhos, linhas terminadas em ":" ("ETAPA 1:") são o rótulo (nome) do
  caminho seguinte, pedaços de URL quebrados em linha ("&classe=01") se juntam ao caminho anterior e frases ("Sem controle de
  vazão") vão para a observação.
- Texto antes ou depois das tabelas, "N/D", tabelas não reconhecidas e linhas não importadas vão para `observacoes_cenarios`.

## Decisões

1. **Equipe GEQTI** para todos os itens por equipe, habilitada na área. A migração não associa usuários a ela.
2. **Autor C161587** em todos os itens (procurado pela matrícula; o script para se não existir).
3. **`CriadoEm`** = data de execução do relatório (relatório, cenários e links); nos cadastros de apoio, projetos, endpoints e RTCs, a
   do relatório mais antigo que os referencia. Assim os indicadores mensais refletem o histórico.
4. **Histórico**: uma linha de criação por item, origem `MIGRACAO_SHAREPOINT` (`V017`) e um único identificador de correlação.
5. **Itens nascem APROVADOS** (padrão da coluna), como os inseridos por script.
6. **Uma tabela de controle** (`cat.CATTB024_MIGRACAO_CONTROLE`, padrão de nomes da CAIXA) torna cada parte idempotente e ordenada.
   Ela é só da migração e não entra no `NomesBanco` (a aplicação não a usa).
7. **Base dos links relativos** do SharePoint: `https://caixa.sharepoint.com` (configurável com `--base-sharepoint`), a confirmar.
8. **Comunidade da sigla**: a do relatório mais recente; o nome de cada época fica em `comunidade_na_execucao`.

## Quantidades

| Tipo de item | Quantidade |
|---|---|
| Comunidades | 32 |
| Siglas | 129 |
| Ambientes | 9 |
| Status de Resultado | 2 |
| RTCs | 662 |
| Projetos | 559 |
| Endpoints de Serviço | 1.242 |
| Relatórios de Teste | 687 (569 OK, 118 OK com observações, 0 não migrados) |
| Cenários de Teste | 1.586 |
| Links | 14 (13 da coluna Link + 1 da coluna RTC) |
| Equipe | 1 (GEQTI) |

Relatórios por resultado: 460 Satisfatórios, 227 Insatisfatórios. Por ano: 2022: 10, 2023: 88, 2024: 221, 2025: 195, 2026: 173.

## Prova de que nada se perdeu

- **No gerador**: para cada relatório, cada valor não vazio do CSV precisa estar num campo ou em `dados_originais`. 12.956 valores
  não vazios, 12.953 em campos, 3 só em `dados_originais` (células sem nenhum texto: `<br>`, `<p><br></p>` e um `<div>` só com estilo), **0 perdas** (`quantidades.md`).
- **No banco**: `dotnet run --project backend/tools/Catalogo.Migracao -- conferir-banco --csv <csv> --banco "<connection string>"`
  lê o `dados_originais` de volta e confere os 687 relatórios e os 12.956 valores: **0 perdas** na simulação.
- **`03_conferencia.sql`**: quantidades por tipo de item, por resultado, por ambiente e por ano: 27 verificações, 0 divergências.

## Teste local de ponta a ponta

`./database/migracao/simular_migracao.sh` cria `Simulacao_Migracao` como o banco da CAIXA hoje (V014 + V015 + C161587 gerado por
`criar-admin --gerar-sql`), roda `01`, as 9 partes e `03` pelo `sqlcmd -i`, roda tudo de novo (nada muda) e confere de novo. Como
apontar a API para esse banco: README do pacote.
