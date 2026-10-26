import { ChangeDetectionStrategy, Component, computed, effect, inject, input, signal, untracked } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Button } from 'primeng/button';
import { Message } from 'primeng/message';
import { Skeleton } from 'primeng/skeleton';
import { Tooltip } from 'primeng/tooltip';
import { AprovacaoService } from '../../core/aprovacao/aprovacao.service';
import { AuthService } from '../../core/auth/auth.service';
import { Listagem } from '../../core/metadados/listagens';
import { MetadadosService } from '../../core/metadados/metadados.service';
import { Entidade } from '../../core/metadados/modelo';
import { PermissoesService } from '../../core/permissoes/permissoes.service';
import { RegistrosService } from '../../core/registros/registros.service';
import { EstadoErroComponent } from '../../shared/estados/estado-erro.component';
import { EstadoVazioComponent } from '../../shared/estados/estado-vazio.component';
import { DataRelativaComponent } from '../../shared/formatacao/data-relativa.component';
import { CabecalhoPaginaComponent } from '../../shared/pagina/cabecalho-pagina.component';
import { Atalho, ItemRecente, atalhosDaArea, itensRecentes, saudacao, textoQuantidade } from './montar-inicio';

/**
 * /:grupo — a página INÍCIO de uma área de gestão (o primeiro lugar ao entrar nela): a saudação, os atalhos para os tipos de item
 * principais (ícone, nome, quantidade visível e "Novo" se o usuário pode criar), "Continuar de onde parou" (os últimos itens que ELE
 * criou ou alterou, GET .../eu/recentes, RN96) e, para GESTOR e ADMINISTRADOR, as pendências de aprovação desta área e o atalho para
 * o Painel. Sem itens recentes (primeiro acesso), uma explicação curta da área e por onde começar.
 * Leve: uma chamada de recentes, as quantidades em paralelo (cada uma aparece quando chega) e skeleton no lugar de cada bloco.
 */
@Component({
  selector: 'app-inicio',
  imports: [
    RouterLink,
    Button,
    Message,
    Skeleton,
    Tooltip,
    CabecalhoPaginaComponent,
    DataRelativaComponent,
    EstadoErroComponent,
    EstadoVazioComponent,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './inicio.component.html',
})
export class InicioComponent {
  readonly grupo = input.required<string>();

  private readonly auth = inject(AuthService);
  private readonly metadados = inject(MetadadosService);
  private readonly registros = inject(RegistrosService);
  private readonly aprovacao = inject(AprovacaoService);
  readonly permissoes = inject(PermissoesService);

  readonly area = signal<Listagem | null>(null);
  readonly atalhos = signal<Atalho[] | null>(null);
  readonly quantidades = signal<Record<string, number>>({});
  readonly recentes = signal<ItemRecente[] | null>(null);
  readonly pendentes = signal<number | null>(null);
  readonly erro = signal(false);
  readonly erroRecentes = signal(false);
  readonly agora = signal(new Date());

  readonly saudacao = computed(() => saudacao(this.auth.usuario()?.nome, this.agora().getHours()));
  readonly gestao = computed(() => this.permissoes.podeVerPainel());
  readonly semEquipes = computed(() => {
    const area = this.area();
    return !!area && !area.possuiEquipes && this.permissoes.podeAdministrar();
  });
  readonly textoQuantidade = textoQuantidade;
  readonly linhasEsqueleto = [1, 2, 3, 4];

  constructor() {
    effect(() => {
      const grupo = this.grupo();
      untracked(() => void this.carregar(grupo));
    });
  }

  async carregar(grupo = this.grupo()): Promise<void> {
    this.erro.set(false);
    this.atalhos.set(null);
    this.recentes.set(null);
    this.quantidades.set({});
    this.pendentes.set(null);
    this.agora.set(new Date());
    let entidades: Entidade[];
    try {
      const [area, todas] = await Promise.all([this.metadados.listagem(grupo), this.metadados.entidades(grupo)]);
      entidades = todas;
      this.area.set(area);
      const minhasEquipes = area?.minhasEquipes ?? [];
      this.atalhos.set(atalhosDaArea(todas, (e) => this.permissoes.podeCriar(e, minhasEquipes)));
    } catch {
      this.erro.set(true);
      return;
    }
    void this.carregarRecentes(grupo, entidades);
    for (const atalho of this.atalhos() ?? []) {
      void this.registros.contar(grupo, atalho.entidade.chave).then(
        (n) => this.quantidades.update((q) => ({ ...q, [atalho.entidade.chave]: n })),
        () => undefined,
      );
    }
    // Pendências desta área: só para quem decide e só se algum tipo de item tem a aprovação ligada.
    if (this.permissoes.podeVerAprovacoes() && entidades.some((e) => e.equipesComAprovacao.length > 0)) {
      void this.aprovacao.pendentes({ grupo }, 1, 1).then(
        (pagina) => this.pendentes.set(pagina.total),
        () => undefined,
      );
    }
  }

  async carregarRecentes(grupo = this.grupo(), entidades?: Entidade[]): Promise<void> {
    this.erroRecentes.set(false);
    try {
      const todas = entidades ?? (await this.metadados.entidades(grupo));
      this.recentes.set(itensRecentes(grupo, await this.registros.recentes(grupo), todas));
    } catch {
      this.erroRecentes.set(true);
    }
  }

  quantidade(atalho: Atalho): number | undefined {
    return this.quantidades()[atalho.entidade.chave];
  }
}
