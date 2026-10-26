import { ChangeDetectionStrategy, Component, OnChanges, computed, inject, input, output } from '@angular/core';
import { FormControl, FormRecord, ReactiveFormsModule } from '@angular/forms';
import { Button } from 'primeng/button';
import { DatePicker } from 'primeng/datepicker';
import { InputNumber } from 'primeng/inputnumber';
import { InputText } from 'primeng/inputtext';
import { Select } from 'primeng/select';
import { Entidade, OpcaoReferencia } from '../../../core/metadados/modelo';
import { BuscarOpcoes, CampoReferenciaComponent } from '../../../shared/campos/campo-referencia.component';
import { ValorCampo } from '../../../shared/formulario/valores';
import { OpcoesFiltroService } from '../comum/opcoes-filtro.service';
import { FiltroDoCampo, filtrosDaEntidade, parametrosDaListaDeFiltros } from './filtros-url';

/**
 * O painel de filtros da listagem, montado a partir dos campos da entidade (um controle por parâmetro da API,
 * ver filtros-url.ts). Não consulta a API: "Filtrar" devolve os parâmetros e a listagem os põe na URL. A análise do painel
 * reaproveita o componente com uma lista própria (`filtrosDisponiveis`), que inclui campos por caminho (RN89).
 */
@Component({
  selector: 'app-filtros-registros',
  imports: [ReactiveFormsModule, Button, DatePicker, InputNumber, InputText, Select, CampoReferenciaComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './filtros-registros.component.html',
})
export class FiltrosRegistrosComponent implements OnChanges {
  readonly grupo = input.required<string>();
  readonly entidade = input.required<Entidade>();
  /** Os valores atuais (lidos da URL pela listagem). */
  readonly valores = input<Record<string, ValorCampo>>({});
  /** Os filtros a mostrar. Nulo = um por campo da entidade (a listagem). */
  readonly filtrosDisponiveis = input<FiltroDoCampo[] | null>(null);

  readonly aplicar = output<Record<string, string>>();

  private readonly opcoesFiltro = inject(OpcoesFiltroService);

  readonly filtros = computed(() => this.filtrosDisponiveis() ?? filtrosDaEntidade(this.entidade()));
  readonly opcoesBooleano = [
    { rotulo: 'Sim', valor: true },
    { rotulo: 'Não', valor: false },
  ];

  formulario = new FormRecord<FormControl<ValorCampo>>({});
  private readonly buscas = new Map<string, BuscarOpcoes>();

  ngOnChanges(): void {
    const formulario = new FormRecord<FormControl<ValorCampo>>({});
    for (const filtro of this.filtros()) {
      formulario.addControl(filtro.parametro, new FormControl<ValorCampo>(this.valores()[filtro.parametro] ?? null));
    }
    this.formulario = formulario;
    void this.completarRotulos();
  }

  controle(filtro: FiltroDoCampo): FormControl<ValorCampo> {
    return this.formulario.controls[filtro.parametro];
  }

  idDo(filtro: FiltroDoCampo): string {
    return `filtro-${filtro.parametro.replaceAll('.', '-')}`;
  }

  /** As opções de uma LISTA, no formato do p-select (a situação de aprovação traz rótulos próprios). */
  opcoesDe(filtro: FiltroDoCampo): { rotulo: string; valor: string }[] {
    return filtro.opcoes ?? filtro.campo.opcoes.map((o) => ({ rotulo: o, valor: o }));
  }

  buscaDe(filtro: FiltroDoCampo): BuscarOpcoes {
    let busca = this.buscas.get(filtro.parametro);
    if (!busca) {
      busca = (texto: string) => this.opcoesFiltro.buscar(this.grupo(), filtro.campo.entidadeReferenciada ?? '', texto);
      this.buscas.set(filtro.parametro, busca);
    }
    return busca;
  }

  filtrar(): void {
    this.aplicar.emit(parametrosDaListaDeFiltros(this.filtros(), this.formulario.getRawValue()));
  }

  limpar(): void {
    this.formulario.reset();
    this.aplicar.emit({});
  }

  /** A URL só guarda o id da referência: busca o rótulo para mostrar no select. */
  private async completarRotulos(): Promise<void> {
    for (const filtro of this.filtros().filter((f) => f.campo.tipo === 'REFERENCIA')) {
      const controle = this.formulario.controls[filtro.parametro];
      const valor = controle.value as OpcaoReferencia | null;
      if (valor && valor.rotulo === null && filtro.campo.entidadeReferenciada) {
        const rotulo = await this.opcoesFiltro.rotulo(this.grupo(), filtro.campo.entidadeReferenciada, valor.id);
        controle.setValue({ id: valor.id, rotulo: rotulo ?? valor.id });
      }
    }
  }
}
