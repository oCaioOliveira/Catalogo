import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { MessageService } from 'primeng/api';
import { firstValueFrom } from 'rxjs';
import { tratarNoComponente } from '../erros/contexto-erros';
import { erroInterceptor } from '../erros/erro.interceptor';
import { authInterceptor } from './auth.interceptor';
import { AuthService } from './auth.service';

describe('interceptors de autenticação e de erros', () => {
  let http: HttpClient;
  let controle: HttpTestingController;
  let router: Router;
  let auth: AuthService;
  let mensagens: MessageService;

  beforeEach(() => {
    sessionStorage.clear();
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        provideHttpClient(withInterceptors([erroInterceptor, authInterceptor])),
        provideHttpClientTesting(),
        MessageService,
      ],
    });
    http = TestBed.inject(HttpClient);
    controle = TestBed.inject(HttpTestingController);
    router = TestBed.inject(Router);
    auth = TestBed.inject(AuthService);
    mensagens = TestBed.inject(MessageService);
    auth.token.set('token-123');
    auth.usuario.set({ id: 'u', matricula: 'm', nome: 'n', perfil: 'INTERNO', deveTrocarSenha: false, equipes: [] });
    spyOn(router, 'navigate').and.resolveTo(true);
    spyOn(mensagens, 'add');
  });

  afterEach(() => controle.verify());

  async function chamarComErro(
    status: number,
    corpo: object,
    url = '/api/v1/grupos',
    contexto = tratarNoComponente(),
  ): Promise<void> {
    const pedido = firstValueFrom(http.get(url, { context: contexto }));
    controle.expectOne(url).flush(corpo, { status, statusText: 'x' });
    await expectAsync(pedido).toBeRejected();
  }

  it('envia o token no cabeçalho Authorization', () => {
    http.get('/api/v1/grupos').subscribe();
    const requisicao = controle.expectOne('/api/v1/grupos');
    expect(requisicao.request.headers.get('Authorization')).toBe('Bearer token-123');
    requisicao.flush([]);
  });

  it('401: limpa a sessão e vai para o login com o returnUrl, sem toast', async () => {
    await chamarComErro(401, { codigo: 'NAO_AUTENTICADO' });
    expect(auth.token()).toBeNull();
    expect(auth.usuario()).toBeNull();
    expect(router.navigate).toHaveBeenCalledWith(
      ['/login'],
      jasmine.objectContaining({ queryParams: jasmine.any(Object) }),
    );
    expect(mensagens.add).not.toHaveBeenCalled();
  });

  it('401 do próprio login não é sessão expirada', async () => {
    await chamarComErro(401, { codigo: 'CREDENCIAIS_INVALIDAS' }, '/api/v1/auth/login', tratarNoComponente(401));
    expect(router.navigate).not.toHaveBeenCalled();
    expect(auth.token()).toBe('token-123');
  });

  it('403 TROCA_DE_SENHA_OBRIGATORIA: vai para a troca de senha', async () => {
    await chamarComErro(403, { codigo: 'TROCA_DE_SENHA_OBRIGATORIA' });
    expect(router.navigate).toHaveBeenCalledWith(['/trocar-senha']);
    expect(mensagens.add).not.toHaveBeenCalled();
  });

  it('outros erros viram toast; 5xx mostra o traceId', async () => {
    await chamarComErro(500, { codigo: 'ERRO_INTERNO', traceId: 'trace-1' });
    expect(mensagens.add).toHaveBeenCalledWith(
      jasmine.objectContaining({ severity: 'error', detail: 'Código para o suporte: trace-1' }),
    );
  });

  it('status tratado pelo componente não vira toast', async () => {
    await chamarComErro(400, { codigo: 'VALIDACAO', errors: {} }, '/api/v1/grupos', tratarNoComponente(400));
    expect(mensagens.add).not.toHaveBeenCalled();
  });
});
