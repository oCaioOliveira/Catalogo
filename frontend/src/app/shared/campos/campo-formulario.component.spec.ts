import { FormControl } from '@angular/forms';
import { TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { campo } from '../../../testes/metadados-de-teste';
import { ValorCampo } from '../formulario/valores';
import { CampoFormularioComponent } from './campo-formulario.component';

/**
 * Nome acessível dos controles do formulário genérico: cada controle tem que ser achado pelo NOME DO CAMPO (como um leitor de
 * tela o anuncia). O p-select (LISTA) não é um <input>: o <label for> não o rotula, e ele precisa de aria-labelledby.
 */
describe('CampoFormularioComponent: nome acessível de cada tipo', () => {
  function renderizar(tipo: Parameters<typeof campo>[1], extras: Parameters<typeof campo>[2] = {}): HTMLElement {
    TestBed.configureTestingModule({ providers: [provideNoopAnimations()] });
    const fixture = TestBed.createComponent(CampoFormularioComponent);
    fixture.componentRef.setInput('campo', campo('metodo', tipo, { nome: 'Método HTTP', ...extras }));
    fixture.componentRef.setInput('controle', new FormControl<ValorCampo>(tipo === 'LISTA' ? 'POST' : null));
    fixture.componentRef.setInput('prefixoId', 'teste');
    fixture.detectChanges();
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  }

  /** O nome acessível: pelo aria-labelledby, pelo aria-label ou pelo <label for>. */
  function nomeAcessivel(raiz: HTMLElement, controle: Element): string {
    const ids = controle.getAttribute('aria-labelledby');
    if (ids) {
      return ids
        .split(' ')
        .map((id) => raiz.querySelector(`[id="${id}"]`)?.textContent?.trim() ?? '')
        .join(' ');
    }
    return (
      controle.getAttribute('aria-label') ??
      raiz.querySelector(`label[for="${controle.id}"]`)?.textContent?.trim() ??
      ''
    );
  }

  it('LISTA: o combobox se chama pelo rótulo do campo, e não pelo valor escolhido', () => {
    const raiz = renderizar('LISTA', { opcoes: ['GET', 'POST'] });
    const combobox = raiz.querySelector('[role="combobox"]')!;
    expect(nomeAcessivel(raiz, combobox)).toContain('Método HTTP');
    expect(nomeAcessivel(raiz, combobox)).not.toBe('POST');
  });

  for (const tipo of ['TEXTO', 'TEXTO_LONGO', 'INTEIRO', 'DATA', 'BOOLEANO', 'URL'] as const) {
    it(`${tipo}: o controle se chama pelo rótulo do campo`, () => {
      const raiz = renderizar(tipo);
      const controle = raiz.querySelector('input, textarea, [role="combobox"], [role="switch"]')!;
      expect(nomeAcessivel(raiz, controle)).toContain('Método HTTP');
    });
  }
});
