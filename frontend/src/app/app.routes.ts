import { Routes } from '@angular/router';
import { anonimoGuard, autenticadoGuard } from './core/auth/auth.guard';
import { sessaoGuard } from './core/auth/sessao.guard';
import { LayoutComponent } from './core/layout/layout.component';
import { rotaAntigaGuard } from './core/layout/rota-antiga.guard';
import { listagemAtualGuard, telaInicialGuard } from './core/metadados/tela-inicial.guard';
import { administracaoGuard } from './core/permissoes/administracao.guard';
import { configuracaoEquipeGuard } from './core/permissoes/configuracao-equipe.guard';
import { painelGuard } from './core/permissoes/painel.guard';

/**
 * Rotas. Só o login é público. Na interface, o grupo se chama ÁREA DE GESTÃO (docs/glossario.md); no código e nas rotas, continua
 * sendo `grupo`.
 *
 *   - /:grupo: a página Início da área; /:grupo/:entidade/...: as telas GENÉRICAS de registros (features/registros/registros.routes.ts).
 *   - /painel/:grupo: o painel gerencial (GESTOR e ADMINISTRADOR, painelGuard, RN78). Vem ANTES de /:grupo/:entidade (senão "painel"
 *     seria lido como um grupo).
 *   - /configuracoes/...: a área de configurações, com menu lateral próprio: Configurações da equipe (GESTOR e
 *     ADMINISTRADOR) e Usuários, Equipes e Áreas de gestão (só ADMINISTRADOR, administracaoGuard, RN26).
 *   - /aprovacoes (GESTOR e ADMINISTRADOR) e /minha-conta (todos), fora das áreas.
 *   - As rotas antigas (/admin/..., /configuracoes-equipe/...) redirecionam para /configuracoes/... (core/layout/rotas-antigas.ts).
 */
export const routes: Routes = [
  {
    path: 'login',
    canActivate: [anonimoGuard],
    title: 'Entrar · Catálogo',
    loadComponent: () => import('./features/auth/login/login.component').then((m) => m.LoginComponent),
  },
  {
    // Fora do layout: com a troca pendente, a API recusa tudo (menu, metadados) até a senha ser trocada.
    path: 'trocar-senha',
    canActivate: [sessaoGuard],
    title: 'Trocar senha · Catálogo',
    loadComponent: () =>
      import('./features/auth/trocar-senha/trocar-senha.component').then((m) => m.TrocarSenhaComponent),
  },
  {
    path: '',
    component: LayoutComponent,
    canActivate: [autenticadoGuard],
    canActivateChild: [autenticadoGuard],
    children: [
      // A tela inicial: o Início da última área usada (ou da primeira) (ADR 0043).
      { path: '', pathMatch: 'full', canActivate: [telaInicialGuard], children: [] },
      {
        path: 'nao-encontrado',
        title: 'Não encontrado · Catálogo',
        loadComponent: () =>
          import('./features/nao-encontrado/nao-encontrado.component').then((m) => m.NaoEncontradoComponent),
      },
      {
        path: 'sem-listagens',
        title: 'Nenhuma área de gestão disponível · Catálogo',
        loadComponent: () =>
          import('./features/sem-listagens/sem-listagens.component').then((m) => m.SemListagensComponent),
      },
      {
        path: 'sem-permissao',
        title: 'Sem permissão · Catálogo',
        loadComponent: () =>
          import('./features/sem-permissao/sem-permissao.component').then((m) => m.SemPermissaoComponent),
      },
      {
        path: 'minha-conta',
        title: 'Minha conta · Catálogo',
        loadComponent: () => import('./features/conta/minha-conta.component').then((m) => m.MinhaContaComponent),
      },
      {
        path: 'configuracoes',
        canActivate: [configuracaoEquipeGuard],
        children: [
          { path: '', pathMatch: 'full', redirectTo: 'equipe' },
          {
            path: 'equipe',
            title: 'Configurações da equipe · Catálogo',
            loadComponent: () =>
              import('./features/configuracoes-equipe/configuracoes-equipe.component').then(
                (m) => m.ConfiguracoesEquipeComponent,
              ),
          },
          {
            path: 'equipe/:equipeId/:grupo/:entidade/modelo',
            title: 'Modelo de avaliação · Catálogo',
            loadComponent: () =>
              import('./features/configuracoes-equipe/editor-modelo.component').then((m) => m.EditorModeloComponent),
          },
          {
            path: 'equipe/:equipeId/:grupo/:entidade/novo-modelo',
            title: 'Novo modelo de avaliação · Catálogo',
            data: { novo: true },
            loadComponent: () =>
              import('./features/configuracoes-equipe/editor-modelo.component').then((m) => m.EditorModeloComponent),
          },
          {
            path: '',
            canActivate: [administracaoGuard],
            loadChildren: () =>
              import('./features/administracao/administracao.routes').then((m) => m.ROTAS_ADMINISTRACAO),
          },
        ],
      },
      // Rotas antigas: redirecionam, mantendo os parâmetros (?grupo=...).
      { path: 'admin', children: [{ path: '**', canActivate: [rotaAntigaGuard], children: [] }] },
      { path: 'configuracoes-equipe', children: [{ path: '**', canActivate: [rotaAntigaGuard], children: [] }] },
      {
        path: 'aprovacoes',
        canActivate: [configuracaoEquipeGuard],
        title: 'Aprovações · Catálogo',
        loadComponent: () =>
          import('./features/aprovacao/fila-aprovacao.component').then((m) => m.FilaAprovacaoComponent),
      },
      {
        path: 'painel/:grupo',
        canActivate: [painelGuard, listagemAtualGuard],
        loadChildren: () => import('./features/painel/painel.routes').then((m) => m.ROTAS_PAINEL),
      },
      {
        path: ':grupo',
        pathMatch: 'full',
        canActivate: [listagemAtualGuard],
        title: 'Início · Catálogo',
        loadComponent: () => import('./features/inicio/inicio.component').then((m) => m.InicioComponent),
      },
      {
        path: ':grupo/:entidade',
        canActivate: [listagemAtualGuard],
        title: 'Catálogo',
        loadChildren: () => import('./features/registros/registros.routes').then((m) => m.ROTAS_REGISTROS),
      },
      {
        path: '**',
        title: 'Não encontrado · Catálogo',
        loadComponent: () =>
          import('./features/nao-encontrado/nao-encontrado.component').then((m) => m.NaoEncontradoComponent),
      },
    ],
  },
];
