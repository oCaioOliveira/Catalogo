import { Perfil } from '../../../core/auth/usuario';

/** Cor da tag de cada perfil (só reforço: o texto do perfil sempre aparece). */
export function severidadePerfil(perfil: Perfil): 'danger' | 'info' | 'success' | 'secondary' {
  switch (perfil) {
    case 'ADMINISTRADOR':
      return 'danger';
    case 'GESTOR':
      return 'info';
    case 'INTERNO':
      return 'success';
    default:
      return 'secondary';
  }
}
