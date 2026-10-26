/* =====================================================================
   V002 — Valores tipados para busca e ordenação (dbo.RegistroValor)
   ---------------------------------------------------------------------
   Por quê: filtrar e ordenar dentro do JSON (Registro.Dados) exigiria SQL
   escrito à mão na aplicação, o que é proibido (docs/adr/0011). Esta tabela
   guarda uma cópia TIPADA de cada campo simples, para a aplicação consultar
   só com LINQ (docs/adr/0013).

   Regras:
     - Uma linha por campo simples PREENCHIDO de um registro. Campo nulo ou
       ausente não gera linha. REFERENCIA não entra (fica em RegistroReferencia).
     - TEXTO, TEXTO_LONGO, LISTA -> ValorTexto
       INTEIRO, DECIMAL         -> ValorNumero
       DATA, DATA_HORA          -> ValorData (DATA_HORA em UTC)
       BOOLEANO                 -> ValorBooleano
     - É gravada pela aplicação SEMPRE junto com Registro.Dados, no mesmo
       SaveChanges. Registro.Dados continua sendo a fonte do que a API devolve.

   O script pode rodar de novo sem erro.
   ===================================================================== */

IF OBJECT_ID(N'dbo.RegistroValor', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.RegistroValor (
        RegistroId    uniqueidentifier NOT NULL,
        EntidadeId    uniqueidentifier NOT NULL,
        CampoId       uniqueidentifier NOT NULL,
        ValorTexto    nvarchar(max)    NULL,
        ValorNumero   decimal(38,10)   NULL,
        ValorData     datetime2        NULL,
        ValorBooleano bit              NULL,
        CONSTRAINT PK_RegistroValor PRIMARY KEY (RegistroId, CampoId),
        -- o valor some junto com o registro
        CONSTRAINT FK_RegistroValor_Registro FOREIGN KEY (RegistroId, EntidadeId)
            REFERENCES dbo.Registro (Id, EntidadeId) ON DELETE CASCADE,
        -- o campo precisa ser da entidade do registro
        CONSTRAINT FK_RegistroValor_Campo FOREIGN KEY (CampoId, EntidadeId)
            REFERENCES dbo.Campo (Id, EntidadeId),
        -- exatamente uma coluna de valor preenchida
        CONSTRAINT CK_RegistroValor_UmValor CHECK (
              CASE WHEN ValorTexto    IS NULL THEN 0 ELSE 1 END
            + CASE WHEN ValorNumero   IS NULL THEN 0 ELSE 1 END
            + CASE WHEN ValorData     IS NULL THEN 0 ELSE 1 END
            + CASE WHEN ValorBooleano IS NULL THEN 0 ELSE 1 END = 1)
    );
END
GO

/* DECIMAL precisa caber em ValorNumero decimal(38,10):
   no máximo 10 casas decimais e 28 dígitos antes da vírgula (RN22). */
ALTER TABLE dbo.Campo DROP CONSTRAINT IF EXISTS CK_Campo_Decimal;
GO
ALTER TABLE dbo.Campo ADD CONSTRAINT CK_Campo_Decimal CHECK (
    (TipoDado = 'DECIMAL' AND Precisao IS NOT NULL AND Escala IS NOT NULL
         AND Precisao BETWEEN 1 AND 38 AND Escala <= Precisao
         AND Escala <= 10 AND Precisao - Escala <= 28)
 OR (TipoDado <> 'DECIMAL' AND Precisao IS NULL AND Escala IS NULL));
GO

/* Carga única dos registros que já existem, a partir do JSON.
   Os valores em Registro.Dados já estão normalizados pela aplicação
   (DATA "aaaa-mm-dd", DATA_HORA "...Z" em UTC, booleano true/false).
   TRY_CONVERT devolve NULL se algum valor estiver fora do formato: nesse caso
   o CHECK CK_RegistroValor_UmValor falha e o script para, em vez de gravar lixo. */
INSERT dbo.RegistroValor (RegistroId, EntidadeId, CampoId, ValorTexto, ValorNumero, ValorData, ValorBooleano)
SELECT r.Id, r.EntidadeId, c.Id,
       CASE WHEN c.TipoDado IN ('TEXTO', 'TEXTO_LONGO', 'LISTA') THEN j.[value] END,
       CASE WHEN c.TipoDado IN ('INTEIRO', 'DECIMAL') THEN TRY_CONVERT(decimal(38,10), j.[value]) END,
       CASE WHEN c.TipoDado = 'DATA'      THEN TRY_CONVERT(datetime2, j.[value], 126)
            WHEN c.TipoDado = 'DATA_HORA' THEN CONVERT(datetime2, SWITCHOFFSET(TRY_CONVERT(datetimeoffset, j.[value], 127), '+00:00')) END,
       CASE WHEN c.TipoDado = 'BOOLEANO'  THEN CASE j.[value] WHEN 'true' THEN CAST(1 AS bit) WHEN 'false' THEN CAST(0 AS bit) END END
FROM dbo.Registro r
JOIN dbo.Campo c
  ON c.EntidadeId = r.EntidadeId
 AND c.TipoDado <> 'REFERENCIA'
CROSS APPLY OPENJSON(r.Dados) j
-- a chave do JSON é exata (diferencia maiúsculas); o OPENJSON devolve [key] em BIN2
WHERE j.[key] = c.Chave COLLATE Latin1_General_BIN2
  AND j.[type] <> 0  -- 0 = null no JSON
  AND NOT EXISTS (SELECT 1 FROM dbo.RegistroValor v WHERE v.RegistroId = r.Id AND v.CampoId = c.Id);
GO
