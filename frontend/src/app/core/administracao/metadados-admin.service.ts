import { Injectable, inject } from '@angular/core';
import { Api } from '../../api/api';
import {
  apiV1AdminMetadadosGruposGet,
  apiV1AdminMetadadosGruposGrupoAtivacaoPatch,
  apiV1AdminMetadadosGruposGrupoEntidadesEntidadeAtivacaoPatch,
  apiV1AdminMetadadosGruposGrupoEntidadesEntidadeCamposCampoAtivacaoPatch,
  apiV1AdminMetadadosGruposGrupoEntidadesEntidadeCamposCampoDelete,
  apiV1AdminMetadadosGruposGrupoEntidadesEntidadeCamposCampoDependenciaDelete,
  apiV1AdminMetadadosGruposGrupoEntidadesEntidadeCamposCampoDependenciaPut,
  apiV1AdminMetadadosGruposGrupoEntidadesEntidadeCamposCampoPut,
  apiV1AdminMetadadosGruposGrupoEntidadesEntidadeCamposOrdemPut,
  apiV1AdminMetadadosGruposGrupoEntidadesEntidadeCamposPost,
  apiV1AdminMetadadosGruposGrupoEntidadesEntidadePut,
  apiV1AdminMetadadosGruposGrupoEntidadesEntidadeRestricoesPost,
  apiV1AdminMetadadosGruposGrupoEntidadesEntidadeRestricoesRestricaoDelete,
  apiV1AdminMetadadosGruposGrupoEntidadesPost,
  apiV1AdminMetadadosGruposGrupoEquipesGet,
  apiV1AdminMetadadosGruposGrupoEquipesPut,
  apiV1AdminMetadadosGruposGrupoGet,
  apiV1AdminMetadadosGruposGrupoHistoricoGet,
  apiV1AdminMetadadosGruposGrupoPut,
  apiV1AdminMetadadosGruposPost,
} from '../../api/functions';
import {
  CampoEntrada,
  DependenciaEntrada,
  EntidadeEntrada,
  GrupoEntrada,
  ResultadoAdministracaoDto,
} from '../../api/models';
import { tratarNoComponente } from '../erros/contexto-erros';
import { MetadadosService } from '../metadados/metadados.service';
import { ItemHistorico, PaginaDe } from '../registros/modelo-registro';
import {
  EquipesDaListagem,
  EstruturaListagem,
  ResultadoAlteracao,
  equipesDaListagemDeDto,
  ResumoListagem,
  estruturaDeDto,
  resultadoDeDto,
  resumoDeDto,
} from './estrutura-listagem';

/**
 * Administração de listagens pela API (/api/v1/admin/metadados, só ADMINISTRADOR, RN50), sempre pelo cliente GERADO. Toda
 * escrita recebe `simular`: as telas simulam ANTES de gravar (ADR 0044). Os erros (400 por campo, 409 de versão ou de bloqueio)
 * ficam com quem chamou (o AlteracaoEstruturaService), sem toast. Depois de uma gravação, os metadados guardados no front são
 * descartados (MetadadosService.invalidar): o menu e as telas da listagem mudam na hora.
 */
@Injectable({ providedIn: 'root' })
export class MetadadosAdminService {
  private readonly api = inject(Api);
  private readonly metadados = inject(MetadadosService);
  private readonly contexto = tratarNoComponente(400, 409);

  async listagens(): Promise<ResumoListagem[]> {
    return (await this.api.invoke(apiV1AdminMetadadosGruposGet)).map(resumoDeDto);
  }

  async estrutura(grupo: string): Promise<EstruturaListagem> {
    return estruturaDeDto(await this.api.invoke(apiV1AdminMetadadosGruposGrupoGet, { grupo }));
  }

  async historico(grupo: string, pagina: number, tamanho: number, entidade?: string): Promise<PaginaDe<ItemHistorico>> {
    const resultado = await this.api.invoke(apiV1AdminMetadadosGruposGrupoHistoricoGet, {
      grupo,
      entidade,
      pagina: String(pagina),
      tamanho: String(tamanho),
    });
    return {
      itens: resultado.itens ?? [],
      pagina: resultado.pagina ?? pagina,
      tamanho: resultado.tamanho ?? tamanho,
      total: resultado.total ?? 0,
      totalPaginas: resultado.totalPaginas ?? 0,
    };
  }

  // ---- Grupo (listagem) ----

  criarListagem(corpo: GrupoEntrada, simular: boolean): Promise<ResultadoAlteracao> {
    return this.escrever(corpo.chave ?? '', simular, () =>
      this.api.invoke(apiV1AdminMetadadosGruposPost, { simular, body: corpo }, this.contexto),
    );
  }

  alterarListagem(grupo: string, corpo: GrupoEntrada, simular: boolean): Promise<ResultadoAlteracao> {
    return this.escrever(grupo, simular, () =>
      this.api.invoke(apiV1AdminMetadadosGruposGrupoPut, { grupo, simular, body: corpo }, this.contexto),
    );
  }

  ativarListagem(grupo: string, versao: string, ativo: boolean, simular: boolean): Promise<ResultadoAlteracao> {
    return this.escrever(grupo, simular, () =>
      this.api.invoke(
        apiV1AdminMetadadosGruposGrupoAtivacaoPatch,
        { grupo, simular, body: { versao, ativo } },
        this.contexto,
      ),
    );
  }

  // ---- Equipes habilitadas (RN90 a RN93) ----

  async equipes(grupo: string): Promise<EquipesDaListagem> {
    return equipesDaListagemDeDto(await this.api.invoke(apiV1AdminMetadadosGruposGrupoEquipesGet, { grupo }));
  }

  /** A lista COMPLETA das equipes habilitadas (vazia = nenhuma) e a versão do grupo. */
  definirEquipes(grupo: string, equipes: string[], versao: string, simular: boolean): Promise<ResultadoAlteracao> {
    return this.escrever(grupo, simular, () =>
      this.api.invoke(
        apiV1AdminMetadadosGruposGrupoEquipesPut,
        { grupo, simular, body: { versao, equipes } },
        this.contexto,
      ),
    );
  }

  // ---- Entidade (tipo de item) ----

  criarEntidade(grupo: string, corpo: EntidadeEntrada, simular: boolean): Promise<ResultadoAlteracao> {
    return this.escrever(grupo, simular, () =>
      this.api.invoke(apiV1AdminMetadadosGruposGrupoEntidadesPost, { grupo, simular, body: corpo }, this.contexto),
    );
  }

  alterarEntidade(
    grupo: string,
    entidade: string,
    corpo: EntidadeEntrada,
    simular: boolean,
  ): Promise<ResultadoAlteracao> {
    return this.escrever(grupo, simular, () =>
      this.api.invoke(
        apiV1AdminMetadadosGruposGrupoEntidadesEntidadePut,
        { grupo, entidade, simular, body: corpo },
        this.contexto,
      ),
    );
  }

  ativarEntidade(
    grupo: string,
    entidade: string,
    versao: string,
    ativo: boolean,
    simular: boolean,
  ): Promise<ResultadoAlteracao> {
    return this.escrever(grupo, simular, () =>
      this.api.invoke(
        apiV1AdminMetadadosGruposGrupoEntidadesEntidadeAtivacaoPatch,
        { grupo, entidade, simular, body: { versao, ativo } },
        this.contexto,
      ),
    );
  }

  // ---- Campos ----

  criarCampo(grupo: string, entidade: string, corpo: CampoEntrada, simular: boolean): Promise<ResultadoAlteracao> {
    return this.escrever(grupo, simular, () =>
      this.api.invoke(
        apiV1AdminMetadadosGruposGrupoEntidadesEntidadeCamposPost,
        { grupo, entidade, simular, body: corpo },
        this.contexto,
      ),
    );
  }

  alterarCampo(
    grupo: string,
    entidade: string,
    campo: string,
    corpo: CampoEntrada,
    simular: boolean,
  ): Promise<ResultadoAlteracao> {
    return this.escrever(grupo, simular, () =>
      this.api.invoke(
        apiV1AdminMetadadosGruposGrupoEntidadesEntidadeCamposCampoPut,
        { grupo, entidade, campo, simular, body: corpo },
        this.contexto,
      ),
    );
  }

  ativarCampo(
    grupo: string,
    entidade: string,
    campo: string,
    versao: string,
    ativo: boolean,
    simular: boolean,
  ): Promise<ResultadoAlteracao> {
    return this.escrever(grupo, simular, () =>
      this.api.invoke(
        apiV1AdminMetadadosGruposGrupoEntidadesEntidadeCamposCampoAtivacaoPatch,
        { grupo, entidade, campo, simular, body: { versao, ativo } },
        this.contexto,
      ),
    );
  }

  excluirCampo(
    grupo: string,
    entidade: string,
    campo: string,
    versao: string,
    simular: boolean,
  ): Promise<ResultadoAlteracao> {
    return this.escrever(grupo, simular, () =>
      this.api.invoke(
        apiV1AdminMetadadosGruposGrupoEntidadesEntidadeCamposCampoDelete,
        { grupo, entidade, campo, versao, simular },
        this.contexto,
      ),
    );
  }

  reordenarCampos(
    grupo: string,
    entidade: string,
    versao: string,
    campos: string[],
    simular: boolean,
  ): Promise<ResultadoAlteracao> {
    return this.escrever(grupo, simular, () =>
      this.api.invoke(
        apiV1AdminMetadadosGruposGrupoEntidadesEntidadeCamposOrdemPut,
        { grupo, entidade, simular, body: { versao, campos } },
        this.contexto,
      ),
    );
  }

  // ---- Unicidades e dependências ----

  criarRestricao(
    grupo: string,
    entidade: string,
    versao: string,
    nome: string,
    campos: string[],
    simular: boolean,
  ): Promise<ResultadoAlteracao> {
    return this.escrever(grupo, simular, () =>
      this.api.invoke(
        apiV1AdminMetadadosGruposGrupoEntidadesEntidadeRestricoesPost,
        { grupo, entidade, simular, body: { versao, nome, campos } },
        this.contexto,
      ),
    );
  }

  excluirRestricao(
    grupo: string,
    entidade: string,
    restricao: string,
    versao: string,
    simular: boolean,
  ): Promise<ResultadoAlteracao> {
    return this.escrever(grupo, simular, () =>
      this.api.invoke(
        apiV1AdminMetadadosGruposGrupoEntidadesEntidadeRestricoesRestricaoDelete,
        { grupo, entidade, restricao, versao, simular },
        this.contexto,
      ),
    );
  }

  definirDependencia(
    grupo: string,
    entidade: string,
    campo: string,
    corpo: DependenciaEntrada,
    simular: boolean,
  ): Promise<ResultadoAlteracao> {
    return this.escrever(grupo, simular, () =>
      this.api.invoke(
        apiV1AdminMetadadosGruposGrupoEntidadesEntidadeCamposCampoDependenciaPut,
        { grupo, entidade, campo, simular, body: corpo },
        this.contexto,
      ),
    );
  }

  excluirDependencia(
    grupo: string,
    entidade: string,
    campo: string,
    versao: string,
    simular: boolean,
  ): Promise<ResultadoAlteracao> {
    return this.escrever(grupo, simular, () =>
      this.api.invoke(
        apiV1AdminMetadadosGruposGrupoEntidadesEntidadeCamposCampoDependenciaDelete,
        { grupo, entidade, campo, versao, simular },
        this.contexto,
      ),
    );
  }

  private async escrever(
    grupo: string,
    simular: boolean,
    chamar: () => Promise<ResultadoAdministracaoDto>,
  ): Promise<ResultadoAlteracao> {
    const resultado = resultadoDeDto(await chamar());
    if (!simular) {
      this.metadados.invalidar(grupo);
    }
    return resultado;
  }
}
