// Regras do tema claro/escuro. Funções puras, sem Angular: preferencia-tema.spec.ts.

/** O que a pessoa escolhe no menu do usuário. "sistema" (o padrão) segue o sistema operacional. */
export type PreferenciaTema = 'claro' | 'escuro' | 'sistema';

/** O tema que fica na tela. */
export type TemaAplicado = 'claro' | 'escuro';

export const CHAVE_TEMA = 'catalogo.tema';

export const OPCOES_TEMA: readonly { valor: PreferenciaTema; rotulo: string; icone: string }[] = [
  { valor: 'claro', rotulo: 'Claro', icone: 'pi pi-sun' },
  { valor: 'escuro', rotulo: 'Escuro', icone: 'pi pi-moon' },
  { valor: 'sistema', rotulo: 'Seguir o sistema', icone: 'pi pi-desktop' },
];

/** O botão de tema (cabeçalho e login) alterna nesta ordem: claro → escuro → seguir o sistema → claro. */
export function proximaPreferencia(atual: PreferenciaTema): PreferenciaTema {
  const ordem = OPCOES_TEMA.map((opcao) => opcao.valor);
  return ordem[(ordem.indexOf(atual) + 1) % ordem.length];
}

/** Um texto salvo vira preferência; qualquer outro valor (ou nada) é "sistema". */
export function lerPreferencia(texto: string | null | undefined): PreferenciaTema {
  return texto === 'claro' || texto === 'escuro' || texto === 'sistema' ? texto : 'sistema';
}

/** "sistema" segue o sistema operacional (prefers-color-scheme); as outras valem como estão. */
export function resolverTema(preferencia: PreferenciaTema, sistemaEscuro: boolean): TemaAplicado {
  if (preferencia === 'sistema') {
    return sistemaEscuro ? 'escuro' : 'claro';
  }
  return preferencia;
}
