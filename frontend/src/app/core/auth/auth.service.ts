import { HttpContext } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { Api } from '../../api/api';
// Direto do arquivo de cada operação (e não do barril api/functions): este serviço carrega na partida, e o barril leva o cliente
// inteiro para o bundle inicial (cada função gerada tem um efeito colateral, o `.PATH`, que impede o tree-shaking).
import { apiV1AuthEuGet } from '../../api/fn/auth/api-v-1-auth-eu-get';
import { apiV1AuthLoginPost } from '../../api/fn/auth/api-v-1-auth-login-post';
import { apiV1AuthTrocarSenhaPost } from '../../api/fn/auth/api-v-1-auth-trocar-senha-post';
import { MetadadosService } from '../metadados/metadados.service';
import { gravarToken, lerToken } from './armazenamento-sessao';
import { UsuarioLogado, usuarioDeDto } from './usuario';

/**
 * Sessão do usuário: o token (espelhado em sessionStorage) e o usuário logado, como signals.
 * Login, troca de senha e logout passam por aqui; o interceptor lê o token daqui.
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly api = inject(Api);
  private readonly router = inject(Router);
  private readonly metadados = inject(MetadadosService);

  readonly token = signal<string | null>(lerToken());
  readonly usuario = signal<UsuarioLogado | null>(null);
  readonly logado = computed(() => this.token() !== null && this.usuario() !== null);

  /** Login (RN23). Erros (401 CREDENCIAIS_INVALIDAS, 429) seguem para a tela, que os mostra. */
  async entrar(matricula: string, senha: string, contexto?: HttpContext): Promise<UsuarioLogado> {
    const resposta = await this.api.invoke(apiV1AuthLoginPost, { body: { matricula, senha } }, contexto);
    this.definirToken(resposta.token ?? null);
    // O "eu" traz as equipes ATIVAS e o perfil atual (podem ter mudado depois do token, RN23).
    return this.carregarUsuario();
  }

  /** Carrega o usuário do token guardado (ao abrir ou recarregar a página). Sem token, ou token inválido: deslogado. */
  async restaurar(): Promise<void> {
    if (!this.token()) {
      return;
    }
    try {
      await this.carregarUsuario();
    } catch {
      this.limparSessao();
    }
  }

  async carregarUsuario(): Promise<UsuarioLogado> {
    const usuario = usuarioDeDto(await this.api.invoke(apiV1AuthEuGet));
    this.usuario.set(usuario);
    return usuario;
  }

  /** RN25: troca a senha. Depois dela, o mesmo token passa a valer para tudo. */
  async trocarSenha(senhaAtual: string, novaSenha: string, contexto?: HttpContext): Promise<void> {
    await this.api.invoke(apiV1AuthTrocarSenhaPost, { body: { senhaAtual, novaSenha } }, contexto);
    await this.carregarUsuario();
  }

  sair(): void {
    this.limparSessao();
    void this.router.navigate(['/login']);
  }

  /** Esquece o token, o usuário e os metadados (o próximo usuário pode ver outras entidades). */
  limparSessao(): void {
    this.definirToken(null);
    this.usuario.set(null);
    this.metadados.limpar();
  }

  private definirToken(token: string | null): void {
    gravarToken(token);
    this.token.set(token);
  }
}
