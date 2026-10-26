/* =====================================================================
   V007 — Tipo de dado URL (docs/adr/0042)
   ---------------------------------------------------------------------
   1. O CHECK de Campo.TipoDado passa a aceitar 'URL'.
   2. Converte link.url e projeto.url_projeto, do grupo testes_desempenho, de TEXTO_LONGO para URL.
   3. ANTES de converter, confere os valores existentes com a mesma regra do validador (UrlValidador): começar com http:// ou
      https://, sem espaços e com até 2000 caracteres. Se algum não servir, o script FALHA (THROW) dizendo o campo e a
      quantidade, e NADA é alterado: tudo roda numa transação só (XACT_ABORT).

   Os valores continuam onde estão: RegistroValor.ValorTexto (como TEXTO_LONGO) e Registro.Dados (texto JSON). Os dois campos
   não estão em restrição única, então nenhuma chave única muda. A Versao das entidades muda (AtualizadoEm), como em toda
   alteração de campo (V006); a API guarda os metadados em cache por até 10 minutos: reinicie-a depois de aplicar.
   O script pode rodar de novo sem erro.
   ===================================================================== */

SET XACT_ABORT ON;
BEGIN TRANSACTION;

ALTER TABLE dbo.Campo DROP CONSTRAINT IF EXISTS CK_Campo_TipoDado;
ALTER TABLE dbo.Campo ADD CONSTRAINT CK_Campo_TipoDado CHECK (TipoDado IN
    ('TEXTO','TEXTO_LONGO','INTEIRO','DECIMAL','BOOLEANO','DATA','DATA_HORA','LISTA','REFERENCIA','URL'));

DECLARE @Convertidos TABLE (CampoId uniqueidentifier NOT NULL, EntidadeId uniqueidentifier NOT NULL, Nome varchar(200) NOT NULL);

INSERT @Convertidos (CampoId, EntidadeId, Nome)
SELECT c.Id, c.EntidadeId, CONCAT(e.Chave, '.', c.Chave)
FROM dbo.Campo c
JOIN dbo.Entidade e ON e.Id = c.EntidadeId
JOIN dbo.Grupo g ON g.Id = e.GrupoId AND g.Chave = 'testes_desempenho'
WHERE c.TipoDado = 'TEXTO_LONGO'
  AND ((e.Chave = 'link' AND c.Chave = 'url') OR (e.Chave = 'projeto' AND c.Chave = 'url_projeto'));

DECLARE @Campo varchar(200), @Quantidade int, @Mensagem nvarchar(2048);
SELECT TOP (1) @Campo = cv.Nome, @Quantidade = COUNT(*)
FROM @Convertidos cv
JOIN dbo.RegistroValor v ON v.CampoId = cv.CampoId
WHERE v.ValorTexto IS NOT NULL
  AND (   NOT (v.ValorTexto LIKE 'http://%' OR v.ValorTexto LIKE 'https://%')
       OR v.ValorTexto LIKE '% %' OR v.ValorTexto LIKE '%' + CHAR(9) + '%'
       OR v.ValorTexto LIKE '%' + CHAR(10) + '%' OR v.ValorTexto LIKE '%' + CHAR(13) + '%'
       OR LEN(v.ValorTexto) > 2000)
GROUP BY cv.Nome
ORDER BY cv.Nome;

IF @Campo IS NOT NULL
BEGIN
    SET @Mensagem = CONCAT(N'V007: o campo ', @Campo, N' tem ', @Quantidade,
        N' valor(es) que não são URL (http:// ou https://, sem espaços, até 2000 caracteres). Nada foi alterado. ',
        N'Corrija esses valores e aplique o script de novo.');
    THROW 50007, @Mensagem, 1;
END;

UPDATE c SET TipoDado = 'URL'
FROM dbo.Campo c
JOIN @Convertidos cv ON cv.CampoId = c.Id;

UPDATE e SET AtualizadoEm = SYSUTCDATETIME()
FROM dbo.Entidade e
WHERE e.Id IN (SELECT EntidadeId FROM @Convertidos);

COMMIT TRANSACTION;
GO
