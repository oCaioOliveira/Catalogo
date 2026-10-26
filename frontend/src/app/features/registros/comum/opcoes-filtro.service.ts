import { Injectable, inject } from '@angular/core';
import { MetadadosService } from '../../../core/metadados/metadados.service';
import { OpcaoReferencia } from '../../../core/metadados/modelo';
import { RegistrosService } from '../../../core/registros/registros.service';
import { environment } from '../../../../environments/environment';
import { rotuloDoRegistro } from './rotulos';

/**
 * Opções dos filtros REFERENCIA da listagem. Diferente do formulário, aqui não há "equipe do registro": as
 * opções são os registros da entidade apontada que o usuário VÊ, buscados pela listagem dela
 * (`<campoExibicao>.contem=<busca>`). O endpoint de opções exige a equipe do registro para o administrador
 * e para quem tem várias equipes (RN21), por isso não serve aqui.
 */
@Injectable({ providedIn: 'root' })
export class OpcoesFiltroService {
  private readonly metadados = inject(MetadadosService);
  private readonly registros = inject(RegistrosService);

  async buscar(grupo: string, entidadeChave: string, busca: string): Promise<OpcaoReferencia[]> {
    const entidade = await this.metadados.entidade(grupo, entidadeChave);
    if (!entidade) {
      return [];
    }
    const exibicao = entidade.campos.find((c) => c.chave === entidade.campoExibicao);
    const parametros: Record<string, string> = { tamanho: String(environment.tamanhoPaginaOpcoes) };
    if (exibicao && (exibicao.tipo === 'TEXTO' || exibicao.tipo === 'URL')) {
      parametros['ordenacao'] = exibicao.chave;
      if (busca.trim()) {
        parametros[`${exibicao.chave}.contem`] = busca.trim();
      }
    }
    const pagina = await this.registros.listar(grupo, entidadeChave, parametros);
    return pagina.itens.map((r) => ({ id: r.id, rotulo: rotuloDoRegistro(entidade, r) }));
  }

  /** O rótulo de um registro escolhido num filtro que veio da URL (só com o id). */
  async rotulo(grupo: string, entidadeChave: string, id: string): Promise<string | null> {
    const entidade = await this.metadados.entidade(grupo, entidadeChave);
    if (!entidade) {
      return null;
    }
    try {
      return rotuloDoRegistro(entidade, await this.registros.obter(grupo, entidadeChave, id));
    } catch {
      return null;
    }
  }
}
