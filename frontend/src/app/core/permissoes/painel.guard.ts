import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { PermissoesService } from './permissoes.service';

/**
 * RN78: as rotas do painel (/painel/:grupo...) só abrem para GESTOR e ADMINISTRADOR. INTERNO e EXTERNO vão para a página de
 * "sem permissão", sem trocar a URL (o endereço pedido continua visível). É conveniência: a API responde 403 de qualquer jeito.
 */
export const painelGuard: CanActivateFn = () => {
  const router = inject(Router);
  if (inject(PermissoesService).podeVerPainel()) {
    return true;
  }
  void router.navigate(['/sem-permissao'], { skipLocationChange: true });
  return false;
};
