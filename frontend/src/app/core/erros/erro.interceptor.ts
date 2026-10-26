import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { MessageService } from 'primeng/api';
import { catchError, throwError } from 'rxjs';
import { STATUS_TRATADOS_PELO_COMPONENTE } from './contexto-erros';
import { mensagemDoErro } from './mensagens-erro';
import { lerProblema } from './problema';

/**
 * Mostra um toast com a mensagem amigável de qualquer erro da API (mapa em mensagens-erro.ts). Ficam de fora:
 *   - os status que a chamada pediu para tratar sozinha (STATUS_TRATADOS_PELO_COMPONENTE, ex.: 400 no formulário);
 *   - 401 e a troca de senha obrigatória, que o interceptor de autenticação resolve indo para outra tela.
 * Erros 5xx mostram o traceId, para o usuário informar ao suporte. O erro continua para quem chamou.
 */
export const erroInterceptor: HttpInterceptorFn = (requisicao, proximo) => {
  const mensagens = inject(MessageService);

  return proximo(requisicao).pipe(
    catchError((erro: unknown) => {
      if (
        erro instanceof HttpErrorResponse &&
        !requisicao.context.get(STATUS_TRATADOS_PELO_COMPONENTE).includes(erro.status)
      ) {
        const problema = lerProblema(erro);
        const resolvidoPeloLogin = problema.status === 401 || problema.codigo === 'TROCA_DE_SENHA_OBRIGATORIA';
        if (!resolvidoPeloLogin) {
          const suporte =
            problema.status >= 500 && problema.traceId ? `Código para o suporte: ${problema.traceId}` : undefined;
          mensagens.add({
            severity: problema.status >= 500 || problema.status === 0 ? 'error' : 'warn',
            summary: mensagemDoErro(problema.codigo, problema.status),
            detail: suporte ?? (problema.status < 500 ? (problema.detalhe ?? undefined) : undefined),
            life: problema.status >= 500 ? 15000 : 6000,
          });
        }
      }
      return throwError(() => erro);
    }),
  );
};
