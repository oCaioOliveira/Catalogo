/**
 * Onde o token fica: sessionStorage (docs/adr/0035). Sobrevive ao recarregar a página e some ao fechar a
 * aba. O acesso pode falhar (navegação privada, armazenamento bloqueado): aí a sessão vive só em memória.
 */
const CHAVE_TOKEN = 'catalogo.token';

export function lerToken(): string | null {
  try {
    return sessionStorage.getItem(CHAVE_TOKEN);
  } catch {
    return null;
  }
}

export function gravarToken(token: string | null): void {
  try {
    if (token) {
      sessionStorage.setItem(CHAVE_TOKEN, token);
    } else {
      sessionStorage.removeItem(CHAVE_TOKEN);
    }
  } catch {
    // Sem armazenamento: o token fica só no signal do AuthService (some ao recarregar).
  }
}
