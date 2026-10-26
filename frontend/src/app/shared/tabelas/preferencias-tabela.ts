import { gravarPreferenciaLocal, lerPreferenciaLocal } from '../preferencias/preferencia-local';
import { LARGURA_MINIMA } from './colunas-tabela';

/**
 * Larguras das colunas (px), salvas no localStorage por tabela (ex.: por tipo de item). É só conveniência: sem armazenamento,
 * valem as larguras padrão. A escolha e a ordem das colunas ficam em features/registros/comum/preferencias-colunas.ts.
 */
function chave(tabela: string): string {
  return `catalogo.larguras.${tabela}`;
}

export function lerLarguras(tabela: string): Record<string, number> {
  const texto = lerPreferenciaLocal(chave(tabela));
  try {
    const valor: unknown = texto ? JSON.parse(texto) : null;
    if (!valor || typeof valor !== 'object' || Array.isArray(valor)) {
      return {};
    }
    return Object.fromEntries(
      Object.entries(valor).filter(([, v]) => typeof v === 'number' && Number.isFinite(v) && v >= LARGURA_MINIMA),
    ) as Record<string, number>;
  } catch {
    return {};
  }
}

export function gravarLargura(tabela: string, coluna: string, largura: number): Record<string, number> {
  const larguras = { ...lerLarguras(tabela), [coluna]: Math.round(Math.max(LARGURA_MINIMA, largura)) };
  gravarPreferenciaLocal(chave(tabela), JSON.stringify(larguras));
  return larguras;
}
