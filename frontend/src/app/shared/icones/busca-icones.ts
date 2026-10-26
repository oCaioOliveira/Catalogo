import { normalizarBusca } from '../../core/layout/navegacao';

// A busca do seletor de ícone (RN95): os nomes do PrimeIcons são em inglês ("pi-chart-line"); quem busca em português acha
// pelos sinônimos abaixo ("gráfico" → chart). Funções puras: busca-icones.spec.ts.

/** Palavras em português → partes dos nomes em inglês do PrimeIcons. */
const SINONIMOS: Record<string, string[]> = {
  usuario: ['user'],
  pessoa: ['user'],
  pessoas: ['users'],
  equipe: ['users'],
  grafico: ['chart'],
  painel: ['chart', 'th-large'],
  pasta: ['folder'],
  arquivo: ['file'],
  documento: ['file', 'book'],
  relatorio: ['chart', 'file'],
  relogio: ['clock'],
  hora: ['clock'],
  calendario: ['calendar'],
  data: ['calendar'],
  dinheiro: ['dollar', 'money', 'wallet'],
  servidor: ['server'],
  banco: ['database'],
  etiqueta: ['tag'],
  sigla: ['tag'],
  bandeira: ['flag'],
  status: ['flag', 'circle'],
  casa: ['home'],
  inicio: ['home'],
  estrela: ['star'],
  favorito: ['star', 'heart'],
  ligacao: ['link'],
  endereco: ['link', 'map'],
  carro: ['car'],
  caixa: ['box', 'inbox'],
  lista: ['list'],
  livro: ['book'],
  maleta: ['briefcase'],
  projeto: ['briefcase', 'folder'],
  bilhete: ['ticket'],
  chamado: ['ticket'],
  nuvem: ['cloud'],
  cadeado: ['lock'],
  chave: ['key'],
  email: ['envelope'],
  telefone: ['phone'],
  configuracao: ['cog', 'sliders'],
  mapa: ['map'],
  predio: ['building'],
  empresa: ['building'],
  imagem: ['image'],
  camera: ['camera'],
  sino: ['bell'],
  aviso: ['exclamation', 'bell'],
  carrinho: ['shopping-cart'],
  compra: ['shopping', 'cart'],
  caminhao: ['truck'],
  entrega: ['truck', 'box'],
  certo: ['check'],
  teste: ['check-square', 'verified'],
  pergunta: ['question'],
  ajuda: ['question'],
  computador: ['desktop'],
  celular: ['mobile'],
  codigo: ['code'],
  comunidade: ['users', 'globe'],
  mundo: ['globe'],
  ambiente: ['server', 'cloud'],
  ferramenta: ['wrench'],
  escudo: ['shield'],
  seguranca: ['shield', 'lock'],
  coracao: ['heart'],
  mensagem: ['comment', 'envelope'],
  comentario: ['comment'],
  busca: ['search'],
  filtro: ['filter'],
};

/**
 * Os ícones cujo nome contém a busca, ou um sinônimo em português dela ("grafico" ou "gráfico" acham os "chart"). Busca vazia =
 * todos, na ordem original.
 */
export function filtrarIcones(icones: readonly string[], busca: string): string[] {
  const termo = normalizarBusca(busca).replace(/^pi-/, '');
  if (!termo) {
    return [...icones];
  }
  const partes = new Set([termo]);
  for (const [palavra, ingles] of Object.entries(SINONIMOS)) {
    if (palavra.startsWith(termo) || termo.startsWith(palavra)) {
      ingles.forEach((p) => partes.add(p));
    }
  }
  return icones.filter((icone) => [...partes].some((p) => icone.includes(p)));
}

/** O nome que o leitor de tela anuncia: sem o "pi-" e com espaços ("chart line"). */
export function nomeDoIcone(icone: string): string {
  return icone.replace(/^pi-/, '').replace(/-/g, ' ');
}
