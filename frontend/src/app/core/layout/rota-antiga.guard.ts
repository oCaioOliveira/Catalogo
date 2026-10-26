import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { destinoDaUrlAntiga } from './rotas-antigas';

/**
 * As rotas antigas (/admin/..., /configuracoes-equipe/...) levam à área de configurações, COM os parâmetros da URL
 * (?grupo=, ?busca=...). Um guard, e não `redirectTo`: ele recebe a URL inteira da navegação (o `redirectTo` com função não
 * recebia a query neste caso, e os filtros se perdiam).
 */
export const rotaAntigaGuard: CanActivateFn = (_rota, estado) => {
  const router = inject(Router);
  const destino = destinoDaUrlAntiga(estado.url);
  return destino ? router.parseUrl(destino) : true;
};
