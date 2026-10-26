import { inject } from '@angular/core';
import { CanDeactivateFn } from '@angular/router';
import { ConfirmationService } from 'primeng/api';

/** Telas com formulário (ou lista editável) que avisam antes de sair com mudanças não salvas. */
export interface ComAlteracoesPendentes {
  temAlteracoesPendentes(): boolean;
}

/** O aviso padrão "Sair sem salvar?": resolve true para sair (descartando) e false para continuar editando. */
export function confirmarSaidaSemSalvar(confirmacao: ConfirmationService): Promise<boolean> {
  return new Promise<boolean>((resolver) =>
    confirmacao.confirm({
      header: 'Sair sem salvar?',
      message: 'Há alterações que não foram salvas. Se sair, elas serão perdidas.',
      icon: 'pi pi-exclamation-triangle',
      acceptButtonProps: { label: 'Sair sem salvar', severity: 'danger' },
      rejectButtonProps: { label: 'Continuar editando', severity: 'secondary', text: true },
      accept: () => resolver(true),
      reject: () => resolver(false),
    }),
  );
}

/** canDeactivate: com alterações pendentes, pergunta antes de sair da tela. */
export const alteracoesPendentesGuard: CanDeactivateFn<ComAlteracoesPendentes> = (componente) => {
  if (!componente.temAlteracoesPendentes()) {
    return true;
  }
  return confirmarSaidaSemSalvar(inject(ConfirmationService));
};
