import { HttpContext } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Api } from '../../api/api';
import {
  apiV1EquipesEquipeIdConfiguracoesGruposGrupoEntidadesEntidadeAvaliacaoAtivacaoPatch,
  apiV1EquipesEquipeIdConfiguracoesGruposGrupoEntidadesEntidadeAvaliacaoModelosGet,
  apiV1EquipesEquipeIdConfiguracoesGruposGrupoEntidadesEntidadeAvaliacaoPost,
  apiV1EquipesEquipeIdConfiguracoesGruposGrupoEntidadesEntidadeAvaliacaoPut,
  apiV1GruposGrupoEntidadesEntidadeRegistrosIdAvaliacoesGet,
  apiV1GruposGrupoEntidadesEntidadeRegistrosIdAvaliacoesMinhaDelete,
  apiV1GruposGrupoEntidadesEntidadeRegistrosIdAvaliacoesMinhaPut,
  apiV1GruposGrupoEntidadesGet,
} from '../../api/functions';
import { AvaliacoesDoRegistroDto, ModeloAvaliacaoEntrada } from '../../api/models';
import { CriterioAvaliacao, ModeloAvaliacao, criterioDeDto, modeloDeDto } from './modelo-avaliacao';

/** O que o item precisa saber da equipe DELE: se a aprovação está ativa e o formulário de avaliação ativo (RN33, RN40). */
export interface RecursosDoItem {
  requerAprovacao: boolean;
  formulario: { modeloId: string; nome: string; criterios: CriterioAvaliacao[] } | null;
}

/**
 * Avaliação pela API (RN40 a RN48), sempre pelo cliente GERADO: os modelos por entidade e equipe (quem configura) e as avaliações
 * dos itens (quem avalia). Os erros que a tela mostra no controle (400 por critério, 409 de versão) vão com o contexto de quem chama.
 */
@Injectable({ providedIn: 'root' })
export class AvaliacaoService {
  private readonly api = inject(Api);

  /** Todos os modelos (o ativo e os inativos) da entidade na equipe, o mais novo primeiro. */
  async modelos(equipeId: string, grupo: string, entidade: string): Promise<ModeloAvaliacao[]> {
    const dtos = await this.api.invoke(
      apiV1EquipesEquipeIdConfiguracoesGruposGrupoEntidadesEntidadeAvaliacaoModelosGet,
      {
        equipeId,
        grupo,
        entidade,
      },
    );
    return dtos.map(modeloDeDto);
  }

  async criarModelo(
    equipeId: string,
    grupo: string,
    entidade: string,
    corpo: ModeloAvaliacaoEntrada,
    contexto?: HttpContext,
  ): Promise<ModeloAvaliacao> {
    return modeloDeDto(
      await this.api.invoke(
        apiV1EquipesEquipeIdConfiguracoesGruposGrupoEntidadesEntidadeAvaliacaoPost,
        { equipeId, grupo, entidade, body: corpo },
        contexto,
      ),
    );
  }

  /** PUT do modelo ATIVO, com a lista inteira de critérios e a `versao` lida (RN43, RN12). */
  async alterarModelo(
    equipeId: string,
    grupo: string,
    entidade: string,
    corpo: ModeloAvaliacaoEntrada,
    contexto?: HttpContext,
  ): Promise<ModeloAvaliacao> {
    return modeloDeDto(
      await this.api.invoke(
        apiV1EquipesEquipeIdConfiguracoesGruposGrupoEntidadesEntidadeAvaliacaoPut,
        { equipeId, grupo, entidade, body: corpo },
        contexto,
      ),
    );
  }

  /** Desativa o ativo (`ativo: false`) ou reativa um modelo (`ativo: true` com o `modeloId`, RN41). */
  async ativarModelo(
    equipeId: string,
    grupo: string,
    entidade: string,
    ativo: boolean,
    modeloId: string | null,
    contexto?: HttpContext,
  ): Promise<ModeloAvaliacao> {
    return modeloDeDto(
      await this.api.invoke(
        apiV1EquipesEquipeIdConfiguracoesGruposGrupoEntidadesEntidadeAvaliacaoAtivacaoPatch,
        { equipeId, grupo, entidade, body: { ativo, modeloId } },
        contexto,
      ),
    );
  }

  /**
   * A aprovação e o formulário de avaliação da entidade NA EQUIPE DO ITEM (GET .../entidades?equipeId=). Sem equipe (entidade
   * global) não há nenhum dos dois.
   */
  async recursosDoItem(grupo: string, entidade: string, equipeId: string | null | undefined): Promise<RecursosDoItem> {
    if (!equipeId) {
      return { requerAprovacao: false, formulario: null };
    }
    const dto = (await this.api.invoke(apiV1GruposGrupoEntidadesGet, { grupo, equipeId })).find(
      (e) => e.chave === entidade,
    );
    const formulario = dto?.modeloAvaliacao;
    return {
      requerAprovacao: dto?.requerAprovacao ?? false,
      formulario: formulario
        ? {
            modeloId: formulario.modeloId ?? '',
            nome: formulario.nome ?? '',
            criterios: (formulario.criterios ?? []).map(criterioDeDto).sort((a, b) => a.ordem - b.ordem),
          }
        : null,
    };
  }

  /** Resumos e avaliações individuais de um item (RN47). `avaliacoes` vem nulo para o EXTERNO. */
  avaliacoesDoItem(
    grupo: string,
    entidade: string,
    id: string,
    pagina: number,
    tamanho: number,
  ): Promise<AvaliacoesDoRegistroDto> {
    return this.api.invoke(apiV1GruposGrupoEntidadesEntidadeRegistrosIdAvaliacoesGet, {
      grupo,
      entidade,
      id,
      pagina: String(pagina),
      tamanho: String(tamanho),
    });
  }

  /** Cria (sem `versao`) ou edita (com a `versao` lida) a avaliação do usuário logado (RN45). */
  async salvarMinha(
    grupo: string,
    entidade: string,
    id: string,
    respostas: Record<string, unknown>,
    versao: string | null,
    contexto?: HttpContext,
  ): Promise<void> {
    await this.api.invoke(
      apiV1GruposGrupoEntidadesEntidadeRegistrosIdAvaliacoesMinhaPut,
      { grupo, entidade, id, body: { versao, respostas } },
      contexto,
    );
  }

  async removerMinha(grupo: string, entidade: string, id: string, contexto?: HttpContext): Promise<void> {
    await this.api.invoke(
      apiV1GruposGrupoEntidadesEntidadeRegistrosIdAvaliacoesMinhaDelete,
      { grupo, entidade, id },
      contexto,
    );
  }
}
