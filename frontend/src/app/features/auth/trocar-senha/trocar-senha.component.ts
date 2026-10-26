import { ChangeDetectionStrategy, Component, inject, input, signal } from '@angular/core';
import {
  AbstractControl,
  FormControl,
  FormGroup,
  ReactiveFormsModule,
  ValidationErrors,
  Validators,
} from '@angular/forms';
import { Router } from '@angular/router';
import { MessageService } from 'primeng/api';
import { Button } from 'primeng/button';
import { Message } from 'primeng/message';
import { Password } from 'primeng/password';
import { AuthService } from '../../../core/auth/auth.service';
import { tratarNoComponente } from '../../../core/erros/contexto-erros';
import { lerProblema } from '../../../core/erros/problema';
import { FocoInicialDirective } from '../../../shared/acessibilidade/foco-inicial.directive';

/** RN25: pelo menos 10 caracteres, com letra e número. (A diferença da senha atual é conferida pela API.) */
export function politicaSenha(controle: AbstractControl): ValidationErrors | null {
  const senha = String(controle.value ?? '');
  const problemas: string[] = [];
  if (senha.length < 10) {
    problemas.push('ter pelo menos 10 caracteres');
  }
  if (!/[A-Za-zÀ-ÿ]/.test(senha)) {
    problemas.push('ter pelo menos uma letra');
  }
  if (!/\d/.test(senha)) {
    problemas.push('ter pelo menos um número');
  }
  return problemas.length > 0 ? { politica: problemas } : null;
}

function confirmacaoIgual(grupo: AbstractControl): ValidationErrors | null {
  const nova = grupo.get('novaSenha')?.value;
  const confirmacao = grupo.get('confirmacao')?.value;
  return confirmacao && nova !== confirmacao ? { confirmacao: true } : null;
}

/** Troca de senha (obrigatória no primeiro acesso, RN25, ou pedida pelo usuário). */
@Component({
  selector: 'app-trocar-senha',
  imports: [FocoInicialDirective, ReactiveFormsModule, Button, Message, Password],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './trocar-senha.component.html',
})
export class TrocarSenhaComponent {
  readonly returnUrl = input<string | undefined>(undefined);

  readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly mensagens = inject(MessageService);

  readonly formulario = new FormGroup(
    {
      senhaAtual: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
      novaSenha: new FormControl('', { nonNullable: true, validators: [Validators.required, politicaSenha] }),
      confirmacao: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    },
    { validators: confirmacaoIgual },
  );
  readonly salvando = signal(false);
  readonly erros = signal<string[]>([]);
  readonly tentouTrocar = signal(false);

  async trocar(): Promise<void> {
    this.tentouTrocar.set(true);
    this.formulario.markAllAsTouched();
    if (this.formulario.invalid) {
      return;
    }
    this.erros.set([]);
    this.salvando.set(true);
    const { senhaAtual, novaSenha } = this.formulario.getRawValue();
    try {
      await this.auth.trocarSenha(senhaAtual, novaSenha, tratarNoComponente(400));
      this.mensagens.add({ severity: 'success', summary: 'Senha alterada.' });
      await this.voltar();
    } catch (erro) {
      const problema = lerProblema(erro);
      const mensagens = Object.values(problema.erros).flat();
      this.erros.set(mensagens.length > 0 ? mensagens : [problema.detalhe ?? 'Não foi possível trocar a senha.']);
    } finally {
      this.salvando.set(false);
    }
  }

  /** Volta para onde o usuário estava (só caminhos internos) ou para a tela inicial. */
  async voltar(): Promise<void> {
    const url = this.returnUrl();
    await this.router.navigateByUrl(url && url.startsWith('/') && !url.startsWith('//') ? url : '/');
  }

  mostrar(nome: 'senhaAtual' | 'novaSenha' | 'confirmacao'): boolean {
    const controle = this.formulario.controls[nome];
    return (
      (controle.dirty || this.tentouTrocar()) &&
      (controle.invalid || (nome === 'confirmacao' && this.formulario.hasError('confirmacao')))
    );
  }

  problemasPolitica(): string[] {
    return (this.formulario.controls.novaSenha.errors?.['politica'] as string[] | undefined) ?? [];
  }
}
