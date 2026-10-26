import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { SelectButton } from 'primeng/selectbutton';
import { Densidade } from './densidade';
import { DensidadeService } from './densidade.service';

/** Escolha da densidade das tabelas: "Compacto" (padrão) ou "Confortável", salva para as próximas visitas. */
@Component({
  selector: 'app-alternar-densidade',
  imports: [FormsModule, SelectButton],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <p-selectbutton
      size="small"
      optionLabel="rotulo"
      optionValue="valor"
      ariaLabelledBy="rotulo-densidade"
      [allowEmpty]="false"
      [options]="opcoes"
      [ngModel]="servico.densidade()"
      (ngModelChange)="escolher($event)"
    />
    <span id="rotulo-densidade" class="sr-only">Densidade da tabela</span>
  `,
})
export class AlternarDensidadeComponent {
  readonly servico = inject(DensidadeService);

  readonly opcoes: { rotulo: string; valor: Densidade }[] = [
    { rotulo: 'Compacto', valor: 'compacto' },
    { rotulo: 'Confortável', valor: 'confortavel' },
  ];

  escolher(densidade: Densidade): void {
    this.servico.definir(densidade);
  }
}
