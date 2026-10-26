import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Button } from 'primeng/button';
import { Tag } from 'primeng/tag';
import { AuthService } from '../../core/auth/auth.service';
import { NOMES_PERFIL } from '../../core/auth/usuario';
import { severidadePerfil } from '../administracao/comum/apresentacao';
import { DESCRICOES_PERFIL } from '../administracao/modelo/perfis';
import { CabecalhoPaginaComponent } from '../../shared/pagina/cabecalho-pagina.component';

/**
 * /minha-conta — para TODOS os perfis: matrícula, nome, perfil (com o que ele permite) e equipes do usuário logado
 * (GET /api/v1/auth/eu, já carregado), e "Trocar senha", que reaproveita a tela de troca (agora também voluntária) e volta para
 * cá com a mensagem de sucesso. A sessão continua depois da troca (RN25).
 */
@Component({
  selector: 'app-minha-conta',
  imports: [CabecalhoPaginaComponent, RouterLink, Button, Tag],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (auth.usuario(); as usuario) {
      <app-cabecalho-pagina
        [trilha]="[{ rotulo: 'Minha conta' }]"
        titulo="Minha conta"
        icone="pi-user"
        subtitulo="Os seus dados de acesso. Para mudar nome, perfil ou equipes, fale com um administrador."
      >
        <p-button
          acoes
          label="Trocar senha"
          icon="pi pi-key"
          routerLink="/trocar-senha"
          [queryParams]="{ returnUrl: '/minha-conta' }"
        />
      </app-cabecalho-pagina>
      <section class="formulario formulario-estreito" aria-label="Seus dados">
        <dl class="grade-leitura">
          <div>
            <dt>Matrícula</dt>
            <dd>{{ usuario.matricula }}</dd>
          </div>
          <div>
            <dt>Nome</dt>
            <dd>{{ usuario.nome }}</dd>
          </div>
          <div class="campo-largo">
            <dt>Perfil</dt>
            <dd>
              <p-tag [value]="nomePerfil()" [severity]="severidade()" />
              <span class="texto-secundario"> {{ descricaoPerfil() }}</span>
            </dd>
          </div>
          <div class="campo-largo">
            <dt>Equipes</dt>
            <dd>
              @if (usuario.perfil === 'ADMINISTRADOR') {
                Todas as equipes
              } @else {
                <ul class="lista-links">
                  @for (equipe of usuario.equipes; track equipe.id) {
                    <li>{{ equipe.nome }}</li>
                  } @empty {
                    <li class="texto-secundario">Nenhuma equipe ativa.</li>
                  }
                </ul>
              }
            </dd>
          </div>
        </dl>
      </section>
    }
  `,
})
export class MinhaContaComponent {
  readonly auth = inject(AuthService);

  readonly nomePerfil = computed(() => {
    const usuario = this.auth.usuario();
    return usuario ? NOMES_PERFIL[usuario.perfil] : '';
  });
  readonly descricaoPerfil = computed(() => {
    const usuario = this.auth.usuario();
    return usuario ? DESCRICOES_PERFIL[usuario.perfil] : '';
  });
  readonly severidade = computed(() => severidadePerfil(this.auth.usuario()?.perfil ?? 'EXTERNO'));
}
