import { ChangeDetectionStrategy, Component, computed, effect, inject, input, signal, untracked } from '@angular/core';
import { MessageService } from 'primeng/api';
import { Button } from 'primeng/button';
import { Message } from 'primeng/message';
import { Paginator, PaginatorState } from 'primeng/paginator';
import { Skeleton } from 'primeng/skeleton';
import { AvaliacaoDto, AvaliacoesDoRegistroDto, ResumoAvaliacaoDto } from '../../../api/models';
import { AvaliacaoService } from '../../../core/avaliacao/avaliacao.service';
import { CriterioAvaliacao } from '../../../core/avaliacao/modelo-avaliacao';
import {
  RespostaFormulario,
  ResumoCriterio,
  respostasDaApi,
  respostasParaApi,
  resumoDoCriterio,
  rotuloEstrelas,
  validarRespostas,
} from '../../../core/avaliacao/regras-avaliacao';
import { tratarNoComponente } from '../../../core/erros/contexto-erros';
import { mensagemDoErro } from '../../../core/erros/mensagens-erro';
import { lerProblema } from '../../../core/erros/problema';
import { PermissoesService } from '../../../core/permissoes/permissoes.service';
import { Registro } from '../../../core/registros/modelo-registro';
import { MediaEstrelasComponent } from '../../../shared/avaliacao/media-estrelas.component';
import { FormularioAvaliacaoComponent } from '../../../shared/avaliacao/formulario-avaliacao.component';
import { formatarDataHoraSistema } from '../../../shared/formatacao/formatar-valor';

/** Um resumo pronto para a tela: o nome do modelo, os avaliadores e cada critério de estrelas. */
interface ResumoTela {
  modelo: string;
  avaliadores: number;
  criterios: ResumoCriterio[];
}

/**
 * A aba Avaliações do detalhe (RN44 a RN47), só quando a entidade tem modelo de avaliação ATIVO na equipe do item:
 *   - Resumo (todos que veem o item, inclusive o EXTERNO): média em estrelas e em número, quantidade e distribuição de 1 a 5 em
 *     barras, por critério de estrelas; o total de avaliadores; e os resumos de modelos anteriores, separados;
 *   - Minha avaliação (INTERNO, GESTOR e ADMINISTRADOR): o formulário do modelo, salvar, editar (com a `versao`) e remover. Item não
 *     aprovado: a mensagem "Este item só pode ser avaliado depois de aprovado" no lugar do formulário;
 *   - Avaliações individuais (INTERNO, GESTOR e ADMINISTRADOR; a API não as manda para o EXTERNO), paginadas.
 */
@Component({
  selector: 'app-aba-avaliacoes',
  imports: [Button, Message, Paginator, Skeleton, FormularioAvaliacaoComponent, MediaEstrelasComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './aba-avaliacoes.component.html',
})
export class AbaAvaliacoesComponent {
  readonly grupo = input.required<string>();
  readonly entidade = input.required<string>();
  readonly registro = input.required<Registro>();
  /** Os critérios do modelo ativo na equipe do item. */
  readonly criterios = input.required<CriterioAvaliacao[]>();

  private readonly avaliacao = inject(AvaliacaoService);
  private readonly mensagens = inject(MessageService);
  readonly permissoes = inject(PermissoesService);

  readonly dados = signal<AvaliacoesDoRegistroDto | null>(null);
  readonly erro = signal(false);
  readonly pagina = signal(1);
  readonly tamanho = 10;

  readonly valores = signal<Record<string, RespostaFormulario>>({});
  readonly errosResposta = signal<Record<string, string[]>>({});
  readonly erroGeral = signal<string | null>(null);
  readonly salvando = signal(false);

  readonly resumo = computed(() => this.resumoTela(this.dados()?.resumo ?? null));
  readonly anteriores = computed(() =>
    (this.dados()?.resumosAnteriores ?? []).map((r) => this.resumoTela(r)).filter((r): r is ResumoTela => r !== null),
  );
  readonly minha = computed<AvaliacaoDto | null>(() => this.dados()?.resumo?.minha ?? null);
  readonly aprovado = computed(() => (this.registro().situacaoAprovacao ?? 'APROVADO') === 'APROVADO');
  readonly individuais = computed(() => this.dados()?.avaliacoes ?? null);
  readonly formatarDataHoraSistema = formatarDataHoraSistema;
  readonly rotuloEstrelas = rotuloEstrelas;

  constructor() {
    effect(() => {
      const [registro, pagina] = [this.registro(), this.pagina()];
      untracked(() => void this.carregar(registro.id, pagina));
    });
  }

  async carregar(id = this.registro().id, pagina = this.pagina()): Promise<void> {
    this.erro.set(false);
    try {
      const dados = await this.avaliacao.avaliacoesDoItem(this.grupo(), this.entidade(), id, pagina, this.tamanho);
      this.dados.set(dados);
      this.valores.set(
        respostasDaApi(this.criterios(), (dados.resumo?.minha?.respostas ?? null) as Record<string, unknown> | null),
      );
    } catch {
      this.erro.set(true);
    }
  }

  private resumoTela(resumo: ResumoAvaliacaoDto | null): ResumoTela | null {
    if (!resumo) {
      return null;
    }
    return {
      modelo: resumo.modelo?.nome ?? '',
      avaliadores: resumo.quantidadeAvaliadores ?? 0,
      criterios: (resumo.criterios ?? []).map(resumoDoCriterio),
    };
  }

  async salvar(): Promise<void> {
    this.erroGeral.set(null);
    const erros = validarRespostas(this.criterios(), this.valores());
    this.errosResposta.set(erros);
    if (Object.keys(erros).length > 0) {
      return;
    }
    const minha = this.minha();
    this.salvando.set(true);
    try {
      await this.avaliacao.salvarMinha(
        this.grupo(),
        this.entidade(),
        this.registro().id,
        respostasParaApi(this.criterios(), this.valores()),
        minha?.versao ?? null,
        tratarNoComponente(400, 409),
      );
      this.mensagens.add({ severity: 'success', summary: minha ? 'Avaliação atualizada.' : 'Avaliação registrada.' });
      await this.carregar();
    } catch (e) {
      this.tratarErro(e);
    } finally {
      this.salvando.set(false);
    }
  }

  async remover(): Promise<void> {
    this.salvando.set(true);
    try {
      await this.avaliacao.removerMinha(
        this.grupo(),
        this.entidade(),
        this.registro().id,
        tratarNoComponente(404, 409),
      );
      this.mensagens.add({ severity: 'success', summary: 'Sua avaliação foi removida.' });
      this.errosResposta.set({});
      await this.carregar();
    } catch (e) {
      this.tratarErro(e);
    } finally {
      this.salvando.set(false);
    }
  }

  private tratarErro(e: unknown): void {
    const problema = lerProblema(e);
    if (problema.status === 400) {
      this.errosResposta.set(problema.erros);
      const semCriterio = Object.entries(problema.erros)
        .filter(([chave]) => !chave.startsWith('respostas.'))
        .flatMap(([, m]) => m);
      this.erroGeral.set(semCriterio.length ? semCriterio.join(' ') : null);
    } else if (problema.codigo === 'CONFLITO_CONCORRENCIA') {
      this.erroGeral.set('Sua avaliação foi alterada em outra tela. Ela foi recarregada: confira e salve de novo.');
      void this.carregar();
    } else {
      this.erroGeral.set(problema.detalhe ?? mensagemDoErro(problema.codigo, problema.status));
    }
  }

  aoMudarPagina(evento: PaginatorState): void {
    this.pagina.set(Math.floor((evento.first ?? 0) / this.tamanho) + 1);
  }

  /** As respostas de uma avaliação individual, na ordem dos critérios ativos (só os respondidos). */
  respostasDe(avaliacao: AvaliacaoDto): { nome: string; estrelas: number | null; comentario: string; texto: string }[] {
    const valores = respostasDaApi(this.criterios(), (avaliacao.respostas ?? null) as Record<string, unknown> | null);
    return this.criterios()
      .filter((c) => c.ativo)
      .map((c) => ({ nome: c.nome, ...valores[c.chave] }))
      .filter((r) => r.estrelas !== null || r.texto || r.comentario);
  }
}
