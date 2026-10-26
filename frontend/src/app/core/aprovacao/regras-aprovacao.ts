import { UsuarioLogado } from '../auth/usuario';
import { EquipeDaListagem, Listagem } from '../metadados/listagens';
import { Entidade } from '../metadados/modelo';
import { RegistroAlvo, aprovacaoAtiva, podeDecidir, podeEditar } from '../permissoes/permissoes';

// Regras de tela da aprovação (RN33 a RN37), em funções puras (regras-aprovacao.spec.ts). Quem decide é a API.

export type Situacao = 'PENDENTE' | 'APROVADO' | 'REJEITADO';

/** RN36: a justificativa tem de 10 a 1000 caracteres, sem os espaços das pontas. */
export const JUSTIFICATIVA = { minimo: 10, maximo: 1000 };

export function validarJustificativa(texto: string | null | undefined): string | null {
  const tamanho = (texto ?? '').trim().length;
  if (tamanho === 0) {
    return 'Informe a justificativa.';
  }
  if (tamanho < JUSTIFICATIVA.minimo) {
    return `A justificativa precisa de pelo menos ${JUSTIFICATIVA.minimo} caracteres (tem ${tamanho}).`;
  }
  if (tamanho > JUSTIFICATIVA.maximo) {
    return `A justificativa pode ter no máximo ${JUSTIFICATIVA.maximo} caracteres (tem ${tamanho}).`;
  }
  return null;
}

/** O nome e a cor (severidade do p-tag) de cada situação: amarelo, verde e vermelho, com o contraste do tema. */
export const SITUACOES: Record<Situacao, { rotulo: string; severidade: 'warn' | 'success' | 'danger' }> = {
  PENDENTE: { rotulo: 'Pendente', severidade: 'warn' },
  APROVADO: { rotulo: 'Aprovado', severidade: 'success' },
  REJEITADO: { rotulo: 'Rejeitado', severidade: 'danger' },
};

export function situacaoDe(valor: string | null | undefined): Situacao {
  return valor === 'PENDENTE' || valor === 'REJEITADO' ? valor : 'APROVADO';
}

/**
 * A situação aparece onde a aprovação está ativa para a entidade e a equipe do item. Um item que não está APROVADO (ex.: rejeitado
 * antes de alguém desligar a aprovação) também mostra a situação, para ninguém achar que ele foi aceito. Nas equipes sem aprovação,
 * todo item é APROVADO (RN34): nada muda nelas.
 */
export function mostraSituacao(entidade: Entidade, registro: RegistroAlvo): boolean {
  return aprovacaoAtiva(entidade, registro.equipeId) || situacaoDe(registro.situacaoAprovacao) !== 'APROVADO';
}

/** A faixa do topo do detalhe: situação, a última decisão e o que a pessoa pode fazer. */
export interface FaixaSituacao {
  situacao: Situacao;
  titulo: string;
  /** Aprovar e Rejeitar (GESTOR da equipe e ADMINISTRADOR, item pendente). */
  podeDecidir: boolean;
  /** O autor EXTERNO de um item rejeitado: corrigir e reenviar. */
  corrigirEReenviar: boolean;
}

export function faixaDeSituacao(
  usuario: UsuarioLogado | null,
  entidade: Entidade,
  registro: RegistroAlvo,
): FaixaSituacao | null {
  if (!mostraSituacao(entidade, registro)) {
    return null;
  }
  const situacao = situacaoDe(registro.situacaoAprovacao);
  const autorExterno = usuario?.perfil === 'EXTERNO' && registro.criadoPor?.id === usuario.id;
  const corrigirEReenviar = situacao === 'REJEITADO' && autorExterno && podeEditar(usuario, entidade, registro);
  const titulos: Record<Situacao, string> = {
    PENDENTE: autorExterno ? 'Pendente — aguardando a aprovação' : 'Pendente de aprovação',
    APROVADO: 'Aprovado',
    REJEITADO: corrigirEReenviar ? 'Rejeitado — corrija e reenvie' : 'Rejeitado',
  };
  return { situacao, titulo: titulos[situacao], podeDecidir: podeDecidir(usuario, registro), corrigirEReenviar };
}

/** O botão de salvar na edição: o EXTERNO que altera um item rejeitado está REENVIANDO (RN35). */
export function rotuloSalvarEdicao(usuario: UsuarioLogado | null, registro: RegistroAlvo): string {
  return usuario?.perfil === 'EXTERNO' && situacaoDe(registro.situacaoAprovacao) === 'REJEITADO'
    ? 'Reenviar para aprovação'
    : 'Salvar alterações';
}

/** Opção de select de referência: itens não aprovados (só os do próprio autor aparecem, RN38) ganham a situação no rótulo. */
export function rotuloComSituacao(rotulo: string | null, situacao: string | null | undefined): string | null {
  const s = situacaoDe(situacao);
  if (s === 'APROVADO') {
    return rotulo;
  }
  return `${rotulo ?? '(sem rótulo)'} (${SITUACOES[s].rotulo.toLowerCase()})`;
}

/** "Aguardando há": menos de 1 hora, N horas, N dias ou N meses, desde a criação. */
export function formatarAguardando(desde: string | Date, agora: Date = new Date()): string {
  const minutos = Math.max(0, Math.floor((agora.getTime() - new Date(desde).getTime()) / 60_000));
  const horas = Math.floor(minutos / 60);
  const dias = Math.floor(horas / 24);
  if (horas < 1) {
    return 'menos de 1 hora';
  }
  if (dias < 1) {
    return horas === 1 ? '1 hora' : `${horas} horas`;
  }
  if (dias < 60) {
    return dias === 1 ? '1 dia' : `${dias} dias`;
  }
  return `${Math.floor(dias / 30)} meses`;
}

/**
 * Os filtros da fila (RN36, RN91): as listagens ativas em que o usuário tem equipe e, no filtro de equipe, as equipes dele na
 * listagem filtrada (sem listagem, as de todas elas, sem repetir, por nome). Uma equipe que não está habilitada em nenhuma listagem
 * não aparece: os pendentes dela ficam fora da fila (RN92).
 */
export function opcoesDaFila(
  listagens: readonly Listagem[],
  grupo: string | null | undefined,
): { listagens: Listagem[]; equipes: EquipeDaListagem[] } {
  const comEquipes = listagens.filter((l) => l.ativo && l.minhasEquipes.length > 0);
  const daListagem = grupo ? comEquipes.filter((l) => l.chave === grupo) : comEquipes;
  const porId = new Map<string, EquipeDaListagem>();
  for (const equipe of daListagem.flatMap((l) => l.minhasEquipes)) {
    porId.set(equipe.id, equipe);
  }
  return {
    listagens: comEquipes,
    equipes: [...porId.values()].sort((a, b) => a.nome.localeCompare(b.nome, 'pt-BR')),
  };
}

/** Valor da opção "Todas as áreas" no filtro da fila (na URL: ?todas=1). */
export const TODAS_AS_AREAS = '*';

/**
 * O filtro inicial da fila: sem ?grupo= e sem ?todas=1 na URL, a fila abre filtrada na ÁREA ATUAL, se o usuário tem
 * equipe nela (RN91). Devolve a área para pôr na URL, ou null para deixar como está.
 */
export function areaInicialDaFila(
  parametros: { grupo?: string | null; todas?: string | null },
  areaAtual: string | null,
  listagens: readonly Listagem[],
): string | null {
  if (parametros.grupo || parametros.todas) {
    return null;
  }
  return opcoesDaFila(listagens, null).listagens.some((l) => l.chave === areaAtual) ? areaAtual : null;
}
