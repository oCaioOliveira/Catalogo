import { Routes } from '@angular/router';

/** /painel/:grupo (visão geral e widgets do grupo) e /painel/:grupo/analise (análise personalizada). Guard em app.routes.ts. */
export const ROTAS_PAINEL: Routes = [
  {
    path: '',
    title: 'Painel gerencial · Catálogo',
    loadComponent: () => import('./pagina/painel.component').then((m) => m.PainelComponent),
  },
  {
    path: 'analise',
    title: 'Análise personalizada · Catálogo',
    loadComponent: () => import('./analise/analise.component').then((m) => m.AnaliseComponent),
  },
];
