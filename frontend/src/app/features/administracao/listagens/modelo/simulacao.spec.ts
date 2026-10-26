import { Impacto } from '../../../../core/administracao/estrutura-listagem';
import { Problema } from '../../../../core/erros/problema';
import { classificarSimulacao, impactosDoBloqueio, orientacao, resumoDoImpacto } from './simulacao';

describe('simulação antes de gravar', () => {
  const impacto = (tipo: string, bloqueia: boolean, quantidade: number): Impacto => ({
    tipo,
    descricao: `descrição de ${tipo}`,
    bloqueia,
    quantidade,
    exemplos: [],
    grupos: null,
    usos: null,
  });

  it('permitido sem impacto: grava direto', () => {
    expect(classificarSimulacao({ permitido: true, impactos: [] }, false)).toEqual({ tipo: 'sem-impacto' });
    // Impacto informativo com zero itens também não pede confirmação.
    const vazio = impacto('VALOR_PADRAO_APLICADO', false, 0);
    expect(classificarSimulacao({ permitido: true, impactos: [vazio] }, true)).toEqual({ tipo: 'sem-impacto' });
    // Campo obrigatório novo numa entidade sem itens: a API avisa "sem valor" com zero itens, e é permitido.
    const semItens = impacto('CAMPO_OBRIGATORIO_SEM_VALOR', true, 0);
    expect(classificarSimulacao({ permitido: true, impactos: [semItens] }, true)).toEqual({ tipo: 'sem-impacto' });
  });

  it('permitido com impacto: confirma, com o resumo', () => {
    const aplicado = impacto('VALOR_PADRAO_APLICADO', false, 120);
    const classificacao = classificarSimulacao({ permitido: true, impactos: [aplicado] }, true);
    expect(classificacao).toEqual({ tipo: 'com-impacto', impactos: [aplicado] });
    expect(resumoDoImpacto(aplicado)).toBe('120 itens receberão o valor padrão.');
    expect(resumoDoImpacto(impacto('VALOR_PADRAO_APLICADO', false, 1))).toBe('1 item receberá o valor padrão.');
  });

  it('bloqueado: relatório só com o que bloqueia (o efeito informativo não aconteceria)', () => {
    const texto = impacto('TEXTO_ACIMA_DO_TAMANHO', true, 2);
    const info = impacto('CHAVES_UNICAS_GERADAS', false, 5);
    expect(classificarSimulacao({ permitido: false, impactos: [info, texto] }, false)).toEqual({
      tipo: 'bloqueado',
      impactos: [texto],
    });
    expect(orientacao('TEXTO_ACIMA_DO_TAMANHO')).toContain('tamanho máximo maior');
  });

  it('só faltam valores num campo que vira obrigatório: pede o valor padrão (se a tela puder)', () => {
    const semValor = impacto('CAMPO_OBRIGATORIO_SEM_VALOR', true, 3);
    expect(classificarSimulacao({ permitido: false, impactos: [semValor] }, true)).toEqual({
      tipo: 'pede-valor-padrao',
      impacto: semValor,
    });
    expect(classificarSimulacao({ permitido: false, impactos: [semValor] }, false).tipo).toBe('bloqueado');
    // Com outro bloqueio junto, o valor padrão não resolve: bloqueado.
    const outro = impacto('TEXTO_ACIMA_DO_TAMANHO', true, 1);
    expect(classificarSimulacao({ permitido: false, impactos: [semValor, outro] }, true).tipo).toBe('bloqueado');
  });

  it('bloqueio por uso (sem itens, com usos) também é mostrado', () => {
    const emUso = { ...impacto('CAMPO_EM_USO', true, 0), usos: ['exibição'] };
    expect(classificarSimulacao({ permitido: false, impactos: [emUso] }, false)).toEqual({
      tipo: 'bloqueado',
      impactos: [emUso],
    });
    const semContagem = impacto('ENTIDADE_COM_CONFIGURACOES', true, 0);
    expect(classificarSimulacao({ permitido: false, impactos: [semContagem] }, false)).toEqual({
      tipo: 'bloqueado',
      impactos: [semContagem],
    });
  });

  it('o 409 de bloqueio vira o mesmo relatório; outros erros não', () => {
    const problema = (codigo: string, detalhes: Record<string, unknown> | null): Problema => ({
      status: 409,
      codigo,
      titulo: null,
      detalhe: null,
      traceId: null,
      erros: {},
      detalhes,
    });
    const impactos = impactosDoBloqueio(
      problema('METADADO_EM_USO', {
        impactos: [{ tipo: 'CAMPO_EM_USO', bloqueia: true, quantidade: 0, usos: ['exibição'] }],
      }),
    );
    expect(impactos?.[0].usos).toEqual(['exibição']);
    expect(impactosDoBloqueio(problema('CONFLITO_CONCORRENCIA', null))).toBeNull();
  });
});
