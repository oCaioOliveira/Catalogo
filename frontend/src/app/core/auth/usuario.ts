import { UsuarioLogadoDto } from '../../api/models';

export type Perfil = 'EXTERNO' | 'INTERNO' | 'GESTOR' | 'ADMINISTRADOR';

/** O usuário logado (GET /api/v1/auth/eu): perfil e equipes ATIVAS dele. */
export interface UsuarioLogado {
  id: string;
  matricula: string;
  nome: string;
  perfil: Perfil;
  deveTrocarSenha: boolean;
  equipes: { id: string; nome: string }[];
}

export function usuarioDeDto(dto: UsuarioLogadoDto): UsuarioLogado {
  return {
    id: dto.id ?? '',
    matricula: dto.matricula ?? '',
    nome: dto.nome ?? '',
    perfil: (dto.perfil ?? 'EXTERNO') as Perfil,
    deveTrocarSenha: dto.deveTrocarSenha ?? false,
    equipes: (dto.equipes ?? []).filter((e) => e.ativa !== false).map((e) => ({ id: e.id ?? '', nome: e.nome ?? '' })),
  };
}

export const NOMES_PERFIL: Record<Perfil, string> = {
  EXTERNO: 'Externo',
  INTERNO: 'Interno',
  GESTOR: 'Gestor',
  ADMINISTRADOR: 'Administrador',
};
