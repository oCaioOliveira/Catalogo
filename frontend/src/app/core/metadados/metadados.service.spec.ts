import { TestBed } from '@angular/core/testing';
import { Api } from '../../api/api';
import { EntidadeDto, GrupoDto } from '../../api/models';
import { MetadadosService } from './metadados.service';

describe('MetadadosService (validade do cache)', () => {
  let servico: MetadadosService;
  let chamadas: string[];
  let agora: number;

  beforeEach(() => {
    chamadas = [];
    const apiFalsa = {
      invoke: (fn: { PATH: string }, params?: { grupo?: string }) => {
        chamadas.push(params?.grupo ?? fn.PATH);
        if (params?.grupo) {
          return Promise.resolve([{ chave: 'relatorio_teste', nome: 'Relatório', campos: [] }] as EntidadeDto[]);
        }
        return Promise.resolve([{ chave: 'testes_desempenho', nome: 'Testes', ativo: true }] as GrupoDto[]);
      },
    };
    TestBed.configureTestingModule({ providers: [{ provide: Api, useValue: apiFalsa }] });
    servico = TestBed.inject(MetadadosService);
    agora = 1_000_000;
    servico.agora = () => agora;
  });

  it('dentro de 5 minutos, usa o que já carregou; depois, recarrega', async () => {
    await servico.entidades('testes_desempenho');
    agora += 4 * 60_000;
    await servico.entidades('testes_desempenho');
    expect(chamadas).toEqual(['testes_desempenho']);

    agora += 2 * 60_000;
    await servico.entidades('testes_desempenho');
    expect(chamadas).toEqual(['testes_desempenho', 'testes_desempenho']);
  });

  it('invalidar descarta na hora (depois de uma alteração na administração) e muda a versão', async () => {
    await servico.entidades('testes_desempenho');
    await servico.listagens();
    const versao = servico.versao();

    servico.invalidar('testes_desempenho');
    await servico.entidades('testes_desempenho');
    await servico.listagens();

    expect(chamadas.filter((c) => c === 'testes_desempenho').length).toBe(2);
    expect(chamadas.length).toBe(4);
    expect(servico.versao()).toBe(versao + 1);
  });

  it('a lista de listagens também tem validade', async () => {
    await servico.listagens();
    await servico.nomeDoGrupo('testes_desempenho');
    expect(chamadas.length).toBe(1);
    agora += 5 * 60_000;
    expect(await servico.nomeDoGrupo('testes_desempenho')).toBe('Testes');
    expect(chamadas.length).toBe(2);
  });
});
