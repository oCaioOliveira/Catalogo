import { Injectable, inject } from '@angular/core';
import { ConfirmationService, MessageService } from 'primeng/api';
import { tratarNoComponente } from '../../../core/erros/contexto-erros';
import { lerProblema } from '../../../core/erros/problema';
import { MetadadosService } from '../../../core/metadados/metadados.service';
import { filhasDe } from '../../../core/metadados/regras-metadados';
import { Entidade } from '../../../core/metadados/modelo';
import { Registro } from '../../../core/registros/modelo-registro';
import { RegistrosService } from '../../../core/registros/registros.service';
import { rotuloDoRegistro } from './rotulos';

interface Dependencia {
  entidade?: string;
  campo?: string;
  quantidade?: number;
  exemplos?: string[];
}

/**
 * Exclusão de um registro, igual em todas as telas: confirmação (avisando dos filhos que vão junto, RN11) e, se
 * a API recusar com 409 EXCLUSAO_BLOQUEADA, a lista do que impede a exclusão (detalhes.dependencias, RN16).
 */
@Injectable({ providedIn: 'root' })
export class ExclusaoService {
  private readonly confirmacao = inject(ConfirmationService);
  private readonly mensagens = inject(MessageService);
  private readonly registros = inject(RegistrosService);
  private readonly metadados = inject(MetadadosService);

  /** true se excluiu. */
  async excluir(grupo: string, entidade: Entidade, registro: Registro): Promise<boolean> {
    const todas = await this.metadados.entidades(grupo);
    const filhas = filhasDe(entidade, todas).map((f) => f.entidade.nome);
    const aviso = filhas.length > 0 ? ` Os itens filhos (${filhas.join(', ')}) também serão excluídos.` : '';

    const confirmou = await new Promise<boolean>((resolver) =>
      this.confirmacao.confirm({
        header: 'Excluir item',
        message: `Excluir "${rotuloDoRegistro(entidade, registro)}"? Esta ação não pode ser desfeita.${aviso}`,
        icon: 'pi pi-exclamation-triangle',
        acceptLabel: 'Excluir',
        rejectLabel: 'Cancelar',
        acceptButtonProps: { severity: 'danger', label: 'Excluir' },
        rejectButtonProps: { severity: 'secondary', text: true, label: 'Cancelar' },
        accept: () => resolver(true),
        reject: () => resolver(false),
      }),
    );
    if (!confirmou) {
      return false;
    }

    try {
      await this.registros.excluir(grupo, entidade.chave, registro.id, tratarNoComponente(409));
      this.mensagens.add({ severity: 'success', summary: 'Item excluído.' });
      return true;
    } catch (erro) {
      const problema = lerProblema(erro);
      if (problema.codigo === 'EXCLUSAO_BLOQUEADA') {
        this.mostrarBloqueio(problema.detalhes?.['dependencias'] as Dependencia[] | undefined, todas);
      } else {
        this.mensagens.add({ severity: 'warn', summary: problema.detalhe ?? 'Não foi possível excluir.' });
      }
      return false;
    }
  }

  private mostrarBloqueio(dependencias: Dependencia[] | undefined, todas: Entidade[]): void {
    const linhas = (dependencias ?? []).map((d) => {
      const entidade = todas.find((e) => e.chave === d.entidade);
      const campo = entidade?.campos.find((c) => c.chave === d.campo);
      const exemplos = d.exemplos?.length ? ` (ex.: ${d.exemplos.join(', ')})` : '';
      return `• ${d.quantidade ?? 0} item(ns) de "${entidade?.nome ?? d.entidade}" pelo campo "${campo?.nome ?? d.campo}"${exemplos}`;
    });
    this.confirmacao.confirm({
      key: 'bloqueio',
      header: 'Exclusão bloqueada',
      message: ['Outros itens dependem deste. Exclua ou altere estes antes:', ...linhas].join('\n'),
      icon: 'pi pi-ban',
      acceptLabel: 'Entendi',
      rejectVisible: false,
      acceptButtonProps: { label: 'Entendi' },
    });
  }
}
