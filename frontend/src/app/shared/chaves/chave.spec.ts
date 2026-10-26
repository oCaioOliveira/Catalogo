import { chaveValida, gerarChave } from './chave';

describe('chave gerada a partir do nome', () => {
  it('minúsculas, sem acentos, com underscore', () => {
    expect(gerarChave('Data de aquisição')).toBe('data_de_aquisicao');
    expect(gerarChave('Inventário')).toBe('inventario');
    expect(gerarChave('  Situação  do  Equipamento ')).toBe('situacao_do_equipamento');
  });

  it('símbolos viram um underscore só, sem nas pontas', () => {
    expect(gerarChave('Valor (R$) / mês')).toBe('valor_r_mes');
    expect(gerarChave('--Nome--')).toBe('nome');
    expect(gerarChave('ÇÃÕ-ñ')).toBe('cao_n');
  });

  it('começando por número ganha "c_"; vazio continua vazio', () => {
    expect(gerarChave('2º trimestre')).toBe('c_2_trimestre');
    expect(gerarChave('!!!')).toBe('');
  });

  it('até 100 caracteres', () => {
    expect(gerarChave('a'.repeat(150)).length).toBe(100);
    expect(gerarChave('a '.repeat(80))).not.toMatch(/_$/);
  });

  it('a validação da chave digitada segue o back-end', () => {
    expect(chaveValida('data_aquisicao')).toBeTrue();
    expect(chaveValida('Data')).toBeFalse();
    expect(chaveValida('com espaço')).toBeFalse();
    expect(chaveValida('')).toBeFalse();
  });
});
