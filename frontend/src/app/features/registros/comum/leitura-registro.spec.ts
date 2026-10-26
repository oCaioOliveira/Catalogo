import { campo, entidade } from '../../../../testes/metadados-de-teste';
import { Registro } from '../../../core/registros/modelo-registro';
import { leituraDoRegistro, listaDeNomes } from './leitura-registro';

describe('leitura-registro (detalhe)', () => {
  const meta = entidade('relatorio_teste', [
    campo('projeto', 'REFERENCIA', { nome: 'Projeto' }),
    campo('bateria', 'TEXTO', { nome: 'Bateria' }),
    campo('contexto', 'TEXTO_LONGO', { nome: 'Contexto' }),
    campo('objetivo', 'TEXTO_LONGO', { nome: 'Objetivo' }),
    campo('ativo', 'BOOLEANO', { nome: 'Ativo' }),
    campo('usuarios', 'INTEIRO', { nome: 'Usuários' }),
    campo('painel', 'URL', { nome: 'Painel' }),
  ]);

  function registro(dados: Record<string, unknown>): Registro {
    return { id: 'r1', dados } as unknown as Registro;
  }

  it('separa curtos e longos preenchidos, e junta TODOS os vazios (de qualquer tipo)', () => {
    const leitura = leituraDoRegistro(
      meta,
      registro({
        projeto: { id: 'p', rotulo: 'Portal' },
        bateria: '  ',
        contexto: 'linha 1\nlinha 2',
        ativo: false,
        usuarios: 0,
      }),
    );

    expect(leitura.curtos.map((c) => c.chave)).toEqual(['projeto', 'ativo', 'usuarios']);
    expect(leitura.longos.map((c) => c.chave)).toEqual(['contexto']);
    expect(leitura.vazios.map((c) => c.chave)).toEqual(['bateria', 'objetivo', 'painel']);
  });

  it('false e 0 são valores, não vazios', () => {
    const leitura = leituraDoRegistro(meta, registro({ ativo: false, usuarios: 0 }));

    expect(leitura.vazios.map((c) => c.chave)).not.toContain('ativo');
    expect(leitura.vazios.map((c) => c.chave)).not.toContain('usuarios');
  });

  it('lista os nomes em português', () => {
    expect(listaDeNomes([])).toBe('');
    expect(listaDeNomes([meta.campos[2]])).toBe('Contexto');
    expect(listaDeNomes([meta.campos[2], meta.campos[3], meta.campos[6]])).toBe('Contexto, Objetivo, Painel');
  });
});
