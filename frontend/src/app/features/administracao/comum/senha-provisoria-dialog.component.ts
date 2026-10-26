import { ChangeDetectionStrategy, Component, output, signal } from '@angular/core';
import { Button } from 'primeng/button';
import { Dialog } from 'primeng/dialog';
import { Message } from 'primeng/message';

/**
 * Mostra a senha PROVISÓRIA (criação de usuário e redefinição de senha) UMA vez (RN25, ADR 0041):
 *   - a senha fica só num signal LOCAL deste componente, desde `mostrar()` até "Já copiei"; ao fechar, é descartada;
 *   - nunca vai para estado global, localStorage, sessionStorage, URL ou log;
 *   - o diálogo não fecha clicando fora nem com Esc: só pelo botão, depois de o administrador copiar.
 */
@Component({
  selector: 'app-senha-provisoria-dialog',
  imports: [Button, Dialog, Message],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <p-dialog
      header="Senha provisória"
      [modal]="true"
      [closable]="false"
      [closeOnEscape]="false"
      [dismissableMask]="false"
      [draggable]="false"
      [resizable]="false"
      [visible]="senha() !== null"
      [style]="{ width: 'min(34rem, 95vw)' }"
    >
      @if (senha(); as valor) {
        <p>
          Senha provisória de <strong>{{ destinatario() }}</strong
          >:
        </p>
        <div class="senha-provisoria">
          <code class="valor-senha" data-senha-provisoria aria-label="Senha provisória">{{ valor }}</code>
          <p-button label="Copiar" icon="pi pi-copy" severity="secondary" [outlined]="true" (onClick)="copiar(valor)" />
        </div>
        <p class="texto-sucesso" role="status">{{ copiada() ? 'Senha copiada.' : '' }}</p>
        <p-message severity="warn">
          Esta senha não será exibida novamente. Envie ao usuário por um canal seguro. Ele deverá trocá-la no primeiro
          acesso.
        </p-message>
      }
      <ng-template #footer>
        <p-button label="Já copiei" icon="pi pi-check" (onClick)="fechar()" />
      </ng-template>
    </p-dialog>
  `,
})
export class SenhaProvisoriaDialogComponent {
  /** Avisa quem abriu que o diálogo fechou (a senha já foi descartada). */
  readonly fechado = output<void>();

  readonly senha = signal<string | null>(null);
  readonly destinatario = signal('');
  readonly copiada = signal(false);

  mostrar(senha: string, destinatario: string): void {
    this.copiada.set(false);
    this.destinatario.set(destinatario);
    this.senha.set(senha);
  }

  async copiar(valor: string): Promise<void> {
    try {
      await navigator.clipboard.writeText(valor);
      this.copiada.set(true);
    } catch {
      // Sem acesso à área de transferência (ex.: http fora de localhost): o administrador seleciona e copia o texto.
      this.copiada.set(false);
    }
  }

  /** "Já copiei": descarta a senha (nada a guarda depois daqui) e fecha. */
  fechar(): void {
    this.senha.set(null);
    this.destinatario.set('');
    this.copiada.set(false);
    this.fechado.emit();
  }
}
