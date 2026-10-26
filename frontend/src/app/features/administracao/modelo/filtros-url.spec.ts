import { environment } from '../../../../environments/environment';
import {
  filtrosEquipesDaUrl,
  filtrosUsuariosDaUrl,
  parametrosApiUsuarios,
  parametrosDosFiltrosEquipes,
  parametrosDosFiltrosUsuarios,
} from './filtros-url';

describe('filtros da administração na URL (ida e volta)', () => {
  it('usuários: sem parâmetros, os padrões, e a URL fica vazia', () => {
    const filtros = filtrosUsuariosDaUrl({});
    expect(filtros).toEqual({
      busca: '',
      perfil: null,
      equipeId: null,
      ativo: null,
      pagina: 1,
      tamanho: environment.tamanhoPagina,
      ordenacao: 'nome',
    });
    expect(parametrosDosFiltrosUsuarios(filtros)).toEqual({});
  });

  it('usuários: todos os filtros, página e ordenação vão e voltam iguais', () => {
    const parametros = {
      busca: 'joão',
      perfil: 'GESTOR',
      equipeId: 'e1',
      ativo: 'false',
      pagina: '3',
      tamanho: '50',
      ordenacao: '-ultimoAcessoEm',
    };
    expect(parametrosDosFiltrosUsuarios(filtrosUsuariosDaUrl(parametros))).toEqual(parametros);
  });

  it('usuários: valores inválidos na URL viram o padrão', () => {
    const filtros = filtrosUsuariosDaUrl({
      perfil: 'CHEFE',
      ativo: 'talvez',
      pagina: '0',
      tamanho: '500',
      ordenacao: 'senha',
    });
    expect([filtros.perfil, filtros.ativo, filtros.pagina, filtros.tamanho, filtros.ordenacao]).toEqual([
      null,
      null,
      1,
      environment.tamanhoPagina,
      'nome',
    ]);
  });

  it('usuários: a API recebe sempre página, tamanho e ordenação', () => {
    expect(parametrosApiUsuarios(filtrosUsuariosDaUrl({ busca: ' ana ' }))).toEqual({
      busca: 'ana',
      pagina: '1',
      tamanho: String(environment.tamanhoPagina),
      ordenacao: 'nome',
    });
  });

  it('equipes: busca, situação e página vão e voltam iguais', () => {
    const parametros = { busca: 'squad', ativa: 'true', pagina: '2' };
    expect(parametrosDosFiltrosEquipes(filtrosEquipesDaUrl(parametros))).toEqual(parametros);
    expect(parametrosDosFiltrosEquipes(filtrosEquipesDaUrl({}))).toEqual({});
  });
});
