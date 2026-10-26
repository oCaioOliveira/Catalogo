import { Injectable, inject } from '@angular/core';
import { AuthService } from '../auth/auth.service';
import { Entidade } from '../metadados/modelo';
import {
  EquipeOferecida,
  RegistroAlvo,
  equipesParaCriar,
  podeAdministrar,
  podeAvaliar,
  podeConfigurarEquipe,
  podeCriar,
  podeDecidir,
  podeEditar,
  podeExcluir,
  podeExportar,
  podeVerAprovacoes,
  podeVerAvaliacoesIndividuais,
  podeVerMinhaConta,
  podeVerPainel,
} from './permissoes';

/** As regras de permissão (permissoes.ts) para o usuário logado. ÚNICO lugar que os componentes consultam. */
@Injectable({ providedIn: 'root' })
export class PermissoesService {
  private readonly auth = inject(AuthService);

  /** `equipesNaListagem`: as equipes do usuário na listagem (RN91, `Listagem.minhasEquipes`). */
  podeCriar(entidade: Entidade, equipesNaListagem: readonly EquipeOferecida[]): boolean {
    return podeCriar(this.auth.usuario(), entidade, equipesNaListagem);
  }

  /** As equipes do select de equipe ao criar: as do usuário na listagem (EXTERNO: só as com aprovação ativa). */
  equipesParaCriar(entidade: Entidade, equipesNaListagem: readonly EquipeOferecida[]): EquipeOferecida[] {
    return equipesParaCriar(this.auth.usuario(), entidade, equipesNaListagem);
  }

  podeEditar(entidade: Entidade, registro: RegistroAlvo): boolean {
    return podeEditar(this.auth.usuario(), entidade, registro);
  }

  podeExcluir(entidade: Entidade, registro: RegistroAlvo): boolean {
    return podeExcluir(this.auth.usuario(), entidade, registro);
  }

  podeDecidir(registro: RegistroAlvo): boolean {
    return podeDecidir(this.auth.usuario(), registro);
  }

  podeConfigurarEquipe(): boolean {
    return podeConfigurarEquipe(this.auth.usuario());
  }

  podeVerAprovacoes(): boolean {
    return podeVerAprovacoes(this.auth.usuario());
  }

  podeAvaliar(): boolean {
    return podeAvaliar(this.auth.usuario());
  }

  podeVerAvaliacoesIndividuais(): boolean {
    return podeVerAvaliacoesIndividuais(this.auth.usuario());
  }

  podeExportar(): boolean {
    return podeExportar(this.auth.usuario());
  }

  podeVerPainel(): boolean {
    return podeVerPainel(this.auth.usuario());
  }

  podeAdministrar(): boolean {
    return podeAdministrar(this.auth.usuario());
  }

  podeVerMinhaConta(): boolean {
    return podeVerMinhaConta(this.auth.usuario());
  }
}
