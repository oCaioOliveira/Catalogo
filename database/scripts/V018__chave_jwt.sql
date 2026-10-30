/* =====================================================================
   V018 — Chave de assinatura do JWT no banco (cat.CATTB025_CHAVE_JWT)
   ---------------------------------------------------------------------
   Por quê: o deploy da CAIXA só substitui seis marcadores no appsettings.json (ENVIRONMENT, CORS_ALLOW e DB_*) e a API precisa
   subir sem nenhuma outra variável (docs/adr/0053). A chave do JWT não é um deles: a própria API gera uma chave aleatória na
   primeira subida e grava aqui; nas próximas, lê a mesma. Assim todas as instâncias assinam e validam com a mesma chave.
   O que faz: cria cat.CATTB025_CHAVE_JWT (uma linha só, com id fixo gravado pela aplicação). Não grava nenhuma chave.
   O papel catalogo_aplicacao já tem SELECT e INSERT no schema cat (V013): nada de GRANT.
   No banco da CAIXA, o mesmo conteúdo vai no pacote entrega/2026-10-30-chave-jwt.
   Idempotente: se a tabela já existir, não faz nada. Compatível com SQL Server 2014 (nível 120).
   No código: ChaveJwtTabela, ChaveJwtConfiguracao e a entrada "ChaveJwt" do NomesBanco.
   ===================================================================== */
SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

IF OBJECT_ID(N'cat.CATTB025_CHAVE_JWT', N'U') IS NULL
BEGIN
    CREATE TABLE cat.CATTB025_CHAVE_JWT
    (
        NU_NSU_CHAVE_JWT uniqueidentifier NOT NULL,
        CO_CHAVE_JWT     varchar(200)     COLLATE Latin1_General_CI_AI NOT NULL,
        DH_CRIADO_CJ     datetime2        NOT NULL CONSTRAINT DF_ChaveJwt_CriadoEm DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_ChaveJwt PRIMARY KEY (NU_NSU_CHAVE_JWT),
        CONSTRAINT CK_ChaveJwt_Tamanho CHECK (LEN(CO_CHAVE_JWT) >= 32)
    );
    PRINT 'V018: cat.CATTB025_CHAVE_JWT criada.';
END
ELSE
    PRINT 'V018: cat.CATTB025_CHAVE_JWT já existe. Nada a fazer.';
GO
