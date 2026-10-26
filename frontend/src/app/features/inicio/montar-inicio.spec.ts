import { campo, entidade } from '../../../testes/metadados-de-teste';
import { atalhosDaArea, itensRecentes, saudacao, textoQuantidade } from './montar-inicio';

describe('página Início (montagem)', () => {
  const relatorio = entidade('relatorio_teste', [campo('nome', 'TEXTO')], {
    nome: 'Relatório de Teste',
    nomePlural: 'Relatórios de Teste',
    icone: 'pi-chart-line',
    ordem: 1,
  });
  const projeto = entidade('projeto', [campo('nome', 'TEXTO')], { nome: 'Projeto', ordem: 2 });
  const sigla = entidade('sigla', [campo('codigo', 'TEXTO')], { escopoEquipe: false, ordem: 3 });
  const cenario = entidade(
    'cenario',
    [
      campo('relatorio_teste', 'REFERENCIA', {
        obrigatorio: true,
        acaoExclusao: 'CASCATA',
        entidadeReferenciada: 'relatorio_teste',
      }),
    ],
    { ordem: 4 },
  );
  const todas = [projeto, sigla, cenario, relatorio];

  it('os atalhos são os tipos de item principais, na ordem do menu, com ícone, plural e se pode criar', () => {
    const atalhos = atalhosDaArea(todas, (e) => e.chave === 'relatorio_teste');

    expect(atalhos.map((a) => a.entidade.chave)).toEqual(['relatorio_teste', 'projeto']);
    expect(atalhos[0].nome).toBe('Relatórios de Teste');
    expect(atalhos[0].rotuloNovo).toBe('Novo: Relatório de Teste');
    expect(atalhos[0].icone).toBe('pi pi-chart-line');
    expect(atalhos[1].icone).toBe('pi pi-file');
    expect(atalhos.map((a) => a.podeCriar)).toEqual([true, false]);
  });

  it('os recentes ganham o tipo e o link; sem rótulo, um texto; tipo que sumiu, fora', () => {
    const recentes = itensRecentes(
      'testes',
      [
        { id: '1', entidade: 'relatorio_teste', rotulo: 'Bateria 01', ocorridoEm: '2026-10-24T12:00:00Z' },
        { id: '2', entidade: 'projeto', rotulo: null, ocorridoEm: '2026-10-24T11:00:00Z' },
        { id: '3', entidade: 'desativada', rotulo: 'X', ocorridoEm: '2026-10-24T10:00:00Z' },
      ],
      todas,
    );

    expect(recentes.map((r) => r.id)).toEqual(['1', '2']);
    expect(recentes[0].tipo).toBe('Relatório de Teste');
    expect(recentes[0].link).toEqual(['/', 'testes', 'relatorio_teste', '1']);
    expect(recentes[1].rotulo).toBe('(sem rótulo)');
  });

  it('a saudação segue a hora e usa o primeiro nome', () => {
    expect(saudacao('Gabriela Gestora', 9)).toBe('Bom dia, Gabriela');
    expect(saudacao('Gabriela Gestora', 14)).toBe('Boa tarde, Gabriela');
    expect(saudacao('Gabriela Gestora', 20)).toBe('Boa noite, Gabriela');
    expect(saudacao('', 3)).toBe('Boa noite');
  });

  it('quantidade em português', () => {
    expect(textoQuantidade(1)).toBe('1 item');
    expect(textoQuantidade(0)).toBe('0 itens');
    expect(textoQuantidade(1234)).toBe('1.234 itens');
  });
});
