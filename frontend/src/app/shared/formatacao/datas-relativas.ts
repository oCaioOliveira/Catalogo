// Datas relativas ("há 3 horas"), para onde o "quando" importa mais que a data exata (Início, histórico, último acesso). A data
// completa fica sempre na dica (DataRelativaComponent). Funções puras: datas-relativas.spec.ts.

const MINUTO = 60_000;
const HORA = 60 * MINUTO;
const DIA = 24 * HORA;

function doisDigitos(n: number): string {
  return String(n).padStart(2, '0');
}

function mesmoDia(a: Date, b: Date): boolean {
  return a.getFullYear() === b.getFullYear() && a.getMonth() === b.getMonth() && a.getDate() === b.getDate();
}

/** "dd/MM/yyyy às HH:mm", no fuso do navegador: a data completa da dica. */
export function formatarDataCompleta(data: Date): string {
  return (
    `${doisDigitos(data.getDate())}/${doisDigitos(data.getMonth() + 1)}/${data.getFullYear()} às ` +
    `${doisDigitos(data.getHours())}:${doisDigitos(data.getMinutes())}`
  );
}

/**
 * "agora mesmo", "há 5 minutos", "há 3 horas", "ontem às 14:30", "há 4 dias"; de uma semana para trás, a data ("12/09/2026").
 * Datas no futuro (relógio do navegador atrasado) contam como "agora mesmo".
 */
export function formatarRelativo(data: Date, agora: Date): string {
  const diferenca = agora.getTime() - data.getTime();
  if (diferenca < MINUTO) {
    return 'agora mesmo';
  }
  if (diferenca < HORA) {
    const minutos = Math.floor(diferenca / MINUTO);
    return minutos === 1 ? 'há 1 minuto' : `há ${minutos} minutos`;
  }
  if (diferenca < DIA && mesmoDia(data, agora)) {
    const horas = Math.floor(diferenca / HORA);
    return horas === 1 ? 'há 1 hora' : `há ${horas} horas`;
  }
  const ontem = new Date(agora.getFullYear(), agora.getMonth(), agora.getDate() - 1);
  if (mesmoDia(data, ontem)) {
    return `ontem às ${doisDigitos(data.getHours())}:${doisDigitos(data.getMinutes())}`;
  }
  const dias = Math.round(
    (new Date(agora.getFullYear(), agora.getMonth(), agora.getDate()).getTime() -
      new Date(data.getFullYear(), data.getMonth(), data.getDate()).getTime()) /
      DIA,
  );
  if (dias < 7) {
    return `há ${dias} dias`;
  }
  return `${doisDigitos(data.getDate())}/${doisDigitos(data.getMonth() + 1)}/${data.getFullYear()}`;
}
