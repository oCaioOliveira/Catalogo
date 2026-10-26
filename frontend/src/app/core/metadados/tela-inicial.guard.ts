import { inject } from '@angular/core';
import { CanActivateFn, RedirectCommand, Router } from '@angular/router';
import { PermissoesService } from '../permissoes/permissoes.service';
import { AreaAtualService } from './area-atual.service';
import { escolherListagem } from './listagens';
import { MetadadosService } from './metadados.service';

// Navegação entre as áreas de gestão (ADR 0043). Nada de grupo ou entidade fixos no front: tudo vem dos metadados.

/**
 * "/": abre a página Início da última área usada (AreaAtualService) ou, na falta dela, da primeira ativa. O administrador sem
 * nenhuma área ativa vai para a administração de áreas de gestão; os demais, para "Nenhuma área de gestão disponível" (RN90:
 * nenhuma área habilitada para as equipes deles).
 */
export const telaInicialGuard: CanActivateFn = async () => {
  // Todo inject() ANTES do primeiro await: depois dele, o guard já saiu do contexto de injeção.
  const router = inject(Router);
  const metadados = inject(MetadadosService);
  const ultima = inject(AreaAtualService).ultima();
  const podeAdministrar = inject(PermissoesService).podeAdministrar();
  try {
    const grupo = escolherListagem(await metadados.listagens(), ultima);
    if (grupo) {
      return router.createUrlTree(['/', grupo]);
    }
  } catch {
    // Metadados fora do ar: o menu mostra o erro com "Tentar de novo".
  }
  return podeAdministrar ? router.createUrlTree(['/configuracoes/areas']) : router.createUrlTree(['/sem-listagens']);
};

/**
 * Rotas de uma área (/:grupo, /:grupo/:entidade/... e /painel/:grupo): ela vira a área atual e a "última usada". Uma área que o
 * usuário não vê (RN90: nenhuma equipe dele habilitada, ou desativada) mostra "não encontrado", sem mudar o endereço.
 */
export const listagemAtualGuard: CanActivateFn = async (rota) => {
  const grupo = rota.paramMap.get('grupo');
  const router = inject(Router);
  const metadados = inject(MetadadosService);
  const atual = inject(AreaAtualService);
  if (grupo) {
    try {
      if (!(await metadados.listagens()).some((l) => l.chave === grupo)) {
        return new RedirectCommand(router.parseUrl('/nao-encontrado'), { skipLocationChange: true });
      }
      atual.definir(grupo);
    } catch {
      // A própria tela mostra o erro.
    }
  }
  return true;
};
