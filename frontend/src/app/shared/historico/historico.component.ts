import { ChangeDetectionStrategy, Component, computed, effect, input, signal, untracked } from '@angular/core';
import { Message } from 'primeng/message';
import { Skeleton } from 'primeng/skeleton';
import { TableLazyLoadEvent, TableModule } from 'primeng/table';
import { Tag } from 'primeng/tag';
import { MudancaDto } from '../../api/models';
import { ItemHistorico, PaginaDe } from '../../core/registros/modelo-registro';
import { formatarDataHoraSistema } from '../formatacao/formatar-valor';

const NOMES_OPERACAO: Record<string, string> = {
  CRIACAO: 'Criação',
  ALTERACAO: 'Alteração',
  EXCLUSAO: 'Exclusão',
  APROVACAO: 'Aprovação',
  REJEICAO: 'Rejeição',
};

/** Busca uma página do histórico (RN32), do mais recente para o mais antigo. */
export type CarregarHistorico = (pagina: number, tamanho: number) => Promise<PaginaDe<ItemHistorico>>;

/**
 * A aba Histórico, GENÉRICA (RN32): quem, quando, a operação e as mudanças por campo (de → para), paginada na API. Quem usa
 * diz como carregar, o nome de cada campo e como mostrar cada valor: o detalhe de registros (pelos metadados) e o de usuários e
 * equipes (administração) usam o mesmo componente.
 */
@Component({
  selector: 'app-historico',
  imports: [Message, Skeleton, TableModule, Tag],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './historico.component.html',
})
export class HistoricoComponent {
  readonly carregar = input.required<CarregarHistorico>();
  readonly nomeCampo = input<(chave: string) => string>((chave) => chave);
  readonly valor = input<(mudanca: MudancaDto, lado: 'de' | 'para') => string>(valorPadrao);

  readonly pagina = signal(1);
  readonly tamanho = 10;
  readonly resultado = signal<PaginaDe<ItemHistorico> | null>(null);
  readonly carregando = signal(true);
  readonly erro = signal(false);
  readonly primeiro = computed(() => (this.pagina() - 1) * this.tamanho);
  readonly formatarDataHoraSistema = formatarDataHoraSistema;

  constructor() {
    effect(() => {
      const [carregar, pagina] = [this.carregar(), this.pagina()];
      untracked(() => void this.buscar(carregar, pagina));
    });
  }

  private async buscar(carregar: CarregarHistorico, pagina: number): Promise<void> {
    this.carregando.set(true);
    this.erro.set(false);
    try {
      this.resultado.set(await carregar(pagina, this.tamanho));
    } catch {
      this.erro.set(true);
    } finally {
      this.carregando.set(false);
    }
  }

  aoMudarPagina(evento: TableLazyLoadEvent): void {
    const pagina = Math.floor((evento.first ?? 0) / this.tamanho) + 1;
    if (pagina !== this.pagina()) {
      this.pagina.set(pagina);
    }
  }

  nomeOperacao(operacao: string | null | undefined): string {
    return NOMES_OPERACAO[operacao ?? ''] ?? operacao ?? '';
  }

  severidade(operacao: string | null | undefined): 'success' | 'info' | 'danger' | 'secondary' {
    return operacao === 'CRIACAO' || operacao === 'APROVACAO'
      ? 'success'
      : operacao === 'EXCLUSAO' || operacao === 'REJEICAO'
        ? 'danger'
        : operacao === 'ALTERACAO'
          ? 'info'
          : 'secondary';
  }

  /** Decisões de aprovação (RN36) ficam em destaque, com a justificativa inteira. */
  ehDecisao(operacao: string | null | undefined): boolean {
    return operacao === 'APROVACAO' || operacao === 'REJEICAO';
  }
}

function valorPadrao(mudanca: MudancaDto, lado: 'de' | 'para'): string {
  const bruto: unknown = mudanca[lado];
  if (bruto === null || bruto === undefined) {
    return '(vazio)';
  }
  return typeof bruto === 'object' ? JSON.stringify(bruto) : String(bruto);
}
