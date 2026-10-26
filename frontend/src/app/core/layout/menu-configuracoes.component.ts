import { ChangeDetectionStrategy, Component, computed, inject, input, output } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { Tooltip } from 'primeng/tooltip';
import { AreaAtualService } from '../metadados/area-atual.service';
import { PermissoesService } from '../permissoes/permissoes.service';

/**
 * O menu lateral da ÁREA DE CONFIGURAÇÕES (/configuracoes/...), que substitui o menu da área de gestão enquanto a pessoa
 * está nela: no topo, "Voltar para <área atual>"; Configurações da equipe (GESTOR e ADMINISTRADOR); Usuários, Equipes e Áreas de
 * gestão (só ADMINISTRADOR, RN26).
 */
@Component({
  selector: 'app-menu-configuracoes',
  imports: [RouterLink, RouterLinkActive, Tooltip],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <a
      class="item-menu voltar-menu"
      [routerLink]="voltarPara()"
      [pTooltip]="recolhido() ? textoVoltar() : undefined"
      tooltipPosition="right"
      (click)="navegou.emit()"
      ><i class="pi pi-arrow-left" aria-hidden="true"></i><span class="rotulo-item">{{ textoVoltar() }}</span></a
    >
    <p class="titulo-menu" [class.sr-only]="recolhido()">Configurações</p>
    <ul class="lista-menu">
      @if (permissoes.podeConfigurarEquipe()) {
        <li>
          <a
            class="item-menu"
            routerLink="/configuracoes/equipe"
            routerLinkActive="ativo"
            ariaCurrentWhenActive="page"
            [pTooltip]="recolhido() ? 'Configurações da equipe' : undefined"
            tooltipPosition="right"
            (click)="navegou.emit()"
            ><i class="pi pi-sliders-h" aria-hidden="true"></i
            ><span class="rotulo-item">Configurações da equipe</span></a
          >
        </li>
      }
      @if (permissoes.podeAdministrar()) {
        <li>
          <a
            class="item-menu"
            routerLink="/configuracoes/usuarios"
            routerLinkActive="ativo"
            ariaCurrentWhenActive="page"
            [pTooltip]="recolhido() ? 'Usuários' : undefined"
            tooltipPosition="right"
            (click)="navegou.emit()"
            ><i class="pi pi-user" aria-hidden="true"></i><span class="rotulo-item">Usuários</span></a
          >
        </li>
        <li>
          <a
            class="item-menu"
            routerLink="/configuracoes/equipes"
            routerLinkActive="ativo"
            ariaCurrentWhenActive="page"
            [pTooltip]="recolhido() ? 'Equipes' : undefined"
            tooltipPosition="right"
            (click)="navegou.emit()"
            ><i class="pi pi-users" aria-hidden="true"></i><span class="rotulo-item">Equipes</span></a
          >
        </li>
        <li>
          <a
            class="item-menu"
            routerLink="/configuracoes/areas"
            routerLinkActive="ativo"
            ariaCurrentWhenActive="page"
            [pTooltip]="recolhido() ? 'Áreas de gestão' : undefined"
            tooltipPosition="right"
            (click)="navegou.emit()"
            ><i class="pi pi-sitemap" aria-hidden="true"></i><span class="rotulo-item">Áreas de gestão</span></a
          >
        </li>
      }
    </ul>
  `,
})
export class MenuConfiguracoesComponent {
  readonly recolhido = input(false);
  readonly navegou = output<void>();

  readonly permissoes = inject(PermissoesService);
  private readonly areaAtual = inject(AreaAtualService);

  readonly voltarPara = computed(() => {
    const grupo = this.areaAtual.grupo();
    return grupo ? ['/', grupo] : ['/'];
  });
  readonly textoVoltar = computed(() => {
    const area = this.areaAtual.area();
    return area ? `Voltar para ${area.nome}` : 'Voltar';
  });
}
