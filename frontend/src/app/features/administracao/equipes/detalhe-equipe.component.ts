import { ChangeDetectionStrategy, Component, computed, effect, inject, input, signal, untracked } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { Button } from 'primeng/button';
import { Skeleton } from 'primeng/skeleton';
import { TableModule } from 'primeng/table';
import { Tab, TabList, TabPanel, TabPanels, Tabs } from 'primeng/tabs';
import { Tag } from 'primeng/tag';
import { MudancaDto } from '../../../api/models';
import { AdministracaoService } from '../../../core/administracao/administracao.service';
import { EquipeAdmin, UsuarioAdmin } from '../../../core/administracao/modelo-administracao';
import { NOMES_PERFIL } from '../../../core/auth/usuario';
import { formatarDataHoraSistema } from '../../../shared/formatacao/formatar-valor';
import { CarregarHistorico, HistoricoComponent } from '../../../shared/historico/historico.component';
import { AcoesAdministracaoService } from '../comum/acoes-usuario.service';
import { severidadePerfil } from '../comum/apresentacao';
import { nomeCampoAdministracao, valorAdministracao } from '../comum/historico-administracao';
import { membrosSemOutraEquipeAtiva } from '../modelo/regras-administracao';
import { situacaoDoUsuario } from '../modelo/situacao';
import { DensidadeService } from '../../../shared/tabelas/densidade.service';
import { CabecalhoPaginaComponent } from '../../../shared/pagina/cabecalho-pagina.component';

/**
 * /configuracoes/equipes/:id — dados, a tabela de MEMBROS (os usuários da equipe, com link) e o Histórico. Os membros que só têm esta
 * equipe ativa aparecem marcados: enquanto houver algum, a equipe não pode ser desativada (RN31).
 */
@Component({
  selector: 'app-detalhe-equipe',
  imports: [
    CabecalhoPaginaComponent,
    RouterLink,
    Button,
    Skeleton,
    TableModule,
    Tab,
    TabList,
    TabPanel,
    TabPanels,
    Tabs,
    Tag,
    HistoricoComponent,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './detalhe-equipe.component.html',
})
export class DetalheEquipeComponent {
  readonly densidade = inject(DensidadeService);
  readonly id = input.required<string>();

  private readonly administracao = inject(AdministracaoService);
  private readonly acoes = inject(AcoesAdministracaoService);
  private readonly router = inject(Router);

  readonly equipe = signal<EquipeAdmin | null>(null);
  readonly membros = signal<UsuarioAdmin[] | null>(null);
  private readonly versao = signal(0);

  readonly soEstaEquipe = computed(
    () => new Set(membrosSemOutraEquipeAtiva(this.membros() ?? [], this.id()).map((u) => u.id)),
  );
  readonly carregarHistorico = computed<CarregarHistorico>(() => {
    const id = this.id();
    this.versao();
    return (pagina, tamanho) => this.administracao.historicoEquipe(id, pagina, tamanho);
  });
  readonly nomeCampo = nomeCampoAdministracao;
  readonly valor = (mudanca: MudancaDto, lado: 'de' | 'para') => valorAdministracao(mudanca, lado, new Map());

  nomePerfil(usuario: UsuarioAdmin): string {
    return NOMES_PERFIL[usuario.perfil];
  }
  readonly severidadePerfil = severidadePerfil;
  readonly formatarDataHoraSistema = formatarDataHoraSistema;

  constructor() {
    effect(() => {
      const id = this.id();
      untracked(() => void this.carregar(id));
    });
  }

  private async carregar(id: string): Promise<void> {
    try {
      const [equipe, membros] = await Promise.all([
        this.administracao.obterEquipe(id),
        this.administracao.membrosDaEquipe(id),
      ]);
      this.equipe.set(equipe);
      this.membros.set(membros);
    } catch {
      void this.router.navigate(['/configuracoes/equipes']);
    }
  }

  situacao(usuario: UsuarioAdmin) {
    return situacaoDoUsuario(usuario, new Date());
  }

  outrasEquipes(usuario: UsuarioAdmin): string {
    const outras = usuario.equipes
      .filter((e) => e.id !== this.id())
      .map((e) => (e.ativa ? e.nome : `${e.nome} (inativa)`));
    return outras.length > 0 ? outras.join(', ') : '—';
  }

  async alterarAtivacao(): Promise<void> {
    const equipe = this.equipe();
    const atualizada = equipe ? await this.acoes.alterarAtivacaoEquipe(equipe, !equipe.ativa) : null;
    if (atualizada) {
      this.equipe.set(atualizada);
      this.versao.update((v) => v + 1);
    }
  }
}
