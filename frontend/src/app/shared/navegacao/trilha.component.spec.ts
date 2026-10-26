import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { providePrimeNG } from 'primeng/config';
import { TrilhaComponent } from './trilha.component';

describe('TrilhaComponent', () => {
  let fixture: ComponentFixture<TrilhaComponent>;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [TrilhaComponent],
      providers: [provideRouter([]), providePrimeNG()],
    });
    fixture = TestBed.createComponent(TrilhaComponent);
    fixture.componentRef.setInput('passos', [
      { rotulo: 'Testes de Desempenho' },
      { rotulo: 'Relatórios de Teste', link: ['/testes_desempenho', 'relatorio_teste'] },
      { rotulo: 'Bateria 1' },
    ]);
    fixture.detectChanges();
  });

  it('é um nav rotulado "Trilha", com links para os passos anteriores e a página atual em texto', () => {
    const elemento: HTMLElement = fixture.nativeElement;
    const nav = elemento.querySelector('nav');
    expect(nav?.getAttribute('aria-label')).toBe('Trilha');

    const links = Array.from(elemento.querySelectorAll('a'));
    expect(links.map((a) => a.textContent?.trim())).toEqual(['Relatórios de Teste']);
    expect(links[0].getAttribute('href')).toBe('/testes_desempenho/relatorio_teste');

    const atual = elemento.querySelector('[aria-current="page"]');
    expect(atual?.textContent?.trim()).toBe('Bateria 1');
  });
});
