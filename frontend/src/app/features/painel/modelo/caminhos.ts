import { Campo, Entidade, TipoDado } from '../../../core/metadados/modelo';

/**
 * Caminhos por referência no front (RN89, docs/api/painel.md): "relatorio_teste.ambiente" é o campo `ambiente` do registro
 * apontado pelo campo REFERENCIA `relatorio_teste`. A API aceita até 2 saltos e valida de novo; aqui servem para montar as
 * opções da análise personalizada (com rótulos como "Relatório › Ambiente") e conferir a configuração dos widgets. Os campos
 * de cada entidade vêm dos metadados (`entidadeReferenciada` diz a entidade apontada). Funções puras (caminhos.spec.ts).
 */

export const MAXIMO_SALTOS = 2;

export interface CaminhoResolvido {
  /** Como vai para a API: "relatorio_teste.ambiente". */
  caminho: string;
  /** Os campos REFERENCIA atravessados, na ordem. */
  saltos: Campo[];
  /** O campo do fim do caminho. */
  campo: Campo;
  /** Para a tela: "Relatório › Ambiente"; sem saltos, o nome do campo. */
  rotulo: string;
}

export type ResultadoCaminho = CaminhoResolvido | { erro: string };

export function ehErro(resultado: ResultadoCaminho): resultado is { erro: string } {
  return 'erro' in resultado;
}

export function resolverCaminho(entidades: Entidade[], entidade: Entidade, caminho: string): ResultadoCaminho {
  const partes = caminho.split('.');
  if (partes.length - 1 > MAXIMO_SALTOS) {
    return { erro: `'${caminho}': no máximo ${MAXIMO_SALTOS} saltos por referência.` };
  }
  let dono = entidade;
  const saltos: Campo[] = [];
  for (let i = 0; i < partes.length; i++) {
    const campo = dono.campos.find((c) => c.chave === partes[i]);
    if (!campo) {
      return { erro: `'${partes.slice(0, i + 1).join('.')}': campo desconhecido em '${dono.nome}'.` };
    }
    if (i === partes.length - 1) {
      return { caminho, saltos, campo, rotulo: rotuloDoCaminho(saltos, campo) };
    }
    const apontada = campo.tipo === 'REFERENCIA' ? entidades.find((e) => e.chave === campo.entidadeReferenciada) : null;
    if (!apontada) {
      return { erro: `'${caminho}': '${campo.chave}' não é uma referência a uma entidade disponível.` };
    }
    saltos.push(campo);
    dono = apontada;
  }
  return { erro: `'${caminho}': caminho vazio.` };
}

export function rotuloDoCaminho(saltos: Campo[], campo: Campo): string {
  return [...saltos.map((s) => s.nome), campo.nome].join(' › ');
}

/**
 * Os campos da entidade e os alcançáveis por UM salto (os da entidade apontada por cada REFERENCIA), que passam no filtro de
 * tipos. Na ordem: primeiro os da própria entidade, depois os de cada referência, na ordem dos metadados.
 */
export function camposAlcancaveis(
  entidades: Entidade[],
  entidade: Entidade,
  tipos: readonly TipoDado[],
): CaminhoResolvido[] {
  const aceita = (campo: Campo) => tipos.includes(campo.tipo);
  const diretos = entidade.campos.filter(aceita).map((campo) => ({
    caminho: campo.chave,
    saltos: [],
    campo,
    rotulo: campo.nome,
  }));
  const porReferencia = entidade.campos
    .filter((salto) => salto.tipo === 'REFERENCIA')
    .flatMap((salto) => {
      const apontada = entidades.find((e) => e.chave === salto.entidadeReferenciada);
      return (apontada?.campos ?? []).filter(aceita).map((campo) => ({
        caminho: `${salto.chave}.${campo.chave}`,
        saltos: [salto],
        campo,
        rotulo: rotuloDoCaminho([salto], campo),
      }));
    });
  return [...diretos, ...porReferencia];
}

/** "Percentil 95 (ms)" → "ms"; "Taxa de erro (%)" → "%". Um texto curto entre parênteses no fim do nome é a unidade. */
export function unidadeDoCampo(campo: Campo): string | null {
  const unidade = /\(([^()]{1,5})\)\s*$/.exec(campo.nome);
  return unidade ? unidade[1] : null;
}

export const TIPOS_DIMENSAO: readonly TipoDado[] = ['LISTA', 'BOOLEANO', 'REFERENCIA', 'TEXTO', 'DATA', 'DATA_HORA'];
export const TIPOS_DATA: readonly TipoDado[] = ['DATA', 'DATA_HORA'];
export const TIPOS_NUMERICOS: readonly TipoDado[] = ['INTEIRO', 'DECIMAL'];
export const TIPOS_FILTRO: readonly TipoDado[] = [
  'TEXTO',
  'TEXTO_LONGO',
  'INTEIRO',
  'DECIMAL',
  'BOOLEANO',
  'DATA',
  'DATA_HORA',
  'LISTA',
  'REFERENCIA',
  'URL',
];
