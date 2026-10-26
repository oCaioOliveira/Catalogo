import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { PermissoesService } from './permissoes.service';

/**
 * RN33, RN36 e RN40: as configurações da equipe (aprovação e modelos de avaliação) e a fila de aprovação só abrem para GESTOR e
 * ADMINISTRADOR. INTERNO e EXTERNO vão para "sem permissão", sem trocar a URL. É conveniência: a API responde 403 de qualquer jeito.
 */
export const configuracaoEquipeGuard: CanActivateFn = () => {
  const router = inject(Router);
  if (inject(PermissoesService).podeConfigurarEquipe()) {
    return true;
  }
  void router.navigate(['/sem-permissao'], { skipLocationChange: true });
  return false;
};
