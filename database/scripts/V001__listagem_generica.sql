/* =====================================================================
   LISTAGEM GENÉRICA — SQL Server
   ---------------------------------------------------------------------
   Duas camadas:
     1. METADADOS: descrevem "o que" pode ser guardado
        Grupo, Entidade, Campo, CampoDependencia, RestricaoUnica, RestricaoUnicaCampo
     2. DADOS: guardam os registros de fato
        Registro, RegistroReferencia, RegistroChaveUnica

   Regras gerais:
     - Campos simples (texto, número, data...) ficam no JSON Registro.Dados.
     - Relacionamentos NUNCA ficam no JSON: ficam em RegistroReferencia,
       com FK real (integridade garantida pelo banco).
     - id, data de criação e controle de concorrência são colunas de sistema
       do Registro, não precisam ser cadastrados como Campo.
   ===================================================================== */


/* =====================================================================
   1. METADADOS
   ===================================================================== */

-- Agrupa as entidades de uma mesma "listagem" (ex.: Testes de Desempenho)
CREATE TABLE dbo.Grupo (
    Id          uniqueidentifier NOT NULL CONSTRAINT DF_Grupo_Id DEFAULT NEWSEQUENTIALID(),
    Chave       varchar(100)     NOT NULL,
    Nome        nvarchar(150)    NOT NULL,
    Descricao   nvarchar(500)    NULL,
    CriadoEm    datetime2        NOT NULL CONSTRAINT DF_Grupo_CriadoEm DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_Grupo PRIMARY KEY (Id),
    CONSTRAINT UQ_Grupo_Chave UNIQUE (Chave)
);

-- Cada "tabela" do diagrama vira uma Entidade (Comunidade, Projeto, RelatorioTeste...)
CREATE TABLE dbo.Entidade (
    Id              uniqueidentifier NOT NULL CONSTRAINT DF_Entidade_Id DEFAULT NEWSEQUENTIALID(),
    GrupoId         uniqueidentifier NOT NULL,
    Chave           varchar(100)     NOT NULL,
    Nome            nvarchar(150)    NOT NULL,
    Descricao       nvarchar(500)    NULL,
    CampoExibicaoId uniqueidentifier NULL,   -- campo usado como rótulo em selects/listas (ex.: Projeto.nome)
    CriadoEm        datetime2        NOT NULL CONSTRAINT DF_Entidade_CriadoEm DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_Entidade PRIMARY KEY (Id),
    CONSTRAINT FK_Entidade_Grupo FOREIGN KEY (GrupoId) REFERENCES dbo.Grupo (Id),
    CONSTRAINT UQ_Entidade_Grupo_Chave UNIQUE (GrupoId, Chave)
);

-- Cada coluna do diagrama vira um Campo
CREATE TABLE dbo.Campo (
    Id                      uniqueidentifier NOT NULL CONSTRAINT DF_Campo_Id DEFAULT NEWSEQUENTIALID(),
    EntidadeId              uniqueidentifier NOT NULL,
    Chave                   varchar(100)     NOT NULL,   -- nome da propriedade no JSON (ex.: "nome_servico")
    Nome                    nvarchar(150)    NOT NULL,   -- rótulo exibido na tela
    TipoDado                varchar(20)      NOT NULL,
    Obrigatorio             bit              NOT NULL CONSTRAINT DF_Campo_Obrigatorio DEFAULT 0,
    TamanhoMaximo           int              NULL,       -- obrigatório para TEXTO
    Precisao                tinyint          NULL,       -- obrigatório para DECIMAL
    Escala                  tinyint          NULL,       -- obrigatório para DECIMAL
    Opcoes                  nvarchar(max)    NULL,       -- obrigatório para LISTA, ex.: ["GET","POST"]
    EntidadeReferenciadaId  uniqueidentifier NULL,       -- obrigatório para REFERENCIA
    AcaoExclusao            varchar(20)      NULL,       -- REFERENCIA: o que fazer quando o registro apontado for excluído
    Ordem                   int              NOT NULL CONSTRAINT DF_Campo_Ordem DEFAULT 0,
    Ativo                   bit              NOT NULL CONSTRAINT DF_Campo_Ativo DEFAULT 1,
    CriadoEm                datetime2        NOT NULL CONSTRAINT DF_Campo_CriadoEm DEFAULT SYSUTCDATETIME(),

    CONSTRAINT PK_Campo PRIMARY KEY (Id),
    CONSTRAINT FK_Campo_Entidade FOREIGN KEY (EntidadeId) REFERENCES dbo.Entidade (Id),
    CONSTRAINT FK_Campo_EntidadeReferenciada FOREIGN KEY (EntidadeReferenciadaId) REFERENCES dbo.Entidade (Id),
    CONSTRAINT UQ_Campo_Entidade_Chave UNIQUE (EntidadeId, Chave),

    -- Alvos das FKs compostas (garantem que campo e registro são da mesma entidade)
    CONSTRAINT UQ_Campo_Id_Entidade UNIQUE (Id, EntidadeId),
    CONSTRAINT UQ_Campo_Id_EntidadeReferenciada UNIQUE (Id, EntidadeReferenciadaId),

    CONSTRAINT CK_Campo_TipoDado CHECK (TipoDado IN
        ('TEXTO','TEXTO_LONGO','INTEIRO','DECIMAL','BOOLEANO','DATA','DATA_HORA','LISTA','REFERENCIA')),

    CONSTRAINT CK_Campo_Referencia CHECK (
        (TipoDado = 'REFERENCIA' AND EntidadeReferenciadaId IS NOT NULL
             AND AcaoExclusao IS NOT NULL AND AcaoExclusao IN ('RESTRINGIR','CASCATA'))
     OR (TipoDado <> 'REFERENCIA' AND EntidadeReferenciadaId IS NULL AND AcaoExclusao IS NULL)),

    CONSTRAINT CK_Campo_Texto CHECK (
        TipoDado <> 'TEXTO' OR (TamanhoMaximo IS NOT NULL AND TamanhoMaximo BETWEEN 1 AND 4000)),

    CONSTRAINT CK_Campo_Decimal CHECK (
        (TipoDado = 'DECIMAL' AND Precisao IS NOT NULL AND Escala IS NOT NULL
             AND Precisao BETWEEN 1 AND 38 AND Escala <= Precisao)
     OR (TipoDado <> 'DECIMAL' AND Precisao IS NULL AND Escala IS NULL)),

    CONSTRAINT CK_Campo_Lista CHECK (
        (TipoDado = 'LISTA' AND Opcoes IS NOT NULL AND ISJSON(Opcoes) = 1)
     OR (TipoDado <> 'LISTA' AND Opcoes IS NULL))
);

-- O campo de exibição precisa pertencer à própria entidade
ALTER TABLE dbo.Entidade ADD CONSTRAINT FK_Entidade_CampoExibicao
    FOREIGN KEY (CampoExibicaoId, Id) REFERENCES dbo.Campo (Id, EntidadeId);

/* Campos dependentes (filtro de opções em campos REFERENCIA).
   Leitura: "as opções de CampoId só mostram registros cujo CampoAlvoId
   seja igual ao valor de CampoOrigemId".
     - CampoViaId NULL      -> CampoOrigemId é da mesma entidade de CampoId
     - CampoViaId preenchido -> percorre essa referência e lê CampoOrigemId
                               na entidade apontada (um salto)
   Ex.: CenarioTeste.endpoint_servico só lista endpoints cujo projeto
        seja o projeto do relatório do cenário:
        CampoId = CenarioTeste.endpoint_servico
        CampoViaId = CenarioTeste.relatorio_teste
        CampoOrigemId = RelatorioTeste.projeto
        CampoAlvoId = EndpointServico.projeto
   A aplicação usa isso para montar o select e para validar ao salvar. */
CREATE TABLE dbo.CampoDependencia (
    CampoId         uniqueidentifier NOT NULL,
    CampoViaId      uniqueidentifier NULL,
    CampoOrigemId   uniqueidentifier NOT NULL,
    CampoAlvoId     uniqueidentifier NOT NULL,
    CONSTRAINT PK_CampoDependencia PRIMARY KEY (CampoId),
    CONSTRAINT FK_CampoDependencia_Campo  FOREIGN KEY (CampoId)       REFERENCES dbo.Campo (Id),
    CONSTRAINT FK_CampoDependencia_Via    FOREIGN KEY (CampoViaId)    REFERENCES dbo.Campo (Id),
    CONSTRAINT FK_CampoDependencia_Origem FOREIGN KEY (CampoOrigemId) REFERENCES dbo.Campo (Id),
    CONSTRAINT FK_CampoDependencia_Alvo   FOREIGN KEY (CampoAlvoId)   REFERENCES dbo.Campo (Id)
);

-- Unicidade simples (1 campo) ou composta (vários campos)
CREATE TABLE dbo.RestricaoUnica (
    Id          uniqueidentifier NOT NULL CONSTRAINT DF_RestricaoUnica_Id DEFAULT NEWSEQUENTIALID(),
    EntidadeId  uniqueidentifier NOT NULL,
    Nome        varchar(100)     NOT NULL,
    CONSTRAINT PK_RestricaoUnica PRIMARY KEY (Id),
    CONSTRAINT FK_RestricaoUnica_Entidade FOREIGN KEY (EntidadeId) REFERENCES dbo.Entidade (Id),
    CONSTRAINT UQ_RestricaoUnica_Entidade_Nome UNIQUE (EntidadeId, Nome),
    CONSTRAINT UQ_RestricaoUnica_Id_Entidade UNIQUE (Id, EntidadeId)
);

CREATE TABLE dbo.RestricaoUnicaCampo (
    RestricaoUnicaId uniqueidentifier NOT NULL,
    EntidadeId       uniqueidentifier NOT NULL,
    CampoId          uniqueidentifier NOT NULL,
    CONSTRAINT PK_RestricaoUnicaCampo PRIMARY KEY (RestricaoUnicaId, CampoId),
    -- restrição e campo precisam ser da mesma entidade
    CONSTRAINT FK_RestricaoUnicaCampo_Restricao FOREIGN KEY (RestricaoUnicaId, EntidadeId)
        REFERENCES dbo.RestricaoUnica (Id, EntidadeId) ON DELETE CASCADE,
    CONSTRAINT FK_RestricaoUnicaCampo_Campo FOREIGN KEY (CampoId, EntidadeId)
        REFERENCES dbo.Campo (Id, EntidadeId)
);


/* =====================================================================
   2. DADOS
   ===================================================================== */

-- Uma linha por item cadastrado (um projeto, um relatório, um cenário...)
CREATE TABLE dbo.Registro (
    Id           uniqueidentifier NOT NULL CONSTRAINT DF_Registro_Id DEFAULT NEWSEQUENTIALID(),
    EntidadeId   uniqueidentifier NOT NULL,
    Dados        nvarchar(max)    NOT NULL CONSTRAINT DF_Registro_Dados DEFAULT N'{}',  -- só campos simples
    CriadoEm     datetime2        NOT NULL CONSTRAINT DF_Registro_CriadoEm DEFAULT SYSUTCDATETIME(),
    AtualizadoEm datetime2        NULL,
    Versao       rowversion       NOT NULL,   -- concorrência otimista no EF Core
    CONSTRAINT PK_Registro PRIMARY KEY (Id),
    CONSTRAINT FK_Registro_Entidade FOREIGN KEY (EntidadeId) REFERENCES dbo.Entidade (Id),
    CONSTRAINT UQ_Registro_Id_Entidade UNIQUE (Id, EntidadeId),
    CONSTRAINT CK_Registro_Dados_Json CHECK (ISJSON(Dados) = 1)
);
CREATE INDEX IX_Registro_Entidade_CriadoEm ON dbo.Registro (EntidadeId, CriadoEm DESC);

/* Relacionamentos entre registros. Cada campo REFERENCIA aponta para
   no máximo 1 registro (lado N de um 1:N). As FKs compostas garantem no banco:
     - o campo pertence à entidade do registro de origem
     - o registro de destino é da entidade que o campo referencia */
CREATE TABLE dbo.RegistroReferencia (
    RegistroOrigemId  uniqueidentifier NOT NULL,
    EntidadeOrigemId  uniqueidentifier NOT NULL,
    CampoId           uniqueidentifier NOT NULL,
    RegistroDestinoId uniqueidentifier NOT NULL,
    EntidadeDestinoId uniqueidentifier NOT NULL,
    CONSTRAINT PK_RegistroReferencia PRIMARY KEY (RegistroOrigemId, CampoId),
    CONSTRAINT FK_RegistroReferencia_Origem FOREIGN KEY (RegistroOrigemId, EntidadeOrigemId)
        REFERENCES dbo.Registro (Id, EntidadeId) ON DELETE CASCADE,
    CONSTRAINT FK_RegistroReferencia_CampoOrigem FOREIGN KEY (CampoId, EntidadeOrigemId)
        REFERENCES dbo.Campo (Id, EntidadeId),
    -- sem cascade: excluir um registro referenciado é bloqueado (RESTRINGIR).
    -- CASCATA é feita pela aplicação, excluindo os filhos antes.
    CONSTRAINT FK_RegistroReferencia_Destino FOREIGN KEY (RegistroDestinoId, EntidadeDestinoId)
        REFERENCES dbo.Registro (Id, EntidadeId),
    CONSTRAINT FK_RegistroReferencia_CampoDestino FOREIGN KEY (CampoId, EntidadeDestinoId)
        REFERENCES dbo.Campo (Id, EntidadeReferenciadaId)
);
-- Consultas inversas: "cenários deste relatório", "endpoints deste projeto"
CREATE INDEX IX_RegistroReferencia_Destino ON dbo.RegistroReferencia (RegistroDestinoId, CampoId);

/* Garante unicidade no banco, inclusive com concorrência.
   A aplicação calcula SHA-256 dos valores normalizados dos campos da restrição
   (na ordem dos campos; referências entram pelo Id do registro de destino) e grava
   aqui na mesma transação do Registro. Se algum valor for nulo, não grava linha
   (mesma semântica do UNIQUE do SQL: nulo não conflita). */
CREATE TABLE dbo.RegistroChaveUnica (
    RestricaoUnicaId uniqueidentifier NOT NULL,
    EntidadeId       uniqueidentifier NOT NULL,
    RegistroId       uniqueidentifier NOT NULL,
    ValorHash        binary(32)       NOT NULL,
    CONSTRAINT PK_RegistroChaveUnica PRIMARY KEY (RestricaoUnicaId, RegistroId),
    CONSTRAINT UQ_RegistroChaveUnica_Valor UNIQUE (RestricaoUnicaId, ValorHash),
    CONSTRAINT FK_RegistroChaveUnica_Restricao FOREIGN KEY (RestricaoUnicaId, EntidadeId)
        REFERENCES dbo.RestricaoUnica (Id, EntidadeId),
    CONSTRAINT FK_RegistroChaveUnica_Registro FOREIGN KEY (RegistroId, EntidadeId)
        REFERENCES dbo.Registro (Id, EntidadeId) ON DELETE CASCADE
);
GO


/* =====================================================================
   3. SEED — o diagrama "Entidades" (testes de desempenho) dentro do modelo
   Obrigatoriedade e unicidades não estavam no diagrama: foram inferidas,
   revisar antes de usar.
   ===================================================================== */

DECLARE @grupo uniqueidentifier = NEWID();
INSERT dbo.Grupo (Id, Chave, Nome) VALUES (@grupo, 'testes_desempenho', N'Testes de Desempenho');

DECLARE @comunidade uniqueidentifier = NEWID(), @sigla    uniqueidentifier = NEWID(),
        @projeto    uniqueidentifier = NEWID(), @endpoint uniqueidentifier = NEWID(),
        @rtc        uniqueidentifier = NEWID(), @ambiente uniqueidentifier = NEWID(),
        @status     uniqueidentifier = NEWID(), @relatorio uniqueidentifier = NEWID(),
        @cenario    uniqueidentifier = NEWID(), @link     uniqueidentifier = NEWID();

INSERT dbo.Entidade (Id, GrupoId, Chave, Nome) VALUES
    (@comunidade, @grupo, 'comunidade',       N'Comunidade'),
    (@sigla,      @grupo, 'sigla',            N'Sigla'),
    (@projeto,    @grupo, 'projeto',          N'Projeto'),
    (@endpoint,   @grupo, 'endpoint_servico', N'Endpoint de Serviço'),
    (@rtc,        @grupo, 'rtc',              N'RTC'),
    (@ambiente,   @grupo, 'ambiente',         N'Ambiente'),
    (@status,     @grupo, 'status_resultado', N'Status do Resultado'),
    (@relatorio,  @grupo, 'relatorio_teste',  N'Relatório de Teste'),
    (@cenario,    @grupo, 'cenario_teste',    N'Cenário de Teste'),
    (@link,       @grupo, 'link',             N'Link');

INSERT dbo.Campo (EntidadeId, Ordem, Chave, Nome, TipoDado, Obrigatorio,
                  TamanhoMaximo, Precisao, Escala, EntidadeReferenciadaId, AcaoExclusao) VALUES
    -- Comunidade
    (@comunidade, 1, 'nome', N'Nome', 'TEXTO', 1, 100, NULL, NULL, NULL, NULL),
    -- Sigla
    (@sigla, 1, 'comunidade',   N'Comunidade',      'REFERENCIA', 1, NULL, NULL, NULL, @comunidade, 'RESTRINGIR'),
    (@sigla, 2, 'codigo_sigla', N'Código da sigla', 'TEXTO',      1, 20,   NULL, NULL, NULL, NULL),
    -- Projeto
    (@projeto, 1, 'sigla',       N'Sigla',          'REFERENCIA',  1, NULL, NULL, NULL, @sigla, 'RESTRINGIR'),
    (@projeto, 2, 'nome',        N'Nome',           'TEXTO',       1, 150,  NULL, NULL, NULL, NULL),
    (@projeto, 3, 'url_projeto', N'URL do projeto', 'TEXTO_LONGO', 0, NULL, NULL, NULL, NULL, NULL),
    -- EndpointServico
    (@endpoint, 1, 'projeto',           N'Projeto',                'REFERENCIA', 1, NULL, NULL, NULL, @projeto, 'RESTRINGIR'),
    (@endpoint, 2, 'nome_servico',      N'Nome do serviço',        'TEXTO',      1, 150,  NULL, NULL, NULL, NULL),
    (@endpoint, 3, 'metodo',            N'Método',                 'TEXTO',      1, 50,   NULL, NULL, NULL, NULL),
    (@endpoint, 4, 'endpoint_url_fila', N'Endpoint / URL da fila', 'TEXTO',      1, 255,  NULL, NULL, NULL, NULL),
    (@endpoint, 5, 'ativo',             N'Ativo',                  'BOOLEANO',   1, NULL, NULL, NULL, NULL, NULL),
    -- RTC
    (@rtc, 1, 'rtc_externo_id', N'ID externo do RTC', 'TEXTO', 1, 20, NULL, NULL, NULL, NULL),
    -- Ambiente
    (@ambiente, 1, 'nome',                         N'Nome',                         'TEXTO',       1, 50,   NULL, NULL, NULL, NULL),
    (@ambiente, 2, 'servidor_gerenciador_filas',   N'Servidor gerenciador de filas', 'TEXTO',      0, 255,  NULL, NULL, NULL, NULL),
    (@ambiente, 3, 'configuracoes_infraestrutura', N'Configurações de infraestrutura', 'TEXTO_LONGO', 0, NULL, NULL, NULL, NULL, NULL),
    -- StatusResultado
    (@status, 1, 'descricao', N'Descrição', 'TEXTO', 1, 30, NULL, NULL, NULL, NULL),
    -- RelatorioTeste
    (@relatorio,  1, 'projeto',          N'Projeto',           'REFERENCIA',  1, NULL, NULL, NULL, @projeto,  'RESTRINGIR'),
    (@relatorio,  2, 'rtc',              N'RTC',               'REFERENCIA',  1, NULL, NULL, NULL, @rtc,      'RESTRINGIR'),
    (@relatorio,  3, 'ambiente',         N'Ambiente',          'REFERENCIA',  1, NULL, NULL, NULL, @ambiente, 'RESTRINGIR'),
    (@relatorio,  4, 'status_resultado', N'Status',            'REFERENCIA',  1, NULL, NULL, NULL, @status,   'RESTRINGIR'),
    (@relatorio,  5, 'data_execucao',    N'Data de execução',  'DATA_HORA',   1, NULL, NULL, NULL, NULL, NULL),
    (@relatorio,  6, 'bateria_testes',   N'Bateria de testes', 'TEXTO',       1, 50,   NULL, NULL, NULL, NULL),
    (@relatorio,  7, 'tipo_teste',       N'Tipo de teste',     'TEXTO',       1, 50,   NULL, NULL, NULL, NULL),
    (@relatorio,  8, 'executado_por',    N'Executado por',     'TEXTO',       1, 150,  NULL, NULL, NULL, NULL),
    (@relatorio,  9, 'contexto',         N'Contexto',          'TEXTO_LONGO', 0, NULL, NULL, NULL, NULL, NULL),
    (@relatorio, 10, 'objetivo',         N'Objetivo',          'TEXTO_LONGO', 0, NULL, NULL, NULL, NULL, NULL),
    (@relatorio, 11, 'apontamentos',     N'Apontamentos',      'TEXTO_LONGO', 0, NULL, NULL, NULL, NULL, NULL),
    (@relatorio, 12, 'conclusao',        N'Conclusão',         'TEXTO_LONGO', 0, NULL, NULL, NULL, NULL, NULL),
    -- CenarioTeste
    (@cenario, 1, 'relatorio_teste',         N'Relatório',               'REFERENCIA', 1, NULL, NULL, NULL, @relatorio, 'CASCATA'),
    (@cenario, 2, 'endpoint_servico',        N'Endpoint',                'REFERENCIA', 1, NULL, NULL, NULL, @endpoint,  'RESTRINGIR'),
    (@cenario, 3, 'numero_cenario',          N'Nº do cenário',           'INTEIRO',    1, NULL, NULL, NULL, NULL, NULL),
    (@cenario, 4, 'usuarios_virtuais',       N'Usuários virtuais',       'INTEIRO',    1, NULL, NULL, NULL, NULL, NULL),
    (@cenario, 5, 'tempo_medio_resposta_ms', N'Tempo médio (ms)',        'INTEIRO',    1, NULL, NULL, NULL, NULL, NULL),
    (@cenario, 6, 'percentil_95_ms',         N'Percentil 95 (ms)',       'INTEIRO',    1, NULL, NULL, NULL, NULL, NULL),
    (@cenario, 7, 'vazao_media_tps',         N'Vazão média (TPS)',       'DECIMAL',    1, NULL, 18,   2,    NULL, NULL),
    (@cenario, 8, 'taxa_erro_percentual',    N'Taxa de erro (%)',        'DECIMAL',    1, NULL, 5,    2,    NULL, NULL),
    -- Link
    (@link, 1, 'relatorio_teste', N'Relatório',          'REFERENCIA',  1, NULL, NULL, NULL, @relatorio, 'CASCATA'),
    (@link, 2, 'rtc',             N'RTC',                'REFERENCIA',  0, NULL, NULL, NULL, @rtc,       'RESTRINGIR'),
    (@link, 3, 'texto_exibicao',  N'Texto de exibição',  'TEXTO',       1, 100,  NULL, NULL, NULL, NULL),
    (@link, 4, 'url',             N'URL',                'TEXTO_LONGO', 1, NULL, NULL, NULL, NULL, NULL);

-- Campo usado como rótulo de cada entidade
UPDATE e SET CampoExibicaoId = c.Id
FROM dbo.Entidade e
JOIN (VALUES (@comunidade, 'nome'), (@sigla, 'codigo_sigla'), (@projeto, 'nome'),
             (@endpoint, 'nome_servico'), (@rtc, 'rtc_externo_id'), (@ambiente, 'nome'),
             (@status, 'descricao'), (@relatorio, 'bateria_testes'),
             (@cenario, 'numero_cenario'), (@link, 'texto_exibicao')) v (EntidadeId, Chave)
     ON v.EntidadeId = e.Id
JOIN dbo.Campo c ON c.EntidadeId = v.EntidadeId AND c.Chave = v.Chave;

-- Unicidades (inferidas)
INSERT dbo.RestricaoUnica (EntidadeId, Nome) VALUES
    (@comunidade, 'uq_comunidade_nome'),
    (@sigla,      'uq_sigla_codigo'),
    (@projeto,    'uq_projeto_sigla_nome'),
    (@rtc,        'uq_rtc_externo_id'),
    (@ambiente,   'uq_ambiente_nome'),
    (@status,     'uq_status_descricao'),
    (@cenario,    'uq_cenario_relatorio_numero'),  -- composta
    (@link,       'uq_link_rtc');                  -- RTC 1 : 0..1 Link

INSERT dbo.RestricaoUnicaCampo (RestricaoUnicaId, EntidadeId, CampoId)
SELECT r.Id, r.EntidadeId, c.Id
FROM (VALUES ('uq_comunidade_nome', 'nome'),
             ('uq_sigla_codigo', 'codigo_sigla'),
             ('uq_projeto_sigla_nome', 'sigla'),
             ('uq_projeto_sigla_nome', 'nome'),
             ('uq_rtc_externo_id', 'rtc_externo_id'),
             ('uq_ambiente_nome', 'nome'),
             ('uq_status_descricao', 'descricao'),
             ('uq_cenario_relatorio_numero', 'relatorio_teste'),
             ('uq_cenario_relatorio_numero', 'numero_cenario'),
             ('uq_link_rtc', 'rtc')) v (Restricao, Campo)
JOIN dbo.RestricaoUnica r ON r.Nome = v.Restricao
JOIN dbo.Entidade e       ON e.Id = r.EntidadeId AND e.GrupoId = @grupo
JOIN dbo.Campo c          ON c.EntidadeId = r.EntidadeId AND c.Chave = v.Campo;

-- Campo dependente: endpoint do cenário filtrado pelo projeto do relatório
INSERT dbo.CampoDependencia (CampoId, CampoViaId, CampoOrigemId, CampoAlvoId)
SELECT
    (SELECT Id FROM dbo.Campo WHERE EntidadeId = @cenario   AND Chave = 'endpoint_servico'),
    (SELECT Id FROM dbo.Campo WHERE EntidadeId = @cenario   AND Chave = 'relatorio_teste'),
    (SELECT Id FROM dbo.Campo WHERE EntidadeId = @relatorio AND Chave = 'projeto'),
    (SELECT Id FROM dbo.Campo WHERE EntidadeId = @endpoint  AND Chave = 'projeto');
GO
