/**
 * Preferências de tela guardadas no localStorage (menu recolhido, grupo aberto...). É só conveniência: sem armazenamento
 * (navegação privada, bloqueado), vale o padrão e a escolha dura até sair da tela.
 */
export function lerPreferenciaLocal(chave: string): string | null {
  try {
    return localStorage.getItem(chave);
  } catch {
    return null;
  }
}

export function gravarPreferenciaLocal(chave: string, valor: string): void {
  try {
    localStorage.setItem(chave, valor);
  } catch {
    // Sem armazenamento: vale só até sair da tela.
  }
}

export function lerBooleanoLocal(chave: string, padrao: boolean): boolean {
  const valor = lerPreferenciaLocal(chave);
  return valor === null ? padrao : valor === 'true';
}
