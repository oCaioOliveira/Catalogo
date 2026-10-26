import { HttpErrorResponse } from '@angular/common/http';
import { MENSAGENS_ERRO, mensagemDoErro } from './mensagens-erro';
import { SEM_CONEXAO, lerProblema } from './problema';

describe('erros da API', () => {
  it('lê codigo, traceId, errors e detalhes do ProblemDetails', () => {
    const problema = lerProblema(
      new HttpErrorResponse({
        status: 409,
        error: {
          codigo: 'CONFLITO_UNICIDADE',
          detail: 'x',
          traceId: 't1',
          errors: { nome: ['y'] },
          detalhes: { campos: ['nome'] },
        },
      }),
    );
    expect(problema).toEqual(
      jasmine.objectContaining({ status: 409, codigo: 'CONFLITO_UNICIDADE', traceId: 't1', detalhe: 'x' }),
    );
    expect(problema.erros).toEqual({ nome: ['y'] });
    expect(problema.detalhes).toEqual({ campos: ['nome'] });
  });

  it('status 0 é falta de conexão com a API', () => {
    const problema = lerProblema(new HttpErrorResponse({ status: 0 }));
    expect(problema.codigo).toBe(SEM_CONEXAO);
    expect(mensagemDoErro(problema.codigo, 0)).toContain('Sem conexão');
  });

  it('código conhecido tem mensagem própria; desconhecido usa o status', () => {
    expect(mensagemDoErro('CONFLITO_CONCORRENCIA', 409)).toBe(MENSAGENS_ERRO['CONFLITO_CONCORRENCIA']);
    expect(mensagemDoErro('CODIGO_NOVO', 503)).toBe(MENSAGENS_ERRO['ERRO_INTERNO']);
    expect(mensagemDoErro('CODIGO_NOVO', 403)).toBe(MENSAGENS_ERRO['SEM_PERMISSAO']);
  });

  it('todos os códigos de docs/api/codigos-de-erro.md têm mensagem', () => {
    const codigos = [
      'VALIDACAO',
      'EXPORTACAO_MUITO_GRANDE',
      'REQUISICAO_INVALIDA',
      'NAO_AUTENTICADO',
      'CREDENCIAIS_INVALIDAS',
      'SEM_PERMISSAO',
      'TROCA_DE_SENHA_OBRIGATORIA',
      'GRUPO_NAO_ENCONTRADO',
      'ENTIDADE_NAO_ENCONTRADA',
      'REGISTRO_NAO_ENCONTRADO',
      'CONFLITO_UNICIDADE',
      'CONFLITO_CONCORRENCIA',
      'EXCLUSAO_BLOQUEADA',
      'ENTIDADE_INATIVA',
      'IMPORTACAO_NAO_ENCONTRADA',
      'IMPORTACAO_SITUACAO_INVALIDA',
      'IMPORTACAO_DADOS_MUDARAM',
      'IMPORTACAO_NAO_PODE_SER_DESFEITA',
      'IMPORTACAO_ARQUIVO_EXPIRADO',
      'MUITAS_TENTATIVAS',
      'ERRO_INTERNO',
    ];
    expect(codigos.filter((c) => !MENSAGENS_ERRO[c])).toEqual([]);
  });
});
