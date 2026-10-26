import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';
import { lerProblema } from '../erros/problema';
import { AuthService } from './auth.service';

const URL_LOGIN = '/api/v1/auth/login';

/**
 * Autenticação de toda chamada à API:
 *   - envia `Authorization: Bearer <token>`;
 *   - 401 (token ausente, expirado ou usuário desativado): limpa a sessão e vai para o login, com o returnUrl;
 *   - 403 TROCA_DE_SENHA_OBRIGATORIA: vai para a tela de troca de senha (RN25).
 * O 401 do próprio login (senha errada) não é sessão expirada: fica para a tela de login.
 */
export const authInterceptor: HttpInterceptorFn = (requisicao, proximo) => {
  const auth = inject(AuthService);
  const router = inject(Router);

  const token = auth.token();
  const comToken = token ? requisicao.clone({ setHeaders: { Authorization: `Bearer ${token}` } }) : requisicao;

  return proximo(comToken).pipe(
    catchError((erro: unknown) => {
      if (erro instanceof HttpErrorResponse && !requisicao.url.endsWith(URL_LOGIN)) {
        if (erro.status === 401) {
          auth.limparSessao();
          const atual = router.url.startsWith('/login') ? undefined : router.url;
          void router.navigate(['/login'], { queryParams: atual ? { returnUrl: atual } : {} });
        } else if (erro.status === 403 && lerProblema(erro).codigo === 'TROCA_DE_SENHA_OBRIGATORIA') {
          void router.navigate(['/trocar-senha']);
        }
      }
      return throwError(() => erro);
    }),
  );
};
