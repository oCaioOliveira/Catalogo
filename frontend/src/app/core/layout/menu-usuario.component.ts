import { ChangeDetectionStrategy, Component, computed, inject, viewChild } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { Popover } from 'primeng/popover';
import { Tooltip } from 'primeng/tooltip';
import { OPCOES_TEMA, PreferenciaTema } from '../../tema/preferencia-tema';
import { TemaService } from '../../tema/tema.service';
import { AuthService } from '../auth/auth.service';
import { NOMES_PERFIL } from '../auth/usuario';
import { iniciais } from './navegacao';

/**
 * O menu do usuário no canto do cabeçalho: o avatar com as iniciais abre Minha conta, Trocar senha, o Tema (claro, escuro ou
 * seguir o sistema, num grupo de rádio) e Sair. É um popover (não um p-menu): o tema precisa de rádios de verdade, que o leitor de
 * tela anuncia com o estado.
 */
@Component({
  selector: 'app-menu-usuario',
  imports: [RouterLink, Popover, Tooltip],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (auth.usuario(); as usuario) {
      <button
        type="button"
        class="botao-avatar"
        aria-haspopup="dialog"
        [attr.aria-expanded]="popover()?.overlayVisible ?? false"
        aria-controls="menu-usuario"
        [attr.aria-label]="'Menu do usuário: ' + usuario.nome"
        pTooltip="Sua conta e preferências"
        tooltipPosition="bottom"
        (click)="menu.toggle($event)"
      >
        <span class="avatar" aria-hidden="true">{{ siglas() }}</span>
      </button>
      <p-popover #menu styleClass="popover-usuario">
        <div id="menu-usuario" class="menu-usuario" role="dialog" aria-label="Menu do usuário">
          <div class="menu-usuario-identidade">
            <span class="avatar avatar-grande" aria-hidden="true">{{ siglas() }}</span>
            <div>
              <strong>{{ usuario.nome }}</strong>
              <small>{{ usuario.matricula }} · {{ nomePerfil() }}</small>
            </div>
          </div>
          <ul class="menu-usuario-acoes">
            <li>
              <a routerLink="/minha-conta" (click)="menu.hide()"
                ><i class="pi pi-user" aria-hidden="true"></i> Minha conta</a
              >
            </li>
            <li>
              <button type="button" (click)="menu.hide(); trocarSenha()">
                <i class="pi pi-key" aria-hidden="true"></i> Trocar senha
              </button>
            </li>
          </ul>
          <fieldset class="escolha-tema">
            <legend>Tema</legend>
            @for (opcao of opcoesTema; track opcao.valor) {
              <label class="opcao-tema" [class.escolhida]="tema.preferencia() === opcao.valor">
                <input
                  type="radio"
                  name="tema"
                  [value]="opcao.valor"
                  [checked]="tema.preferencia() === opcao.valor"
                  (change)="escolherTema(opcao.valor)"
                />
                <i [class]="opcao.icone" aria-hidden="true"></i>
                {{ opcao.rotulo }}
              </label>
            }
          </fieldset>
          <button type="button" class="botao-sair" (click)="auth.sair()">
            <i class="pi pi-sign-out" aria-hidden="true"></i> Sair
          </button>
        </div>
      </p-popover>
    }
  `,
})
export class MenuUsuarioComponent {
  readonly auth = inject(AuthService);
  readonly tema = inject(TemaService);
  private readonly router = inject(Router);
  readonly popover = viewChild<Popover>('menu');

  readonly opcoesTema = OPCOES_TEMA;
  readonly siglas = computed(() => iniciais(this.auth.usuario()?.nome));
  readonly nomePerfil = computed(() => {
    const usuario = this.auth.usuario();
    return usuario ? NOMES_PERFIL[usuario.perfil] : '';
  });

  escolherTema(preferencia: PreferenciaTema): void {
    this.tema.escolher(preferencia);
  }

  trocarSenha(): void {
    void this.router.navigate(['/trocar-senha'], { queryParams: { returnUrl: this.router.url } });
  }
}
