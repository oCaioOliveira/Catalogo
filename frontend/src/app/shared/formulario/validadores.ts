import { AbstractControl, ValidationErrors, ValidatorFn } from '@angular/forms';
import { Campo } from '../../core/metadados/modelo';

// Validação no front a partir dos metadados, igual à do back-end (RN03 e RN04). O back-end valida de novo:
// isto só evita uma ida ao servidor e mostra o erro mais cedo.

const MENOR_INTEIRO = -2147483648;
const MAIOR_INTEIRO = 2147483647;

/** RN03: vazio é ausente, null ou texto só com espaços. */
export function vazio(valor: unknown): boolean {
  return valor === null || valor === undefined || (typeof valor === 'string' && valor.trim() === '');
}

export function obrigatorio(): ValidatorFn {
  return (controle: AbstractControl): ValidationErrors | null => (vazio(controle.value) ? { obrigatorio: true } : null);
}

/** TEXTO: até `maximo` caracteres, contados sem os espaços das pontas. */
export function tamanhoMaximo(maximo: number): ValidatorFn {
  return (controle) => {
    const valor = controle.value;
    if (typeof valor !== 'string') {
      return null;
    }
    const atual = valor.trim().length;
    return atual > maximo ? { tamanhoMaximo: { maximo, atual } } : null;
  };
}

/** INTEIRO: número inteiro de 32 bits. */
export function inteiro(): ValidatorFn {
  return (controle) => {
    const valor = controle.value;
    if (typeof valor !== 'number') {
      return null;
    }
    if (!Number.isInteger(valor)) {
      return { inteiro: true };
    }
    return valor < MENOR_INTEIRO || valor > MAIOR_INTEIRO
      ? { faixa: { minimo: MENOR_INTEIRO, maximo: MAIOR_INTEIRO } }
      : null;
  };
}

/** DECIMAL(precisao, escala): até `escala` casas e até `precisao − escala` dígitos antes da vírgula. */
export function decimal(precisao: number, escala: number): ValidatorFn {
  return (controle) => {
    const valor = controle.value;
    if (typeof valor !== 'number') {
      return null;
    }
    const [parteInteira, parteDecimal = ''] = Math.abs(valor).toString().split('.');
    if (parteDecimal.replace(/0+$/, '').length > escala) {
      return { escala: { escala } };
    }
    const digitos = parteInteira.replace(/^0+/, '').length;
    return digitos > precisao - escala ? { digitos: { maximo: precisao - escala } } : null;
  };
}

/** URL: o mesmo limite do back-end (UrlValidador, ADR 0042). */
export const MAXIMO_CARACTERES_URL = 2000;

/**
 * URL (RN04): endereço ABSOLUTO com esquema http ou https e um host, sem espaços, até 2000 caracteres (contados sem os espaços
 * das pontas, que são removidos). Igual ao UrlValidador do back-end, que confere de novo.
 */
export function url(): ValidatorFn {
  return (controle) => {
    const valor = controle.value;
    if (typeof valor !== 'string' || vazio(valor)) {
      return null;
    }
    return erroDeUrl(valor.trim());
  };
}

/** A regra da URL como função pura (também usada pelo botão "Abrir link", que só aparece com uma URL válida). */
export function erroDeUrl(texto: string): ValidationErrors | null {
  if (texto.length > MAXIMO_CARACTERES_URL) {
    return { tamanhoMaximo: { maximo: MAXIMO_CARACTERES_URL, atual: texto.length } };
  }
  if (/\s/.test(texto)) {
    return { urlComEspacos: true };
  }
  if (!/^https?:\/\//i.test(texto)) {
    return { url: true };
  }
  try {
    const endereco = new URL(texto);
    return endereco.hostname ? null : { url: true };
  } catch {
    return { url: true };
  }
}

/** LISTA: uma das opções (diferencia maiúsculas). */
export function umaDasOpcoes(opcoes: string[]): ValidatorFn {
  return (controle) => {
    const valor = controle.value;
    return vazio(valor) || opcoes.includes(String(valor)) ? null : { opcao: { opcoes } };
  };
}

/** Os validadores de um campo, conforme os metadados. */
export function validadoresDoCampo(campo: Campo): ValidatorFn[] {
  const validadores: ValidatorFn[] = [];
  // BOOLEANO nunca está vazio no formulário (o toggle começa em "não").
  if (campo.obrigatorio && campo.tipo !== 'BOOLEANO') {
    validadores.push(obrigatorio());
  }
  switch (campo.tipo) {
    case 'TEXTO':
      if (campo.tamanhoMaximo) {
        validadores.push(tamanhoMaximo(campo.tamanhoMaximo));
      }
      break;
    case 'INTEIRO':
      validadores.push(inteiro());
      break;
    case 'DECIMAL':
      validadores.push(decimal(campo.precisao ?? 18, campo.escala ?? 2));
      break;
    case 'LISTA':
      validadores.push(umaDasOpcoes(campo.opcoes));
      break;
    case 'URL':
      validadores.push(url());
      break;
  }
  return validadores;
}

/** Mensagens em português dos erros de um controle (inclusive os que vieram do servidor). */
export function mensagensDeErro(erros: ValidationErrors | null): string[] {
  if (!erros) {
    return [];
  }
  const mensagens: string[] = [];
  for (const [chave, detalhe] of Object.entries(erros)) {
    switch (chave) {
      case 'obrigatorio':
        mensagens.push('Campo obrigatório.');
        break;
      case 'tamanhoMaximo':
        mensagens.push(`Deve ter no máximo ${detalhe.maximo} caracteres (tem ${detalhe.atual}).`);
        break;
      case 'inteiro':
        mensagens.push('Deve ser um número inteiro.');
        break;
      case 'faixa':
        mensagens.push('Número fora da faixa permitida.');
        break;
      case 'escala':
        mensagens.push(`Deve ter no máximo ${detalhe.escala} casas decimais.`);
        break;
      case 'digitos':
        mensagens.push(`Deve ter no máximo ${detalhe.maximo} dígitos antes da vírgula.`);
        break;
      case 'opcao':
        mensagens.push('Escolha uma das opções.');
        break;
      case 'url':
        mensagens.push(
          'Informe uma URL completa, começando com http:// ou https:// (ex.: https://exemplo.com.br/pagina).',
        );
        break;
      case 'urlComEspacos':
        mensagens.push('A URL não pode ter espaços.');
        break;
      case 'servidor':
        mensagens.push(...(detalhe as string[]));
        break;
      default:
        mensagens.push('Valor inválido.');
    }
  }
  return mensagens;
}
