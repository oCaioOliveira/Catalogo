# 0042. Tipo de dado URL e a conversão dos campos de endereço existentes

- Status: Aceito
- Data: 2026-10-22

## Contexto

Os endereços da listagem de Testes de Desempenho (`link.url` e `projeto.url_projeto`) eram `TEXTO_LONGO`. Por isso:

- ficavam **fora das tabelas** (TEXTO_LONGO não é coluna padrão): a seção de Links do relatório não mostrava a URL, que é a
  informação principal do link;
- não eram **clicáveis** em lugar nenhum (tabela, detalhe, exportação);
- aceitavam qualquer texto ("intranet/painel", "ver com o João"), e nada garantia que o endereço abrisse.

## Decisão

1. **Tipo novo `URL`** no motor (RN04), com validador próprio (`UrlValidador`): endereço **absoluto**, esquema **http ou https**, com
   host, **sem espaços** e com **até 2000 caracteres**. Normalização: só o trim (caminho e consulta diferenciam maiúsculas). O valor vai
   para `RegistroValor.ValorTexto`, como os textos. Sem atributos próprios (como o TEXTO_LONGO).
2. **Unicidade** (RN09): o valor normalizado entra no hash com o **esquema e o host em minúsculas**; caminho, consulta e fragmento ficam
   como vieram (`HTTPS://Exemplo.com/a` = `https://exemplo.com/a`; `.../A` é outra URL).
3. **Consulta**: filtros igual e `contem` (RN18); ordenação permitida (RN19). Pode ser campo de exibição e entrar em restrição única.
4. **Administração de metadados** (RN66): `TEXTO`/`TEXTO_LONGO` → `URL` é permitido, mas **bloqueado (409, `VALOR_NAO_E_URL`) se
   algum valor existente não for uma URL válida**, com o relatório de impacto. A verificação usa a MESMA regra do validador
   (`UrlValidador.Conferir`), lendo os valores em lotes. `URL` → `TEXTO_LONGO` é sempre permitido. As chaves únicas do campo são
   recalculadas, porque a normalização muda.
5. **Conversão dos campos existentes por script** (`V007__tipo_url.sql`): recria o CHECK de `Campo.TipoDado` e converte `link.url` e
   `projeto.url_projeto` do grupo `testes_desempenho`. **Antes** de converter, confere os valores com a regra da URL (http:// ou
   https://, sem espaços, até 2000); se algum não servir, o script **falha com `THROW`** dizendo o campo e a quantidade, e não altera
   nada (tudo numa transação, `XACT_ABORT`). É uma exceção consciente ao "metadados só pela API" (RN50): a carga da listagem é do
   `V001`, e um ambiente novo precisa sair do script já com o tipo certo. Os valores não mudam de coluna (já estavam em
   `ValorTexto`), e nenhum dos dois campos está em restrição única: não há hash a recalcular.
6. **Front**: componente de campo próprio (input `type="url"` com a mesma validação e um botão "Abrir … numa nova aba"); em tabelas e
   no detalhe, **link** com `target="_blank"` e `rel="noopener noreferrer"` (na tabela, truncado, com a URL inteira na dica). URL pode
   ser coluna das tabelas, diferente do TEXTO_LONGO. Na exportação XLSX vira **hiperlink**; no CSV, o texto como foi gravado.

### Alternativas descartadas

- **Continuar TEXTO_LONGO e "detectar" links no front**: o front teria uma regra que o back-end não garante (e valores inválidos
  continuariam entrando), e o TEXTO_LONGO seguiria fora das tabelas.
- **Usar TEXTO com tamanho 2000 e uma máscara**: sem validação de esquema no back-end, e o front teria de adivinhar quais TEXTO são
  endereços (por chave ou nome), o que contraria o ADR 0034.
- **Aceitar outros esquemas (ftp, mailto, file)**: não há uso hoje, e `javascript:`/`data:` precisariam de lista de bloqueio. Liberar
  outro esquema depois é mudança de regra, com um ADR novo.
- **Converter os campos pela API de administração**: funciona num banco que já existe, mas um ambiente novo criado pelos scripts
  nasceria com TEXTO_LONGO.

## Consequências

- ✅ A URL aparece nas tabelas (inclusive na seção de Links do relatório) e abre com um clique, também no Excel.
- ✅ Endereço inválido é recusado na gravação, com a mensagem no campo.
- ✅ Outras listagens podem usar o tipo e converter campos existentes pela administração, com o relatório de impacto.
- ⚠️ Registros com endereço inválido impedem a conversão: é preciso corrigi-los antes (pelo formulário ou pela API).
- ⚠️ A API guarda os metadados em cache por até 10 minutos: depois de aplicar o `V007`, reinicie-a.
