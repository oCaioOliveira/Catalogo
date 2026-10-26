import { campo, entidade } from '../../../../testes/metadados-de-teste';
import { campoFixo, colunasCandidatasAoPadrao, colunasDaEntidade } from './colunas-registros';

describe('colunas da listagem de registros', () => {
  const relatorio = entidade(
    'relatorio_teste',
    [
      campo('projeto', 'REFERENCIA'),
      campo('conclusao', 'TEXTO_LONGO'),
      campo('bateria', 'TEXTO'),
      campo('usuarios', 'INTEIRO'),
    ],
    { campoExibicao: 'bateria', criteriosAvaliacao: [{ chave: 'qualidade', nome: 'Qualidade' }] },
  );

  it('o campo de exibição é a coluna fixa, primeira', () => {
    const colunas = colunasDaEntidade(relatorio, { comEquipe: false, comSituacao: false });

    expect(colunas[0]).toEqual(jasmine.objectContaining({ chave: 'bateria', fixa: true }));
    expect(colunas.filter((c) => c.fixa).length).toBe(1);
  });

  it('ordem: fixa, equipe, situação, campos curtos, textos longos, médias, criado em', () => {
    const colunas = colunasDaEntidade(relatorio, { comEquipe: true, comSituacao: true });

    expect(colunas.map((c) => c.chave)).toEqual([
      'bateria',
      'equipeId',
      'situacaoAprovacao',
      'projeto',
      'usuarios',
      'conclusao',
      'avaliacao.qualidade',
      'criadoEm',
    ]);
    expect(colunas.find((c) => c.chave === 'usuarios')?.numero).toBeTrue();
  });

  it('texto longo e médias não entram no padrão da primeira visita', () => {
    const candidatas = colunasCandidatasAoPadrao(
      colunasDaEntidade(relatorio, { comEquipe: false, comSituacao: false }),
    );

    expect(candidatas.map((c) => c.chave)).toEqual(['bateria', 'projeto', 'usuarios', 'criadoEm']);
  });

  it('sem campo de exibição, o primeiro campo que não é texto longo fica fixo', () => {
    const semExibicao = entidade('x', [campo('obs', 'TEXTO_LONGO'), campo('codigo', 'TEXTO')], { campoExibicao: null });

    expect(campoFixo(semExibicao)?.chave).toBe('codigo');
  });
});
