/**
 * Os ícones do PrimeIcons (`.pi`) são desenhados com um caractere de fonte; sem `aria-hidden`, esse caractere
 * entra no nome acessível (" Salvar") e o leitor de tela lê lixo. Este observador marca TODO ícone `.pi`
 * da página como decorativo. Ícone com significado precisa de texto ao lado (visível ou `.sr-only`), e botão só
 * com ícone precisa de `ariaLabel`.
 */
export function esconderIconesDecorativos(raiz: HTMLElement): MutationObserver {
  const marcar = (elemento: Element) => {
    if (elemento.classList.contains('pi') && !elemento.hasAttribute('aria-hidden')) {
      elemento.setAttribute('aria-hidden', 'true');
    }
    elemento.querySelectorAll('.pi:not([aria-hidden])').forEach((icone) => icone.setAttribute('aria-hidden', 'true'));
  };

  marcar(raiz);
  const observador = new MutationObserver((mudancas) => {
    for (const mudanca of mudancas) {
      mudanca.addedNodes.forEach((no) => {
        if (no instanceof Element) {
          marcar(no);
        }
      });
      if (mudanca.type === 'attributes' && mudanca.target instanceof Element) {
        marcar(mudanca.target);
      }
    }
  });
  observador.observe(raiz, { childList: true, subtree: true, attributes: true, attributeFilter: ['class'] });
  return observador;
}
