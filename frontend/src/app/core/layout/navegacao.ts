// Regras da moldura (cabeçalho, menu lateral e área de configurações). Funções puras, sem Angular: navegacao.spec.ts.

/** Acima de quantas áreas o seletor do cabeçalho ganha busca. */
export const LIMITE_BUSCA_AREAS = 5;

/** Acima de quantos tipos de item o menu lateral ganha busca. */
export const LIMITE_BUSCA_MENU = 8;

/** Abaixo desta largura (px), o menu lateral vira uma gaveta por cima do conteúdo. */
export const LARGURA_GAVETA = 1024;

/** Primeiros segmentos de rota que NÃO são uma área de gestão (/:grupo/...). */
const ROTAS_FORA_DE_AREA = new Set([
  'configuracoes',
  'configuracoes-equipe',
  'admin',
  'aprovacoes',
  'minha-conta',
  'nao-encontrado',
  'sem-listagens',
  'sem-permissao',
  'login',
  'trocar-senha',
]);

function segmentos(url: string): string[] {
  return url
    .split(/[?#]/)[0]
    .split('/')
    .filter((s) => s.length > 0);
}

/** A tela atual é da área de configurações (/configuracoes/...)? Lá, o menu lateral é outro. */
export function ehAreaDeConfiguracoes(url: string): boolean {
  return segmentos(url)[0] === 'configuracoes';
}

/**
 * Para onde ir quando a pessoa troca a área no seletor do cabeçalho:
 *   - no painel de uma área → o painel da nova;
 *   - em qualquer outra tela de uma área (Início, listagem, item) → o Início da nova;
 *   - fora das áreas (configurações, aprovações, minha conta) → fica onde está (null): só a área atual muda, e a tela a segue.
 */
export function destinoAoTrocarArea(url: string, grupo: string): string[] | null {
  const [primeiro] = segmentos(url);
  if (primeiro === 'painel') {
    return ['/painel', grupo];
  }
  if (!primeiro || ROTAS_FORA_DE_AREA.has(primeiro)) {
    return null;
  }
  return ['/', grupo];
}

/** As iniciais do avatar: a primeira letra do primeiro e do último nome ("Gabriela Gestora" → "GG"; "Admin" → "A"). */
export function iniciais(nome: string | null | undefined): string {
  const partes = (nome ?? '')
    .trim()
    .split(/\s+/)
    .filter((p) => p.length > 0);
  if (partes.length === 0) {
    return '?';
  }
  const primeira = partes[0][0];
  const ultima = partes.length > 1 ? partes[partes.length - 1][0] : '';
  return (primeira + ultima).toLocaleUpperCase('pt-BR');
}

/** Texto para comparar na busca: minúsculo e sem acentos ("Relatórios" ≈ "relatorios"). */
export function normalizarBusca(texto: string): string {
  return texto.normalize('NFD').replace(/[̀-ͯ]/g, '').toLocaleLowerCase('pt-BR').trim();
}

/** Os itens cujo nome contém a busca (sem diferenciar acentos nem maiúsculas). Busca vazia = todos. */
export function filtrarPorNome<T extends { nome: string }>(itens: readonly T[], busca: string): T[] {
  const termo = normalizarBusca(busca);
  return termo ? itens.filter((i) => normalizarBusca(i.nome).includes(termo)) : [...itens];
}
