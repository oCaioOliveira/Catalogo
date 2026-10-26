import { ConfiguracaoPainel } from '../modelo/configuracao';
import { PAINEL_TESTES_DESEMPENHO } from './testes-desempenho.painel';

/**
 * Os painéis configurados, um arquivo por grupo. Grupo sem arquivo: a página mostra só a visão geral. Painéis salvos no
 * servidor ficam fora do escopo por enquanto (ADR 0039).
 */
const PAINEIS: readonly ConfiguracaoPainel[] = [PAINEL_TESTES_DESEMPENHO];

export function configuracaoDoPainel(grupo: string): ConfiguracaoPainel | null {
  return PAINEIS.find((p) => p.grupo === grupo) ?? null;
}
