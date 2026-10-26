import { TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideRouter } from '@angular/router';
import { campo } from '../../../testes/metadados-de-teste';
import { Campo } from '../../core/metadados/modelo';
import { ValorCampoComponent } from './valor-campo.component';

describe('ValorCampoComponent (leitura nas tabelas e no detalhe)', () => {
  function renderizar(c: Campo, valor: unknown, extras: { comoLink?: boolean; modo?: 'tabela' | 'detalhe' } = {}) {
    TestBed.configureTestingModule({ providers: [provideRouter([]), provideNoopAnimations()] });
    const fixture = TestBed.createComponent(ValorCampoComponent);
    fixture.componentRef.setInput('campo', c);
    fixture.componentRef.setInput('valor', valor);
    fixture.componentRef.setInput('grupo', 'testes_desempenho');
    fixture.componentRef.setInput('comoLink', extras.comoLink ?? false);
    fixture.componentRef.setInput('modo', extras.modo ?? 'tabela');
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  }

  it('URL vira link para uma nova aba, seguro, avisando o leitor de tela; na tabela, truncado', () => {
    const raiz = renderizar(campo('url', 'URL'), 'https://grafana.exemplo.com/d/1');
    const link = raiz.querySelector('a')!;
    expect(link.getAttribute('href')).toBe('https://grafana.exemplo.com/d/1');
    expect(link.getAttribute('target')).toBe('_blank');
    expect(link.getAttribute('rel')).toBe('noopener noreferrer');
    expect(link.classList).toContain('link-url-truncado');
    expect(link.textContent).toContain('(abre numa nova aba)');
  });

  it('URL no detalhe não é truncada', () => {
    const raiz = renderizar(campo('url', 'URL'), 'https://exemplo.com', { modo: 'detalhe' });
    expect(raiz.querySelector('a')!.classList).not.toContain('link-url-truncado');
  });

  it('referência para entidade por equipe é link para o item; para cadastro de apoio, texto', () => {
    const projeto = campo('projeto', 'REFERENCIA', { entidadeReferenciada: 'projeto' });
    const comoLink = renderizar(projeto, { id: 'p1', rotulo: 'Portal' }, { comoLink: true });
    expect(comoLink.querySelector('a')?.getAttribute('href')).toBe('/testes_desempenho/projeto/p1');
    expect(comoLink.textContent?.trim()).toBe('Portal');

    TestBed.resetTestingModule();
    const ambiente = campo('ambiente', 'REFERENCIA', { entidadeReferenciada: 'ambiente' });
    const comoTexto = renderizar(ambiente, { id: 'a1', rotulo: 'Homologação' }, { comoLink: false });
    expect(comoTexto.querySelector('a')).toBeNull();
    expect(comoTexto.textContent?.trim()).toBe('Homologação');
  });

  it('booleano: ícone com texto para o leitor de tela na tabela, "Sim/Não" no detalhe', () => {
    const tabela = renderizar(campo('ativo', 'BOOLEANO'), false);
    expect(tabela.querySelector('.pi-times-circle')).not.toBeNull();
    expect(tabela.querySelector('.sr-only')?.textContent).toBe('Não');

    TestBed.resetTestingModule();
    expect(renderizar(campo('ativo', 'BOOLEANO'), true, { modo: 'detalhe' }).textContent?.trim()).toBe('Sim');
  });

  it('texto longo preserva as quebras de linha', () => {
    const raiz = renderizar(campo('contexto', 'TEXTO_LONGO'), 'linha 1\nlinha 2', { modo: 'detalhe' });
    expect(raiz.querySelector('.texto-longo')?.textContent).toBe('linha 1\nlinha 2');
  });
});
