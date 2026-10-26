import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { SemListagensComponent } from './sem-listagens.component';

// RN90: quem não tem nenhuma área de gestão habilitada para as suas equipes cai nesta página (telaInicialGuard → /sem-listagens).
describe('SemListagensComponent', () => {
  it('explica que não há área de gestão para as equipes e manda falar com o administrador', () => {
    TestBed.configureTestingModule({ imports: [SemListagensComponent], providers: [provideRouter([])] });
    const fixture = TestBed.createComponent(SemListagensComponent);
    fixture.detectChanges();
    const elemento = fixture.nativeElement as HTMLElement;
    expect(elemento.querySelector('h1')?.textContent).toBe('Nenhuma área de gestão disponível');
    expect(elemento.textContent).toContain('Nenhuma área de gestão disponível para as suas equipes');
    expect(elemento.textContent).toContain('Fale com o administrador');
    expect(elemento.querySelector('a')?.getAttribute('href')).toBe('/minha-conta');
  });
});
