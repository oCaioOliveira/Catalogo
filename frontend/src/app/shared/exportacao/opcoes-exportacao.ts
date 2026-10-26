import { MenuItem } from 'primeng/api';

// As opções de TODO menu "Exportar" do sistema (listagens, widgets do painel e análise personalizada), num lugar só: os rótulos
// não podem divergir entre telas. Função pura: opcoes-exportacao.spec.ts.

export type FormatoExportacao = 'csv' | 'xlsx';

export interface OpcaoExportacao {
  formato: FormatoExportacao;
  rotulo: string;
  icone: string;
  /** Dica do item no menu (null = sem dica). */
  dica: string | null;
}

/** Na ordem do menu: a planilha primeiro (a mais usada). O formato dos arquivos é o da API (RN49 e RN88). */
export const OPCOES_EXPORTACAO: readonly OpcaoExportacao[] = [
  { formato: 'xlsx', rotulo: 'Planilha Excel (.xlsx)', icone: 'pi pi-file-excel', dica: null },
  {
    formato: 'csv',
    rotulo: 'Arquivo CSV (.csv)',
    icone: 'pi pi-file',
    dica: 'Separado por ponto e vírgula; abre direto no Excel',
  },
];

/** Os itens do p-menu de exportação; `exportar` recebe o formato escolhido. */
export function itensExportacao(exportar: (formato: FormatoExportacao) => void): MenuItem[] {
  return OPCOES_EXPORTACAO.map((opcao) => ({
    label: opcao.rotulo,
    icon: opcao.icone,
    command: () => exportar(opcao.formato),
    ...(opcao.dica ? { tooltipOptions: { tooltipLabel: opcao.dica, tooltipPosition: 'left' } } : {}),
  }));
}
