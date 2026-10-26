import { HttpClient, HttpContext, HttpResponse } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { filter, firstValueFrom } from 'rxjs';
import { Api } from '../../api/api';
import { ApiConfiguration } from '../../api/api-configuration';
import {
  apiV1GruposGrupoEntidadesEntidadeImportacaoModeloGet,
  apiV1GruposGrupoEntidadesEntidadeImportacoesGet,
  apiV1GruposGrupoEntidadesEntidadeImportacoesIdDesfazerPost,
  apiV1GruposGrupoEntidadesEntidadeImportacoesIdExecutarPost,
  apiV1GruposGrupoEntidadesEntidadeImportacoesIdGet,
  apiV1GruposGrupoEntidadesEntidadeImportacoesIdRelatorioGet,
  apiV1GruposGrupoEntidadesEntidadeImportacoesIdValidarPost,
  apiV1GruposGrupoEntidadesEntidadeImportacoesPost,
} from '../../api/functions';
import { RequestBuilder } from '../../api/request-builder';
import { PaginaDe } from '../registros/modelo-registro';
import { nomeDoArquivo } from '../registros/registros.service';
import {
  Destino,
  ImportacaoEnviada,
  OpcoesImportacao,
  PreviaImportacao,
  ResumoImportacao,
  enviadaDeDto,
  previaDeDto,
  resumoDeDto,
} from './modelo-importacao';

/** Página e filtro das linhas da prévia. */
export interface PaginaPrevia {
  pagina: number;
  tamanho: number;
  somenteComErro: boolean;
}

/**
 * Importação de itens por planilha (RN97 a RN104, docs/api/importacao.md), sempre pelo cliente GERADO. Os downloads (modelo e relatório)
 * usam o RequestBuilder com o PATH da operação gerada, como a exportação, para ler o nome do arquivo no Content-Disposition.
 */
@Injectable({ providedIn: 'root' })
export class ImportacaoService {
  private readonly api = inject(Api);
  private readonly http = inject(HttpClient);
  private readonly configuracao = inject(ApiConfiguration);

  baixarModelo(grupo: string, entidade: string): Promise<{ arquivo: Blob; nome: string }> {
    return this.baixar(
      apiV1GruposGrupoEntidadesEntidadeImportacaoModeloGet.PATH,
      { grupo, entidade },
      `modelo_${entidade}.xlsx`,
    );
  }

  baixarRelatorio(grupo: string, entidade: string, id: string): Promise<{ arquivo: Blob; nome: string }> {
    return this.baixar(
      apiV1GruposGrupoEntidadesEntidadeImportacoesIdRelatorioGet.PATH,
      { grupo, entidade, id },
      'relatorio_importacao.xlsx',
    );
  }

  async enviar(grupo: string, entidade: string, arquivo: File, contexto?: HttpContext): Promise<ImportacaoEnviada> {
    return enviadaDeDto(
      await this.api.invoke(
        apiV1GruposGrupoEntidadesEntidadeImportacoesPost,
        { grupo, entidade, body: { arquivo } },
        contexto,
      ),
    );
  }

  async validar(
    grupo: string,
    entidade: string,
    id: string,
    mapeamento: { indice: number; destino: Destino }[],
    opcoes: OpcoesImportacao,
    pagina: PaginaPrevia,
    contexto?: HttpContext,
  ): Promise<PreviaImportacao> {
    return previaDeDto(
      await this.api.invoke(
        apiV1GruposGrupoEntidadesEntidadeImportacoesIdValidarPost,
        {
          grupo,
          entidade,
          id,
          pagina: String(pagina.pagina),
          tamanho: String(pagina.tamanho),
          somenteComErro: String(pagina.somenteComErro),
          body: { mapeamento, opcoes },
        },
        contexto,
      ),
    );
  }

  async obter(grupo: string, entidade: string, id: string, pagina: PaginaPrevia): Promise<PreviaImportacao> {
    return previaDeDto(
      await this.api.invoke(apiV1GruposGrupoEntidadesEntidadeImportacoesIdGet, {
        grupo,
        entidade,
        id,
        pagina: String(pagina.pagina),
        tamanho: String(pagina.tamanho),
        somenteComErro: String(pagina.somenteComErro),
      }),
    );
  }

  async executar(grupo: string, entidade: string, id: string, contexto?: HttpContext): Promise<ResumoImportacao> {
    return resumoDeDto(
      await this.api.invoke(
        apiV1GruposGrupoEntidadesEntidadeImportacoesIdExecutarPost,
        { grupo, entidade, id },
        contexto,
      ),
    );
  }

  async desfazer(grupo: string, entidade: string, id: string, contexto?: HttpContext): Promise<ResumoImportacao> {
    return resumoDeDto(
      await this.api.invoke(
        apiV1GruposGrupoEntidadesEntidadeImportacoesIdDesfazerPost,
        { grupo, entidade, id },
        contexto,
      ),
    );
  }

  async listar(grupo: string, entidade: string, pagina: number, tamanho: number): Promise<PaginaDe<ResumoImportacao>> {
    const dto = await this.api.invoke(apiV1GruposGrupoEntidadesEntidadeImportacoesGet, {
      grupo,
      entidade,
      pagina: String(pagina),
      tamanho: String(tamanho),
    });
    return {
      itens: (dto.itens ?? []).map(resumoDeDto),
      pagina: dto.pagina ?? pagina,
      tamanho: dto.tamanho ?? tamanho,
      total: dto.total ?? 0,
      totalPaginas: dto.totalPaginas ?? 0,
    };
  }

  private async baixar(
    caminho: string,
    parametros: Record<string, string>,
    nomePadrao: string,
  ): Promise<{ arquivo: Blob; nome: string }> {
    const rb = new RequestBuilder(this.configuracao.rootUrl, caminho, 'get');
    for (const [nome, valor] of Object.entries(parametros)) {
      rb.path(nome, valor, {});
    }
    const resposta = await firstValueFrom(
      this.http
        .request<Blob>(rb.build({ responseType: 'blob', accept: '*/*' }))
        .pipe(filter((r): r is HttpResponse<Blob> => r instanceof HttpResponse)),
    );
    return {
      arquivo: resposta.body ?? new Blob(),
      nome: nomeDoArquivo(resposta.headers.get('Content-Disposition')) ?? nomePadrao,
    };
  }
}
