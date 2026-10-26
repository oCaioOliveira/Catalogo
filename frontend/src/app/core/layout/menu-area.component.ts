import { ChangeDetectionStrategy, Component, computed, inject, input, output, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { NavigationEnd, Router, RouterLink, RouterLinkActive } from '@angular/router';
import { Button } from 'primeng/button';
import { Skeleton } from 'primeng/skeleton';
import { Tooltip } from 'primeng/tooltip';
import { filter, map } from 'rxjs';
import { Entidade } from '../metadados/modelo';
import { classeIcone, montarMenu, nomePlural } from '../metadados/regras-metadados';
import { PermissoesService } from '../permissoes/permissoes.service';
import { gravarPreferenciaLocal, lerBooleanoLocal } from '../../shared/preferencias/preferencia-local';
import { LIMITE_BUSCA_MENU, filtrarPorNome } from './navegacao';

const CHAVE_APOIO_ABERTO = 'catalogo.menu.apoioAberto';

/** Um item do menu: o tipo de item com o nome no plural (o que aparece e o que a busca compara). */
interface ItemMenu {
  entidade: Entidade;
  nome: string;
}

/**
 * O menu lateral de uma ÁREA DE GESTÃO: só o trabalho do dia a dia. Início; Painel (GESTOR e ADMINISTRADOR, RN78); os
 * tipos de item principais (por equipe e não filhos), na ordem dos metadados e com o ícone de cada um (RN95); e os Cadastros de
 * apoio num grupo recolhível, RECOLHIDO por padrão (o estado fica no localStorage; aberto também quando a tela atual é um deles).
 * Com mais de 8 tipos de item, uma busca. Recolhido (só ícones), os nomes vão para a dica e continuam no nome acessível.
 */
@Component({
  selector: 'app-menu-area',
  imports: [RouterLink, RouterLinkActive, Button, Skeleton, Tooltip],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './menu-area.component.html',
})
export class MenuAreaComponent {
  readonly grupo = input.required<string | null>();
  /** null = carregando. */
  readonly entidades = input.required<Entidade[] | null>();
  readonly erro = input(false);
  readonly recolhido = input(false);
  readonly navegou = output<void>();
  readonly tentarDeNovo = output<void>();

  readonly permissoes = inject(PermissoesService);
  private readonly router = inject(Router);

  readonly busca = signal('');
  readonly apoioAberto = signal(lerBooleanoLocal(CHAVE_APOIO_ABERTO, false));
  private readonly url = toSignal(
    this.router.events.pipe(
      filter((e) => e instanceof NavigationEnd),
      map(() => this.router.url),
    ),
    { initialValue: this.router.url },
  );

  readonly classeIcone = classeIcone;
  private readonly menu = computed(() => montarMenu(this.entidades() ?? []));
  private readonly todos = computed(() => this.menu().principais.length + this.menu().apoio.length);
  readonly comBusca = computed(() => !this.recolhido() && this.todos() > LIMITE_BUSCA_MENU);
  readonly principais = computed(() => filtrarPorNome(this.itens(this.menu().principais), this.busca()));
  readonly apoio = computed(() => filtrarPorNome(this.itens(this.menu().apoio), this.busca()));
  /** A tela atual é um cadastro de apoio: o grupo aparece aberto (sem mudar a preferência guardada). */
  private readonly emApoio = computed(() => {
    const segmentos = this.url().split(/[?#]/)[0].split('/');
    return segmentos[1] === this.grupo() && this.menu().apoio.some((e) => e.chave === segmentos[2]);
  });
  readonly apoioVisivel = computed(() => this.apoioAberto() || this.emApoio() || this.busca().trim().length > 0);

  private itens(entidades: Entidade[]): ItemMenu[] {
    return entidades.map((entidade) => ({ entidade, nome: nomePlural(entidade) }));
  }

  alternarApoio(): void {
    const aberto = !this.apoioVisivel();
    this.apoioAberto.set(aberto);
    gravarPreferenciaLocal(CHAVE_APOIO_ABERTO, String(aberto));
  }
}
