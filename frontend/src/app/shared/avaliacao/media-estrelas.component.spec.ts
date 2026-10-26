import { TestBed } from '@angular/core/testing';
import { MediaEstrelasComponent } from './media-estrelas.component';

describe('MediaEstrelasComponent (RN48)', () => {
  function montar(media: number | null): HTMLElement {
    const fixture = TestBed.createComponent(MediaEstrelasComponent);
    fixture.componentRef.setInput('media', media);
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  }

  it('sem nota mostra um traço', () => {
    expect(montar(null).textContent?.trim()).toBe('—');
  });

  it('4,3 vira quatro estrelas e meia, com o número e o nome acessível', () => {
    const el = montar(4.3);
    const icones = Array.from(el.querySelectorAll('i')).map((i) => i.className.replace('pi ', '').trim());
    expect(icones).toEqual(['pi-star-fill', 'pi-star-fill', 'pi-star-fill', 'pi-star-fill', 'pi-star-half-fill']);
    expect(el.querySelector('[role="img"]')?.getAttribute('aria-label')).toBe('Média 4,3 de 5');
  });
});
