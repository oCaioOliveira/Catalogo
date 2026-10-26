import { UsuarioLogado } from '../auth/usuario';
import { Entidade } from '../metadados/modelo';

/**
 * Quem pode o quê na tela (RN26, RN27, aprovação RN33 a RN37, avaliação RN40, RN44, RN47). É SÓ conveniência para esconder
 * botões: quem decide de verdade é o back-end, e os 403 dele continuam tratados. Funções puras (testadas em permissoes.spec.ts);
 * o PermissoesService só as liga ao usuário logado.
 *
 *   EXTERNO: só vê. Com a APROVAÇÃO ativa numa equipe dele, também cria (o item nasce pendente) e altera os PRÓPRIOS itens
 *     enquanto PENDENTES ou REJEITADOS (alterar um rejeitado é reenviar). Nunca exclui, decide nem avalia.
 *   INTERNO: cria, edita e avalia.  GESTOR: cria, edita, exclui, avalia e decide nas suas equipes.  ADMINISTRADOR: tudo.
 *   Entidades GLOBAIS: só o ADMINISTRADOR cria, edita e exclui.
 *   Por equipe: o não-administrador só altera registros das equipes dele; cria só nas equipes dele NA listagem (RN91).
 *   Painel, configurações da equipe e fila de aprovação: GESTOR e ADMINISTRADOR. Administração: só ADMINISTRADOR (RN26).
 */

/** O que importa do registro para a permissão: a equipe, o autor e a situação de aprovação. */
export interface RegistroAlvo {
  equipeId?: string | null;
  criadoPor?: { id?: string } | null;
  situacaoAprovacao?: string | null;
}

function ehDaEquipe(usuario: UsuarioLogado, registro: RegistroAlvo): boolean {
  return usuario.perfil === 'ADMINISTRADOR' || usuario.equipes.some((e) => e.id === registro.equipeId);
}

/** RN33: a aprovação está ativa para a entidade na equipe (das que o usuário vê)? */
export function aprovacaoAtiva(entidade: Entidade, equipeId: string | null | undefined): boolean {
  return !!equipeId && entidade.equipesComAprovacao.includes(equipeId);
}

/** Uma equipe como as telas a oferecem (id e nome). */
export interface EquipeOferecida {
  id: string;
  nome: string;
}

/**
 * As equipes em que o usuário pode CRIAR um item desta entidade (o select de equipe do formulário). Parte das equipes do usuário
 * NESTA listagem (RN91, `minhasEquipes` de GET /grupos: as dele habilitadas nela; para o ADMINISTRADOR, todas as habilitadas).
 * EXTERNO: só as em que a aprovação está ativa (RN34).
 */
export function equipesParaCriar(
  usuario: UsuarioLogado | null,
  entidade: Entidade,
  equipesNaListagem: readonly EquipeOferecida[],
): EquipeOferecida[] {
  if (!usuario) {
    return [];
  }
  return usuario.perfil === 'EXTERNO'
    ? equipesNaListagem.filter((e) => aprovacaoAtiva(entidade, e.id))
    : [...equipesNaListagem];
}

/**
 * Criar: globais só o ADMINISTRADOR; por equipe, é preciso ter onde criar NESTA listagem (RN91). Vale também para o
 * ADMINISTRADOR: numa listagem sem equipe habilitada (RN93), ele não cria itens por equipe.
 */
export function podeCriar(
  usuario: UsuarioLogado | null,
  entidade: Entidade,
  equipesNaListagem: readonly EquipeOferecida[],
): boolean {
  if (!usuario || !entidade.ativo) {
    return false;
  }
  if (!entidade.escopoEquipe) {
    return usuario.perfil === 'ADMINISTRADOR';
  }
  return equipesParaCriar(usuario, entidade, equipesNaListagem).length > 0;
}

export function podeEditar(usuario: UsuarioLogado | null, entidade: Entidade, registro: RegistroAlvo): boolean {
  if (!usuario || !entidade.ativo) {
    return false;
  }
  if (usuario.perfil === 'ADMINISTRADOR') {
    return true;
  }
  if (!entidade.escopoEquipe || !ehDaEquipe(usuario, registro)) {
    return false;
  }
  if (usuario.perfil === 'EXTERNO') {
    // RN35: só os próprios, PENDENTES ou REJEITADOS, com a aprovação ativa. Aprovado não volta a ser editado por ele.
    return (
      aprovacaoAtiva(entidade, registro.equipeId) &&
      registro.criadoPor?.id === usuario.id &&
      (registro.situacaoAprovacao === 'PENDENTE' || registro.situacaoAprovacao === 'REJEITADO')
    );
  }
  return true;
}

export function podeExcluir(usuario: UsuarioLogado | null, entidade: Entidade, registro: RegistroAlvo): boolean {
  if (!podeEditar(usuario, entidade, registro)) {
    return false;
  }
  return usuario!.perfil === 'GESTOR' || usuario!.perfil === 'ADMINISTRADOR';
}

/** RN36: decide (aprova ou rejeita) o GESTOR da equipe do registro ou o ADMINISTRADOR, e só registros PENDENTES. */
export function podeDecidir(usuario: UsuarioLogado | null, registro: RegistroAlvo): boolean {
  if (!usuario || registro.situacaoAprovacao !== 'PENDENTE') {
    return false;
  }
  return usuario.perfil === 'ADMINISTRADOR' || (usuario.perfil === 'GESTOR' && ehDaEquipe(usuario, registro));
}

/**
 * RN33 e RN40: configurar a aprovação e os modelos de avaliação de uma equipe é do GESTOR (nas dele) e do ADMINISTRADOR. Também
 * decide quem vê a fila de aprovação (RN36).
 */
export function podeConfigurarEquipe(usuario: UsuarioLogado | null): boolean {
  return usuario?.perfil === 'GESTOR' || usuario?.perfil === 'ADMINISTRADOR';
}

export function podeVerAprovacoes(usuario: UsuarioLogado | null): boolean {
  return podeConfigurarEquipe(usuario);
}

/** RN44: avaliam INTERNO, GESTOR e ADMINISTRADOR (o EXTERNO não). A situação e o modelo são conferidos na tela. */
export function podeAvaliar(usuario: UsuarioLogado | null): boolean {
  return !!usuario && usuario.perfil !== 'EXTERNO';
}

/** RN47: as avaliações individuais são só para INTERNO, GESTOR e ADMINISTRADOR; o EXTERNO vê só o resumo. */
export function podeVerAvaliacoesIndividuais(usuario: UsuarioLogado | null): boolean {
  return podeAvaliar(usuario);
}

/** Exportar é ver: todos os perfis exportam o que veem. */
export function podeExportar(usuario: UsuarioLogado | null): boolean {
  return usuario !== null;
}

/** RN78: o painel gerencial é só para GESTOR e ADMINISTRADOR (a API dá 403 aos demais). */
export function podeVerPainel(usuario: UsuarioLogado | null): boolean {
  return usuario?.perfil === 'GESTOR' || usuario?.perfil === 'ADMINISTRADOR';
}

/** RN26: usuários, equipes e listagens são administrados só pelo ADMINISTRADOR (a API dá 403 aos demais). */
export function podeAdministrar(usuario: UsuarioLogado | null): boolean {
  return usuario?.perfil === 'ADMINISTRADOR';
}

/** "Minha conta" (ver os próprios dados e trocar a senha): qualquer perfil logado. */
export function podeVerMinhaConta(usuario: UsuarioLogado | null): boolean {
  return usuario !== null;
}
