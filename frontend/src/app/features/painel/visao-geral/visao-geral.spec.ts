import { TestBed } from '@angular/core/testing';
import { entidadeDesempenho } from '../../../../testes/metadados-desempenho';
import { VisaoGeral } from '../../../core/painel/modelo-painel';
import { BlocoAprovacaoComponent, BlocoAvaliacaoComponent } from './blocos-gestao.component';
import { indicadoresCriadosPorMes, kpisDaVisaoGeral, resumoAprovacao, resumoAvaliacao } from './visao-geral';

/** Uma visão geral simulada COM aprovação e avaliação (Testes de Desempenho hoje não as usa). */
const visao: VisaoGeral = {
  volume: {
    total: 120,
    criadosNoPeriodo: 30,
    alteradosNoPeriodo: 7,
    porEntidade: [{ entidade: 'relatorio_teste', nome: 'Relatório de Teste', total: 40, criadosNoPeriodo: 12 }],
    porEquipe: [
      { equipeId: 'a', nome: 'Alfa', quantidade: 80 },
      { equipeId: 'b', nome: 'Beta', quantidade: 0 },
    ],
    criadosPorMes: [{ mes: '2026-09', quantidade: 10 }],
  },
  aprovacao: {
    diasPendenteAntigo: 7,
    total: {
      pendentes: 3,
      pendentesAntigos: 1,
      aprovados: 20,
      rejeitados: 2,
      tempoMedioPrimeiraDecisaoHoras: 5.5,
      taxaRejeicao: 0.25,
    },
    porEntidade: [
      {
        entidade: 'relatorio_teste',
        nome: 'Relatório de Teste',
        numeros: { pendentes: 3, aprovados: 20, rejeitados: 2, taxaRejeicao: 0.25 },
      },
    ],
  },
  avaliacao: {
    minimoAvaliacoesRanking: 3,
    porEntidade: [
      {
        entidade: 'relatorio_teste',
        nome: 'Relatório de Teste',
        criterios: [
          {
            criterio: 'qualidade',
            nome: 'Qualidade',
            media: 3.5,
            quantidade: 8,
            distribuicao: { '1': 0, '2': 3, '3': 1, '4': 1, '5': 3 },
            melhores: [{ id: 'x', rotulo: 'Bateria 1', media: 4, quantidade: 3 }],
            piores: [],
          },
        ],
      },
    ],
  },
};

describe('visão geral (dados → tela)', () => {
  it('KPIs: total e criados da entidade principal, alterados e equipes com itens', () => {
    expect(kpisDaVisaoGeral(visao, entidadeDesempenho('relatorio_teste')).map((k) => [k.chave, k.valor])).toEqual([
      ['total', 40],
      ['criados', 12],
      ['alterados', 7],
      ['equipes', 1],
    ]);
  });

  it('os cartões da entidade principal usam o nome no plural', () => {
    const principal = { ...entidadeDesempenho('relatorio_teste'), nomePlural: 'Relatórios de Teste' };
    expect(kpisDaVisaoGeral(visao, principal)[0].rotulo).toBe('Relatórios de Teste: total');
  });

  it('criados por mês viram indicadores com dimensão de mês', () => {
    expect(indicadoresCriadosPorMes(visao).linhas).toEqual([{ mes: '2026-09', quantidade: 10 }]);
  });

  it('aprovação e avaliação: nulos sem dados (o bloco não aparece), resumidos com dados', () => {
    expect(resumoAprovacao({ ...visao, aprovacao: undefined })).toBeNull();
    expect(resumoAvaliacao({ ...visao, avaliacao: undefined })).toBeNull();
    expect(resumoAprovacao(visao)!.cartoes.find((c) => c.chave === 'taxa')!.valor).toBe(25);
    const distribuicao = resumoAvaliacao(visao)!.criterios[0].distribuicao;
    // Critério sem nenhuma nota não entra; se nenhum tem nota, não há bloco.
    const semNotas = structuredClone(visao);
    semNotas.avaliacao!.porEntidade![0].criterios![0].quantidade = 0;
    expect(resumoAvaliacao(semNotas)).toBeNull();
    expect(distribuicao.linhas.map((l) => l['quantidade'])).toEqual([0, 3, 1, 1, 3]);
  });
});

describe('blocos de aprovação e avaliação (renderizados com dados simulados)', () => {
  it('aprovação: os cartões e a tabela por tipo de item', () => {
    const fixture = TestBed.createComponent(BlocoAprovacaoComponent);
    fixture.componentRef.setInput('resumo', resumoAprovacao(visao));
    fixture.detectChanges();
    const texto = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(texto).toContain('Pendentes há mais de 7 dias');
    expect(texto).toContain('5,5 h');
    expect(texto).toContain('25%');
    expect(texto).toContain('Relatório de Teste');
  });

  it('avaliação: o gráfico de distribuição e o ranking', () => {
    const fixture = TestBed.createComponent(BlocoAvaliacaoComponent);
    const resumo = resumoAvaliacao(visao)!;
    fixture.componentRef.setInput('criterios', resumo.criterios);
    fixture.detectChanges();
    const elemento = fixture.nativeElement as HTMLElement;
    expect(elemento.textContent).toContain('Relatório de Teste: Qualidade: média 3,5 de 5 (8 notas)');
    expect(elemento.textContent).toContain('Bateria 1: 4 (3 notas)');
    expect(elemento.querySelector('canvas')).not.toBeNull();
  });
});
