import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideRouter } from '@angular/router';
import { MessageService } from 'primeng/api';
import { EquipesDaListagem, ResultadoAlteracao } from '../../../core/administracao/estrutura-listagem';
import { MetadadosAdminService } from '../../../core/administracao/metadados-admin.service';
import { AbaEquipesComponent } from './aba-equipes.component';
import { AlteracaoEstruturaService } from './comum/alteracao-estrutura.service';
import { resumoDoImpacto } from './modelo/simulacao';

// RN92 e ADR 0044: tirar uma equipe com itens passa pela simulação, que diz quantos itens ficam ocultos, e só grava se confirmar.
describe('AbaEquipesComponent (equipes da listagem, com a simulação)', () => {
  let fixture: ComponentFixture<AbaEquipesComponent>;
  let alteracao: AlteracaoEstruturaService;
  let chamadas: { equipes: string[]; versao: string; simular: boolean }[];
  let dados: EquipesDaListagem;

  const oculto = {
    tipo: 'ITENS_OCULTOS',
    descricao: "Os itens da equipe 'D' ficam ocultos para os membros dela (só o administrador os vê).",
    bloqueia: false,
    quantidade: 3,
    exemplos: [],
    grupos: null,
    usos: null,
  };

  beforeEach(async () => {
    chamadas = [];
    dados = {
      versao: 'v1',
      equipes: [
        { id: 't', nome: 'T', ativa: true, habilitada: false, quantidadeItens: 0 },
        { id: 'd', nome: 'D', ativa: true, habilitada: true, quantidadeItens: 3 },
        { id: 'x', nome: 'X', ativa: false, habilitada: false, quantidadeItens: 0 },
      ],
    };
    await TestBed.configureTestingModule({
      imports: [AbaEquipesComponent],
      providers: [
        provideRouter([]),
        provideNoopAnimations(),
        { provide: MessageService, useValue: { add: () => undefined } },
        {
          provide: MetadadosAdminService,
          useValue: {
            equipes: async () => dados,
            definirEquipes: async (
              _grupo: string,
              equipes: string[],
              versao: string,
              simular: boolean,
            ): Promise<ResultadoAlteracao> => {
              chamadas.push({ equipes, versao, simular });
              return { permitido: true, simulado: simular, impactos: [oculto], estrutura: null };
            },
          },
        },
      ],
    }).compileComponents();
    alteracao = TestBed.inject(AlteracaoEstruturaService);
    fixture = TestBed.createComponent(AbaEquipesComponent);
    fixture.componentRef.setInput('grupo', 'tasks');
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
  });

  function caixa(nome: string): HTMLInputElement {
    const elemento = fixture.nativeElement as HTMLElement;
    const rotulo = [...elemento.querySelectorAll('label')].find((l) => l.textContent?.trim() === nome)!;
    return elemento.querySelector(`#${rotulo.htmlFor}`) as HTMLInputElement;
  }

  async function dialogo(): Promise<string | undefined> {
    for (let i = 0; i < 20 && !alteracao.dialogo(); i++) {
      await Promise.resolve();
    }
    return alteracao.dialogo()?.modo;
  }

  it('mostra todas as equipes, com as inativas identificadas e um rótulo em cada caixa', async () => {
    // O ngModel escreve o valor na caixa de forma assíncrona.
    await fixture.whenStable();
    fixture.detectChanges();
    const texto = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(texto).toContain('Inativa');
    expect(caixa('T').checked).toBeFalse();
    expect(caixa('D').checked).toBeTrue();
    expect(caixa('X')).toBeTruthy();
  });

  it('tirar uma equipe com itens avisa antes, simula, mostra quantos ficam ocultos e grava só ao confirmar', async () => {
    const componente = fixture.componentInstance;
    componente.marcar('d', false);
    componente.marcar('t', true);
    fixture.detectChanges();
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('3 itens da equipe D ficarão ocultos');

    const salvando = componente.salvar();
    expect(await dialogo()).toBe('confirmar');
    const estado = alteracao.dialogo();
    const impactos = estado && estado.modo === 'confirmar' ? estado.impactos : [];
    expect(impactos.map(resumoDoImpacto)).toEqual([
      "Os itens da equipe 'D' ficam ocultos para os membros dela (só o administrador os vê). Itens que ficam ocultos: 3.",
    ]);
    expect(chamadas).toEqual([{ equipes: ['t'], versao: 'v1', simular: true }]);

    alteracao.responder(true);
    await salvando;
    expect(chamadas.map((c) => c.simular)).toEqual([true, false]);
    expect(chamadas[1]).toEqual({ equipes: ['t'], versao: 'v1', simular: false });
  });

  it('cancelar a confirmação não grava nada', async () => {
    const componente = fixture.componentInstance;
    componente.marcar('d', false);
    const salvando = componente.salvar();
    expect(await dialogo()).toBe('confirmar');
    alteracao.responder(false);
    await salvando;
    expect(chamadas.map((c) => c.simular)).toEqual([true]);
  });

  it('sem mudança, Salvar fica desabilitado; Desfazer volta ao que está gravado', () => {
    const componente = fixture.componentInstance;
    expect(componente.alterou()).toBeFalse();
    componente.marcar('x', true);
    expect(componente.alterou()).toBeTrue();
    componente.desfazer();
    expect(componente.alterou()).toBeFalse();
  });
});
