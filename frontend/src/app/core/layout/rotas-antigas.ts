// As rotas antigas da administração e das configurações da equipe continuam valendo (links salvos, favoritos): elas redirecionam
// para a área de configurações. Funções puras: rotas-antigas.spec.ts. Os parâmetros da URL (?grupo=...) são mantidos.

/** /admin/... → /configuracoes/...: "listagens" virou "areas" (Áreas de gestão); /admin sozinho abre Usuários. */
export function destinoDaAdministracaoAntiga(segmentos: readonly string[]): string {
  if (segmentos.length === 0) {
    return '/configuracoes/usuarios';
  }
  const [primeiro, ...resto] = segmentos;
  return ['/configuracoes', primeiro === 'listagens' ? 'areas' : primeiro, ...resto].join('/');
}

/** /configuracoes-equipe/... → /configuracoes/equipe/... */
export function destinoDasConfiguracoesAntigas(segmentos: readonly string[]): string {
  return ['/configuracoes/equipe', ...segmentos].join('/');
}

/**
 * O destino de uma URL antiga inteira ("/admin/usuarios?busca=x" → "/configuracoes/usuarios?busca=x"), ou null se ela não for
 * antiga. A query e o fragmento são mantidos.
 */
export function destinoDaUrlAntiga(url: string): string | null {
  const [caminhoEQuery, fragmento] = url.split('#');
  const [caminho, query] = caminhoEQuery.split('?');
  const [primeiro, ...resto] = caminho.split('/').filter((s) => s.length > 0);
  let destino: string;
  if (primeiro === 'admin') {
    destino = destinoDaAdministracaoAntiga(resto);
  } else if (primeiro === 'configuracoes-equipe') {
    destino = destinoDasConfiguracoesAntigas(resto);
  } else {
    return null;
  }
  return destino + (query ? `?${query}` : '') + (fragmento ? `#${fragmento}` : '');
}
