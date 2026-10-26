import {
  ColunaArquivoDto,
  DestinoDisponivelDto,
  ImportacaoEnviadaDto,
  ImportacaoResumoDto,
  LinhaImportacaoDto,
  PreviaImportacaoDto,
  TotaisImportacaoDto,
} from '../../api/models';
import { PaginaDe } from '../registros/modelo-registro';

/** Para onde vai uma coluna do arquivo (RN98): ignorar, um campo (na referência, pelo nome), o "(ID)" de uma referência ou a equipe. */
export type TipoDestino = 'IGNORAR' | 'CAMPO' | 'CAMPO_ID' | 'EQUIPE';

export interface Destino {
  tipo: TipoDestino;
  campo: string | null;
}

/** Um destino que o select de cada coluna oferece (vem da API, na ordem do formulário). */
export interface DestinoDisponivel extends Destino {
  rotulo: string;
  obrigatorio: boolean;
}

export interface ColunaArquivo {
  indice: number;
  cabecalho: string;
  exemplos: string[];
  destino: Destino;
}

/** Resposta do envio do arquivo: as colunas, com a sugestão de mapeamento, e os destinos possíveis. */
export interface ImportacaoEnviada {
  id: string;
  nomeArquivo: string;
  totalLinhas: number;
  colunas: ColunaArquivo[];
  destinos: DestinoDisponivel[];
}

export type AcaoDuplicados = 'PULAR' | 'ATUALIZAR';
export type TratamentoErros = 'SO_VALIDAS' | 'TUDO_OU_NADA';

export interface OpcoesImportacao {
  duplicados: AcaoDuplicados;
  linhasComErro: TratamentoErros;
  equipePadraoId: string | null;
}

export interface Totais {
  novos: number;
  atualizacoes: number;
  ignorados: number;
  comErro: number;
  total: number;
}

export type SituacaoLinha = 'NOVA' | 'ATUALIZACAO' | 'IGNORADA' | 'ERRO';

export interface ErroLinha {
  /** A posição da célula no arquivo; nula = erro da linha inteira (ex.: repetida no arquivo). */
  coluna: number | null;
  campo: string | null;
  mensagem: string;
}

export interface ReferenciaResolvida {
  coluna: number;
  campo: string;
  id: string;
  nome: string | null;
}

export interface LinhaImportacao {
  numero: number;
  situacao: SituacaoLinha;
  valores: string[];
  erros: ErroLinha[];
  referencias: ReferenciaResolvida[];
  registroId: string | null;
}

export type SituacaoImportacao = 'ENVIADA' | 'VALIDADA' | 'CONCLUIDA' | 'DESFEITA' | 'EXPIRADA';

/** Uma importação no histórico (RN104). */
export interface ResumoImportacao {
  id: string;
  nomeArquivo: string;
  formato: string;
  situacao: SituacaoImportacao;
  criadoEm: string;
  concluidaEm: string | null;
  desfeitaEm: string | null;
  usuario: { id: string; matricula: string; nome: string };
  totais: Totais | null;
  arquivoDisponivel: boolean;
  podeDesfazer: boolean;
}

/** A prévia (ou o resultado) de uma importação, com as linhas paginadas. */
export interface PreviaImportacao {
  importacao: ResumoImportacao;
  totais: Totais;
  executavel: boolean;
  cabecalhos: string[];
  linhas: PaginaDe<LinhaImportacao>;
}

const TOTAIS_VAZIOS: Totais = { novos: 0, atualizacoes: 0, ignorados: 0, comErro: 0, total: 0 };

function destinoDe(dto: { tipo?: string | null; campo?: string | null } | undefined): Destino {
  return { tipo: (dto?.tipo as TipoDestino | undefined) ?? 'IGNORAR', campo: dto?.campo ?? null };
}

function totaisDe(dto: TotaisImportacaoDto | null | undefined): Totais {
  return dto
    ? {
        novos: dto.novos ?? 0,
        atualizacoes: dto.atualizacoes ?? 0,
        ignorados: dto.ignorados ?? 0,
        comErro: dto.comErro ?? 0,
        total: dto.total ?? 0,
      }
    : TOTAIS_VAZIOS;
}

export function enviadaDeDto(dto: ImportacaoEnviadaDto): ImportacaoEnviada {
  return {
    id: dto.id ?? '',
    nomeArquivo: dto.nomeArquivo ?? '',
    totalLinhas: dto.totalLinhas ?? 0,
    colunas: (dto.colunas ?? []).map((c: ColunaArquivoDto) => ({
      indice: c.indice ?? 0,
      cabecalho: c.cabecalho ?? '',
      exemplos: c.exemplos ?? [],
      destino: destinoDe(c.destino),
    })),
    destinos: (dto.destinos ?? []).map((d: DestinoDisponivelDto) => ({
      ...destinoDe(d),
      rotulo: d.rotulo ?? '',
      obrigatorio: d.obrigatorio ?? false,
    })),
  };
}

export function resumoDeDto(dto: ImportacaoResumoDto): ResumoImportacao {
  return {
    id: dto.id ?? '',
    nomeArquivo: dto.nomeArquivo ?? '',
    formato: dto.formato ?? '',
    situacao: (dto.situacao as SituacaoImportacao | undefined) ?? 'ENVIADA',
    criadoEm: dto.criadoEm ?? '',
    concluidaEm: dto.concluidaEm ?? null,
    desfeitaEm: dto.desfeitaEm ?? null,
    usuario: { id: dto.usuario?.id ?? '', matricula: dto.usuario?.matricula ?? '', nome: dto.usuario?.nome ?? '' },
    totais: dto.totais ? totaisDe(dto.totais) : null,
    arquivoDisponivel: dto.arquivoDisponivel ?? false,
    podeDesfazer: dto.podeDesfazer ?? false,
  };
}

function linhaDeDto(dto: LinhaImportacaoDto): LinhaImportacao {
  return {
    numero: dto.numero ?? 0,
    situacao: (dto.situacao as SituacaoLinha | undefined) ?? 'ERRO',
    valores: dto.valores ?? [],
    erros: (dto.erros ?? []).map((e) => ({
      coluna: e.coluna ?? null,
      campo: e.campo ?? null,
      mensagem: e.mensagem ?? '',
    })),
    referencias: (dto.referencias ?? []).map((r) => ({
      coluna: r.coluna ?? 0,
      campo: r.campo ?? '',
      id: r.id ?? '',
      nome: r.nome ?? null,
    })),
    registroId: dto.registroId ?? null,
  };
}

export function previaDeDto(dto: PreviaImportacaoDto): PreviaImportacao {
  return {
    importacao: resumoDeDto(dto.importacao ?? {}),
    totais: totaisDe(dto.totais),
    executavel: dto.executavel ?? false,
    cabecalhos: dto.cabecalhos ?? [],
    linhas: {
      itens: (dto.linhas?.itens ?? []).map(linhaDeDto),
      pagina: dto.linhas?.pagina ?? 1,
      tamanho: dto.linhas?.tamanho ?? 20,
      total: dto.linhas?.total ?? 0,
      totalPaginas: dto.linhas?.totalPaginas ?? 0,
    },
  };
}
