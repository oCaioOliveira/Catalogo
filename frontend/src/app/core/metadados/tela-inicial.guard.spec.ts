import { TestBed } from '@angular/core/testing';
import {
  ActivatedRouteSnapshot,
  RedirectCommand,
  RouterStateSnapshot,
  UrlTree,
  convertToParamMap,
  provideRouter,
} from '@angular/router';
import { PermissoesService } from '../permissoes/permissoes.service';
import { AreaAtualService } from './area-atual.service';
import { Listagem } from './listagens';
import { MetadadosService } from './metadados.service';
import { listagemAtualGuard, telaInicialGuard } from './tela-inicial.guard';

// ADR 0043: a tela inicial vem dos metadados (sem listagem nem entidade fixas no front).
describe('guards da tela inicial', () => {
  let listagens: Listagem[];
  let ultima: string | null;
  let administrador: boolean;
  let definida: string | null;

  beforeEach(() => {
    listagens = [];
    ultima = null;
    administrador = false;
    definida = null;
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        { provide: MetadadosService, useValue: { listagens: async () => listagens } },
        {
          provide: AreaAtualService,
          useValue: { ultima: () => ultima, grupo: () => null, definir: (g: string) => (definida = g) },
        },
        { provide: PermissoesService, useValue: { podeAdministrar: () => administrador } },
      ],
    });
  });

  async function rodar(guard: typeof telaInicialGuard, grupo?: string): Promise<string> {
    const rota = { paramMap: convertToParamMap(grupo ? { grupo } : {}) } as ActivatedRouteSnapshot;
    const resultado = await TestBed.runInInjectionContext(() => guard(rota, {} as RouterStateSnapshot));
    return (resultado as UrlTree).toString();
  }

  it('"/" abre o Início da última área usada, se ativa; senão, da primeira ativa', async () => {
    listagens = [
      { chave: 'inventario', nome: 'Inventário', ativo: true, minhasEquipes: [], possuiEquipes: true },
      { chave: 'testes_desempenho', nome: 'Testes', ativo: true, minhasEquipes: [], possuiEquipes: true },
    ];
    ultima = 'testes_desempenho';
    expect(await rodar(telaInicialGuard)).toBe('/testes_desempenho');
    ultima = null;
    expect(await rodar(telaInicialGuard)).toBe('/inventario');
  });

  it('"/" sem área visível: o administrador vai para a administração; os demais, para "Nenhuma área de gestão disponível"', async () => {
    // RN90: a API não devolve nenhuma listagem (nenhuma habilitada para as equipes do usuário).
    expect(await rodar(telaInicialGuard)).toBe('/sem-listagens');
    administrador = true;
    expect(await rodar(telaInicialGuard)).toBe('/configuracoes/areas');
  });

  it('RN90: uma área que o usuário não vê mostra "não encontrado" sem mudar o endereço; uma visível vira a atual', async () => {
    listagens = [{ chave: 'testes_desempenho', nome: 'Testes', ativo: true, minhasEquipes: [], possuiEquipes: true }];
    const rota = (grupo: string) => ({ paramMap: convertToParamMap({ grupo }) }) as ActivatedRouteSnapshot;
    const oculta = await TestBed.runInInjectionContext(() =>
      listagemAtualGuard(rota('tasks'), {} as RouterStateSnapshot),
    );
    expect(oculta instanceof RedirectCommand).toBeTrue();
    expect((oculta as RedirectCommand).redirectTo.toString()).toBe('/nao-encontrado');
    expect((oculta as RedirectCommand).navigationBehaviorOptions?.skipLocationChange).toBeTrue();
    expect(definida).toBeNull();

    const visivel = await TestBed.runInInjectionContext(() =>
      listagemAtualGuard(rota('testes_desempenho'), {} as RouterStateSnapshot),
    );
    expect(visivel).toBeTrue();
    expect(definida as string | null).toBe('testes_desempenho');
  });
});
