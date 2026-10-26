import { FormatoExportacao, itensExportacao } from './opcoes-exportacao';

describe('opções de exportação (todos os menus "Exportar")', () => {
  it('a planilha vem primeiro; os rótulos e a dica do CSV são exatos', () => {
    const itens = itensExportacao(() => undefined);
    expect(itens.map((i) => i.label)).toEqual(['Planilha Excel (.xlsx)', 'Arquivo CSV (.csv)']);
    expect(itens[0].tooltipOptions).toBeUndefined();
    expect(itens[1].tooltipOptions?.tooltipLabel).toBe('Separado por ponto e vírgula; abre direto no Excel');
  });

  it('cada item exporta no formato dele', () => {
    const pedidos: FormatoExportacao[] = [];
    const itens = itensExportacao((formato) => pedidos.push(formato));
    itens.forEach((item) => item.command?.({}));
    expect(pedidos).toEqual(['xlsx', 'csv']);
  });
});
