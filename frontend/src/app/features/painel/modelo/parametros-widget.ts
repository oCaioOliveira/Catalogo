import { Entidade } from '../../../core/metadados/modelo';
import { FiltrosPainel } from '../../../core/painel/modelo-painel';
import { ConfiguracaoWidget } from './configuracao';

/**
 * Configuração do widget + filtros globais do painel → os parâmetros de GET .../indicadores (docs/api/painel.md). Função pura
 * (parametros-widget.spec.ts). O período vai para o campo que o widget declara (`campoPeriodo`: `<campo>.de` e `<campo>.ate`,
 * também por caminho) ou, sem ele, para a data de criação (`de` e `ate`). A equipe só vai para entidades por equipe: numa global
 * a API recusaria `equipeId`. Os filtros globais são aplicados por último: prevalecem sobre um filtro fixo com a mesma chave.
 */
export function parametrosDoWidget(
  configuracao: ConfiguracaoWidget,
  filtros: FiltrosPainel,
  entidade: Pick<Entidade, 'escopoEquipe'> | null,
): Record<string, string> {
  const parametros: Record<string, string> = { ...(configuracao.filtros ?? {}) };
  if (configuracao.agruparPor.length > 0) {
    parametros['agruparPor'] = configuracao.agruparPor.join(',');
  }
  if (configuracao.metricas.length > 0) {
    parametros['metricas'] = configuracao.metricas.join(',');
  }
  if (configuracao.ordenarPor) {
    parametros['ordenarPor'] = configuracao.ordenarPor;
  }
  if (configuracao.limite !== undefined) {
    parametros['limite'] = String(configuracao.limite);
  }

  const prefixo = configuracao.campoPeriodo ? `${configuracao.campoPeriodo}.` : '';
  parametros[`${prefixo}de`] = filtros.de;
  parametros[`${prefixo}ate`] = filtros.ate;

  if (filtros.equipeId && entidade?.escopoEquipe) {
    parametros['equipeId'] = filtros.equipeId;
  }
  return parametros;
}
