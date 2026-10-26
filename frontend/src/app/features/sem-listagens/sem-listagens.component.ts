import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { EstadoVazioComponent } from '../../shared/estados/estado-vazio.component';

/**
 * Página de quem não vê nenhuma área de gestão (RN90): nenhuma está habilitada para as equipes dele. Quem habilita é o
 * administrador (Configurações → Áreas de gestão → aba Equipes). Minha conta continua disponível pelo menu do usuário.
 */
@Component({
  selector: 'app-sem-listagens',
  imports: [RouterLink, EstadoVazioComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h1 class="sr-only">Nenhuma área de gestão disponível</h1>
    <app-estado-vazio
      icone="pi-sitemap"
      titulo="Nenhuma área de gestão disponível para as suas equipes"
      texto="Fale com o administrador do sistema: é ele quem habilita as áreas de gestão para cada equipe."
    >
      <a class="p-button p-button-outlined p-button-secondary link-acao" routerLink="/minha-conta"
        >Ver as minhas equipes</a
      >
    </app-estado-vazio>
  `,
})
export class SemListagensComponent {}
