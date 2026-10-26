import { TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { SenhaProvisoriaDialogComponent } from './senha-provisoria-dialog.component';

describe('SenhaProvisoriaDialogComponent (ADR 0041)', () => {
  const SENHA = 'Prov1s0r1aXyz987';

  function criar() {
    TestBed.configureTestingModule({ providers: [provideNoopAnimations()] });
    const fixture = TestBed.createComponent(SenhaProvisoriaDialogComponent);
    fixture.detectChanges();
    return fixture;
  }

  it('mostra a senha, e "Já copiei" a descarta: some do componente e da página', () => {
    const fixture = criar();
    let fechou = false;
    fixture.componentInstance.fechado.subscribe(() => (fechou = true));

    fixture.componentInstance.mostrar(SENHA, 'Fulano (c123)');
    fixture.detectChanges();
    expect(document.body.textContent).toContain(SENHA);
    expect(document.body.textContent).toContain('Esta senha não será exibida novamente');

    fixture.componentInstance.fechar();
    fixture.detectChanges();
    expect(fixture.componentInstance.senha()).toBeNull();
    expect(fixture.componentInstance.destinatario()).toBe('');
    expect(document.body.textContent).not.toContain(SENHA);
    expect(fechou).toBeTrue();
  });

  it('a senha não vai para sessionStorage, localStorage nem URL', () => {
    const fixture = criar();
    fixture.componentInstance.mostrar(SENHA, 'Fulano');
    fixture.detectChanges();
    const armazenado = [
      ...Object.keys(sessionStorage).map((k) => sessionStorage.getItem(k)),
      ...Object.keys(localStorage).map((k) => localStorage.getItem(k)),
      window.location.href,
    ].join(' ');
    expect(armazenado).not.toContain(SENHA);
    fixture.componentInstance.fechar();
  });

  it('não fecha com Esc nem clicando fora (só pelo botão)', () => {
    const fixture = criar();
    fixture.componentInstance.mostrar(SENHA, 'Fulano');
    fixture.detectChanges();
    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', code: 'Escape', bubbles: true }));
    (document.querySelector('.p-dialog-mask') as HTMLElement | null)?.click();
    fixture.detectChanges();
    expect(fixture.componentInstance.senha()).toBe(SENHA);
    expect(document.body.textContent).toContain(SENHA);
    fixture.componentInstance.fechar();
    fixture.detectChanges();
  });
});
