import JSZip from 'jszip';

/**
 * Leitura e preenchimento de XLSX nos E2E da importação, direto no XML do pacote (JSZip). O modelo e o relatório são gerados pelo
 * ClosedXML, que grava os elementos com prefixo de namespace (`<x:row>`): o Excel abre normalmente, mas o exceljs não lê. Aqui só o
 * necessário para os testes: as abas, o texto das células, as colunas com lista suspensa e acrescentar linhas na primeira aba.
 */

export interface AbaLida {
  nome: string;
  oculta: boolean;
  /** As células em texto, por linha e coluna (a partir de 0). Linhas e células vazias ficam como "" ou faltando. */
  linhas: string[][];
  /** As colunas (a partir de 1) com validação de lista (a lista suspensa do Excel). */
  colunasComLista: number[];
}

const QUALQUER = '(?:\\w+:)?';

function decodificar(texto: string): string {
  return texto
    .replace(/&lt;/g, '<')
    .replace(/&gt;/g, '>')
    .replace(/&quot;/g, '"')
    .replace(/&apos;/g, "'")
    .replace(/&amp;/g, '&');
}

function escapar(texto: string): string {
  return texto.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
}

function atributo(atributos: string, nome: string): string | null {
  return new RegExp(`\\b${nome}="([^"]*)"`).exec(atributos)?.[1] ?? null;
}

/** "C12" → coluna 3 (a partir de 1) e linha 12. */
function posicao(referencia: string): { coluna: number; linha: number } {
  const [, letras, numero] = /^([A-Z]+)(\d+)$/.exec(referencia)!;
  const coluna = [...letras].reduce((total, letra) => total * 26 + (letra.charCodeAt(0) - 64), 0);
  return { coluna, linha: Number(numero) };
}

function letrasDaColuna(coluna: number): string {
  let letras = '';
  for (let n = coluna; n > 0; n = Math.floor((n - 1) / 26)) {
    letras = String.fromCharCode(65 + ((n - 1) % 26)) + letras;
  }
  return letras;
}

function textosDe(xml: string): string {
  return [...xml.matchAll(new RegExp(`<${QUALQUER}t(?:\\s[^>]*)?>([\\s\\S]*?)</${QUALQUER}t>`, 'g'))]
    .map((m) => decodificar(m[1]))
    .join('');
}

/** As abas do pacote, na ordem do workbook, com o caminho do XML de cada uma. */
async function abasDoPacote(zip: JSZip): Promise<{ nome: string; oculta: boolean; caminho: string }[]> {
  const workbook = await zip.file('xl/workbook.xml')!.async('string');
  const relacoes = await zip.file('xl/_rels/workbook.xml.rels')!.async('string');
  const alvos = new Map(
    [...relacoes.matchAll(/<Relationship\b([^>]*)\/>/g)].map((m) => [atributo(m[1], 'Id'), atributo(m[1], 'Target')]),
  );
  return [...workbook.matchAll(new RegExp(`<${QUALQUER}sheet\\b([^>]*)/>`, 'g'))].map((m) => {
    const alvo = alvos.get(atributo(m[1], 'r:id'))!.replace(/^\/?xl\//, '');
    return {
      nome: decodificar(atributo(m[1], 'name') ?? ''),
      oculta: atributo(m[1], 'state') === 'hidden',
      caminho: `xl/${alvo}`,
    };
  });
}

export async function lerPlanilha(arquivo: Buffer): Promise<AbaLida[]> {
  const zip = await JSZip.loadAsync(arquivo);
  const compartilhadas = zip.file('xl/sharedStrings.xml');
  const textos = compartilhadas
    ? [
        ...(await compartilhadas.async('string')).matchAll(
          new RegExp(`<${QUALQUER}si>([\\s\\S]*?)</${QUALQUER}si>`, 'g'),
        ),
      ].map((m) => textosDe(m[1]))
    : [];

  const abas: AbaLida[] = [];
  for (const aba of await abasDoPacote(zip)) {
    const xml = await zip.file(aba.caminho)!.async('string');
    const linhas: string[][] = [];
    for (const celula of xml.matchAll(new RegExp(`<${QUALQUER}c\\b([^>]*?)(?:/>|>([\\s\\S]*?)</${QUALQUER}c>)`, 'g'))) {
      const referencia = atributo(celula[1], 'r');
      if (!referencia) {
        continue;
      }
      const { coluna, linha } = posicao(referencia);
      const conteudo = celula[2] ?? '';
      const valor = new RegExp(`<${QUALQUER}v>([\\s\\S]*?)</${QUALQUER}v>`).exec(conteudo)?.[1] ?? '';
      const tipo = atributo(celula[1], 't');
      const texto =
        tipo === 's' ? (textos[Number(valor)] ?? '') : tipo === 'inlineStr' ? textosDe(conteudo) : decodificar(valor);
      (linhas[linha - 1] ??= [])[coluna - 1] = texto;
    }
    const colunasComLista = new Set<number>();
    for (const validacao of xml.matchAll(
      new RegExp(`<${QUALQUER}dataValidation\\b([^>]*)>([\\s\\S]*?)</${QUALQUER}dataValidation>`, 'g'),
    )) {
      if (atributo(validacao[1], 'type') !== 'list') {
        continue;
      }
      const intervalo =
        atributo(validacao[1], 'sqref') ?? new RegExp(`<${QUALQUER}sqref>([^<]*)<`).exec(validacao[2])?.[1] ?? '';
      for (const parte of intervalo.split(' ').filter(Boolean)) {
        colunasComLista.add(posicao(parte.split(':')[0]).coluna);
      }
    }
    abas.push({
      nome: aba.nome,
      oculta: aba.oculta,
      linhas: Array.from(linhas, (l) => Array.from(l ?? [], (c) => c ?? '')),
      colunasComLista: [...colunasComLista].sort((a, b) => a - b),
    });
  }
  return abas;
}

/**
 * Acrescenta linhas na primeira aba (a partir da linha 2), mantendo o resto do modelo (listas suspensas, formatos, Instruções).
 * Texto vira célula de texto; número, célula numérica.
 */
export async function preencherPrimeiraAba(arquivo: Buffer, linhas: (string | number)[][]): Promise<Buffer> {
  const zip = await JSZip.loadAsync(arquivo);
  const [primeira] = await abasDoPacote(zip);
  const xml = await zip.file(primeira.caminho)!.async('string');
  const prefixo = new RegExp(`<(\\w+:)?sheetData>`).exec(xml)?.[1] ?? '';
  const novas = linhas
    .map((linha, i) => {
      const numero = i + 2;
      const celulas = linha
        .map((valor, c) => {
          const referencia = `${letrasDaColuna(c + 1)}${numero}`;
          return typeof valor === 'number'
            ? `<${prefixo}c r="${referencia}"><${prefixo}v>${valor}</${prefixo}v></${prefixo}c>`
            : `<${prefixo}c r="${referencia}" t="inlineStr"><${prefixo}is><${prefixo}t>${escapar(valor)}</${prefixo}t></${prefixo}is></${prefixo}c>`;
        })
        .join('');
      return `<${prefixo}row r="${numero}">${celulas}</${prefixo}row>`;
    })
    .join('');
  zip.file(primeira.caminho, xml.replace(`</${prefixo}sheetData>`, `${novas}</${prefixo}sheetData>`));
  return zip.generateAsync({ type: 'nodebuffer' });
}
