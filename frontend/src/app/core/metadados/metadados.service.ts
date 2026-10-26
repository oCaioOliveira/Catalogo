import { Injectable, inject, signal } from '@angular/core';
import { Api } from '../../api/api';
// Direto do arquivo de cada operação (e não do barril api/functions): este serviço carrega na partida, e o barril leva o cliente
// inteiro para o bundle inicial (cada função gerada tem um efeito colateral, o `.PATH`, que impede o tree-shaking).
import { apiV1GruposGrupoEntidadesGet } from '../../api/fn/entidades/api-v-1-grupos-grupo-entidades-get';
import { apiV1GruposGet } from '../../api/fn/grupos/api-v-1-grupos-get';
import { EquipeDaListagem, Listagem, cacheValido } from './listagens';
import { Entidade, entidadeDeDto } from './modelo';

interface EmCache<T> {
  valor: T;
  carregadoEm: number;
}

/**
 * Metadados dos grupos (GET /api/v1/grupos/{grupo}/entidades) e a lista de grupos (GET /api/v1/grupos), guardados em memória
 * por VALIDADE_METADADOS_MS (5 minutos, listagens.ts). Toda tela pega daqui a entidade e os campos: nada de tela escrita à mão
 * por tipo de item. A administração de listagens chama `invalidar` depois de cada alteração gravada: a próxima leitura vem da API,
 * e quem observa `versao` (o menu) recarrega na hora.
 */
@Injectable({ providedIn: 'root' })
export class MetadadosService {
  private readonly api = inject(Api);

  /** Relógio (trocado nos testes). */
  agora: () => number = () => Date.now();

  /** Entidades por grupo, já carregadas. */
  private readonly cache = new Map<string, EmCache<Entidade[]>>();
  private grupos: EmCache<Listagem[]> | null = null;

  /** Pedidos em andamento, para dois componentes não carregarem o mesmo grupo ao mesmo tempo. */
  private readonly pendentes = new Map<string, Promise<Entidade[]>>();
  private gruposPendentes: Promise<Listagem[]> | null = null;

  /** Muda a cada invalidação: quem mostra metadados (o menu) recarrega quando ela muda. */
  readonly versao = signal(0);

  async entidades(grupo: string): Promise<Entidade[]> {
    const carregadas = this.cache.get(grupo);
    if (carregadas && cacheValido(carregadas.carregadoEm, this.agora())) {
      return carregadas.valor;
    }

    let pedido = this.pendentes.get(grupo);
    if (!pedido) {
      pedido = this.api
        .invoke(apiV1GruposGrupoEntidadesGet, { grupo })
        .then((dtos) => dtos.map(entidadeDeDto))
        .finally(() => this.pendentes.delete(grupo));
      this.pendentes.set(grupo, pedido);
    }

    const entidades = await pedido;
    this.cache.set(grupo, { valor: entidades, carregadoEm: this.agora() });
    return entidades;
  }

  /** A entidade pela chave, ou null se ela não existe (ou o usuário não a vê). */
  async entidade(grupo: string, chave: string): Promise<Entidade | null> {
    return (await this.entidades(grupo)).find((e) => e.chave === chave) ?? null;
  }

  /**
   * As listagens que o usuário vê (GET /api/v1/grupos), na ordem da API. O administrador recebe também as desativadas
   * (`ativo` false); o seletor do menu mostra só as ativas.
   */
  async listagens(): Promise<Listagem[]> {
    if (this.grupos && cacheValido(this.grupos.carregadoEm, this.agora())) {
      return this.grupos.valor;
    }
    this.gruposPendentes ??= this.api
      .invoke(apiV1GruposGet)
      .then((dtos) =>
        dtos.map((g) => ({
          chave: g.chave ?? '',
          nome: g.nome ?? g.chave ?? '',
          descricao: g.descricao?.trim() || null,
          ativo: g.ativo ?? true,
          minhasEquipes: (g.minhasEquipes ?? []).map((e) => ({ id: e.id ?? '', nome: e.nome ?? '' })),
          possuiEquipes: g.possuiEquipes ?? false,
        })),
      )
      .finally(() => (this.gruposPendentes = null));
    const listagens = await this.gruposPendentes;
    this.grupos = { valor: listagens, carregadoEm: this.agora() };
    return listagens;
  }

  /** A listagem pela chave, ou null se o usuário não a vê (RN90). */
  async listagem(grupo: string): Promise<Listagem | null> {
    return (await this.listagens()).find((g) => g.chave === grupo) ?? null;
  }

  /** As equipes do usuário NESTA listagem (RN91); vazia se ele não a vê. */
  async equipesDaListagem(grupo: string): Promise<EquipeDaListagem[]> {
    return (await this.listagem(grupo))?.minhasEquipes ?? [];
  }

  /** O nome do grupo, para o título do menu e as trilhas. */
  async nomeDoGrupo(grupo: string): Promise<string> {
    return (await this.listagens()).find((g) => g.chave === grupo)?.nome ?? grupo;
  }

  /**
   * Descarta os metadados guardados (de um grupo e a lista de grupos, ou tudo): a próxima leitura vem da API. Chamado pela
   * administração depois de cada alteração gravada e pelo formulário quando a API recusa campos desatualizados.
   */
  invalidar(grupo?: string): void {
    if (grupo) {
      this.cache.delete(grupo);
    } else {
      this.cache.clear();
    }
    this.grupos = null;
    this.versao.update((v) => v + 1);
  }

  /** Esquece tudo (no logout: outro usuário pode ver outras entidades). */
  limpar(): void {
    this.invalidar();
  }
}
