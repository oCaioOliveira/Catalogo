import { Entidade } from '../../../core/metadados/modelo';
import {
  ColunaArquivo,
  Destino,
  DestinoDisponivel,
  ErroLinha,
  LinhaImportacao,
  SituacaoImportacao,
  SituacaoLinha,
  Totais,
} from '../../../core/importacao/modelo-importacao';

/**
 * Regras da tela de importação (RN97 a RN104), sem Angular. O mapeamento na tela é um "valor de select" por coluna: `IGNORAR`,
 * `EQUIPE`, `CAMPO:<chave>` ou `CAMPO_ID:<chave>`. A API decide e valida de novo; aqui é só conveniência.
 */

/** Limites conferidos antes de enviar (a API confere de novo, RN98). */
export const TAMANHO_MAXIMO_BYTES = 5 * 1024 * 1024;
export const EXTENSOES_ACEITAS = ['.xlsx', '.csv'];
export const IGNORAR = 'IGNORAR';

export type MapeamentoTela = Record<number, string>;

export function valorDoDestino(destino: Destino): string {
  return destino.tipo === 'CAMPO' || destino.tipo === 'CAMPO_ID' ? `${destino.tipo}:${destino.campo}` : destino.tipo;
}

export function destinoDoValor(valor: string): Destino {
  const [tipo, campo] = valor.split(':');
  return tipo === 'CAMPO' || tipo === 'CAMPO_ID'
    ? { tipo, campo: campo ?? null }
    : { tipo: tipo === 'EQUIPE' ? 'EQUIPE' : 'IGNORAR', campo: null };
}

/** Sem acentos, sem "*" e em minúsculas: para reconhecer a mesma coluna num arquivo reenviado. */
export function normalizarCabecalho(texto: string): string {
  return texto.normalize('NFD').replace(/[̀-ͯ]/g, '').replace(/\*/g, '').trim().toLowerCase();
}

/**
 * O mapeamento inicial de um arquivo: a sugestão da API, mas, ao reenviar a planilha corrigida, o que a pessoa escolheu antes para
 * uma coluna de mesmo cabeçalho vale mais (RN101: "sem perder o mapeamento"). Um destino nunca fica em duas colunas.
 */
export function mapeamentoInicial(
  colunas: readonly ColunaArquivo[],
  anterior?: ReadonlyMap<string, string>,
): MapeamentoTela {
  let mapa: MapeamentoTela = {};
  for (const coluna of colunas) {
    const escolhido = anterior?.get(normalizarCabecalho(coluna.cabecalho));
    mapa = escolherDestino(mapa, coluna.indice, escolhido ?? valorDoDestino(coluna.destino));
  }
  return mapa;
}

/** Guarda o mapeamento pelo cabeçalho (para o próximo arquivo). */
export function lembrarMapeamento(colunas: readonly ColunaArquivo[], mapa: MapeamentoTela): Map<string, string> {
  return new Map(colunas.map((c) => [normalizarCabecalho(c.cabecalho), mapa[c.indice] ?? IGNORAR]));
}

/** Escolhe o destino de uma coluna. Se outra coluna já ia para ele, ela passa a ser ignorada (cada destino recebe uma coluna só). */
export function escolherDestino(mapa: MapeamentoTela, indice: number, valor: string): MapeamentoTela {
  const novo: MapeamentoTela = {};
  for (const [chave, atual] of Object.entries(mapa)) {
    novo[Number(chave)] = valor !== IGNORAR && atual === valor ? IGNORAR : atual;
  }
  novo[indice] = valor;
  return novo;
}

export function mapeamentoParaApi(
  colunas: readonly ColunaArquivo[],
  mapa: MapeamentoTela,
): { indice: number; destino: Destino }[] {
  return colunas.map((c) => ({ indice: c.indice, destino: destinoDoValor(mapa[c.indice] ?? IGNORAR) }));
}

/** As opções do select de cada coluna: "Ignorar esta coluna" e os destinos da API (obrigatórios com "*"). */
export function opcoesDoSelect(destinos: readonly DestinoDisponivel[]): { rotulo: string; valor: string }[] {
  return [
    { rotulo: 'Ignorar esta coluna', valor: IGNORAR },
    ...destinos.map((d) => ({ rotulo: d.obrigatorio ? `${d.rotulo} *` : d.rotulo, valor: valorDoDestino(d) })),
  ];
}

/** Campos obrigatórios que nenhuma coluna preenche (nem pelo nome, nem pelo "(ID)"): as linhas ficariam com erro. */
export function obrigatoriosSemColuna(
  destinos: readonly DestinoDisponivel[],
  mapa: MapeamentoTela,
): DestinoDisponivel[] {
  const usados = new Set(
    Object.values(mapa)
      .map(destinoDoValor)
      .filter((d) => d.campo)
      .map((d) => d.campo),
  );
  return destinos.filter((d) => d.tipo === 'CAMPO' && d.obrigatorio && !usados.has(d.campo));
}

/** Alguma coluna vai para a Equipe? */
export function temColunaEquipe(mapa: MapeamentoTela): boolean {
  return Object.values(mapa).includes('EQUIPE');
}

/** O tipo de item tem referências? (o aviso da etapa Arquivo) */
export function temReferencias(entidade: Entidade): boolean {
  return entidade.campos.some((c) => c.ativo && c.tipo === 'REFERENCIA');
}

/** Os nomes dos campos de cada restrição única ("Relatório + Nº do cenário"), para explicar o que é um item repetido. */
export function descricaoDuplicados(entidade: Entidade): string[] {
  return entidade.restricoesUnicas.map((r) =>
    r.campos.map((chave) => entidade.campos.find((c) => c.chave === chave)?.nome ?? chave).join(' + '),
  );
}

/** Confere o arquivo antes de enviar: extensão e tamanho. Nulo = pode enviar. */
export function problemaDoArquivo(nome: string, tamanho: number): string | null {
  const extensao = nome.slice(nome.lastIndexOf('.')).toLowerCase();
  if (!EXTENSOES_ACEITAS.includes(extensao)) {
    return 'Envie uma planilha .xlsx (Excel) ou um arquivo .csv.';
  }
  if (tamanho > TAMANHO_MAXIMO_BYTES) {
    const mb = (tamanho / 1024 / 1024).toLocaleString('pt-BR', { maximumFractionDigits: 1 });
    return `O arquivo tem ${mb} MB; o máximo é 5 MB.`;
  }
  return null;
}

const SITUACOES_LINHA: Record<
  SituacaoLinha,
  { rotulo: string; severidade: 'success' | 'info' | 'secondary' | 'danger' }
> = {
  NOVA: { rotulo: 'Novo', severidade: 'success' },
  ATUALIZACAO: { rotulo: 'Atualizar', severidade: 'info' },
  IGNORADA: { rotulo: 'Ignorado', severidade: 'secondary' },
  ERRO: { rotulo: 'Com erro', severidade: 'danger' },
};

export function situacaoDaLinha(situacao: SituacaoLinha): {
  rotulo: string;
  severidade: 'success' | 'info' | 'secondary' | 'danger';
} {
  return SITUACOES_LINHA[situacao];
}

const SITUACOES_IMPORTACAO: Record<
  SituacaoImportacao,
  { rotulo: string; severidade: 'success' | 'info' | 'secondary' | 'warn' | 'danger' }
> = {
  ENVIADA: { rotulo: 'Sem prévia', severidade: 'secondary' },
  VALIDADA: { rotulo: 'Prévia feita', severidade: 'info' },
  CONCLUIDA: { rotulo: 'Concluída', severidade: 'success' },
  DESFEITA: { rotulo: 'Desfeita', severidade: 'warn' },
  EXPIRADA: { rotulo: 'Expirada', severidade: 'secondary' },
};

export function situacaoDaImportacao(situacao: SituacaoImportacao): {
  rotulo: string;
  severidade: 'success' | 'info' | 'secondary' | 'warn' | 'danger';
} {
  return SITUACOES_IMPORTACAO[situacao];
}

/** As mensagens de erro de uma célula (a posição da coluna no arquivo). */
export function errosDaCelula(linha: LinhaImportacao, coluna: number): string[] {
  return linha.erros.filter((e) => e.coluna === coluna).map((e) => e.mensagem);
}

/** Os erros que não são de uma célula (linha repetida, obrigatório sem coluna), com o nome do campo quando há. */
export function errosDaLinha(linha: LinhaImportacao, entidade: Entidade | null): string[] {
  return linha.erros.filter((e: ErroLinha) => e.coluna === null).map((e) => comCampo(e, entidade));
}

function comCampo(erro: ErroLinha, entidade: Entidade | null): string {
  const campo = erro.campo ? entidade?.campos.find((c) => c.chave === erro.campo) : undefined;
  return campo ? `${campo.nome}: ${erro.mensagem}` : erro.mensagem;
}

/** O item encontrado para uma célula de referência ("Transferências Alfa"), para a dica da prévia. */
export function referenciaDaCelula(linha: LinhaImportacao, coluna: number): string | null {
  const referencia = linha.referencias.find((r) => r.coluna === coluna);
  return referencia ? (referencia.nome ?? referencia.id) : null;
}

function plural(quantidade: number, singular: string, varios: string): string {
  return `${quantidade.toLocaleString('pt-BR')} ${quantidade === 1 ? singular : varios}`;
}

/** "16 novos · 1 ignorado · 3 com erro" (o que não aparece é zero). */
export function textoTotais(totais: Totais | null): string {
  if (!totais) {
    return '—';
  }
  const partes = [
    totais.novos > 0 ? plural(totais.novos, 'novo', 'novos') : null,
    totais.atualizacoes > 0 ? plural(totais.atualizacoes, 'atualização', 'atualizações') : null,
    totais.ignorados > 0 ? plural(totais.ignorados, 'ignorado', 'ignorados') : null,
    totais.comErro > 0 ? plural(totais.comErro, 'com erro', 'com erro') : null,
  ].filter((p): p is string => p !== null);
  return partes.length > 0 ? partes.join(' · ') : 'nenhuma linha';
}

/** O texto do botão de executar: "Importar 16 itens" (ou "Importar 16 itens e atualizar 2"). */
export function rotuloExecutar(totais: Totais): string {
  const criar = plural(totais.novos, 'item', 'itens');
  if (totais.atualizacoes === 0) {
    return `Importar ${criar}`;
  }
  return totais.novos === 0
    ? `Atualizar ${plural(totais.atualizacoes, 'item', 'itens')}`
    : `Importar ${criar} e atualizar ${totais.atualizacoes}`;
}

/** Por que a prévia não pode ser executada (nulo se pode). */
export function motivoNaoExecutavel(totais: Totais, executavel: boolean): string | null {
  if (executavel) {
    return null;
  }
  return totais.novos + totais.atualizacoes > 0
    ? 'A opção é "tudo ou nada" e há linhas com erro: corrija a planilha e envie de novo, ou escolha importar só as válidas.'
    : 'Não há nenhuma linha para importar: corrija os erros na planilha e envie de novo.';
}

/** A mensagem do resultado, na conclusão. */
export function textoConclusao(totais: Totais): string {
  const partes = [
    `${plural(totais.novos, 'item criado', 'itens criados')}`,
    totais.atualizacoes > 0 ? plural(totais.atualizacoes, 'item atualizado', 'itens atualizados') : null,
    totais.ignorados > 0
      ? plural(totais.ignorados, 'linha ignorada (já existia)', 'linhas ignoradas (já existiam)')
      : null,
    totais.comErro > 0
      ? plural(totais.comErro, 'linha com erro não importada', 'linhas com erro não importadas')
      : null,
  ].filter((p): p is string => p !== null);
  return partes.join(', ') + '.';
}

/** O texto da confirmação do desfazer, explicando o que sai e o que fica (RN103). */
export function mensagemDesfazer(totais: Totais | null): string {
  const criados = totais?.novos ?? 0;
  const atualizacoes =
    (totais?.atualizacoes ?? 0) > 0 ? ` As ${totais!.atualizacoes} atualizações feitas por ela não são desfeitas.` : '';
  return (
    `Desfazer exclui ${plural(criados, 'item criado', 'itens criados')} por esta importação.${atualizacoes} ` +
    'Só é possível se nenhum deles tiver sido alterado nem estiver sendo usado por outro item.'
  );
}

interface Impedimento {
  motivo?: string;
  quantidade?: number;
  itens?: { id?: string; rotulo?: string | null }[];
}

/** As linhas da mensagem quando o desfazer é recusado (409 IMPORTACAO_NAO_PODE_SER_DESFEITA, detalhes.impedimentos). */
export function linhasDosImpedimentos(detalhes: Record<string, unknown> | null): string[] {
  const impedimentos = (detalhes?.['impedimentos'] as Impedimento[] | undefined) ?? [];
  return impedimentos.map((i) => {
    const itens = (i.itens ?? []).map((item) => item.rotulo ?? item.id ?? '').filter((t) => t.length > 0);
    const mais = (i.quantidade ?? itens.length) > itens.length ? ` e mais ${(i.quantidade ?? 0) - itens.length}` : '';
    return `• ${i.motivo ?? ''}${itens.length > 0 ? ` (${itens.join(', ')}${mais})` : ''}`;
  });
}
