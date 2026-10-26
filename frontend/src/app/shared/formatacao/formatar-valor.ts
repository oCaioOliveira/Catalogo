import { formatDate, formatNumber } from '@angular/common';
import { Campo, OpcaoReferencia } from '../../core/metadados/modelo';
import { dataDeTextoIso } from './datas';

export const LOCALE = 'pt-BR';

/** Um valor que veio da API, como TEXTO para a tela, no formato pt-BR. Vazio = "". */
export function formatarValor(campo: Campo, valor: unknown): string {
  if (valor === null || valor === undefined || valor === '') {
    return '';
  }
  switch (campo.tipo) {
    case 'INTEIRO':
      return formatNumber(Number(valor), LOCALE, '1.0-0');
    case 'DECIMAL': {
      const escala = campo.escala ?? 2;
      return formatNumber(Number(valor), LOCALE, `1.${escala}-${escala}`);
    }
    case 'BOOLEANO':
      return valor === true ? 'Sim' : 'Não';
    case 'DATA': {
      const data = typeof valor === 'string' ? dataDeTextoIso(valor) : null;
      return data ? formatDate(data, 'dd/MM/yyyy', LOCALE) : String(valor);
    }
    case 'DATA_HORA':
      return formatDate(String(valor), 'dd/MM/yyyy HH:mm', LOCALE);
    case 'REFERENCIA':
      return rotuloDaReferencia(valor);
    default:
      return String(valor);
  }
}

/** { id, rotulo } → o rótulo; sem rótulo (o campo de exibição está vazio), um texto genérico. */
export function rotuloDaReferencia(valor: unknown): string {
  const referencia = valor as OpcaoReferencia | null;
  if (!referencia || typeof referencia !== 'object') {
    return '';
  }
  return referencia.rotulo ?? '(sem rótulo)';
}

/** Data de sistema (UTC, "...Z") na hora local: "21/10/2026 14:30". */
export function formatarDataHoraSistema(valor: string | null | undefined): string {
  return valor ? formatDate(valor, 'dd/MM/yyyy HH:mm', LOCALE) : '';
}
