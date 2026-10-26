import { ChangeDetectionStrategy, Component, computed, effect, inject, signal, untracked } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { MessageService } from 'primeng/api';
import { Button } from 'primeng/button';
import { Dialog } from 'primeng/dialog';
import { Message } from 'primeng/message';
import { Select } from 'primeng/select';
import { Skeleton } from 'primeng/skeleton';
import { TableModule } from 'primeng/table';
import { Tag } from 'primeng/tag';
import { ToggleSwitch } from 'primeng/toggleswitch';
import { AprovacaoService } from '../../core/aprovacao/aprovacao.service';
import { AuthService } from '../../core/auth/auth.service';
import { AvaliacaoService } from '../../core/avaliacao/avaliacao.service';
import { ModeloAvaliacao } from '../../core/avaliacao/modelo-avaliacao';
import { tratarNoComponente } from '../../core/erros/contexto-erros';
import { mensagemDoErro } from '../../core/erros/mensagens-erro';
import { lerProblema } from '../../core/erros/problema';
import { AreaAtualService } from '../../core/metadados/area-atual.service';
import { Listagem } from '../../core/metadados/listagens';
import { MetadadosService } from '../../core/metadados/metadados.service';
import { Entidade } from '../../core/metadados/modelo';
import { nomePlural } from '../../core/metadados/regras-metadados';
import { EstadoErroComponent } from '../../shared/estados/estado-erro.component';
import { EstadoVazioComponent } from '../../shared/estados/estado-vazio.component';
import { CabecalhoPaginaComponent } from '../../shared/pagina/cabecalho-pagina.component';
import { DensidadeService } from '../../shared/tabelas/densidade.service';
import { escolhaDaConfiguracao } from './modelo/escolha-configuracao';

/** Uma linha da tabela: o tipo de item, a aprovação na equipe e os modelos de avaliação dele nela. */
interface LinhaConfiguracao {
  entidade: Entidade;
  aprovacao: boolean;
  ativo: ModeloAvaliacao | null;
  /** O inativo mais recente (para reativar), quando não há ativo. */
  inativo: ModeloAvaliacao | null;
}

/**
 * /configuracoes/equipe — a aprovação (RN33) e os modelos de avaliação (RN40) de cada tipo de item POR EQUIPE, numa equipe e numa
 * listagem escolhidas (na URL: ?equipeId=&grupo=). A listagem vem primeiro, e a equipe é uma das do usuário NELA (RN91: o GESTOR,
 * as dele habilitadas nela; o ADMINISTRADOR, todas as habilitadas; modelo/escolha-configuracao.ts). Ligar a
 * aprovação mostra antes o que muda para cada perfil; desligar com pendentes mostra a quantidade e um link para a fila filtrada.
 */
@Component({
  selector: 'app-configuracoes-equipe',
  imports: [
    FormsModule,
    RouterLink,
    Button,
    Dialog,
    Message,
    Select,
    Skeleton,
    TableModule,
    Tag,
    ToggleSwitch,
    CabecalhoPaginaComponent,
    EstadoErroComponent,
    EstadoVazioComponent,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './configuracoes-equipe.component.html',
})
export class ConfiguracoesEquipeComponent {
  private readonly rota = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly auth = inject(AuthService);
  private readonly metadados = inject(MetadadosService);
  private readonly aprovacao = inject(AprovacaoService);
  private readonly avaliacao = inject(AvaliacaoService);
  private readonly mensagens = inject(MessageService);
  private readonly areaAtual = inject(AreaAtualService);
  readonly densidade = inject(DensidadeService);

  private readonly parametros = toSignal(this.rota.queryParamMap);
  readonly equipeId = computed(() => this.parametros()?.get('equipeId') ?? null);
  readonly grupo = computed(() => this.parametros()?.get('grupo') ?? null);

  private readonly todasAsListagens = signal<Listagem[] | null>(null);
  private readonly escolha = computed(() =>
    escolhaDaConfiguracao(this.todasAsListagens() ?? [], this.grupo(), this.equipeId()),
  );
  readonly listagens = computed(() => this.escolha().listagens);
  readonly equipes = computed(() => this.escolha().equipes);
  /** Carregou e não há nenhuma listagem com equipe do usuário para configurar. */
  readonly semOpcoes = computed(() => this.todasAsListagens() !== null && this.listagens().length === 0);
  readonly linhas = signal<LinhaConfiguracao[] | null>(null);
  readonly erro = signal(false);
  readonly alterando = signal<string | null>(null);

  /** Ligar a aprovação: a explicação antes (a entidade da linha). */
  readonly explicando = signal<LinhaConfiguracao | null>(null);
  /** Desligar com pendentes: a mensagem da API e o link para a fila. */
  readonly bloqueioPendentes = signal<{ linha: LinhaConfiguracao; mensagem: string; quantidade: number } | null>(null);

  readonly nomePlural = nomePlural;
  readonly nomeEquipe = computed(() => this.equipes().find((e) => e.id === this.equipeId())?.nome ?? '');

  constructor() {
    void this.carregarOpcoes();
    // A área trocada no seletor do cabeçalho (ou em outra tela) vale aqui também.
    let anterior: string | null | undefined;
    effect(() => {
      const area = this.areaAtual.grupo();
      const primeira = anterior === undefined;
      anterior = area;
      if (!primeira && area && area !== this.grupo()) {
        untracked(() => this.escolher(this.equipeId(), area));
      }
    });
    effect(() => {
      const [equipeId, grupo, escolha] = [this.equipeId(), this.grupo(), this.escolha()];
      // Só carrega o que vale (RN91); o que a URL pede e não vale é corrigido por carregarOpcoes/escolher.
      const valida = this.todasAsListagens() !== null && escolha.grupo === grupo && escolha.equipeId === equipeId;
      untracked(() => void this.carregarLinhas(valida ? equipeId : null, valida ? grupo : null));
    });
  }

  /** As listagens que o usuário vê, com as equipes dele em cada uma (GET /grupos); corrige a URL se ela pede o que não vale. */
  private async carregarOpcoes(): Promise<void> {
    this.todasAsListagens.set(await this.metadados.listagens());
    this.escolher(this.equipeId(), this.grupo() ?? this.areaAtual.grupo());
  }

  /** A pessoa escolheu outra área aqui: ela vira a área atual de todas as telas. */
  trocarArea(grupo: string): void {
    this.areaAtual.definir(grupo);
    this.escolher(this.equipeId(), grupo);
  }

  /** Escolher outra listagem mantém a equipe se ela estiver habilitada nela; senão, vai a primeira equipe do usuário nela. */
  escolher(equipeId: string | null, grupo: string | null): void {
    const escolha = escolhaDaConfiguracao(this.todasAsListagens() ?? [], grupo, equipeId);
    if (escolha.equipeId === this.equipeId() && escolha.grupo === this.grupo()) {
      return;
    }
    void this.router.navigate([], {
      relativeTo: this.rota,
      queryParams: { equipeId: escolha.equipeId ?? undefined, grupo: escolha.grupo ?? undefined },
      replaceUrl: true,
    });
  }

  recarregar(): void {
    void this.carregarLinhas(this.equipeId(), this.grupo());
  }

  private async carregarLinhas(equipeId: string | null, grupo: string | null): Promise<void> {
    this.linhas.set(null);
    this.erro.set(false);
    if (!equipeId || !grupo) {
      return;
    }
    try {
      const entidades = (await this.metadados.entidades(grupo)).filter((e) => e.escopoEquipe && e.ativo);
      const linhas = await Promise.all(
        entidades.map(async (entidade): Promise<LinhaConfiguracao> => {
          const [aprovacao, modelos] = await Promise.all([
            this.aprovacao.aprovacaoAtiva(equipeId, grupo, entidade.chave),
            this.avaliacao.modelos(equipeId, grupo, entidade.chave),
          ]);
          return {
            entidade,
            aprovacao,
            ativo: modelos.find((m) => m.ativo) ?? null,
            inativo: modelos.find((m) => !m.ativo) ?? null,
          };
        }),
      );
      this.linhas.set(linhas);
    } catch {
      this.erro.set(true);
    }
  }

  // ---- Aprovação ----

  /** Ligar pede a explicação antes; desligar vai direto (a API recusa com pendentes, RN33). */
  alternarAprovacao(linha: LinhaConfiguracao, ativa: boolean): void {
    if (ativa) {
      this.explicando.set(linha);
    } else {
      void this.gravarAprovacao(linha, false);
    }
  }

  confirmarLigar(): void {
    const linha = this.explicando();
    this.explicando.set(null);
    if (linha) {
      void this.gravarAprovacao(linha, true);
    }
  }

  private async gravarAprovacao(linha: LinhaConfiguracao, ativa: boolean): Promise<void> {
    const [equipeId, grupo] = [this.equipeId()!, this.grupo()!];
    this.alterando.set(linha.entidade.chave);
    try {
      await this.aprovacao.configurar(equipeId, grupo, linha.entidade.chave, ativa, tratarNoComponente(409));
      this.mensagens.add({
        severity: 'success',
        summary: `Aprovação ${ativa ? 'ligada' : 'desligada'} em "${nomePlural(linha.entidade)}".`,
      });
      // Os metadados guardam em que equipes a aprovação está ativa (situação, filtros, "Novo" do EXTERNO): recarrega.
      this.metadados.invalidar(grupo);
    } catch (erro) {
      const problema = lerProblema(erro);
      if (problema.codigo === 'APROVACOES_PENDENTES') {
        this.bloqueioPendentes.set({
          linha,
          mensagem: problema.detalhe ?? mensagemDoErro(problema.codigo, problema.status),
          quantidade: Number(problema.detalhes?.['quantidade'] ?? 0),
        });
      } else {
        this.mensagens.add({
          severity: 'error',
          summary: problema.detalhe ?? mensagemDoErro(problema.codigo, problema.status),
        });
      }
    } finally {
      this.alterando.set(null);
      await this.carregarLinhas(equipeId, grupo);
    }
  }

  // ---- Avaliação ----

  async alternarModelo(linha: LinhaConfiguracao, ativo: boolean): Promise<void> {
    const [equipeId, grupo] = [this.equipeId()!, this.grupo()!];
    this.alterando.set(linha.entidade.chave);
    try {
      await this.avaliacao.ativarModelo(
        equipeId,
        grupo,
        linha.entidade.chave,
        ativo,
        ativo ? (linha.inativo?.id ?? null) : null,
        tratarNoComponente(409),
      );
      this.mensagens.add({
        severity: 'success',
        summary: `Modelo de avaliação ${ativo ? 'reativado' : 'desativado'} em "${nomePlural(linha.entidade)}".`,
      });
      this.metadados.invalidar(grupo);
    } catch (erro) {
      const problema = lerProblema(erro);
      this.mensagens.add({
        severity: 'error',
        summary: problema.detalhe ?? mensagemDoErro(problema.codigo, problema.status),
      });
    } finally {
      this.alterando.set(null);
      await this.carregarLinhas(equipeId, grupo);
    }
  }

  linkEditor(linha: LinhaConfiguracao, novo: boolean): unknown[] {
    return [
      '/configuracoes/equipe',
      this.equipeId(),
      this.grupo(),
      linha.entidade.chave,
      novo ? 'novo-modelo' : 'modelo',
    ];
  }
}
