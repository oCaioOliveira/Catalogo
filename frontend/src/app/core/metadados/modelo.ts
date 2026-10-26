import { CampoDto, EntidadeDto } from '../../api/models';

/**
 * Os metadados como o front usa: os mesmos dados de GET /api/v1/grupos/{grupo}/entidades, com os campos
 * obrigatórios preenchidos (os modelos gerados deixam tudo opcional). Ver docs/frontend.md.
 */

export type TipoDado =
  | 'TEXTO'
  | 'TEXTO_LONGO'
  | 'INTEIRO'
  | 'DECIMAL'
  | 'BOOLEANO'
  | 'DATA'
  | 'DATA_HORA'
  | 'LISTA'
  | 'REFERENCIA'
  /** Endereço http(s) absoluto, até 2000 caracteres (ADR 0042). */
  | 'URL';

export type AcaoExclusao = 'RESTRINGIR' | 'CASCATA';

export interface Campo {
  chave: string;
  nome: string;
  tipo: TipoDado;
  obrigatorio: boolean;
  ordem: number;
  ativo: boolean;
  tamanhoMaximo: number | null;
  precisao: number | null;
  escala: number | null;
  opcoes: string[];
  /** Chave da entidade apontada (só REFERENCIA). */
  entidadeReferenciada: string | null;
  acaoExclusao: AcaoExclusao | null;
  /** Chave do campo do formulário cujo valor vai em "dependeDe" ao buscar as opções (RN21). */
  dependeDe: string | null;
}

export interface Entidade {
  chave: string;
  /** Nome no singular ("Relatório de Teste"): "Novo …", "Editar …" e mensagens sobre um item. */
  nome: string;
  /** Nome no plural ("Relatórios de Teste"): títulos de listagem, menu, seções de filhos e trilhas. Nulo = use o nome. */
  nomePlural: string | null;
  descricao: string | null;
  /** Ícone do PrimeIcons ("pi-chart-line", RN95). Nulo = o genérico (regras-metadados.ts → classeIcone). */
  icone: string | null;
  /** true = cada registro é de uma equipe; false = registros globais, alterados só pelo ADMINISTRADOR (RN27). */
  escopoEquipe: boolean;
  /** Chave do campo usado como rótulo do registro. */
  campoExibicao: string | null;
  ordem: number;
  ativo: boolean;
  /** Só os campos ativos, na ordem dos metadados. */
  campos: Campo[];
  restricoesUnicas: { nome: string; campos: string[] }[];
  /**
   * As equipes QUE O USUÁRIO VÊ com a aprovação ativa para esta entidade (RN33, ADR 0045). Vazia = sem aprovação para ele: nada
   * de situação, filtro ou fila nas telas. Sempre vazia nas globais.
   */
  equipesComAprovacao: string[];
  /** Critérios de estrelas dos modelos de avaliação ativos nas equipes que ele vê (RN48): colunas, ordenação e filtro da listagem. */
  criteriosAvaliacao: { chave: string; nome: string }[];
}

/** Um registro apontado por um campo REFERENCIA: o id e o texto para mostrar (RN20). */
export interface OpcaoReferencia {
  id: string;
  rotulo: string | null;
}

export function campoDeDto(dto: CampoDto): Campo {
  return {
    chave: dto.chave ?? '',
    nome: dto.nome ?? dto.chave ?? '',
    tipo: (dto.tipoDado ?? 'TEXTO') as TipoDado,
    obrigatorio: dto.obrigatorio ?? false,
    ordem: dto.ordem ?? 0,
    ativo: dto.ativo ?? true,
    tamanhoMaximo: dto.tamanhoMaximo ?? null,
    precisao: dto.precisao ?? null,
    escala: dto.escala ?? null,
    opcoes: dto.opcoes ?? [],
    entidadeReferenciada: dto.entidadeReferenciada ?? null,
    acaoExclusao: (dto.acaoExclusao as AcaoExclusao | null | undefined) ?? null,
    dependeDe: dto.dependeDe ?? null,
  };
}

export function entidadeDeDto(dto: EntidadeDto): Entidade {
  return {
    chave: dto.chave ?? '',
    nome: dto.nome ?? dto.chave ?? '',
    nomePlural: dto.nomePlural?.trim() || null,
    descricao: dto.descricao ?? null,
    icone: dto.icone?.trim() || null,
    escopoEquipe: dto.escopoEquipe ?? true,
    campoExibicao: dto.campoExibicao ?? null,
    ordem: dto.ordem ?? 0,
    ativo: dto.ativo ?? true,
    campos: (dto.campos ?? [])
      .map(campoDeDto)
      .filter((c) => c.ativo)
      .sort((a, b) => a.ordem - b.ordem),
    restricoesUnicas: (dto.restricoesUnicas ?? []).map((r) => ({ nome: r.nome ?? '', campos: r.campos ?? [] })),
    equipesComAprovacao: dto.equipesComAprovacao ?? [],
    criteriosAvaliacao: (dto.criteriosAvaliacao ?? []).map((c) => ({
      chave: c.chave ?? '',
      nome: c.nome ?? c.chave ?? '',
    })),
  };
}
