import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  computed,
  effect,
  inject,
  signal,
  untracked,
} from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { NavigationEnd, Router, RouterLink, RouterOutlet } from '@angular/router';
import { Badge } from 'primeng/badge';
import { Button } from 'primeng/button';
import { ConfirmDialog } from 'primeng/confirmdialog';
import { Tooltip } from 'primeng/tooltip';
import { filter, map } from 'rxjs';
import { BotaoTemaComponent } from '../../tema/botao-tema.component';
import { LogoMarcaComponent } from '../../tema/logo-marca.component';
import { gravarPreferenciaLocal, lerBooleanoLocal } from '../../shared/preferencias/preferencia-local';
import { PendentesService } from '../aprovacao/pendentes.service';
import { AreaAtualService } from '../metadados/area-atual.service';
import { MetadadosService } from '../metadados/metadados.service';
import { Entidade } from '../metadados/modelo';
import { PermissoesService } from '../permissoes/permissoes.service';
import { MenuAreaComponent } from './menu-area.component';
import { MenuConfiguracoesComponent } from './menu-configuracoes.component';
import { MenuUsuarioComponent } from './menu-usuario.component';
import { LARGURA_GAVETA, ehAreaDeConfiguracoes } from './navegacao';
import { SeletorAreaComponent } from './seletor-area.component';

const CHAVE_MENU_RECOLHIDO = 'catalogo.menu.recolhido';

/**
 * A moldura das telas logadas, no padrão de sistemas como Jira e GitHub:
 *
 *   - CABEÇALHO: à esquerda, o botão do menu, a logo da CAIXA com o nome do sistema (leva ao Início da área atual) e o seletor de
 *     ÁREA DE GESTÃO (sempre visível); à direita, Aprovações (caixa de entrada com o contador; GESTOR e ADMINISTRADOR, só se
 *     alguma área tem a aprovação ativa), a engrenagem das Configurações (GESTOR e ADMINISTRADOR), o atalho de tema e o menu do
 *     usuário (Minha conta, Tema, Sair).
 *   - MENU LATERAL: o da área atual (Início, Painel, tipos de item, Cadastros de apoio) ou, em /configuracoes/..., o da área de
 *     configurações, com "Voltar para <área>". Recolhido, vira uma barra só de ícones (o estado fica no localStorage). Abaixo de
 *     1024 px, vira uma gaveta por cima do conteúdo, que fecha ao navegar.
 *
 * A área atual vem do AreaAtualService. O menu recarrega quando a área muda ou a administração altera os metadados
 * (MetadadosService.versao).
 */
@Component({
  selector: 'app-layout',
  imports: [
    RouterOutlet,
    RouterLink,
    Badge,
    Button,
    ConfirmDialog,
    Tooltip,
    MenuAreaComponent,
    MenuConfiguracoesComponent,
    MenuUsuarioComponent,
    SeletorAreaComponent,
    LogoMarcaComponent,
    BotaoTemaComponent,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './layout.component.html',
  host: { '(document:keydown.escape)': 'fecharGaveta()' },
})
export class LayoutComponent {
  readonly permissoes = inject(PermissoesService);
  readonly pendentes = inject(PendentesService);
  readonly areaAtual = inject(AreaAtualService);
  private readonly metadados = inject(MetadadosService);
  private readonly router = inject(Router);

  readonly entidades = signal<Entidade[] | null>(null);
  readonly erroMenu = signal(false);
  /** Alguma área visível tem a aprovação ativa num tipo de item (só então o atalho de Aprovações aparece). */
  readonly algumaAprovacao = signal(false);

  /** Telas largas: o menu fica ao lado, aberto ou recolhido (só ícones). */
  readonly recolhido = signal(lerBooleanoLocal(CHAVE_MENU_RECOLHIDO, false));
  /** Telas estreitas: o menu é uma gaveta, fechada por padrão. */
  readonly telaEstreita = signal(false);
  readonly gavetaAberta = signal(false);

  private readonly url = toSignal(
    this.router.events.pipe(
      filter((e) => e instanceof NavigationEnd),
      map(() => this.router.url),
    ),
    { initialValue: this.router.url },
  );
  readonly emConfiguracoes = computed(() => ehAreaDeConfiguracoes(this.url()));
  /** A logo leva ao Início da área atual (sem área ainda: a tela inicial calculada). */
  readonly inicio = computed(() => {
    const grupo = this.areaAtual.grupo();
    return grupo ? ['/', grupo] : ['/'];
  });
  readonly mostrarAprovacoes = computed(() => this.permissoes.podeVerAprovacoes() && this.algumaAprovacao());
  /** O menu aparece só com ícones (telas largas, recolhido). */
  readonly soIcones = computed(() => !this.telaEstreita() && this.recolhido());
  readonly menuVisivel = computed(() => !this.telaEstreita() || this.gavetaAberta());
  readonly rotuloBotaoMenu = computed(() => {
    if (this.telaEstreita()) {
      return this.gavetaAberta() ? 'Fechar menu' : 'Abrir menu';
    }
    return this.recolhido() ? 'Expandir menu' : 'Recolher menu';
  });

  constructor() {
    // GESTOR e ADMINISTRADOR: o contador de "Aprovações", atualizado ao navegar e a cada 2 minutos (ADR 0045).
    if (this.permissoes.podeVerAprovacoes()) {
      this.pendentes.iniciar();
    }
    // Troca de área ou metadados alterados na administração: recarrega as áreas e o menu.
    effect(() => {
      this.metadados.versao();
      const grupo = this.areaAtual.grupo();
      untracked(() => void this.carregarMenu(grupo));
    });
    this.observarLargura();
  }

  async carregarMenu(grupo = this.areaAtual.grupo()): Promise<void> {
    this.erroMenu.set(false);
    try {
      const areas = await this.metadados.listagens();
      this.areaAtual.atualizarAreas(areas);
      void this.verificarAprovacoes();
      const atual = this.areaAtual.grupo() ?? grupo;
      this.entidades.set(atual ? await this.metadados.entidades(atual) : []);
    } catch {
      this.erroMenu.set(true);
    }
  }

  /** O atalho de Aprovações só faz sentido se alguma área visível tem a aprovação ligada (os metadados já ficam em memória). */
  private async verificarAprovacoes(): Promise<void> {
    if (!this.permissoes.podeVerAprovacoes()) {
      return;
    }
    const porArea = await Promise.all(
      this.areaAtual.ativas().map((a) => this.metadados.entidades(a.chave).catch(() => [] as Entidade[])),
    );
    this.algumaAprovacao.set(porArea.some((entidades) => entidades.some((e) => e.equipesComAprovacao.length > 0)));
  }

  alternarMenu(): void {
    if (this.telaEstreita()) {
      this.gavetaAberta.update((aberta) => !aberta);
      return;
    }
    const recolhido = !this.recolhido();
    this.recolhido.set(recolhido);
    gravarPreferenciaLocal(CHAVE_MENU_RECOLHIDO, String(recolhido));
  }

  /** Na gaveta (telas estreitas), escolher um item fecha o menu. */
  aoNavegar(): void {
    if (this.telaEstreita()) {
      this.gavetaAberta.set(false);
    }
  }

  /** Esc fecha a gaveta (telas estreitas). */
  fecharGaveta(): void {
    if (this.telaEstreita() && this.gavetaAberta()) {
      this.gavetaAberta.set(false);
    }
  }

  private observarLargura(): void {
    const consulta = window.matchMedia(`(max-width: ${LARGURA_GAVETA - 0.02}px)`);
    const atualizar = () => {
      this.telaEstreita.set(consulta.matches);
      this.gavetaAberta.set(false);
    };
    atualizar();
    consulta.addEventListener('change', atualizar);
    inject(DestroyRef).onDestroy(() => consulta.removeEventListener('change', atualizar));
  }
}
