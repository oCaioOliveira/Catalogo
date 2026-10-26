import { Routes } from '@angular/router';
import { alteracoesPendentesGuard } from '../../shared/formulario/alteracoes-pendentes.guard';

/**
 * /configuracoes/areas... (Áreas de gestão: a administração dos metadados), /configuracoes/usuarios... e /configuracoes/equipes...,
 * só para o ADMINISTRADOR (administracaoGuard em app.routes.ts). As rotas antigas /admin/... redirecionam para cá.
 */
export const ROTAS_ADMINISTRACAO: Routes = [
  {
    path: 'areas',
    loadComponent: () => import('./listagens/listagens-admin.component').then((m) => m.ListagensAdminComponent),
    children: [
      {
        path: '',
        title: 'Áreas de gestão · Catálogo',
        loadComponent: () => import('./listagens/lista-listagens.component').then((m) => m.ListaListagensComponent),
      },
      {
        path: ':grupo',
        title: 'Área de gestão · Catálogo',
        canDeactivate: [alteracoesPendentesGuard],
        loadComponent: () => import('./listagens/detalhe-listagem.component').then((m) => m.DetalheListagemComponent),
      },
      {
        path: ':grupo/entidades/:entidade',
        title: 'Tipo de item · Catálogo',
        loadComponent: () => import('./listagens/editor-entidade.component').then((m) => m.EditorEntidadeComponent),
      },
    ],
  },
  {
    path: 'usuarios',
    title: 'Usuários · Catálogo',
    loadComponent: () => import('./usuarios/lista-usuarios.component').then((m) => m.ListaUsuariosComponent),
  },
  {
    path: 'usuarios/novo',
    title: 'Novo usuário · Catálogo',
    loadComponent: () => import('./usuarios/formulario-usuario.component').then((m) => m.FormularioUsuarioComponent),
  },
  {
    path: 'usuarios/:id',
    title: 'Usuário · Catálogo',
    loadComponent: () => import('./usuarios/detalhe-usuario.component').then((m) => m.DetalheUsuarioComponent),
  },
  {
    path: 'usuarios/:id/editar',
    title: 'Editar usuário · Catálogo',
    loadComponent: () => import('./usuarios/formulario-usuario.component').then((m) => m.FormularioUsuarioComponent),
  },
  {
    path: 'equipes',
    title: 'Equipes · Catálogo',
    loadComponent: () => import('./equipes/lista-equipes.component').then((m) => m.ListaEquipesComponent),
  },
  {
    path: 'equipes/novo',
    title: 'Nova equipe · Catálogo',
    loadComponent: () => import('./equipes/formulario-equipe.component').then((m) => m.FormularioEquipeComponent),
  },
  {
    path: 'equipes/:id',
    title: 'Equipe · Catálogo',
    loadComponent: () => import('./equipes/detalhe-equipe.component').then((m) => m.DetalheEquipeComponent),
  },
  {
    path: 'equipes/:id/editar',
    title: 'Editar equipe · Catálogo',
    loadComponent: () => import('./equipes/formulario-equipe.component').then((m) => m.FormularioEquipeComponent),
  },
];
