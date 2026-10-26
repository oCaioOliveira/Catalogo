import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { LOGO_CAIXA, NOME_MARCA, larguraProporcional, logoDoTema } from './marca';
import { TemaService } from './tema.service';

/**
 * A logo da CAIXA direto sobre o fundo: a versão positiva no tema claro e a negativa no escuro, trocadas na hora em que o tema
 * muda (inclusive "seguir o sistema", pelo TemaService). O TemaService lê a escolha antes da primeira tela, então a primeira
 * imagem já é a do tema certo (sem piscar). width/height vão na <img> (proporção do arquivo) para a página não "pular".
 */
@Component({
  selector: 'app-logo-marca',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <img class="logo-marca" [src]="arquivo()" [alt]="nome" [width]="largura()" [height]="altura()" decoding="async" />
  `,
})
export class LogoMarcaComponent {
  private readonly tema = inject(TemaService);

  /** Altura da imagem, em pixels. */
  readonly altura = input(24);

  readonly nome = NOME_MARCA;
  readonly arquivo = computed(() => logoDoTema(LOGO_CAIXA, this.tema.aplicado()));
  readonly largura = computed(() => larguraProporcional(LOGO_CAIXA, this.altura()));
}
