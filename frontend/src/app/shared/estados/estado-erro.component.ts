import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { Button } from 'primeng/button';

/**
 * O estado de ERRO padrão (docs/design/guia-de-interface.md): uma mensagem humana (o que não deu certo, sem código técnico) e
 * "Tentar de novo". Fica no lugar do conteúdo que falhou; o resto da tela continua funcionando.
 */
@Component({
  selector: 'app-estado-erro',
  imports: [Button],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="estado-erro" role="alert" [class.compacto]="compacto()">
      <i class="pi pi-exclamation-circle" aria-hidden="true"></i>
      <div class="estado-erro-texto">
        <p class="estado-titulo">{{ mensagem() }}</p>
        <p class="estado-texto">Verifique a conexão e tente de novo. Se continuar, avise o suporte.</p>
      </div>
      <p-button
        label="Tentar de novo"
        icon="pi pi-refresh"
        size="small"
        [outlined]="true"
        (onClick)="tentarDeNovo.emit()"
      />
    </div>
  `,
})
export class EstadoErroComponent {
  readonly mensagem = input('Não foi possível carregar.');
  readonly compacto = input(false);
  readonly tentarDeNovo = output<void>();
}
