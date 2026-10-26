import { HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { MessageService } from 'primeng/api';
import { campo } from '../../../../../testes/metadados-de-teste';
import { Impacto, ResultadoAlteracao } from '../../../../core/administracao/estrutura-listagem';
import { AlteracaoEstruturaService } from './alteracao-estrutura.service';

// ADR 0044: toda alteração simula antes de gravar, e o resultado da simulação decide o fluxo.
describe('AlteracaoEstruturaService (simular antes de gravar)', () => {
  let servico: AlteracaoEstruturaService;
  let chamadas: { simular: boolean; valorPadrao: unknown }[];

  const impacto = (tipo: string, bloqueia: boolean, quantidade: number): Impacto => ({
    tipo,
    descricao: tipo,
    bloqueia,
    quantidade,
    exemplos: [],
    grupos: null,
    usos: null,
  });
  const resultado = (permitido: boolean, impactos: Impacto[] = [], simulado = true): ResultadoAlteracao => ({
    permitido,
    simulado,
    impactos,
    estrutura: null,
  });

  beforeEach(() => {
    chamadas = [];
    TestBed.configureTestingModule({ providers: [{ provide: MessageService, useValue: { add: () => undefined } }] });
    servico = TestBed.inject(AlteracaoEstruturaService);
  });

  /** Uma chamada que responde conforme a lista (uma resposta por chamada, na ordem). */
  function chamar(...respostas: (ResultadoAlteracao | HttpErrorResponse)[]) {
    return async (simular: boolean, valorPadrao?: unknown) => {
      chamadas.push({ simular, valorPadrao });
      const resposta = respostas.shift()!;
      if (resposta instanceof HttpErrorResponse) {
        throw resposta;
      }
      return resposta;
    };
  }

  /** Espera o diálogo abrir e devolve o modo dele. */
  async function dialogo(): Promise<string | undefined> {
    for (let i = 0; i < 20 && !servico.dialogo(); i++) {
      await Promise.resolve();
    }
    return servico.dialogo()?.modo;
  }

  it('permitido sem impacto: grava direto, sem diálogo', async () => {
    const desfecho = await servico.executar({
      grupo: 'g',
      titulo: 't',
      chamar: chamar(resultado(true), resultado(true, [], false)),
    });
    expect(desfecho.status).toBe('gravado');
    expect(chamadas.map((c) => c.simular)).toEqual([true, false]);
  });

  it('permitido com impacto: confirma; cancelar não grava', async () => {
    const execucao = servico.executar({
      grupo: 'g',
      titulo: 't',
      chamar: chamar(resultado(true, [impacto('CHAVES_UNICAS_GERADAS', false, 5)])),
    });
    expect(await dialogo()).toBe('confirmar');
    servico.responder(false);
    expect((await execucao).status).toBe('cancelado');
    expect(chamadas.map((c) => c.simular)).toEqual([true]);
  });

  it('permitido com impacto: confirmar grava', async () => {
    const execucao = servico.executar({
      grupo: 'g',
      titulo: 't',
      chamar: chamar(resultado(true, [impacto('CHAVES_UNICAS_GERADAS', false, 5)]), resultado(true, [], false)),
    });
    expect(await dialogo()).toBe('confirmar');
    servico.responder(true);
    expect((await execucao).status).toBe('gravado');
    expect(chamadas.map((c) => c.simular)).toEqual([true, false]);
  });

  it('bloqueado: mostra o relatório e não grava', async () => {
    const execucao = servico.executar({
      grupo: 'g',
      titulo: 't',
      chamar: chamar(resultado(false, [impacto('TEXTO_ACIMA_DO_TAMANHO', true, 2)])),
    });
    expect(await dialogo()).toBe('bloqueado');
    servico.responder(false);
    expect((await execucao).status).toBe('bloqueado');
    expect(chamadas.length).toBe(1);
  });

  it('obrigatório com itens vazios: pede o valor padrão, simula de novo com ele, confirma e grava com ele', async () => {
    const execucao = servico.executar({
      grupo: 'g',
      titulo: 't',
      campoValorPadrao: campo('situacao', 'LISTA', { opcoes: ['Em uso'] }),
      chamar: chamar(
        resultado(false, [impacto('CAMPO_OBRIGATORIO_SEM_VALOR', true, 3)]),
        resultado(true, [impacto('VALOR_PADRAO_APLICADO', false, 3)]),
        resultado(true, [impacto('VALOR_PADRAO_APLICADO', false, 3)], false),
      ),
    });
    expect(await dialogo()).toBe('valor-padrao');
    servico.responder('Em uso');
    expect(await dialogo()).toBe('confirmar');
    servico.responder(true);
    expect((await execucao).status).toBe('gravado');
    expect(chamadas).toEqual([
      { simular: true, valorPadrao: undefined },
      { simular: true, valorPadrao: 'Em uso' },
      { simular: false, valorPadrao: 'Em uso' },
    ]);
  });

  it('409 de versão: avisa do conflito (quem chamou recarrega)', async () => {
    const conflito = new HttpErrorResponse({ status: 409, error: { codigo: 'CONFLITO_CONCORRENCIA' } });
    const execucao = servico.executar({ grupo: 'g', titulo: 't', chamar: chamar(conflito) });
    expect(await dialogo()).toBe('conflito');
    servico.responder(true);
    expect((await execucao).status).toBe('conflito');
  });

  it('400: devolve os erros por campo, sem diálogo', async () => {
    const invalido = new HttpErrorResponse({
      status: 400,
      error: { codigo: 'VALIDACAO', errors: { tamanhoMaximo: ['De 1 a 4000.'] } },
    });
    const desfecho = await servico.executar({ grupo: 'g', titulo: 't', chamar: chamar(invalido) });
    expect(desfecho).toEqual({ status: 'invalido', erros: { tamanhoMaximo: ['De 1 a 4000.'] }, mensagem: null });
    expect(servico.dialogo()).toBeNull();
  });
});
