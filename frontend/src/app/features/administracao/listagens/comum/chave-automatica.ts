import { DestroyRef } from '@angular/core';
import { FormControl } from '@angular/forms';
import { gerarChave } from '../../../../shared/chaves/chave';

/**
 * Liga a chave ao nome na CRIAÇÃO: enquanto a pessoa não editar a chave à mão, ela acompanha o nome (gerarChave). Depois de
 * criada, a chave não muda (RN51) e a tela a mostra travada.
 */
export function ligarChaveAoNome(nome: FormControl<string>, chave: FormControl<string>, destruir: DestroyRef): void {
  let editadaAMao = false;
  const doUsuario = chave.valueChanges.subscribe((valor) => {
    editadaAMao = valor !== gerarChave(nome.value);
  });
  const doNome = nome.valueChanges.subscribe((valor) => {
    if (!editadaAMao) {
      chave.setValue(gerarChave(valor), { emitEvent: false });
    }
  });
  destruir.onDestroy(() => {
    doUsuario.unsubscribe();
    doNome.unsubscribe();
  });
}

/** Os erros 400 da API (por campo do corpo) nos controles da tela: { chave: ["..."] } → setErrors({ servidor }). */
export function aplicarErros(
  controles: Record<string, FormControl<unknown>>,
  erros: Record<string, string[]>,
): string[] {
  const gerais: string[] = [];
  for (const [chave, mensagens] of Object.entries(erros)) {
    const controle = controles[chave];
    if (controle) {
      controle.setErrors({ servidor: mensagens });
      controle.markAsTouched();
    } else {
      gerais.push(...mensagens);
    }
  }
  return gerais;
}

/** As mensagens de um controle: as do servidor, ou a de obrigatório. */
export function errosDe(controle: FormControl<unknown>): string[] {
  // Desabilitado (ex.: a chave travada depois de criada) não tem erro a mostrar: o status dele é DISABLED, não VALID.
  if (controle.disabled || !controle.touched || controle.valid) {
    return [];
  }
  if (controle.errors?.['servidor']) {
    return controle.errors['servidor'] as string[];
  }
  if (controle.errors?.['required']) {
    return ['Campo obrigatório.'];
  }
  if (controle.errors?.['pattern']) {
    return ['Use só letras minúsculas sem acento, números e "_".'];
  }
  if (controle.errors?.['maxlength']) {
    return [`No máximo ${controle.errors['maxlength'].requiredLength} caracteres.`];
  }
  return ['Valor inválido.'];
}
