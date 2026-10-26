import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { EstadoVazioComponent } from '../../shared/estados/estado-vazio.component';

/** Página de "não encontrado": rota desconhecida ou entidade que não existe nos metadados. */
@Component({
  selector: 'app-nao-encontrado',
  imports: [RouterLink, EstadoVazioComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h1 class="sr-only">Página não encontrada</h1>
    <app-estado-vazio
      icone="pi-map"
      titulo="Página não encontrada"
      texto="O endereço não existe, ou o que você procura não está disponível para você."
    >
      <a class="p-button link-acao" routerLink="/">Ir para o Início</a>
    </app-estado-vazio>
  `,
})
export class NaoEncontradoComponent {}
