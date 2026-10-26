/**
 * Conversões de data entre a tela (Date, no fuso do navegador) e a API (ISO 8601). Na tela as datas são
 * "dd/MM/yyyy" e "dd/MM/yyyy HH:mm" (DatePipe com LOCALE_ID pt-BR); para a API, sempre ISO.
 */

function doisDigitos(n: number): string {
  return String(n).padStart(2, '0');
}

/** DATA: "2026-10-21" → Date à meia-noite LOCAL (sem fuso, para o dia não mudar). */
export function dataDeTextoIso(texto: string): Date | null {
  const partes = /^(\d{4})-(\d{2})-(\d{2})$/.exec(texto);
  return partes ? new Date(Number(partes[1]), Number(partes[2]) - 1, Number(partes[3])) : null;
}

/** DATA: Date → "2026-10-21" (o dia que o usuário vê). */
export function dataParaTextoIso(data: Date): string {
  return `${data.getFullYear()}-${doisDigitos(data.getMonth() + 1)}-${doisDigitos(data.getDate())}`;
}

/**
 * DATA_HORA: Date → "2026-10-21T14:30:00-03:00", com o fuso do navegador. A API exige o fuso (RN04) e
 * devolve em UTC ("...Z"), que `new Date(...)` converte de volta para a hora local.
 */
export function dataHoraParaTextoIso(data: Date): string {
  const minutosFuso = -data.getTimezoneOffset();
  const sinal = minutosFuso >= 0 ? '+' : '-';
  const fuso = `${sinal}${doisDigitos(Math.floor(Math.abs(minutosFuso) / 60))}:${doisDigitos(Math.abs(minutosFuso) % 60)}`;
  return (
    `${dataParaTextoIso(data)}T${doisDigitos(data.getHours())}:${doisDigitos(data.getMinutes())}:` +
    `${doisDigitos(data.getSeconds())}${fuso}`
  );
}
