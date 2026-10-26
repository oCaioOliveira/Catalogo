import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from './auth.service';

/** Rotas que exigem login (todas, menos o login). Com a troca de senha pendente, só a tela de troca abre. */
export const autenticadoGuard: CanActivateFn = (_rota, estado) => {
  const auth = inject(AuthService);
  const router = inject(Router);

  const usuario = auth.usuario();
  if (!auth.token() || !usuario) {
    return router.createUrlTree(['/login'], { queryParams: { returnUrl: estado.url } });
  }
  if (usuario.deveTrocarSenha && !estado.url.startsWith('/trocar-senha')) {
    return router.createUrlTree(['/trocar-senha']);
  }
  return true;
};

/** A tela de login para quem já está logado leva à tela inicial. */
export const anonimoGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  return auth.logado() ? inject(Router).createUrlTree(['/']) : true;
};
