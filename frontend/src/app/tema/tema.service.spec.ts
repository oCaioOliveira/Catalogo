import { TestBed } from '@angular/core/testing';
import { CHAVE_TEMA } from './preferencia-tema';
import { CLASSE_ESCURO, TemaService } from './tema.service';

describe('TemaService', () => {
  afterEach(() => {
    localStorage.removeItem(CHAVE_TEMA);
    document.documentElement.classList.remove(CLASSE_ESCURO);
  });

  it('escolher "escuro" aplica a classe no <html> e guarda a escolha', () => {
    const tema = TestBed.inject(TemaService);

    tema.escolher('escuro');

    expect(document.documentElement.classList).toContain(CLASSE_ESCURO);
    expect(localStorage.getItem(CHAVE_TEMA)).toBe('escuro');
    expect(tema.aplicado()).toBe('escuro');
  });

  it('voltar para "claro" tira a classe', () => {
    const tema = TestBed.inject(TemaService);
    tema.escolher('escuro');

    tema.escolher('claro');

    expect(document.documentElement.classList).not.toContain(CLASSE_ESCURO);
    expect(tema.preferencia()).toBe('claro');
  });

  it('começa pela escolha guardada', () => {
    localStorage.setItem(CHAVE_TEMA, 'escuro');

    const tema = TestBed.inject(TemaService);

    expect(tema.preferencia()).toBe('escuro');
    expect(document.documentElement.classList).toContain(CLASSE_ESCURO);
  });
});
