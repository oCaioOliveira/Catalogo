/* ---------------------------------------------------------------------
   Impressão digital da configuração de UMA área de gestão (trecho reaproveitado, não é um script completo)
   ---------------------------------------------------------------------
   Pré-requisito: as tabelas temporárias #AreaGrupo, #AreaEntidade, #AreaCampo, #AreaRestricao, #AreaRestricaoCampo e
   #AreaDependencia já preenchidas (ler_area_cat.sql ou ler_area_catalogo.sql), com os nomes LÓGICOS das colunas.
   Resultado:
     - @Canonico: uma linha de texto por objeto da configuração (área, tipos de item, campos, unicidades e seus campos,
       dependências), com TODOS os atributos de configuração, na ordem dos identificadores;
     - @Impressao: o SHA-256 dessas linhas. Duas áreas com a mesma impressão têm exatamente a mesma configuração, seja qual
       for o schema de origem (catalogo ou cat).
   Ficam de fora o que não é configuração: datas de criação e alteração e a versão (rowversion), que mudam a cada instalação.
   Exige SET QUOTED_IDENTIFIER ON (padrão do SSMS; no sqlcmd, use -I), por causa do .value() do XML.
   Compatível com SQL Server 2014: o HASHBYTES aceita no máximo 8000 bytes, então o texto é resumido em partes de 3500
   caracteres, encadeadas (hash = SHA-256(hash anterior + parte)).
   --------------------------------------------------------------------- */
DECLARE @Canonico TABLE (Secao tinyint NOT NULL, Chave1 uniqueidentifier NOT NULL, Chave2 uniqueidentifier NULL, Linha nvarchar(max) NOT NULL);

INSERT @Canonico (Secao, Chave1, Chave2, Linha)
SELECT 1, g.Id, NULL, CONCAT(N'Grupo|', g.Id, N'|', g.Chave, N'|', g.Nome, N'|', COALESCE(N'"' + g.Descricao + N'"', N'~'),
                             N'|Ativo=', CAST(g.Ativo AS int))
FROM #AreaGrupo g;

INSERT @Canonico (Secao, Chave1, Chave2, Linha)
SELECT 2, e.Id, NULL, CONCAT(N'Entidade|', e.Id, N'|', e.Chave, N'|', e.Nome, N'|', COALESCE(N'"' + e.NomePlural + N'"', N'~'),
                             N'|', COALESCE(N'"' + e.Descricao + N'"', N'~'), N'|', COALESCE(e.Icone, N'~'),
                             N'|EscopoEquipe=', CAST(e.EscopoEquipe AS int), N'|Ativo=', CAST(e.Ativo AS int), N'|Ordem=', e.Ordem,
                             N'|CampoExibicao=', COALESCE(CAST(e.CampoExibicaoId AS nvarchar(36)), N'~'))
FROM #AreaEntidade e;

INSERT @Canonico (Secao, Chave1, Chave2, Linha)
SELECT 3, c.Id, NULL, CONCAT(N'Campo|', c.Id, N'|', c.EntidadeId, N'|', c.Chave, N'|', c.Nome, N'|', c.TipoDado,
                             N'|Obrigatorio=', CAST(c.Obrigatorio AS int), N'|TamanhoMaximo=', COALESCE(CAST(c.TamanhoMaximo AS nvarchar(11)), N'~'),
                             N'|Precisao=', COALESCE(CAST(c.Precisao AS nvarchar(3)), N'~'), N'|Escala=', COALESCE(CAST(c.Escala AS nvarchar(3)), N'~'),
                             N'|Opcoes=', COALESCE(c.Opcoes, N'~'),
                             N'|Referencia=', COALESCE(CAST(c.EntidadeReferenciadaId AS nvarchar(36)), N'~'), N'|AcaoExclusao=', COALESCE(c.AcaoExclusao, N'~'),
                             N'|Ordem=', c.Ordem, N'|Ativo=', CAST(c.Ativo AS int))
FROM #AreaCampo c;

INSERT @Canonico (Secao, Chave1, Chave2, Linha)
SELECT 4, r.Id, NULL, CONCAT(N'RestricaoUnica|', r.Id, N'|', r.EntidadeId, N'|', r.Nome)
FROM #AreaRestricao r;

INSERT @Canonico (Secao, Chave1, Chave2, Linha)
SELECT 5, rc.RestricaoUnicaId, rc.CampoId, CONCAT(N'RestricaoUnicaCampo|', rc.RestricaoUnicaId, N'|', rc.EntidadeId, N'|', rc.CampoId)
FROM #AreaRestricaoCampo rc;

INSERT @Canonico (Secao, Chave1, Chave2, Linha)
SELECT 6, d.CampoId, NULL, CONCAT(N'CampoDependencia|', d.CampoId, N'|Via=', COALESCE(CAST(d.CampoViaId AS nvarchar(36)), N'~'),
                                  N'|Origem=', d.CampoOrigemId, N'|Alvo=', d.CampoAlvoId)
FROM #AreaDependencia d;

DECLARE @TextoCanonico nvarchar(max) = COALESCE(
    (SELECT k.Linha + NCHAR(10) FROM @Canonico k ORDER BY k.Secao, k.Chave1, k.Chave2 FOR XML PATH(''), TYPE).value('.', 'nvarchar(max)'), N'');
DECLARE @Impressao varbinary(32) = HASHBYTES('SHA2_256', N''), @Posicao int = 1;
WHILE @Posicao <= DATALENGTH(@TextoCanonico) / 2
BEGIN
    SET @Impressao = HASHBYTES('SHA2_256', @Impressao + CAST(SUBSTRING(@TextoCanonico, @Posicao, 3500) AS varbinary(max)));
    SET @Posicao = @Posicao + 3500;
END;
-- Fim do trecho da impressão digital.
