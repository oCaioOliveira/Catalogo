import { FormControl } from '@angular/forms';
import { TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { AutoCompleteSelectEvent } from 'primeng/autocomplete';
import { ID_CRIAR_NOVO } from '../formulario/criar-relacionado';
import { ValorCampo } from '../formulario/valores';
import { CampoReferenciaComponent } from './campo-referencia.component';

// A opção "+ Criar <tipo>" no fim da lista do select de referência.
describe('CampoReferenciaComponent: "+ Criar <tipo>"', () => {
  function criar(criarNovo: string | null, valor: ValorCampo = null) {
    TestBed.configureTestingModule({ providers: [provideNoopAnimations()] });
    const fixture = TestBed.createComponent(CampoReferenciaComponent);
    const controle = new FormControl<ValorCampo>(valor);
    fixture.componentRef.setInput('controle', controle);
    fixture.componentRef.setInput('inputId', 'rtc');
    fixture.componentRef.setInput('buscar', async () => [{ id: 'r1', rotulo: 'RTC-ALFA-101' }]);
    fixture.componentRef.setInput('criarNovo', criarNovo);
    fixture.detectChanges();
    return { componente: fixture.componentInstance, controle };
  }

  it('aparece no fim da lista, com o texto digitado, quando o usuário pode criar', async () => {
    const { componente } = criar('RTC');

    await componente.pesquisar({ query: 'RTC-9', originalEvent: new Event('input') });

    expect(componente.sugestoes().map((s) => s.rotulo)).toEqual(['RTC-ALFA-101', "+ Criar RTC 'RTC-9'"]);
  });

  it('não aparece sem permissão (nem nos filtros, que não passam o tipo)', async () => {
    const { componente } = criar(null);

    await componente.pesquisar({ query: 'RTC-9', originalEvent: new Event('input') });

    expect(componente.sugestoes().map((s) => s.id)).toEqual(['r1']);
  });

  it('escolher a opção emite o texto e não muda o valor do campo', async () => {
    const anterior = { id: 'r1', rotulo: 'RTC-ALFA-101' };
    const { componente, controle } = criar('RTC', anterior);
    let emitido: string | null = null;
    componente.criar.subscribe((texto) => (emitido = texto));
    await componente.pesquisar({ query: ' RTC-9 ', originalEvent: new Event('input') });

    controle.setValue({ id: ID_CRIAR_NOVO, rotulo: '+ Criar' });
    componente.aoEscolher({
      value: { id: ID_CRIAR_NOVO },
      originalEvent: new Event('click'),
    } as AutoCompleteSelectEvent);

    expect(emitido as string | null).toBe('RTC-9');
    expect(controle.value).toEqual(anterior);
  });
});
