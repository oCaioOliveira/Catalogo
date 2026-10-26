/* ---------------------------------------------------------------------
   Impressão digital da configuração de UMA área de gestão (trecho reaproveitado, não é um script completo)
   ---------------------------------------------------------------------
   Pré-requisito: a variável @GrupoChave varchar(100) já declarada com a chave da área (ex.: 'testes_desempenho').
   Resultado:
     - @Canonico: uma linha de texto por objeto da configuração (área, tipos de item, campos, unicidades e seus campos,
       dependências), com TODOS os atributos de configuração, na ordem dos identificadores;
     - @Impressao: o SHA-256 dessas linhas. Duas áreas com a mesma impressão têm exatamente a mesma configuração.
   Ficam de fora o que não é configuração: CriadoEm, AtualizadoEm e Versao (rowversion), que mudam a cada instalação.
   Exige SET QUOTED_IDENTIFIER ON (padrão do SSMS; no sqlcmd, use -I), por causa do .value() do XML.
   Usado pelo gerador (gerar_carga_area.sh), pela própria carga gerada (autoconferência antes do COMMIT) e pela
   comparação comparar_bancos.sh. Compatível com SQL Server 2016.
   --------------------------------------------------------------------- */
DECLARE @Canonico TABLE (Secao tinyint NOT NULL, Chave1 uniqueidentifier NOT NULL, Chave2 uniqueidentifier NULL, Linha nvarchar(max) NOT NULL);
DECLARE @GrupoAlvo uniqueidentifier = (SELECT Id FROM catalogo.Grupo WHERE Chave = @GrupoChave);

INSERT @Canonico (Secao, Chave1, Chave2, Linha)
SELECT 1, g.Id, NULL, CONCAT(N'Grupo|', g.Id, N'|', g.Chave, N'|', g.Nome, N'|', COALESCE(N'"' + g.Descricao + N'"', N'~'),
                             N'|Ativo=', CAST(g.Ativo AS int))
FROM catalogo.Grupo g WHERE g.Id = @GrupoAlvo;

INSERT @Canonico (Secao, Chave1, Chave2, Linha)
SELECT 2, e.Id, NULL, CONCAT(N'Entidade|', e.Id, N'|', e.Chave, N'|', e.Nome, N'|', COALESCE(N'"' + e.NomePlural + N'"', N'~'),
                             N'|', COALESCE(N'"' + e.Descricao + N'"', N'~'), N'|', COALESCE(e.Icone, N'~'),
                             N'|EscopoEquipe=', CAST(e.EscopoEquipe AS int), N'|Ativo=', CAST(e.Ativo AS int), N'|Ordem=', e.Ordem,
                             N'|CampoExibicao=', COALESCE(CAST(e.CampoExibicaoId AS nvarchar(36)), N'~'))
FROM catalogo.Entidade e WHERE e.GrupoId = @GrupoAlvo;

INSERT @Canonico (Secao, Chave1, Chave2, Linha)
SELECT 3, c.Id, NULL, CONCAT(N'Campo|', c.Id, N'|', c.EntidadeId, N'|', c.Chave, N'|', c.Nome, N'|', c.TipoDado,
                             N'|Obrigatorio=', CAST(c.Obrigatorio AS int), N'|TamanhoMaximo=', COALESCE(CAST(c.TamanhoMaximo AS nvarchar(11)), N'~'),
                             N'|Precisao=', COALESCE(CAST(c.Precisao AS nvarchar(3)), N'~'), N'|Escala=', COALESCE(CAST(c.Escala AS nvarchar(3)), N'~'),
                             N'|Opcoes=', COALESCE(c.Opcoes, N'~'),
                             N'|Referencia=', COALESCE(CAST(c.EntidadeReferenciadaId AS nvarchar(36)), N'~'), N'|AcaoExclusao=', COALESCE(c.AcaoExclusao, N'~'),
                             N'|Ordem=', c.Ordem, N'|Ativo=', CAST(c.Ativo AS int))
FROM catalogo.Campo c JOIN catalogo.Entidade e ON e.Id = c.EntidadeId WHERE e.GrupoId = @GrupoAlvo;

INSERT @Canonico (Secao, Chave1, Chave2, Linha)
SELECT 4, r.Id, NULL, CONCAT(N'RestricaoUnica|', r.Id, N'|', r.EntidadeId, N'|', r.Nome)
FROM catalogo.RestricaoUnica r JOIN catalogo.Entidade e ON e.Id = r.EntidadeId WHERE e.GrupoId = @GrupoAlvo;

INSERT @Canonico (Secao, Chave1, Chave2, Linha)
SELECT 5, rc.RestricaoUnicaId, rc.CampoId, CONCAT(N'RestricaoUnicaCampo|', rc.RestricaoUnicaId, N'|', rc.EntidadeId, N'|', rc.CampoId)
FROM catalogo.RestricaoUnicaCampo rc JOIN catalogo.Entidade e ON e.Id = rc.EntidadeId WHERE e.GrupoId = @GrupoAlvo;

INSERT @Canonico (Secao, Chave1, Chave2, Linha)
SELECT 6, d.CampoId, NULL, CONCAT(N'CampoDependencia|', d.CampoId, N'|Via=', COALESCE(CAST(d.CampoViaId AS nvarchar(36)), N'~'),
                                  N'|Origem=', d.CampoOrigemId, N'|Alvo=', d.CampoAlvoId)
FROM catalogo.CampoDependencia d
JOIN catalogo.Campo c ON c.Id = d.CampoId
JOIN catalogo.Entidade e ON e.Id = c.EntidadeId
WHERE e.GrupoId = @GrupoAlvo;

DECLARE @Impressao varbinary(32) = HASHBYTES('SHA2_256',
    (SELECT k.Linha + NCHAR(10) FROM @Canonico k ORDER BY k.Secao, k.Chave1, k.Chave2 FOR XML PATH(''), TYPE).value('.', 'nvarchar(max)'));
-- Fim do trecho da impressão digital.
