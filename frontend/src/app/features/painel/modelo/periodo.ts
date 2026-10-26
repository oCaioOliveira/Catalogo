import { dataDeTextoIso, dataParaTextoIso } from '../../../shared/formatacao/datas';

/**
 * Atalhos de período do painel. As datas vão para a API como "aaaa-mm-dd" (inclusivas). Os de meses começam no PRIMEIRO dia
 * do mês, para os gráficos por mês terem meses inteiros: "Últimos 12 meses" em 22/10/2026 = 01/11/2025 a 22/10/2026 (12 meses,
 * contando o atual). Funções puras: `hoje` vem de quem chama (periodo.spec.ts).
 */

export type AtalhoPeriodo = '30d' | '3m' | '12m' | 'ano' | 'personalizado';

export const ATALHO_PADRAO: AtalhoPeriodo = '12m';

export const ATALHOS: { valor: AtalhoPeriodo; rotulo: string }[] = [
  { valor: '30d', rotulo: 'Últimos 30 dias' },
  { valor: '3m', rotulo: 'Últimos 3 meses' },
  { valor: '12m', rotulo: 'Últimos 12 meses' },
  { valor: 'ano', rotulo: 'Ano atual' },
  { valor: 'personalizado', rotulo: 'Personalizado' },
];

export function ehAtalho(valor: unknown): valor is AtalhoPeriodo {
  return ATALHOS.some((a) => a.valor === valor);
}

/** As datas de um atalho (o personalizado não tem cálculo: as datas são as escolhidas). */
export function calcularPeriodo(
  atalho: Exclude<AtalhoPeriodo, 'personalizado'>,
  hoje: Date,
): { de: string; ate: string } {
  const ate = dataParaTextoIso(hoje);
  switch (atalho) {
    case '30d':
      return { de: dataParaTextoIso(new Date(hoje.getFullYear(), hoje.getMonth(), hoje.getDate() - 29)), ate };
    case '3m':
      return { de: dataParaTextoIso(new Date(hoje.getFullYear(), hoje.getMonth() - 2, 1)), ate };
    case 'ano':
      return { de: dataParaTextoIso(new Date(hoje.getFullYear(), 0, 1)), ate };
    default:
      return { de: dataParaTextoIso(new Date(hoje.getFullYear(), hoje.getMonth() - 11, 1)), ate };
  }
}

/** "aaaa-mm-dd" válido (a data existe). */
export function ehDataValida(texto: string | null | undefined): texto is string {
  const data = texto ? dataDeTextoIso(texto) : null;
  return data !== null && dataParaTextoIso(data) === texto;
}
