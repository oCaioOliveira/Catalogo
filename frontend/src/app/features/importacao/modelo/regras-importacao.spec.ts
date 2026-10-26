import { campo, entidade } from '../../../../testes/metadados-de-teste';
import { ColunaArquivo, DestinoDisponivel, LinhaImportacao, Totais } from '../../../core/importacao/modelo-importacao';
import {
  descricaoDuplicados,
  destinoDoValor,
  errosDaCelula,
  errosDaLinha,
  escolherDestino,
  lembrarMapeamento,
  linhasDosImpedimentos,
  mapeamentoInicial,
  mapeamentoParaApi,
  mensagemDesfazer,
  motivoNaoExecutavel,
  obrigatoriosSemColuna,
  opcoesDoSelect,
  problemaDoArquivo,
  referenciaDaCelula,
  rotuloExecutar,
  temReferencias,
  textoConclusao,
  textoTotais,
  valorDoDestino,
} from './regras-importacao';

// RN97 a RN104: as regras da tela de importação.
describe('regras-importacao', () => {
  const colunas: ColunaArquivo[] = [
    { indice: 0, cabecalho: 'Projeto *', exemplos: ['Alfa'], destino: { tipo: 'CAMPO', campo: 'projeto' } },
    { indice: 1, cabecalho: 'Observação', exemplos: [], destino: { tipo: 'IGNORAR', campo: null } },
    { indice: 2, cabecalho: 'Equipe', exemplos: ['Squad'], destino: { tipo: 'EQUIPE', campo: null } },
  ];
  const destinos: DestinoDisponivel[] = [
    { tipo: 'CAMPO', campo: 'projeto', rotulo: 'Projeto', obrigatorio: true },
    { tipo: 'CAMPO_ID', campo: 'projeto', rotulo: 'Projeto (ID)', obrigatorio: false },
    { tipo: 'CAMPO', campo: 'bateria', rotulo: 'Bateria', obrigatorio: true },
    { tipo: 'CAMPO', campo: 'conclusao', rotulo: 'Conclusão', obrigatorio: false },
    { tipo: 'EQUIPE', campo: null, rotulo: 'Equipe', obrigatorio: false },
  ];

  it('valor do select e destino vão e voltam', () => {
    for (const d of destinos) {
      expect(destinoDoValor(valorDoDestino(d))).toEqual({ tipo: d.tipo, campo: d.campo });
    }
    expect(destinoDoValor('IGNORAR')).toEqual({ tipo: 'IGNORAR', campo: null });
  });

  it('mapeamento inicial: a sugestão da API', () => {
    expect(mapeamentoInicial(colunas)).toEqual({ 0: 'CAMPO:projeto', 1: 'IGNORAR', 2: 'EQUIPE' });
  });

  it('ao reenviar, o que a pessoa escolheu para o mesmo cabeçalho vale mais que a sugestão', () => {
    const anterior = lembrarMapeamento(colunas, { 0: 'CAMPO:projeto', 1: 'CAMPO:conclusao', 2: 'IGNORAR' });
    const reenviadas: ColunaArquivo[] = [
      { indice: 0, cabecalho: 'observacao', exemplos: [], destino: { tipo: 'IGNORAR', campo: null } },
      { indice: 1, cabecalho: 'PROJETO', exemplos: [], destino: { tipo: 'CAMPO', campo: 'projeto' } },
      { indice: 2, cabecalho: 'Bateria', exemplos: [], destino: { tipo: 'CAMPO', campo: 'bateria' } },
    ];

    expect(mapeamentoInicial(reenviadas, anterior)).toEqual({
      0: 'CAMPO:conclusao',
      1: 'CAMPO:projeto',
      2: 'CAMPO:bateria',
    });
  });

  it('escolher um destino já usado tira-o da outra coluna', () => {
    const mapa = escolherDestino({ 0: 'CAMPO:projeto', 1: 'IGNORAR' }, 1, 'CAMPO:projeto');

    expect(mapa).toEqual({ 0: 'IGNORAR', 1: 'CAMPO:projeto' });
    expect(escolherDestino({ 0: 'IGNORAR', 1: 'IGNORAR' }, 0, 'IGNORAR')).toEqual({ 0: 'IGNORAR', 1: 'IGNORAR' });
  });

  it('mapeamento para a API: uma entrada por coluna', () => {
    expect(mapeamentoParaApi(colunas, { 0: 'CAMPO_ID:projeto' })).toEqual([
      { indice: 0, destino: { tipo: 'CAMPO_ID', campo: 'projeto' } },
      { indice: 1, destino: { tipo: 'IGNORAR', campo: null } },
      { indice: 2, destino: { tipo: 'IGNORAR', campo: null } },
    ]);
  });

  it('opções do select: "Ignorar esta coluna" primeiro e "*" nos obrigatórios', () => {
    const opcoes = opcoesDoSelect(destinos);

    expect(opcoes[0]).toEqual({ rotulo: 'Ignorar esta coluna', valor: 'IGNORAR' });
    expect(opcoes[1].rotulo).toBe('Projeto *');
    expect(opcoes[2].rotulo).toBe('Projeto (ID)');
  });

  it('obrigatórios sem coluna: nem pelo nome nem pelo (ID)', () => {
    expect(obrigatoriosSemColuna(destinos, { 0: 'CAMPO:projeto' }).map((d) => d.campo)).toEqual(['bateria']);
    expect(obrigatoriosSemColuna(destinos, { 0: 'CAMPO_ID:projeto', 1: 'CAMPO:bateria' })).toEqual([]);
  });

  it('referências e duplicados vêm dos metadados', () => {
    const cenario = entidade(
      'cenario',
      [campo('relatorio', 'REFERENCIA', { nome: 'Relatório' }), campo('numero', 'INTEIRO', { nome: 'Nº do cenário' })],
      { restricoesUnicas: [{ nome: 'uq', campos: ['relatorio', 'numero'] }] },
    );

    expect(temReferencias(cenario)).toBeTrue();
    expect(temReferencias(entidade('comunidade', [campo('nome', 'TEXTO')]))).toBeFalse();
    expect(descricaoDuplicados(cenario)).toEqual(['Relatório + Nº do cenário']);
  });

  it('arquivo: só .xlsx e .csv, até 5 MB', () => {
    expect(problemaDoArquivo('relatorios.XLSX', 1000)).toBeNull();
    expect(problemaDoArquivo('dados.csv', 5 * 1024 * 1024)).toBeNull();
    expect(problemaDoArquivo('antigo.xls', 10)).toContain('.xlsx');
    expect(problemaDoArquivo('grande.csv', 6 * 1024 * 1024)).toBe('O arquivo tem 6 MB; o máximo é 5 MB.');
  });

  const linha: LinhaImportacao = {
    numero: 3,
    situacao: 'ERRO',
    valores: ['Projeto X', 'RTC-1'],
    erros: [
      { coluna: 0, campo: 'projeto', mensagem: 'Nenhum item com o nome.' },
      { coluna: null, campo: 'bateria', mensagem: 'Campo obrigatório.' },
      { coluna: null, campo: null, mensagem: 'Repetida no arquivo.' },
    ],
    referencias: [{ coluna: 1, campo: 'rtc', id: 'x', nome: 'RTC-1' }],
    registroId: null,
  };

  it('erros por célula e da linha inteira (com o nome do campo)', () => {
    const meta = entidade('r', [campo('bateria', 'TEXTO', { nome: 'Bateria de testes' })]);

    expect(errosDaCelula(linha, 0)).toEqual(['Nenhum item com o nome.']);
    expect(errosDaCelula(linha, 1)).toEqual([]);
    expect(errosDaLinha(linha, meta)).toEqual(['Bateria de testes: Campo obrigatório.', 'Repetida no arquivo.']);
    expect(referenciaDaCelula(linha, 1)).toBe('RTC-1');
  });

  const totais = (novos: number, atualizacoes = 0, ignorados = 0, comErro = 0): Totais => ({
    novos,
    atualizacoes,
    ignorados,
    comErro,
    total: novos + atualizacoes + ignorados + comErro,
  });

  it('textos dos totais, do botão e da conclusão', () => {
    expect(textoTotais(totais(16, 0, 1, 3))).toBe('16 novos · 1 ignorado · 3 com erro');
    expect(textoTotais(totais(0))).toBe('nenhuma linha');
    expect(rotuloExecutar(totais(1))).toBe('Importar 1 item');
    expect(rotuloExecutar(totais(16, 2))).toBe('Importar 16 itens e atualizar 2');
    expect(rotuloExecutar(totais(0, 3))).toBe('Atualizar 3 itens');
    expect(textoConclusao(totais(16, 0, 1, 3))).toBe(
      '16 itens criados, 1 linha ignorada (já existia), 3 linhas com erro não importadas.',
    );
  });

  it('por que não executa: tudo ou nada com erro, ou nada a gravar', () => {
    expect(motivoNaoExecutavel(totais(2, 0, 0, 1), true)).toBeNull();
    expect(motivoNaoExecutavel(totais(2, 0, 0, 1), false)).toContain('tudo ou nada');
    expect(motivoNaoExecutavel(totais(0, 0, 1, 1), false)).toContain('Não há nenhuma linha');
  });

  it('desfazer: o que sai, o que fica e por que foi recusado', () => {
    expect(mensagemDesfazer(totais(16, 2))).toContain('exclui 16 itens criados');
    expect(mensagemDesfazer(totais(16, 2))).toContain('As 2 atualizações feitas por ela não são desfeitas.');
    expect(
      linhasDosImpedimentos({
        impedimentos: [
          { motivo: 'Itens alterados depois da importação.', quantidade: 12, itens: [{ id: 'a', rotulo: 'B1' }] },
        ],
      }),
    ).toEqual(['• Itens alterados depois da importação. (B1 e mais 11)']);
    expect(linhasDosImpedimentos(null)).toEqual([]);
  });
});
