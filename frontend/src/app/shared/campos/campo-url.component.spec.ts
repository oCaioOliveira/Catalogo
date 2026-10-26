import { TestBed } from '@angular/core/testing';
import { FormControl } from '@angular/forms';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { campo } from '../../../testes/metadados-de-teste';
import { erroDeUrl, url, validadoresDoCampo } from '../formulario/validadores';
import { ValorCampo } from '../formulario/valores';
import { CampoUrlComponent } from './campo-url.component';

// URL (RN04, ADR 0042): a mesma regra do UrlValidador do back-end.
describe('validação de URL', () => {
  it('aceita endereços absolutos http e https (com porta, caminho, consulta e fragmento)', () => {
    for (const valor of [
      'https://exemplo.com.br',
      'http://localhost:8080/painel?aba=1#topo',
      'HTTPS://Exemplo.COM/a',
      '  https://exemplo.com/a  ',
    ]) {
      expect(url()(new FormControl(valor)))
        .withContext(valor)
        .toBeNull();
    }
  });

  it('recusa sem esquema, outro esquema, relativa, sem host e com espaços', () => {
    expect(erroDeUrl('exemplo.com.br')).toEqual({ url: true });
    expect(erroDeUrl('www.exemplo.com')).toEqual({ url: true });
    expect(erroDeUrl('/caminho/relativo')).toEqual({ url: true });
    expect(erroDeUrl('ftp://arquivos.exemplo.com')).toEqual({ url: true });
    expect(erroDeUrl('javascript:alert(1)')).toEqual({ url: true });
    expect(erroDeUrl('https://')).toEqual({ url: true });
    expect(erroDeUrl('https://exemplo.com/com espaco')).toEqual({ urlComEspacos: true });
  });

  it('até 2000 caracteres (limite)', () => {
    const inicio = 'https://exemplo.com/';
    expect(erroDeUrl(inicio + 'a'.repeat(2000 - inicio.length))).toBeNull();
    expect(erroDeUrl(inicio + 'a'.repeat(2001 - inicio.length))).toEqual({
      tamanhoMaximo: { maximo: 2000, atual: 2001 },
    });
  });

  it('vazio não é erro de URL (a obrigatoriedade é outra regra)', () => {
    expect(url()(new FormControl(''))).toBeNull();
    expect(url()(new FormControl(null))).toBeNull();
  });

  it('os validadores do campo URL incluem a URL e a obrigatoriedade', () => {
    const controle = new FormControl<ValorCampo>('sem-esquema', {
      validators: validadoresDoCampo(campo('url', 'URL', { obrigatorio: true })),
    });
    expect(controle.errors).toEqual({ url: true });
    controle.setValue('');
    expect(controle.errors).toEqual({ obrigatorio: true });
  });
});

describe('CampoUrlComponent', () => {
  function renderizar(valor: string | null) {
    TestBed.configureTestingModule({ providers: [provideNoopAnimations()] });
    const fixture = TestBed.createComponent(CampoUrlComponent);
    const controle = new FormControl<ValorCampo>(valor);
    fixture.componentRef.setInput('campo', campo('url', 'URL', { nome: 'URL do painel' }));
    fixture.componentRef.setInput('controle', controle);
    fixture.componentRef.setInput('inputId', 'teste-url');
    fixture.detectChanges();
    return { fixture, controle, raiz: fixture.nativeElement as HTMLElement };
  }

  it('input do tipo url', () => {
    const { raiz } = renderizar(null);
    expect(raiz.querySelector('input')?.getAttribute('type')).toBe('url');
  });

  it('com URL válida, o botão é um link para uma nova aba, seguro e com nome acessível', () => {
    const { raiz } = renderizar('https://grafana.exemplo.com/d/1');
    const link = raiz.querySelector('a')!;
    expect(link.getAttribute('href')).toBe('https://grafana.exemplo.com/d/1');
    expect(link.getAttribute('target')).toBe('_blank');
    expect(link.getAttribute('rel')).toBe('noopener noreferrer');
    expect(link.getAttribute('aria-label')).toBe('Abrir URL do painel numa nova aba');
  });

  it('sem URL válida, o botão fica desabilitado; ao digitar uma válida, vira link', () => {
    const { fixture, controle, raiz } = renderizar('exemplo.com');
    expect(raiz.querySelector('a')).toBeNull();
    expect(raiz.querySelector('button')?.disabled).toBeTrue();

    controle.setValue('https://exemplo.com');
    fixture.detectChanges();
    expect(raiz.querySelector('a')?.getAttribute('href')).toBe('https://exemplo.com');
  });
});
