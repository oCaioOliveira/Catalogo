import { Routes } from '@angular/router';
import { alteracoesPendentesGuard } from '../../shared/formulario/alteracoes-pendentes.guard';

/**
 * Rotas GENÉRICAS dos registros, sob /:grupo/:entidade (carregadas sob demanda). Valem para qualquer entidade:
 * a tela vem dos metadados. Os parâmetros da rota chegam aos componentes como inputs.
 */
export const ROTAS_REGISTROS: Routes = [
  {
    path: '',
    loadComponent: () => import('./listagem/listagem-registros.component').then((m) => m.ListagemRegistrosComponent),
  },
  {
    path: 'novo',
    loadComponent: () => import('./formulario/pagina-formulario.component').then((m) => m.PaginaFormularioComponent),
    canDeactivate: [alteracoesPendentesGuard],
  },
  // Importação por planilha (RN97 a RN104): antes de ':id', senão "importar" seria lido como o id de um item.
  {
    path: 'importar',
    title: 'Importar planilha · Catálogo',
    loadComponent: () =>
      import('../importacao/assistente-importacao.component').then((m) => m.AssistenteImportacaoComponent),
  },
  {
    path: 'importacoes',
    title: 'Importações anteriores · Catálogo',
    loadComponent: () =>
      import('../importacao/historico-importacoes.component').then((m) => m.HistoricoImportacoesComponent),
  },
  {
    path: ':id',
    loadComponent: () => import('./detalhe/detalhe-registro.component').then((m) => m.DetalheRegistroComponent),
  },
  {
    path: ':id/editar',
    loadComponent: () => import('./formulario/pagina-formulario.component').then((m) => m.PaginaFormularioComponent),
    canDeactivate: [alteracoesPendentesGuard],
  },
];
