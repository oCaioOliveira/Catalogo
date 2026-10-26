import { HttpClient, HttpContext, HttpResponse } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { filter, firstValueFrom } from 'rxjs';
import { Api } from '../../api/api';
import { ApiConfiguration } from '../../api/api-configuration';
import {
  apiV1PainelGruposGrupoEntidadesEntidadeIndicadoresExportarGet,
  apiV1PainelGruposGrupoEntidadesEntidadeIndicadoresGet,
  apiV1PainelGruposGrupoVisaoGeralGet,
} from '../../api/functions';
import { IndicadoresDto } from '../../api/models';
import { RequestBuilder } from '../../api/request-builder';
import { nomeDoArquivo } from '../registros/registros.service';
import { FiltrosPainel, Indicadores, VisaoGeral, indicadoresDeDto } from './modelo-painel';

/**
 * O painel gerencial pela API (docs/api/painel.md), sempre pelo cliente GERADO. Os indicadores aceitam os filtros da listagem,
 * que dependem da entidade e não estão no contrato: como em RegistrosService.listar, a requisição é montada com o RequestBuilder
 * e o PATH da operação gerada, acrescentando os parâmetros um a um.
 */
@Injectable({ providedIn: 'root' })
export class PainelService {
  private readonly api = inject(Api);
  private readonly http = inject(HttpClient);
  private readonly configuracao = inject(ApiConfiguration);

  async visaoGeral(grupo: string, filtros: FiltrosPainel, contexto?: HttpContext): Promise<VisaoGeral> {
    return this.api.invoke(
      apiV1PainelGruposGrupoVisaoGeralGet,
      { grupo, de: filtros.de, ate: filtros.ate, equipeId: filtros.equipeId ?? undefined },
      contexto,
    );
  }

  /** A agregação configurável. `parametros` já no formato da API (agruparPor, metricas, filtros...). */
  async indicadores(
    grupo: string,
    entidade: string,
    parametros: Record<string, string>,
    contexto?: HttpContext,
  ): Promise<Indicadores> {
    const rb = this.requisicao(apiV1PainelGruposGrupoEntidadesEntidadeIndicadoresGet.PATH, grupo, entidade, parametros);
    const resposta = await firstValueFrom(
      this.http
        .request<IndicadoresDto>(rb.build({ responseType: 'json', accept: 'application/json', context: contexto }))
        .pipe(filter((r): r is HttpResponse<IndicadoresDto> => r instanceof HttpResponse)),
    );
    return indicadoresDeDto(resposta.body ?? {});
  }

  /** A mesma agregação em CSV ou XLSX (RN88). O nome vem do Content-Disposition. */
  async exportarIndicadores(
    grupo: string,
    entidade: string,
    formato: 'csv' | 'xlsx',
    parametros: Record<string, string>,
  ): Promise<{ arquivo: Blob; nome: string }> {
    const rb = this.requisicao(apiV1PainelGruposGrupoEntidadesEntidadeIndicadoresExportarGet.PATH, grupo, entidade, {
      ...parametros,
      formato,
    });
    const resposta = await firstValueFrom(
      this.http
        .request<Blob>(rb.build({ responseType: 'blob', accept: '*/*' }))
        .pipe(filter((r): r is HttpResponse<Blob> => r instanceof HttpResponse)),
    );
    return {
      arquivo: resposta.body ?? new Blob(),
      nome: nomeDoArquivo(resposta.headers.get('Content-Disposition')) ?? `${entidade}_indicadores.${formato}`,
    };
  }

  private requisicao(
    caminho: string,
    grupo: string,
    entidade: string,
    parametros: Record<string, string>,
  ): RequestBuilder {
    const rb = new RequestBuilder(this.configuracao.rootUrl, caminho, 'get');
    rb.path('grupo', grupo, {});
    rb.path('entidade', entidade, {});
    for (const [nome, valor] of Object.entries(parametros)) {
      rb.query(nome, valor, {});
    }
    return rb;
  }
}
