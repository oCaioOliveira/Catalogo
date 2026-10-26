import { TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { SeletorIconeComponent } from './seletor-icone.component';

// RN95: o seletor visual de ícone do editor do tipo de item.
describe('SeletorIconeComponent', () => {
  function criar(valor: string | null) {
    TestBed.configureTestingModule({ providers: [provideNoopAnimations()] });
    const fixture = TestBed.createComponent(SeletorIconeComponent);
    fixture.componentRef.setInput('valor', valor);
    fixture.componentRef.setInput('rotulo', 'Projeto');
    fixture.detectChanges();
    return fixture;
  }

  it('mostra o ícone atual (ou o genérico) com o nome legível', () => {
    const fixture = criar('pi-briefcase');
    const raiz = fixture.nativeElement as HTMLElement;

    expect(raiz.querySelector('.icone-atual i')?.className).toContain('pi-briefcase');
    expect(raiz.textContent).toContain('briefcase');
    expect(raiz.querySelector('button')?.getAttribute('aria-label')).toBe('Trocar ícone de Projeto');
  });

  it('sem ícone, o botão diz "Escolher" e o genérico aparece', () => {
    const fixture = criar(null);
    const raiz = fixture.nativeElement as HTMLElement;

    expect(raiz.querySelector('.icone-atual i')?.className).toContain('pi-file');
    expect(raiz.querySelector('button')?.getAttribute('aria-label')).toBe('Escolher ícone de Projeto');
  });

  it('a busca filtra a grade e a escolha é emitida', () => {
    const fixture = criar(null);
    const componente = fixture.componentInstance;
    let escolhido: string | null | undefined;
    componente.mudou.subscribe((icone) => (escolhido = icone));

    componente.busca.set('maleta');
    componente.escolher('pi-briefcase');

    expect(componente.encontrados()).toContain('pi-briefcase');
    expect(escolhido).toBe('pi-briefcase');
  });

  it('o ícone escolhido é o único da grade que recebe o Tab', () => {
    const componente = criar('pi-users').componentInstance;

    expect(componente.encontrados()[componente.indiceFocavel()]).toBe('pi-users');
  });
});
