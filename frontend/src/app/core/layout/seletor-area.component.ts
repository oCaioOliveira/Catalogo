import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { Select } from 'primeng/select';
import { AreaAtualService } from '../metadados/area-atual.service';
import { LIMITE_BUSCA_AREAS, destinoAoTrocarArea } from './navegacao';

/**
 * O seletor de ÁREA DE GESTÃO do cabeçalho, sempre visível (mesmo com o menu recolhido). Com uma área só, mostra só o nome; com
 * mais de 5, a lista ganha busca. Trocar a área segue `destinoAoTrocarArea`: nas telas de uma área, abre a mesma tela (Início ou
 * painel) da nova; fora delas, só muda a área atual (AreaAtualService), e a tela aberta a segue.
 */
@Component({
  selector: 'app-seletor-area',
  imports: [FormsModule, Select],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (areas().length === 1) {
      <span class="area-unica">
        <span class="sr-only">Área de gestão:</span>
        <span class="area-unica-nome" [title]="areas()[0].nome">{{ areas()[0].nome }}</span>
      </span>
    } @else if (areas().length > 1) {
      <span id="seletor-area-rotulo" class="sr-only">Área de gestão</span>
      <p-select
        inputId="seletor-area"
        ariaLabelledBy="seletor-area-rotulo"
        styleClass="seletor-area"
        panelStyleClass="painel-seletor-area"
        optionLabel="nome"
        optionValue="chave"
        appendTo="body"
        filterPlaceholder="Buscar área de gestão"
        emptyFilterMessage="Nenhuma área encontrada"
        [filter]="comBusca()"
        [options]="areas()"
        [ngModel]="areaAtual.grupo()"
        (onChange)="trocar($event.value)"
      />
    }
  `,
})
export class SeletorAreaComponent {
  readonly areaAtual = inject(AreaAtualService);
  private readonly router = inject(Router);

  readonly areas = this.areaAtual.ativas;
  readonly comBusca = computed(() => this.areas().length > LIMITE_BUSCA_AREAS);

  /** Só na escolha da pessoa (onChange): o ngModelChange também dispara quando as opções são recarregadas. */
  trocar(grupo: string): void {
    if (!grupo || grupo === this.areaAtual.grupo()) {
      return;
    }
    this.areaAtual.definir(grupo);
    const destino = destinoAoTrocarArea(this.router.url, grupo);
    if (destino) {
      void this.router.navigate(destino);
    }
  }
}
