import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { PermissoesService } from './permissoes.service';

/**
 * RN26: as rotas de administração (/configuracoes/usuarios, /equipes e /areas) só abrem para o ADMINISTRADOR. Os demais vão para a página de "sem permissão" (a mesma do painel),
 * sem trocar a URL. É conveniência: a API responde 403 de qualquer jeito.
 */
export const administracaoGuard: CanActivateFn = () => {
  if (inject(PermissoesService).podeAdministrar()) {
    return true;
  }
  void inject(Router).navigate(['/sem-permissao'], { skipLocationChange: true });
  return false;
};
