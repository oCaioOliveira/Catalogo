import { DimensaoIndicador } from '../../../core/painel/modelo-painel';

/** Números e rótulos do painel no formato pt-BR. Funções puras (transformar-dados.spec.ts). */

const NUMERO = new Intl.NumberFormat('pt-BR', { maximumFractionDigits: 2 });
const MESES = ['jan', 'fev', 'mar', 'abr', 'mai', 'jun', 'jul', 'ago', 'set', 'out', 'nov', 'dez'];
const SITUACOES: Record<string, string> = { PENDENTE: 'Pendente', APROVADO: 'Aprovado', REJEITADO: 'Rejeitado' };

export const ROTULO_SEM_VALOR = '(sem valor)';

/** 1234.5 → "1.234,5"; com unidade: "1.234,5 TPS", "480 ms", "2,5%". Nulo → "—". */
export function formatarNumero(valor: number | null | undefined, unidade: string | null = null): string {
  if (valor === null || valor === undefined || !Number.isFinite(valor)) {
    return '—';
  }
  const numero = NUMERO.format(valor);
  if (!unidade) {
    return numero;
  }
  return unidade === '%' ? `${numero}%` : `${numero} ${unidade}`;
}

/**
 * O valor de uma dimensão como texto para a tela: { id, rotulo } → o rótulo; "2026-03" → "mar/2026"; "2026-03-15" →
 * "15/03/2026"; "2026-T1" → "1º tri/2026"; true → "Sim"; "PENDENTE" → "Pendente"; nulo → "(sem valor)".
 */
export function rotuloDoValorDimensao(dimensao: DimensaoIndicador, valor: unknown): string {
  if (valor === null || valor === undefined || valor === '') {
    return ROTULO_SEM_VALOR;
  }
  if (typeof valor === 'object') {
    const rotulo = (valor as { rotulo?: string | null }).rotulo;
    return rotulo ?? '(sem rótulo)';
  }
  if (typeof valor === 'boolean') {
    return valor ? 'Sim' : 'Não';
  }
  const texto = String(valor);
  if (dimensao.tipo === 'SITUACAO_APROVACAO') {
    return SITUACOES[texto] ?? texto;
  }
  switch (dimensao.periodo) {
    case 'mes': {
      const [ano, mes] = texto.split('-');
      return `${MESES[Number(mes) - 1] ?? mes}/${ano}`;
    }
    case 'dia': {
      const [ano, mes, dia] = texto.split('-');
      return `${dia}/${mes}/${ano}`;
    }
    case 'trimestre': {
      const [ano, trimestre] = texto.split('-T');
      return `${trimestre}º tri/${ano}`;
    }
    default:
      return texto;
  }
}
