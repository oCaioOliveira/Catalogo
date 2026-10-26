import { HttpClient, HttpContext, HttpResponse } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { filter, firstValueFrom } from 'rxjs';
import { rotuloComSituacao } from '../aprovacao/regras-aprovacao';
import { Api } from '../../api/api';
import { ApiConfiguration } from '../../api/api-configuration';
import {
  apiV1EquipesGet,
  apiV1GruposGrupoEuRecentesGet,
  apiV1GruposGrupoEntidadesEntidadeCamposCampoOpcoesGet,
  apiV1GruposGrupoEntidadesEntidadeRegistrosExportarGet,
  apiV1GruposGrupoEntidadesEntidadeRegistrosGet,
  apiV1GruposGrupoEntidadesEntidadeRegistrosIdDelete,
  apiV1GruposGrupoEntidadesEntidadeRegistrosIdGet,
  apiV1GruposGrupoEntidadesEntidadeRegistrosIdRelacionadosGet,
  apiV1GruposGrupoEntidadesEntidadeRegistrosIdHistoricoGet,
  apiV1GruposGrupoEntidadesEntidadeRegistrosIdPut,
  apiV1GruposGrupoEntidadesEntidadeRegistrosPost,
} from '../../api/functions';
import { RegistroDtoPaginaDto } from '../../api/models';
import { RequestBuilder } from '../../api/request-builder';
import { OpcaoReferencia } from '../metadados/modelo';
import {
  CorpoRegistro,
  ItemHistorico,
  PaginaDe,
  Recente,
  Registro,
  Relacionado,
  registroDeDto,
} from './modelo-registro';

/** Parâmetros de URL da listagem e da exportação: filtros (RN18), página e ordenação, já no formato da API. */
export type ParametrosConsulta = Record<string, string>;

/**
 * Acesso aos registros pela API, sempre pelo cliente GERADO (src/app/api). A listagem e a exportação aceitam
 * filtros que dependem da entidade (não estão no contrato): por isso montam a requisição com o RequestBuilder
 * gerado e o PATH da operação gerada, acrescentando os filtros um a um.
 */
@Injectable({ providedIn: 'root' })
export class RegistrosService {
  private readonly api = inject(Api);
  private readonly http = inject(HttpClient);
  private readonly configuracao = inject(ApiConfiguration);

  async listar(
    grupo: string,
    entidade: string,
    parametros: ParametrosConsulta,
    contexto?: HttpContext,
  ): Promise<PaginaDe<Registro>> {
    const rb = new RequestBuilder(this.configuracao.rootUrl, apiV1GruposGrupoEntidadesEntidadeRegistrosGet.PATH, 'get');
    rb.path('grupo', grupo, {});
    rb.path('entidade', entidade, {});
    for (const [nome, valor] of Object.entries(parametros)) {
      rb.query(nome, valor, {});
    }
    const resposta = await firstValueFrom(
      this.http
        .request<RegistroDtoPaginaDto>(
          rb.build({ responseType: 'json', accept: 'application/json', context: contexto }),
        )
        .pipe(filter((r): r is HttpResponse<RegistroDtoPaginaDto> => r instanceof HttpResponse)),
    );
    const pagina = resposta.body ?? {};
    return {
      itens: (pagina.itens ?? []).map(registroDeDto),
      pagina: pagina.pagina ?? 1,
      tamanho: pagina.tamanho ?? 0,
      total: pagina.total ?? 0,
      totalPaginas: pagina.totalPaginas ?? 0,
    };
  }

  async obter(grupo: string, entidade: string, id: string, contexto?: HttpContext): Promise<Registro> {
    return registroDeDto(
      await this.api.invoke(apiV1GruposGrupoEntidadesEntidadeRegistrosIdGet, { grupo, entidade, id }, contexto),
    );
  }

  /** RN94: os itens relacionados ao registro (uma entrada por campo REFERENCIA que aponta para ele, com a quantidade). */
  async relacionados(grupo: string, entidade: string, id: string): Promise<Relacionado[]> {
    const dtos = await this.api.invoke(apiV1GruposGrupoEntidadesEntidadeRegistrosIdRelacionadosGet, {
      grupo,
      entidade,
      id,
    });
    return dtos.map((r) => ({
      entidade: {
        chave: r.entidade?.chave ?? '',
        nome: r.entidade?.nome ?? '',
        nomePlural: r.entidade?.nomePlural?.trim() || null,
      },
      campo: { chave: r.campo?.chave ?? '', nome: r.campo?.nome ?? '' },
      filha: r.filha ?? false,
      quantidade: r.quantidade ?? 0,
    }));
  }

  /** RN96: os itens que o usuário criou ou alterou por último na área (no máximo 8), só os que ele ainda vê. */
  async recentes(grupo: string): Promise<Recente[]> {
    const dtos = await this.api.invoke(apiV1GruposGrupoEuRecentesGet, { grupo });
    return dtos.map((r) => ({
      id: r.id ?? '',
      entidade: r.entidade ?? '',
      rotulo: r.rotulo ?? null,
      ocorridoEm: r.ocorridoEm ?? '',
    }));
  }

  /** Quantos itens do tipo o usuário vê (o total da listagem, com uma página de um item só). */
  async contar(grupo: string, entidade: string): Promise<number> {
    return (await this.listar(grupo, entidade, { tamanho: '1' })).total;
  }

  async criar(grupo: string, entidade: string, corpo: CorpoRegistro, contexto?: HttpContext): Promise<Registro> {
    return registroDeDto(
      await this.api.invoke(apiV1GruposGrupoEntidadesEntidadeRegistrosPost, { grupo, entidade, body: corpo }, contexto),
    );
  }

  /** PUT: o registro INTEIRO (campo ausente = apagado, RN15) e a `versao` lida (RN12). */
  async atualizar(
    grupo: string,
    entidade: string,
    id: string,
    corpo: CorpoRegistro,
    contexto?: HttpContext,
  ): Promise<Registro> {
    return registroDeDto(
      await this.api.invoke(
        apiV1GruposGrupoEntidadesEntidadeRegistrosIdPut,
        { grupo, entidade, id, body: corpo },
        contexto,
      ),
    );
  }

  async excluir(grupo: string, entidade: string, id: string, contexto?: HttpContext): Promise<void> {
    await this.api.invoke(apiV1GruposGrupoEntidadesEntidadeRegistrosIdDelete, { grupo, entidade, id }, contexto);
  }

  async historico(
    grupo: string,
    entidade: string,
    id: string,
    pagina: number,
    tamanho = 20,
  ): Promise<PaginaDe<ItemHistorico>> {
    const resposta = await this.api.invoke(apiV1GruposGrupoEntidadesEntidadeRegistrosIdHistoricoGet, {
      grupo,
      entidade,
      id,
      pagina: String(pagina),
      tamanho: String(tamanho),
    });
    return {
      itens: resposta.itens ?? [],
      pagina: resposta.pagina ?? 1,
      tamanho: resposta.tamanho ?? tamanho,
      total: resposta.total ?? 0,
      totalPaginas: resposta.totalPaginas ?? 0,
    };
  }

  /**
   * Opções de um campo REFERENCIA para o FORMULÁRIO (RN21): os registros que podem ser escolhidos para um registro
   * da equipe `equipeId`, filtrados pela `busca` e pelo valor do campo de que ele depende.
   */
  async opcoes(
    grupo: string,
    entidade: string,
    campo: string,
    filtro: { busca?: string; dependeDe?: string; equipeId?: string | null; tamanho: number },
  ): Promise<OpcaoReferencia[]> {
    const pagina = await this.api.invoke(apiV1GruposGrupoEntidadesEntidadeCamposCampoOpcoesGet, {
      grupo,
      entidade,
      campo,
      busca: filtro.busca || undefined,
      dependeDe: filtro.dependeDe || undefined,
      // Obrigatório para o administrador e para quem tem mais de uma equipe (RN21).
      equipeId: filtro.equipeId || undefined,
      tamanho: String(filtro.tamanho),
    });
    // RN38: itens não aprovados (só os do próprio autor chegam aqui) ganham a situação no rótulo: "Relatório X (pendente)".
    return (pagina.itens ?? []).map((o) => ({
      id: o.id ?? '',
      rotulo: rotuloComSituacao(o.rotulo ?? null, o.situacaoAprovacao),
    }));
  }

  /** CSV ou XLSX com os mesmos filtros e ordenação da listagem (RN49). O nome vem do Content-Disposition. */
  async exportar(
    grupo: string,
    entidade: string,
    formato: 'csv' | 'xlsx',
    parametros: ParametrosConsulta,
  ): Promise<{ arquivo: Blob; nome: string }> {
    const rb = new RequestBuilder(
      this.configuracao.rootUrl,
      apiV1GruposGrupoEntidadesEntidadeRegistrosExportarGet.PATH,
      'get',
    );
    rb.path('grupo', grupo, {});
    rb.path('entidade', entidade, {});
    for (const [nome, valor] of Object.entries(parametros)) {
      if (nome !== 'pagina' && nome !== 'tamanho') {
        rb.query(nome, valor, {});
      }
    }
    rb.query('formato', formato, {});
    const resposta = await firstValueFrom(
      this.http
        .request<Blob>(rb.build({ responseType: 'blob', accept: '*/*' }))
        .pipe(filter((r): r is HttpResponse<Blob> => r instanceof HttpResponse)),
    );
    return {
      arquivo: resposta.body ?? new Blob(),
      nome: nomeDoArquivo(resposta.headers.get('Content-Disposition')) ?? `${entidade}.${formato}`,
    };
  }

  /** Todas as equipes ativas (só o ADMINISTRADOR pode listar). */
  async equipesAtivas(): Promise<{ id: string; nome: string }[]> {
    const pagina = await this.api.invoke(apiV1EquipesGet, { ativa: 'true', tamanho: '100' });
    return (pagina.itens ?? []).map((e) => ({ id: e.id ?? '', nome: e.nome ?? '' }));
  }
}

/** "attachment; filename=x.csv; filename*=UTF-8''x.csv" → "x.csv". */
export function nomeDoArquivo(cabecalho: string | null): string | null {
  if (!cabecalho) {
    return null;
  }
  const utf8 = /filename\*=UTF-8''([^;]+)/i.exec(cabecalho);
  if (utf8) {
    return decodeURIComponent(utf8[1].trim());
  }
  const simples = /filename="?([^";]+)"?/i.exec(cabecalho);
  return simples ? simples[1].trim() : null;
}
