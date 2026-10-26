import { ChangeDetectionStrategy, Component, OnChanges, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Button } from 'primeng/button';
import { DatePicker } from 'primeng/datepicker';
import { Select } from 'primeng/select';
import { dataDeTextoIso, dataParaTextoIso } from '../../../shared/formatacao/datas';
import { EstadoPainel, OpcaoEquipe } from '../modelo/estado-url';
import { ATALHOS, AtalhoPeriodo, calcularPeriodo } from '../modelo/periodo';

/**
 * Filtros globais do painel, numa linha acima dos blocos: período (atalhos ou personalizado) e equipe. Não consulta a API:
 * devolve o novo estado e a página o põe na URL (todos os blocos recarregam a partir dela).
 */
@Component({
  selector: 'app-filtros-painel',
  imports: [FormsModule, Button, DatePicker, Select],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <form class="filtros-painel" aria-label="Filtros do painel" (ngSubmit)="aplicarPersonalizado()">
      <div class="campo">
        <label class="campo-rotulo" for="filtro-periodo" id="filtro-periodo-rotulo">Período</label>
        <p-select
          inputId="filtro-periodo"
          ariaLabelledBy="filtro-periodo-rotulo"
          name="periodo"
          optionLabel="rotulo"
          optionValue="valor"
          appendTo="body"
          [options]="atalhos"
          [ngModel]="estado().atalho"
          (ngModelChange)="escolherAtalho($event)"
        />
      </div>
      @if (personalizado()) {
        <div class="campo">
          <label class="campo-rotulo" for="filtro-de">De</label>
          <p-datepicker
            inputId="filtro-de"
            name="de"
            dateFormat="dd/mm/yy"
            placeholder="dd/mm/aaaa"
            appendTo="body"
            [showIcon]="true"
            [(ngModel)]="de"
          />
        </div>
        <div class="campo">
          <label class="campo-rotulo" for="filtro-ate">Até</label>
          <p-datepicker
            inputId="filtro-ate"
            name="ate"
            dateFormat="dd/mm/yy"
            placeholder="dd/mm/aaaa"
            appendTo="body"
            [showIcon]="true"
            [(ngModel)]="ate"
          />
        </div>
        <div class="campo campo-acao">
          <p-button type="submit" label="Aplicar período" icon="pi pi-check" [disabled]="!de || !ate || de > ate" />
        </div>
      }
      @if (opcoesEquipe(); as opcoes) {
        <div class="campo">
          <label class="campo-rotulo" for="filtro-equipe" id="filtro-equipe-rotulo">Equipe</label>
          <p-select
            inputId="filtro-equipe"
            ariaLabelledBy="filtro-equipe-rotulo"
            name="equipe"
            optionLabel="rotulo"
            optionValue="valor"
            appendTo="body"
            [options]="opcoes"
            [ngModel]="estado().equipeId"
            (ngModelChange)="escolherEquipe($event)"
          />
        </div>
      }
      <p class="periodo-aplicado texto-secundario" aria-live="polite">
        {{ textoPeriodo() }}
      </p>
    </form>
  `,
})
export class FiltrosPainelComponent implements OnChanges {
  readonly estado = input.required<EstadoPainel>();
  /** Nulo = o filtro de equipe não aparece (usuário com uma equipe só). */
  readonly opcoesEquipe = input<OpcaoEquipe[] | null>(null);

  readonly mudou = output<EstadoPainel>();

  readonly atalhos = ATALHOS;
  readonly personalizado = signal(false);
  de: Date | null = null;
  ate: Date | null = null;

  ngOnChanges(): void {
    this.personalizado.set(this.estado().atalho === 'personalizado');
    this.de = dataDeTextoIso(this.estado().de);
    this.ate = dataDeTextoIso(this.estado().ate);
  }

  escolherAtalho(atalho: AtalhoPeriodo): void {
    if (atalho === 'personalizado') {
      // As datas atuais ficam como ponto de partida; o período só muda ao aplicar.
      this.personalizado.set(true);
      return;
    }
    this.personalizado.set(false);
    this.mudou.emit({ ...this.estado(), atalho, ...calcularPeriodo(atalho, new Date()) });
  }

  escolherEquipe(equipeId: string | null): void {
    this.mudou.emit({ ...this.estado(), equipeId });
  }

  aplicarPersonalizado(): void {
    if (this.de && this.ate && this.de <= this.ate) {
      this.mudou.emit({
        ...this.estado(),
        atalho: 'personalizado',
        de: dataParaTextoIso(this.de),
        ate: dataParaTextoIso(this.ate),
      });
    }
  }

  textoPeriodo(): string {
    const { de, ate } = this.estado();
    const formatar = (texto: string) => texto.split('-').reverse().join('/');
    return `De ${formatar(de)} a ${formatar(ate)}`;
  }
}
