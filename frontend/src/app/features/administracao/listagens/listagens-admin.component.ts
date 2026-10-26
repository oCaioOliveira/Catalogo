import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { DialogoAlteracaoComponent } from './comum/dialogo-alteracao.component';

/** Moldura das telas de administração de listagens: as páginas e o diálogo único das alterações (simulação, ADR 0044). */
@Component({
  selector: 'app-listagens-admin',
  imports: [RouterOutlet, DialogoAlteracaoComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<router-outlet /><app-dialogo-alteracao />`,
})
export class ListagensAdminComponent {}
