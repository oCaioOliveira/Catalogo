import { ChangeDetectionStrategy, Component, inject, input, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { Button } from 'primeng/button';
import { InputText } from 'primeng/inputtext';
import { Message } from 'primeng/message';
import { Password } from 'primeng/password';
import { AuthService } from '../../../core/auth/auth.service';
import { tratarNoComponente } from '../../../core/erros/contexto-erros';
import { mensagemDoErro } from '../../../core/erros/mensagens-erro';
import { lerProblema } from '../../../core/erros/problema';
import { FocoInicialDirective } from '../../../shared/acessibilidade/foco-inicial.directive';
import { BotaoTemaComponent } from '../../../tema/botao-tema.component';
import { LogoMarcaComponent } from '../../../tema/logo-marca.component';

/**
 * Login por matrícula e senha (RN23). A API nunca diz POR QUE recusou (senha errada, usuário inativo ou bloqueado
 * dão o mesmo 401 CREDENCIAIS_INVALIDAS): a tela mostra a mensagem genérica. Excesso de tentativas: 429.
 */
@Component({
  selector: 'app-login',
  imports: [
    FocoInicialDirective,
    ReactiveFormsModule,
    Button,
    InputText,
    Message,
    Password,
    LogoMarcaComponent,
    BotaoTemaComponent,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './login.component.html',
})
export class LoginComponent {
  /** Para onde voltar depois do login (query param, posto pelo guard e pelo interceptor). */
  readonly returnUrl = input<string | undefined>(undefined);

  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  readonly formulario = new FormGroup({
    matricula: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    senha: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
  });
  readonly entrando = signal(false);
  readonly erro = signal<string | null>(null);
  readonly tentouEntrar = signal(false);

  async entrar(): Promise<void> {
    this.tentouEntrar.set(true);
    this.formulario.markAllAsTouched();
    if (this.formulario.invalid) {
      return;
    }
    this.erro.set(null);
    this.entrando.set(true);
    const { matricula, senha } = this.formulario.getRawValue();
    try {
      const usuario = await this.auth.entrar(matricula.trim(), senha, tratarNoComponente(401, 429));
      const destino = this.destinoSeguro();
      if (usuario.deveTrocarSenha) {
        await this.router.navigate(['/trocar-senha'], { queryParams: destino ? { returnUrl: destino } : {} });
      } else {
        await this.router.navigateByUrl(destino ?? '/');
      }
    } catch (erro) {
      const problema = lerProblema(erro);
      this.erro.set(mensagemDoErro(problema.codigo, problema.status));
    } finally {
      this.entrando.set(false);
    }
  }

  invalido(nome: 'matricula' | 'senha'): boolean {
    const controle = this.formulario.controls[nome];
    // Só depois de digitar ou tentar entrar (o campo vem focado: sair dele sem digitar não é erro ainda).
    return controle.invalid && (controle.dirty || this.tentouEntrar());
  }

  /** Só caminhos internos ("/..."), para o returnUrl não levar a outro site. */
  private destinoSeguro(): string | null {
    const url = this.returnUrl();
    return url && url.startsWith('/') && !url.startsWith('//') && !url.startsWith('/login') ? url : null;
  }
}
