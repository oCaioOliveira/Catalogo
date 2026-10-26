import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  input,
  signal,
  untracked,
  viewChild,
} from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { Button } from 'primeng/button';
import { Skeleton } from 'primeng/skeleton';
import { Tab, TabList, TabPanel, TabPanels, Tabs } from 'primeng/tabs';
import { Tag } from 'primeng/tag';
import { MudancaDto } from '../../../api/models';
import { AdministracaoService } from '../../../core/administracao/administracao.service';
import { UsuarioAdmin } from '../../../core/administracao/modelo-administracao';
import { NOMES_PERFIL } from '../../../core/auth/usuario';
import { formatarDataHoraSistema } from '../../../shared/formatacao/formatar-valor';
import { CarregarHistorico, HistoricoComponent } from '../../../shared/historico/historico.component';
import { AcoesAdministracaoService } from '../comum/acoes-usuario.service';
import { severidadePerfil } from '../comum/apresentacao';
import { nomeCampoAdministracao, valorAdministracao } from '../comum/historico-administracao';
import { SenhaProvisoriaDialogComponent } from '../comum/senha-provisoria-dialog.component';
import { DESCRICOES_PERFIL } from '../modelo/perfis';
import { situacaoDoUsuario } from '../modelo/situacao';
import { CabecalhoPaginaComponent } from '../../../shared/pagina/cabecalho-pagina.component';

/** /configuracoes/usuarios/:id — dados, situação, equipes (com link) e o Histórico (sem nenhum valor de senha, RN32). */
@Component({
  selector: 'app-detalhe-usuario',
  imports: [
    CabecalhoPaginaComponent,
    RouterLink,
    Button,
    Skeleton,
    Tab,
    TabList,
    TabPanel,
    TabPanels,
    Tabs,
    Tag,
    HistoricoComponent,
    SenhaProvisoriaDialogComponent,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './detalhe-usuario.component.html',
})
export class DetalheUsuarioComponent {
  readonly id = input.required<string>();

  private readonly administracao = inject(AdministracaoService);
  private readonly acoes = inject(AcoesAdministracaoService);
  private readonly router = inject(Router);
  private readonly dialogoSenha = viewChild.required(SenhaProvisoriaDialogComponent);

  readonly usuario = signal<UsuarioAdmin | null>(null);
  readonly nomesDasEquipes = signal<ReadonlyMap<string, string>>(new Map());
  /** Muda depois de cada ação, para o histórico recarregar. */
  private readonly versao = signal(0);

  readonly situacao = computed(() => {
    const usuario = this.usuario();
    return usuario ? situacaoDoUsuario(usuario, new Date()) : null;
  });
  readonly carregarHistorico = computed<CarregarHistorico>(() => {
    const id = this.id();
    this.versao();
    return (pagina, tamanho) => this.administracao.historicoUsuario(id, pagina, tamanho);
  });
  readonly nomeCampo = nomeCampoAdministracao;
  readonly valor = (mudanca: MudancaDto, lado: 'de' | 'para') =>
    valorAdministracao(mudanca, lado, this.nomesDasEquipes());

  readonly nomesPerfil = NOMES_PERFIL;
  readonly descricoesPerfil = DESCRICOES_PERFIL;
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
      const [usuario, equipes] = await Promise.all([
        this.administracao.obterUsuario(id),
        this.administracao.todasAsEquipes(),
      ]);
      this.usuario.set(usuario);
      this.nomesDasEquipes.set(new Map(equipes.map((e) => [e.id, e.nome])));
    } catch {
      void this.router.navigate(['/configuracoes/usuarios']);
    }
  }

  async redefinirSenha(): Promise<void> {
    const usuario = this.usuario();
    const senha = usuario ? await this.acoes.redefinirSenha(usuario) : null;
    if (usuario && senha) {
      this.dialogoSenha().mostrar(senha, `${usuario.nome} (${usuario.matricula})`);
      this.versao.update((v) => v + 1);
      void this.carregar(usuario.id);
    }
  }

  async alterarAtivacao(): Promise<void> {
    const usuario = this.usuario();
    const atualizado = usuario ? await this.acoes.alterarAtivacao(usuario, !usuario.ativo) : null;
    if (atualizado) {
      this.usuario.set(atualizado);
      this.versao.update((v) => v + 1);
    }
  }
}
