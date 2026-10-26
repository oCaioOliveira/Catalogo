import { Injectable, computed, signal } from '@angular/core';
import { Listagem, escolherListagem } from './listagens';

const CHAVE_ULTIMA = 'catalogo.ultimaListagem';

/**
 * A ÁREA DE GESTÃO ATUAL (no código, o grupo): a fonte da verdade de todas as telas.
 *
 *   - nas rotas de uma área (/:grupo/... e /painel/:grupo), ela vem da URL (listagemAtualGuard chama `definir`);
 *   - fora delas (configurações, aprovações, minha conta), vale a ÚLTIMA área usada, guardada no localStorage (com try/catch: sem
 *     armazenamento, vale só nesta aba);
 *   - se essa área não estiver mais visível (RN90), vale a primeira visível (listagens.ts → escolherListagem).
 *
 * Configurações da equipe, painel, fila de aprovações e administração de tipos de item começam por ela, e trocar a área nessas
 * telas chama `definir`: ao voltar para o trabalho, a pessoa está na área que escolheu por último. As áreas visíveis vêm de
 * GET /grupos (o layout chama `atualizarAreas` sempre que os metadados mudam).
 */
@Injectable({ providedIn: 'root' })
export class AreaAtualService {
  /** As áreas que o usuário vê (null = ainda não carregadas). */
  readonly areas = signal<Listagem[] | null>(null);
  readonly ativas = computed(() => (this.areas() ?? []).filter((a) => a.ativo));

  /** A escolhida por último (rota ou seletor), antes de conferir se ainda é visível. */
  private readonly escolhida = signal<string | null>(lerUltima());

  /** A chave da área atual, já conferida contra as visíveis. Antes de carregar as áreas, a escolhida. */
  readonly grupo = computed(() => {
    const areas = this.areas();
    return areas === null ? this.escolhida() : escolherListagem(areas, this.escolhida());
  });

  /** A área atual inteira (nome, equipes do usuário nela), ou null. */
  readonly area = computed(() => this.ativas().find((a) => a.chave === this.grupo()) ?? null);

  definir(grupo: string): void {
    this.escolhida.set(grupo);
    try {
      localStorage.setItem(CHAVE_ULTIMA, grupo);
    } catch {
      // Sem armazenamento: vale só nesta aba.
    }
  }

  atualizarAreas(areas: Listagem[]): void {
    this.areas.set(areas);
  }

  /** A última área usada (pode não ser mais visível: quem usa confere com escolherListagem). */
  ultima(): string | null {
    return this.escolhida();
  }
}

function lerUltima(): string | null {
  try {
    return localStorage.getItem(CHAVE_ULTIMA);
  } catch {
    return null;
  }
}
