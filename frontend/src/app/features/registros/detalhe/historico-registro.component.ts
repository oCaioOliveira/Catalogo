import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { MudancaDto } from '../../../api/models';
import { SITUACOES, situacaoDe } from '../../../core/aprovacao/regras-aprovacao';
import { Entidade } from '../../../core/metadados/modelo';
import { RegistrosService } from '../../../core/registros/registros.service';
import { formatarValor } from '../../../shared/formatacao/formatar-valor';
import { CarregarHistorico, HistoricoComponent } from '../../../shared/historico/historico.component';

/** Nomes das colunas de sistema que podem aparecer nas mudanças (não são campos da entidade). */
const NOMES_SISTEMA: Record<string, string> = { equipeId: 'Equipe', situacaoAprovacao: 'Situação de aprovação' };

/**
 * Aba Histórico do detalhe de um registro (GET .../registros/{id}/historico, RN32): o histórico genérico
 * (shared/historico) com os nomes dos campos e os valores formatados pelos metadados.
 */
@Component({
  selector: 'app-historico-registro',
  imports: [HistoricoComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<app-historico [carregar]="carregar()" [nomeCampo]="nomeCampo" [valor]="valor" />`,
})
export class HistoricoRegistroComponent {
  readonly grupo = input.required<string>();
  readonly entidade = input.required<Entidade>();
  readonly id = input.required<string>();

  private readonly registros = inject(RegistrosService);

  readonly carregar = computed<CarregarHistorico>(() => {
    const [grupo, entidade, id] = [this.grupo(), this.entidade().chave, this.id()];
    return (pagina, tamanho) => this.registros.historico(grupo, entidade, id, pagina, tamanho);
  });

  readonly nomeCampo = (chave: string): string =>
    this.entidade().campos.find((c) => c.chave === chave)?.nome ?? NOMES_SISTEMA[chave] ?? chave;

  /** Valor de uma mudança, formatado pelo tipo do campo. Referências vêm como { id, rotulo } (RN32). */
  readonly valor = (mudanca: MudancaDto, lado: 'de' | 'para'): string => {
    const bruto: unknown = mudanca[lado];
    if (bruto === null || bruto === undefined) {
      return '(vazio)';
    }
    if (mudanca.campo === 'situacaoAprovacao') {
      return SITUACOES[situacaoDe(String(bruto))].rotulo;
    }
    const campo = this.entidade().campos.find((c) => c.chave === mudanca.campo);
    if (!campo) {
      return typeof bruto === 'object' ? JSON.stringify(bruto) : String(bruto);
    }
    return formatarValor(campo, bruto) || '(vazio)';
  };
}
