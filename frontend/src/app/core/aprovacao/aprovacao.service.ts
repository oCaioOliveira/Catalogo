import { HttpContext } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Api } from '../../api/api';
// Direto do arquivo de cada operação (e não do barril api/functions): este serviço carrega na partida (o contador de pendentes da
// moldura), e o barril leva o cliente inteiro para o bundle inicial (cada função gerada tem um efeito colateral, o `.PATH`, que
// impede o tree-shaking).
import { apiV1AprovacoesPendentesGet } from '../../api/fn/aprovacoes/api-v-1-aprovacoes-pendentes-get';
import { apiV1EquipesEquipeIdConfiguracoesGruposGrupoEntidadesEntidadeAprovacaoGet } from '../../api/fn/configuracoes/api-v-1-equipes-equipe-id-configuracoes-grupos-grupo-entidades-entidade-aprovacao-get';
import { apiV1EquipesEquipeIdConfiguracoesGruposGrupoEntidadesEntidadeAprovacaoPut } from '../../api/fn/configuracoes/api-v-1-equipes-equipe-id-configuracoes-grupos-grupo-entidades-entidade-aprovacao-put';
import { apiV1GruposGrupoEntidadesEntidadeRegistrosIdAprovacaoPost } from '../../api/fn/registros/api-v-1-grupos-grupo-entidades-entidade-registros-id-aprovacao-post';
import { PaginaDe, Registro, registroDeDto } from '../registros/modelo-registro';

/** Um item da fila de aprovação (GET /api/v1/aprovacoes/pendentes, RN36). */
export interface Pendente {
  id: string;
  grupo: string;
  entidade: { chave: string; nome: string };
  equipe: { id: string; nome: string } | null;
  rotulo: string | null;
  criadoPor: { nome: string; matricula: string } | null;
  criadoEm: string;
}

/** Filtros da fila, no formato da API (todos opcionais). */
export interface FiltrosFila {
  grupo?: string;
  entidade?: string;
  equipeId?: string;
}

/**
 * Aprovação pela API (RN33 a RN36), sempre pelo cliente GERADO: a configuração por entidade e equipe, a decisão e a fila. Os
 * erros que a tela mostra no próprio controle (400 da justificativa, 409 de pendentes) vão com o contexto de quem chama.
 */
@Injectable({ providedIn: 'root' })
export class AprovacaoService {
  private readonly api = inject(Api);

  async aprovacaoAtiva(equipeId: string, grupo: string, entidade: string): Promise<boolean> {
    const dto = await this.api.invoke(apiV1EquipesEquipeIdConfiguracoesGruposGrupoEntidadesEntidadeAprovacaoGet, {
      equipeId,
      grupo,
      entidade,
    });
    return dto.ativa ?? false;
  }

  async configurar(
    equipeId: string,
    grupo: string,
    entidade: string,
    ativa: boolean,
    contexto?: HttpContext,
  ): Promise<boolean> {
    const dto = await this.api.invoke(
      apiV1EquipesEquipeIdConfiguracoesGruposGrupoEntidadesEntidadeAprovacaoPut,
      { equipeId, grupo, entidade, body: { ativa } },
      contexto,
    );
    return dto.ativa ?? ativa;
  }

  async decidir(
    grupo: string,
    entidade: string,
    id: string,
    decisao: 'APROVAR' | 'REJEITAR',
    justificativa: string,
    contexto?: HttpContext,
  ): Promise<Registro> {
    return registroDeDto(
      await this.api.invoke(
        apiV1GruposGrupoEntidadesEntidadeRegistrosIdAprovacaoPost,
        { grupo, entidade, id, body: { decisao, justificativa } },
        contexto,
      ),
    );
  }

  /** A fila, do mais antigo para o mais novo (RN36). */
  async pendentes(filtros: FiltrosFila, pagina: number, tamanho: number): Promise<PaginaDe<Pendente>> {
    const resultado = await this.api.invoke(apiV1AprovacoesPendentesGet, {
      grupo: filtros.grupo || undefined,
      entidade: filtros.entidade || undefined,
      equipeId: filtros.equipeId || undefined,
      pagina: String(pagina),
      tamanho: String(tamanho),
    });
    return {
      itens: (resultado.itens ?? []).map((p) => ({
        id: p.id ?? '',
        grupo: p.grupo ?? '',
        entidade: { chave: p.entidade?.chave ?? '', nome: p.entidade?.nome ?? p.entidade?.chave ?? '' },
        equipe: p.equipe ? { id: p.equipe.id ?? '', nome: p.equipe.nome ?? '' } : null,
        rotulo: p.rotulo ?? null,
        criadoPor: p.criadoPor ? { nome: p.criadoPor.nome ?? '', matricula: p.criadoPor.matricula ?? '' } : null,
        criadoEm: p.criadoEm ?? '',
      })),
      pagina: resultado.pagina ?? pagina,
      tamanho: resultado.tamanho ?? tamanho,
      total: resultado.total ?? 0,
      totalPaginas: resultado.totalPaginas ?? 0,
    };
  }

  /** Quantos pendentes o usuário pode decidir (o contador do menu): uma página de um item, só pelo total. */
  async quantidadePendentes(): Promise<number> {
    return (await this.pendentes({}, 1, 1)).total;
  }
}
