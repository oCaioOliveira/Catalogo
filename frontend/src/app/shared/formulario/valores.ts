import { Campo, OpcaoReferencia } from '../../core/metadados/modelo';
import { dataDeTextoIso, dataHoraParaTextoIso, dataParaTextoIso } from '../formatacao/datas';

/**
 * Valor de um campo NO FORMULÁRIO, conforme o tipo:
 *   TEXTO, TEXTO_LONGO, LISTA, URL → string;  INTEIRO, DECIMAL → number;  BOOLEANO → boolean;
 *   DATA, DATA_HORA → Date;  REFERENCIA → { id, rotulo }.  Vazio → null.
 */
export type ValorCampo = string | number | boolean | Date | OpcaoReferencia | null;

/** Da API (dados do registro) para o formulário. */
export function valorDaApi(campo: Campo, valor: unknown): ValorCampo {
  if (valor === null || valor === undefined) {
    return campo.tipo === 'BOOLEANO' ? false : null;
  }
  switch (campo.tipo) {
    case 'DATA':
      return typeof valor === 'string' ? dataDeTextoIso(valor) : null;
    case 'DATA_HORA':
      return typeof valor === 'string' ? new Date(valor) : null;
    case 'INTEIRO':
    case 'DECIMAL':
      return Number(valor);
    case 'BOOLEANO':
      return valor === true;
    case 'REFERENCIA':
      return valor as OpcaoReferencia;
    default:
      return String(valor);
  }
}

/** Do formulário para o corpo do POST/PUT (RN04): datas em ISO, referência só com o id, texto sem espaços nas pontas. */
export function valorParaApi(campo: Campo, valor: ValorCampo): unknown {
  if (valor === null || valor === undefined) {
    return null;
  }
  switch (campo.tipo) {
    case 'DATA':
      return valor instanceof Date ? dataParaTextoIso(valor) : null;
    case 'DATA_HORA':
      return valor instanceof Date ? dataHoraParaTextoIso(valor) : null;
    case 'REFERENCIA':
      return (valor as OpcaoReferencia).id ?? null;
    case 'TEXTO':
    case 'TEXTO_LONGO':
    case 'LISTA':
    case 'URL': {
      const texto = String(valor).trim();
      return texto === '' ? null : texto;
    }
    default:
      return valor;
  }
}
