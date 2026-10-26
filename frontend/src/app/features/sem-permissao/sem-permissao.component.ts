import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { EstadoVazioComponent } from '../../shared/estados/estado-vazio.component';

/**
 * Página de "sem permissão": rota que o perfil do usuário não pode abrir (o painel para INTERNO e EXTERNO, RN78; a
 * administração para quem não é ADMINISTRADOR, RN26). Os guards mandam para cá sem trocar a URL.
 */
@Component({
  selector: 'app-sem-permissao',
  imports: [RouterLink, EstadoVazioComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h1 class="sr-only">Sem permissão</h1>
    <app-estado-vazio
      icone="pi-lock"
      titulo="O seu perfil não tem acesso a esta página"
      texto="O painel, as aprovações e as configurações da equipe são para gestores e administradores; usuários, equipes e áreas de gestão, só para administradores."
    >
      <a class="p-button link-acao" routerLink="/">Ir para o Início</a>
    </app-estado-vazio>
  `,
})
export class SemPermissaoComponent {}
