import { HttpContext } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Api } from '../../api/api';
import {
  apiV1EquipesGet,
  apiV1EquipesIdAtivacaoPatch,
  apiV1EquipesIdGet,
  apiV1EquipesIdHistoricoGet,
  apiV1EquipesIdPut,
  apiV1EquipesPost,
  apiV1UsuariosGet,
  apiV1UsuariosIdAtivacaoPatch,
  apiV1UsuariosIdGet,
  apiV1UsuariosIdHistoricoGet,
  apiV1UsuariosIdPut,
  apiV1UsuariosIdRedefinirSenhaPost,
  apiV1UsuariosPost,
} from '../../api/functions';
import { ItemHistorico, PaginaDe } from '../registros/modelo-registro';
import { DadosUsuario, EquipeAdmin, UsuarioAdmin, equipeAdminDeDto, usuarioAdminDeDto } from './modelo-administracao';

/** Parâmetros das listagens da administração, já no formato da API (busca, perfil, equipeId, ativo, pagina...). */
export type ParametrosAdministracao = Record<string, string>;

/**
 * Usuários e equipes pela API (só ADMINISTRADOR, RN26), sempre pelo cliente GERADO. A senha provisória (criação e redefinição)
 * é DEVOLVIDA a quem chamou e nunca guardada aqui: ela vai direto para o diálogo que a mostra uma vez (ADR 0041).
 */
@Injectable({ providedIn: 'root' })
export class AdministracaoService {
  private readonly api = inject(Api);

  // ---- Usuários ----

  async listarUsuarios(parametros: ParametrosAdministracao): Promise<PaginaDe<UsuarioAdmin>> {
    const pagina = await this.api.invoke(apiV1UsuariosGet, parametros);
    return {
      itens: (pagina.itens ?? []).map(usuarioAdminDeDto),
      pagina: pagina.pagina ?? 1,
      tamanho: pagina.tamanho ?? 0,
      total: pagina.total ?? 0,
      totalPaginas: pagina.totalPaginas ?? 0,
    };
  }

  /** Todos os usuários de uma equipe (os membros), em páginas de 100. */
  async membrosDaEquipe(equipeId: string): Promise<UsuarioAdmin[]> {
    const membros: UsuarioAdmin[] = [];
    for (let pagina = 1; ; pagina++) {
      const resultado = await this.listarUsuarios({ equipeId, tamanho: '100', pagina: String(pagina) });
      membros.push(...resultado.itens);
      if (pagina >= resultado.totalPaginas) {
        return membros;
      }
    }
  }

  async obterUsuario(id: string): Promise<UsuarioAdmin> {
    return usuarioAdminDeDto(await this.api.invoke(apiV1UsuariosIdGet, { id }));
  }

  /** Cria o usuário e devolve a senha provisória (uma vez, RN25): quem chama a passa direto ao diálogo. */
  async criarUsuario(
    dados: DadosUsuario,
    contexto?: HttpContext,
  ): Promise<{ usuario: UsuarioAdmin; senhaProvisoria: string }> {
    const criado = await this.api.invoke(apiV1UsuariosPost, { body: dados }, contexto);
    return { usuario: usuarioAdminDeDto(criado.usuario ?? {}), senhaProvisoria: criado.senhaProvisoria ?? '' };
  }

  async atualizarUsuario(id: string, dados: DadosUsuario, contexto?: HttpContext): Promise<UsuarioAdmin> {
    return usuarioAdminDeDto(await this.api.invoke(apiV1UsuariosIdPut, { id, body: dados }, contexto));
  }

  async alterarAtivacaoUsuario(id: string, ativo: boolean, contexto?: HttpContext): Promise<UsuarioAdmin> {
    return usuarioAdminDeDto(await this.api.invoke(apiV1UsuariosIdAtivacaoPatch, { id, body: { ativo } }, contexto));
  }

  /** Nova senha provisória (uma vez): quem chama a passa direto ao diálogo. */
  async redefinirSenha(id: string): Promise<string> {
    return (await this.api.invoke(apiV1UsuariosIdRedefinirSenhaPost, { id })).senhaProvisoria ?? '';
  }

  async historicoUsuario(id: string, pagina: number, tamanho: number): Promise<PaginaDe<ItemHistorico>> {
    return paginaHistorico(
      await this.api.invoke(apiV1UsuariosIdHistoricoGet, { id, pagina: String(pagina), tamanho: String(tamanho) }),
      tamanho,
    );
  }

  // ---- Equipes ----

  async listarEquipes(parametros: ParametrosAdministracao): Promise<PaginaDe<EquipeAdmin>> {
    const pagina = await this.api.invoke(apiV1EquipesGet, parametros);
    return {
      itens: (pagina.itens ?? []).map(equipeAdminDeDto),
      pagina: pagina.pagina ?? 1,
      tamanho: pagina.tamanho ?? 0,
      total: pagina.total ?? 0,
      totalPaginas: pagina.totalPaginas ?? 0,
    };
  }

  /** Todas as equipes (ativas e inativas), para nomes no histórico e opções de filtro. */
  async todasAsEquipes(): Promise<EquipeAdmin[]> {
    const equipes: EquipeAdmin[] = [];
    for (let pagina = 1; ; pagina++) {
      const resultado = await this.listarEquipes({ tamanho: '100', pagina: String(pagina) });
      equipes.push(...resultado.itens);
      if (pagina >= resultado.totalPaginas) {
        return equipes;
      }
    }
  }

  async obterEquipe(id: string): Promise<EquipeAdmin> {
    return equipeAdminDeDto(await this.api.invoke(apiV1EquipesIdGet, { id }));
  }

  async criarEquipe(nome: string, contexto?: HttpContext): Promise<EquipeAdmin> {
    return equipeAdminDeDto(await this.api.invoke(apiV1EquipesPost, { body: { nome } }, contexto));
  }

  async atualizarEquipe(id: string, nome: string, contexto?: HttpContext): Promise<EquipeAdmin> {
    return equipeAdminDeDto(await this.api.invoke(apiV1EquipesIdPut, { id, body: { nome } }, contexto));
  }

  async alterarAtivacaoEquipe(id: string, ativa: boolean, contexto?: HttpContext): Promise<EquipeAdmin> {
    return equipeAdminDeDto(await this.api.invoke(apiV1EquipesIdAtivacaoPatch, { id, body: { ativa } }, contexto));
  }

  async historicoEquipe(id: string, pagina: number, tamanho: number): Promise<PaginaDe<ItemHistorico>> {
    return paginaHistorico(
      await this.api.invoke(apiV1EquipesIdHistoricoGet, { id, pagina: String(pagina), tamanho: String(tamanho) }),
      tamanho,
    );
  }
}

function paginaHistorico(
  resposta: { itens?: ItemHistorico[] | null; pagina?: number; total?: number; totalPaginas?: number },
  tamanho: number,
): PaginaDe<ItemHistorico> {
  return {
    itens: resposta.itens ?? [],
    pagina: resposta.pagina ?? 1,
    tamanho,
    total: resposta.total ?? 0,
    totalPaginas: resposta.totalPaginas ?? 0,
  };
}
