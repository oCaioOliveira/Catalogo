import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from './auth.service';

/** Tela de troca de senha: exige estar logado, mesmo com a troca pendente. */
export const sessaoGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  return auth.token() && auth.usuario() ? true : inject(Router).createUrlTree(['/login']);
};
