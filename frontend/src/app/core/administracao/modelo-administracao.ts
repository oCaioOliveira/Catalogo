import { EquipeDto, UsuarioDto } from '../../api/models';
import { Perfil } from '../auth/usuario';

/**
 * Usuários e equipes como a administração usa (GET /api/v1/usuarios e /api/v1/equipes), com os campos obrigatórios preenchidos
 * (os modelos gerados deixam tudo opcional). NUNCA há senha aqui: a provisória só passa pelo diálogo (ADR 0041).
 */

export interface EquipeDoUsuario {
  id: string;
  nome: string;
  ativa: boolean;
}

export interface UsuarioAdmin {
  id: string;
  matricula: string;
  nome: string;
  perfil: Perfil;
  ativo: boolean;
  deveTrocarSenha: boolean;
  /** UTC ("...Z"): bloqueado por tentativas até esse instante (RN24). Nulo ou no passado = não bloqueado. */
  bloqueadoAte: string | null;
  ultimoAcessoEm: string | null;
  criadoEm: string;
  equipes: EquipeDoUsuario[];
}

export interface EquipeAdmin {
  id: string;
  nome: string;
  ativa: boolean;
  criadoEm: string;
  /** Usuários ativos da equipe. */
  membrosAtivos: number;
  /** As listagens em que a equipe está habilitada (RN90), por nome. */
  listagens: { chave: string; nome: string; ativo: boolean }[];
}

export function usuarioAdminDeDto(dto: UsuarioDto): UsuarioAdmin {
  return {
    id: dto.id ?? '',
    matricula: dto.matricula ?? '',
    nome: dto.nome ?? '',
    perfil: (dto.perfil ?? 'EXTERNO') as Perfil,
    ativo: dto.ativo ?? true,
    deveTrocarSenha: dto.deveTrocarSenha ?? false,
    bloqueadoAte: dto.bloqueadoAte ?? null,
    ultimoAcessoEm: dto.ultimoAcessoEm ?? null,
    criadoEm: dto.criadoEm ?? '',
    equipes: (dto.equipes ?? []).map((e) => ({ id: e.id ?? '', nome: e.nome ?? '', ativa: e.ativa ?? true })),
  };
}

export function equipeAdminDeDto(dto: EquipeDto): EquipeAdmin {
  return {
    id: dto.id ?? '',
    nome: dto.nome ?? '',
    ativa: dto.ativa ?? true,
    criadoEm: dto.criadoEm ?? '',
    membrosAtivos: dto.membrosAtivos ?? 0,
    listagens: (dto.listagens ?? []).map((l) => ({
      chave: l.chave ?? '',
      nome: l.nome ?? l.chave ?? '',
      ativo: l.ativo ?? true,
    })),
  };
}

/** Corpo do POST e do PUT de usuário (na edição, a matrícula é ignorada pela API). */
export interface DadosUsuario {
  matricula: string;
  nome: string;
  perfil: Perfil;
  equipes: string[];
}
