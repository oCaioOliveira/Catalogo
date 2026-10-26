import { registerLocaleData } from '@angular/common';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import localePt from '@angular/common/locales/pt';
import {
  ApplicationConfig,
  LOCALE_ID,
  inject,
  provideAppInitializer,
  provideBrowserGlobalErrorListeners,
  provideZoneChangeDetection,
} from '@angular/core';
import { provideAnimationsAsync } from '@angular/platform-browser/animations/async';
import { provideRouter, withComponentInputBinding, withRouterConfig } from '@angular/router';
import { ConfirmationService, MessageService } from 'primeng/api';
import { providePrimeNG } from 'primeng/config';
import { provideApiConfiguration } from './api/api-configuration';
import { routes } from './app.routes';
import { authInterceptor } from './core/auth/auth.interceptor';
import { AuthService } from './core/auth/auth.service';
import { erroInterceptor } from './core/erros/erro.interceptor';
import { traducaoPrimeNgPtBr } from './tema/primeng-pt-br';
import { TemaCatalogo, opcoesTema } from './tema/tema';
import { TemaService } from './tema/tema.service';

registerLocaleData(localePt, 'pt-BR');

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideZoneChangeDetection({ eventCoalescing: true }),
    // Os parâmetros da rota (inclusive os do pai: :grupo e :entidade) chegam aos componentes como inputs.
    provideRouter(routes, withComponentInputBinding(), withRouterConfig({ paramsInheritanceStrategy: 'always' })),
    // A ordem importa: o de autenticação vê o erro primeiro (401, troca de senha); o de erros mostra o toast.
    provideHttpClient(withInterceptors([erroInterceptor, authInterceptor])),
    // A API é chamada por caminhos relativos (/api/v1/...); no `npm start`, o proxy leva para a API local.
    provideApiConfiguration(''),
    provideAnimationsAsync(),
    providePrimeNG({ theme: { preset: TemaCatalogo, options: opcoesTema }, translation: traducaoPrimeNgPtBr }),
    { provide: LOCALE_ID, useValue: 'pt-BR' },
    MessageService,
    ConfirmationService,
    // Com um token guardado (página recarregada), carrega o usuário antes da primeira tela.
    provideAppInitializer(() => inject(AuthService).restaurar()),
    // Tema claro, escuro ou "seguir o sistema" (o padrão), desde a primeira tela, inclusive o login.
    provideAppInitializer(() => void inject(TemaService)),
  ],
};
