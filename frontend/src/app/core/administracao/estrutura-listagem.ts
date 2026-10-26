import {
  CampoAdminDto,
  EntidadeAdminDto,
  EquipesListagemDto,
  GrupoAdminDto,
  GrupoResumoAdminDto,
  ImpactoDto,
  ResultadoAdministracaoDto,
} from '../../api/models';
import { TipoDado } from '../metadados/modelo';

/**
 * A estrutura de uma listagem como a ADMINISTRAÇÃO a edita (GET /api/v1/admin/metadados/grupos/{grupo}): entidades e campos
 * ativos E inativos, com as versões (RN52). Os modelos gerados têm tudo opcional: aqui viram tipos completos.
 */

export interface ResumoListagem {
  chave: string;
  nome: string;
  descricao: string | null;
  ativo: boolean;
  versao: string;
  quantidadeEntidades: number;
  quantidadeRegistros: number;
  /** Equipes habilitadas (RN90). Zero = só o administrador vê a listagem (RN93). */
  quantidadeEquipes: number;
}

export interface DependenciaEstrutura {
  via: string | null;
  origem: string;
  alvo: string;
}

export interface CampoEstrutura {
  chave: string;
  nome: string;
  tipo: TipoDado;
  obrigatorio: boolean;
  tamanhoMaximo: number | null;
  precisao: number | null;
  escala: number | null;
  opcoes: string[];
  entidadeReferenciada: string | null;
  acaoExclusao: 'RESTRINGIR' | 'CASCATA' | null;
  ordem: number;
  ativo: boolean;
  dependencia: DependenciaEstrutura | null;
}

export interface EntidadeEstrutura {
  chave: string;
  nome: string;
  nomePlural: string | null;
  /** Ícone do PrimeIcons (RN95); nulo = o genérico. */
  icone: string | null;
  descricao: string | null;
  ordem: number;
  escopoEquipe: boolean;
  ativo: boolean;
  versao: string;
  campoExibicao: string | null;
  quantidadeRegistros: number;
  /** Ativos e inativos, na ordem. */
  campos: CampoEstrutura[];
  restricoes: { nome: string; campos: string[] }[];
}

export interface EstruturaListagem {
  chave: string;
  nome: string;
  descricao: string | null;
  ativo: boolean;
  versao: string;
  /** Equipes habilitadas (RN90). Zero = só o administrador vê a listagem (RN93). */
  quantidadeEquipes: number;
  /** Na ordem do menu. */
  entidades: EntidadeEstrutura[];
}

/** Uma equipe na aba Equipes da listagem (GET .../grupos/{grupo}/equipes): todas, ativas e inativas. */
export interface EquipeNaListagem {
  id: string;
  nome: string;
  ativa: boolean;
  habilitada: boolean;
  /** Os itens dela nesta listagem: os que ficam ocultos para os membros se ela deixar de ser habilitada (RN92). */
  quantidadeItens: number;
}

export interface EquipesDaListagem {
  /** A versão do GRUPO: o PUT a manda de volta (RN52). */
  versao: string;
  equipes: EquipeNaListagem[];
}

export function equipesDaListagemDeDto(dto: EquipesListagemDto): EquipesDaListagem {
  return {
    versao: dto.versao ?? '',
    equipes: (dto.equipes ?? []).map((e) => ({
      id: e.id ?? '',
      nome: e.nome ?? '',
      ativa: e.ativa ?? true,
      habilitada: e.habilitada ?? false,
      quantidadeItens: e.quantidadeItens ?? 0,
    })),
  };
}

/** Um efeito da alteração nos dados (RN58): quantos itens, exemplos, grupos de duplicados e usos. */
export interface Impacto {
  tipo: string;
  descricao: string;
  bloqueia: boolean;
  quantidade: number;
  exemplos: { id: string; rotulo: string | null }[];
  grupos: { id: string; rotulo: string | null }[][] | null;
  usos: string[] | null;
}

/** A resposta de TODA escrita da administração (simulada ou não). */
export interface ResultadoAlteracao {
  permitido: boolean;
  simulado: boolean;
  impactos: Impacto[];
  /** A estrutura como ficou (só nas gravações). */
  estrutura: EstruturaListagem | null;
}

export function resumoDeDto(dto: GrupoResumoAdminDto): ResumoListagem {
  return {
    chave: dto.chave ?? '',
    nome: dto.nome ?? dto.chave ?? '',
    descricao: dto.descricao ?? null,
    ativo: dto.ativo ?? true,
    versao: dto.versao ?? '',
    quantidadeEntidades: dto.quantidadeEntidades ?? 0,
    quantidadeRegistros: dto.quantidadeRegistros ?? 0,
    quantidadeEquipes: dto.quantidadeEquipes ?? 0,
  };
}

export function campoEstruturaDeDto(dto: CampoAdminDto): CampoEstrutura {
  return {
    chave: dto.chave ?? '',
    nome: dto.nome ?? dto.chave ?? '',
    tipo: (dto.tipoDado ?? 'TEXTO') as TipoDado,
    obrigatorio: dto.obrigatorio ?? false,
    tamanhoMaximo: dto.tamanhoMaximo ?? null,
    precisao: dto.precisao ?? null,
    escala: dto.escala ?? null,
    opcoes: dto.opcoes ?? [],
    entidadeReferenciada: dto.entidadeReferenciada ?? null,
    acaoExclusao: (dto.acaoExclusao as CampoEstrutura['acaoExclusao']) ?? null,
    ordem: dto.ordem ?? 0,
    ativo: dto.ativo ?? true,
    dependencia: dto.dependencia
      ? {
          via: dto.dependencia.campoVia ?? null,
          origem: dto.dependencia.campoOrigem ?? '',
          alvo: dto.dependencia.campoAlvo ?? '',
        }
      : null,
  };
}

export function entidadeEstruturaDeDto(dto: EntidadeAdminDto): EntidadeEstrutura {
  return {
    chave: dto.chave ?? '',
    nome: dto.nome ?? dto.chave ?? '',
    nomePlural: dto.nomePlural?.trim() || null,
    icone: dto.icone?.trim() || null,
    descricao: dto.descricao ?? null,
    ordem: dto.ordem ?? 0,
    escopoEquipe: dto.escopoEquipe ?? true,
    ativo: dto.ativo ?? true,
    versao: dto.versao ?? '',
    campoExibicao: dto.campoExibicao ?? null,
    quantidadeRegistros: dto.quantidadeRegistros ?? 0,
    campos: (dto.campos ?? []).map(campoEstruturaDeDto).sort((a, b) => a.ordem - b.ordem),
    restricoes: (dto.restricoesUnicas ?? []).map((r) => ({ nome: r.nome ?? '', campos: r.campos ?? [] })),
  };
}

export function estruturaDeDto(dto: GrupoAdminDto): EstruturaListagem {
  return {
    chave: dto.chave ?? '',
    nome: dto.nome ?? dto.chave ?? '',
    descricao: dto.descricao ?? null,
    ativo: dto.ativo ?? true,
    versao: dto.versao ?? '',
    quantidadeEquipes: dto.quantidadeEquipes ?? 0,
    entidades: (dto.entidades ?? []).map(entidadeEstruturaDeDto).sort((a, b) => a.ordem - b.ordem),
  };
}

export function impactoDeDto(dto: ImpactoDto): Impacto {
  const registro = (r: { id?: string; rotulo?: string | null }) => ({ id: r.id ?? '', rotulo: r.rotulo ?? null });
  return {
    tipo: dto.tipo ?? '',
    descricao: dto.descricao ?? '',
    bloqueia: dto.bloqueia ?? false,
    quantidade: dto.quantidade ?? 0,
    exemplos: (dto.exemplos ?? []).map(registro),
    grupos: dto.grupos ? dto.grupos.map((g) => g.map(registro)) : null,
    usos: dto.usos ?? null,
  };
}

export function resultadoDeDto(dto: ResultadoAdministracaoDto): ResultadoAlteracao {
  return {
    permitido: dto.permitido ?? false,
    simulado: dto.simulado ?? false,
    impactos: (dto.impactos ?? []).map(impactoDeDto),
    estrutura: dto.grupo ? estruturaDeDto(dto.grupo) : null,
  };
}
