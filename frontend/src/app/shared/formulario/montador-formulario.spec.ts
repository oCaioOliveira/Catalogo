import { HttpErrorResponse } from '@angular/common/http';
import { campo, entidade } from '../../../testes/metadados-de-teste';
import { lerProblema } from '../../core/erros/problema';
import {
  aplicarErrosDoServidor,
  corpoDoFormulario,
  layoutDoFormulario,
  marcarUnicidade,
  montarFormulario,
  preservarValores,
} from './montador-formulario';
import { mensagensDeErro } from './validadores';

describe('montador-formulario', () => {
  const meta = entidade('teste', [
    campo('nome', 'TEXTO', { obrigatorio: true, tamanhoMaximo: 5 }),
    campo('descricao', 'TEXTO_LONGO'),
    campo('quantidade', 'INTEIRO', { obrigatorio: true }),
    campo('vazao', 'DECIMAL', { precisao: 5, escala: 2 }),
    campo('ativo', 'BOOLEANO', { obrigatorio: true }),
    campo('dia', 'DATA'),
    campo('execucao', 'DATA_HORA'),
    campo('metodo', 'LISTA', { opcoes: ['GET', 'POST'] }),
    campo('projeto', 'REFERENCIA', { obrigatorio: true, entidadeReferenciada: 'projeto' }),
    campo('painel', 'URL'),
  ]);

  it('layout: os curtos no grid de cima, na ordem; os TEXTO_LONGO embaixo, um por linha', () => {
    const comDoisLongos = entidade('teste', [
      campo('contexto', 'TEXTO_LONGO'),
      campo('nome', 'TEXTO'),
      campo('objetivo', 'TEXTO_LONGO'),
      campo('url', 'URL'),
    ]);
    const layout = layoutDoFormulario(comDoisLongos.campos);
    expect(layout.curtos.map((c) => c.chave)).toEqual(['nome', 'url']);
    expect(layout.longos.map((c) => c.chave)).toEqual(['contexto', 'objetivo']);
  });

  it('URL: validada como a do back-end e enviada sem espaços nas pontas', () => {
    const formulario = montarFormulario(meta);
    formulario.controls['painel'].setValue('grafana/painel');
    expect(formulario.controls['painel'].errors).toEqual({ url: true });
    expect(mensagensDeErro(formulario.controls['painel'].errors)[0]).toContain('http://');

    formulario.controls['painel'].setValue('  https://grafana.exemplo.com/d/1  ');
    expect(formulario.controls['painel'].errors).toBeNull();
    expect(corpoDoFormulario(meta, formulario)['painel']).toBe('https://grafana.exemplo.com/d/1');
  });

  it('metadados recarregados: o digitado volta para os campos que continuam, com o mesmo tipo', () => {
    const antes = entidade('teste', [campo('nome', 'TEXTO'), campo('cor', 'TEXTO'), campo('qtd', 'INTEIRO')]);
    const depois = entidade('teste', [campo('nome', 'TEXTO'), campo('qtd', 'TEXTO'), campo('novo', 'TEXTO')]);
    const novo = montarFormulario(depois);

    preservarValores(novo, depois, { nome: 'Notebook', cor: 'azul', qtd: 3 }, antes.campos);

    expect(novo.getRawValue()).toEqual({ nome: 'Notebook', qtd: null, novo: null });
    expect(novo.controls['nome'].dirty).toBeTrue();
  });

  it('cria um controle por campo ativo', () => {
    const formulario = montarFormulario(meta);
    expect(Object.keys(formulario.controls)).toEqual(meta.campos.map((c) => c.chave));
  });

  it('obrigatório: vazio e texto só com espaços são recusados (RN03); booleano começa em "não" e é válido', () => {
    const formulario = montarFormulario(meta);
    formulario.controls['nome'].setValue('   ');
    expect(formulario.controls['nome'].hasError('obrigatorio')).toBeTrue();
    expect(formulario.controls['quantidade'].hasError('obrigatorio')).toBeTrue();
    expect(formulario.controls['projeto'].hasError('obrigatorio')).toBeTrue();
    expect(formulario.controls['ativo'].value).toBeFalse();
    expect(formulario.controls['ativo'].valid).toBeTrue();
  });

  it('TEXTO: tamanho máximo contado sem os espaços das pontas', () => {
    const controle = montarFormulario(meta).controls['nome'];
    controle.setValue('  abcde  ');
    expect(controle.valid).toBeTrue();
    controle.setValue('abcdef');
    expect(mensagensDeErro(controle.errors)).toEqual(['Deve ter no máximo 5 caracteres (tem 6).']);
  });

  it('INTEIRO: sem casas e dentro de 32 bits', () => {
    const controle = montarFormulario(meta).controls['quantidade'];
    controle.setValue(1.5);
    expect(controle.hasError('inteiro')).toBeTrue();
    controle.setValue(2147483648);
    expect(controle.hasError('faixa')).toBeTrue();
    controle.setValue(-2147483648);
    expect(controle.valid).toBeTrue();
  });

  it('DECIMAL: escala e dígitos antes da vírgula (precisão − escala)', () => {
    const controle = montarFormulario(meta).controls['vazao'];
    controle.setValue(123.45);
    expect(controle.valid).toBeTrue();
    controle.setValue(1.234);
    expect(controle.hasError('escala')).toBeTrue();
    controle.setValue(1234.5);
    expect(controle.hasError('digitos')).toBeTrue();
  });

  it('LISTA: só uma das opções', () => {
    const controle = montarFormulario(meta).controls['metodo'];
    controle.setValue('PUT');
    expect(controle.hasError('opcao')).toBeTrue();
    controle.setValue('GET');
    expect(controle.valid).toBeTrue();
  });

  it('edição: os valores da API viram valores de tela (datas como Date, referência como { id, rotulo })', () => {
    const formulario = montarFormulario(meta, {
      id: 'r1',
      dados: {
        nome: 'abc',
        dia: '2026-10-21',
        execucao: '2026-10-21T17:30:00Z',
        projeto: { id: 'p1', rotulo: 'Pix' },
        vazao: 12.5,
      },
    });
    const dia = formulario.controls['dia'].value as Date;
    expect([dia.getFullYear(), dia.getMonth(), dia.getDate()]).toEqual([2026, 9, 21]);
    expect((formulario.controls['execucao'].value as Date).toISOString()).toBe('2026-10-21T17:30:00.000Z');
    expect(formulario.controls['projeto'].value).toEqual({ id: 'p1', rotulo: 'Pix' });
  });

  it('o corpo leva todos os campos no formato da API (ISO, ponto decimal, só o id da referência)', () => {
    const formulario = montarFormulario(meta);
    formulario.patchValue({
      nome: ' abc ',
      quantidade: 3,
      vazao: 12.5,
      ativo: true,
      dia: new Date(2026, 9, 21),
      metodo: 'GET',
      projeto: { id: 'p1', rotulo: 'Pix' },
      descricao: '   ',
    });
    formulario.controls['projeto'].disable();
    const corpo = corpoDoFormulario(meta, formulario);
    expect(corpo).toEqual(
      jasmine.objectContaining({
        nome: 'abc',
        quantidade: 3,
        vazao: 12.5,
        ativo: true,
        dia: '2026-10-21',
        metodo: 'GET',
        projeto: 'p1',
        descricao: null,
        execucao: null,
      }),
    );
  });

  it('DATA_HORA vai com o fuso do navegador', () => {
    const formulario = montarFormulario(meta);
    formulario.controls['execucao'].setValue(new Date(2026, 9, 21, 14, 30));
    const texto = corpoDoFormulario(meta, formulario)['execucao'] as string;
    expect(texto).toMatch(/^2026-10-21T14:30:00[+-]\d{2}:\d{2}$/);
    expect(new Date(texto).getTime()).toBe(new Date(2026, 9, 21, 14, 30).getTime());
  });

  it('erros 400 do servidor vão para os campos; os sem campo voltam para o topo', () => {
    const formulario = montarFormulario(meta);
    const problema = lerProblema(
      new HttpErrorResponse({
        status: 400,
        error: { codigo: 'VALIDACAO', errors: { nome: ['Já existe.'], equipeId: ['Obrigatório.'] } },
      }),
    );
    const gerais = aplicarErrosDoServidor(formulario, problema);
    expect(mensagensDeErro(formulario.controls['nome'].errors)).toContain('Já existe.');
    expect(formulario.controls['nome'].touched).toBeTrue();
    expect(gerais).toEqual(['Obrigatório.']);
  });

  it('409 de unicidade marca os campos da restrição', () => {
    const formulario = montarFormulario(meta);
    const problema = lerProblema(
      new HttpErrorResponse({
        status: 409,
        error: { codigo: 'CONFLITO_UNICIDADE', detail: 'Valor repetido.', detalhes: { campos: ['nome', 'metodo'] } },
      }),
    );
    expect(marcarUnicidade(formulario, problema)).toEqual(['nome', 'metodo']);
    expect(mensagensDeErro(formulario.controls['metodo'].errors)).toEqual(['Valor repetido.']);
  });
});
