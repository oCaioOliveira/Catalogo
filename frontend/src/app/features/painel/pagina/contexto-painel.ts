import { Injectable, computed, inject, signal } from '@angular/core';
import { AuthService } from '../../../core/auth/auth.service';
import { MetadadosService } from '../../../core/metadados/metadados.service';
import { Entidade } from '../../../core/metadados/modelo';
import { opcoesDeEquipe } from '../modelo/estado-url';

/**
 * O que as duas páginas do painel carregam igual: os metadados do grupo (para validar widgets e montar as opções da análise),
 * o nome do grupo e as opções do filtro de equipe: as equipes do usuário NESTA listagem (RN91; para o administrador, todas as
 * habilitadas nela), que vêm junto com a lista de listagens. Fornecido em cada página.
 */
@Injectable()
export class ContextoPainel {
  private readonly auth = inject(AuthService);
  private readonly metadados = inject(MetadadosService);

  readonly entidades = signal<Entidade[] | null>(null);
  readonly nomeGrupo = signal('');
  readonly erro = signal(false);
  private readonly equipesDaListagem = signal<{ id: string; nome: string }[]>([]);

  readonly opcoesEquipe = computed(() => opcoesDeEquipe(this.auth.usuario()?.perfil, this.equipesDaListagem()));

  async carregar(grupo: string): Promise<boolean> {
    this.erro.set(false);
    try {
      const [entidades, nome, equipes] = await Promise.all([
        this.metadados.entidades(grupo),
        this.metadados.nomeDoGrupo(grupo),
        this.metadados.equipesDaListagem(grupo),
      ]);
      this.entidades.set(entidades);
      this.nomeGrupo.set(nome);
      this.equipesDaListagem.set(equipes);
    } catch {
      this.erro.set(true);
      return false;
    }
    return true;
  }
}
