import { ChangeDetectionStrategy, Component, computed, inject, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Button } from 'primeng/button';
import { Message } from 'primeng/message';
import { Select } from 'primeng/select';
import {
  CampoEstrutura,
  EntidadeEstrutura,
  EstruturaListagem,
} from '../../../../core/administracao/estrutura-listagem';
import { MetadadosAdminService } from '../../../../core/administracao/metadados-admin.service';
import { AlteracaoEstruturaService, Desfecho } from '../comum/alteracao-estrutura.service';
import {
  EscolhaDependencia,
  TrechoFrase,
  alvosPossiveis,
  camposDependentes,
  exemploDependencia,
  fraseDaDependenciaAtual,
  fraseDependencia,
  origensPossiveis,
  viasPossiveis,
} from '../modelo/dependencias';

/** Valor do select "via" que quer dizer "neste mesmo item" (o p-select não lida bem com null como opção). */
const SEM_VIA = '__mesmo_item__';

/**
 * Aba Dependências (RN08, RN21, RN55, RN73): para cada campo REFERENCIA, a regra que filtra as opções dele, montada por um
 * CONSTRUTOR GUIADO em frase. Os selects são encadeados (via → origem → alvo) e só oferecem combinações válidas
 * (modelo/dependencias.ts); abaixo, a frase e um exemplo de como o campo se comporta no formulário. Criar com itens que não
 * combinam com a regra é bloqueado (relatório com os itens).
 */
@Component({
  selector: 'app-aba-dependencias',
  imports: [FormsModule, Button, Message, Select],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './aba-dependencias.component.html',
})
export class AbaDependenciasComponent {
  readonly estrutura = input.required<EstruturaListagem>();
  readonly entidade = input.required<EntidadeEstrutura>();
  readonly alterada = output<EstruturaListagem | null>();

  private readonly admin = inject(MetadadosAdminService);
  private readonly alteracao = inject(AlteracaoEstruturaService);

  readonly dependentes = computed(() => camposDependentes(this.entidade()));
  readonly SEM_VIA = SEM_VIA;

  /** O campo cuja regra está sendo montada (null = nenhum construtor aberto). */
  readonly editando = signal<string | null>(null);
  readonly via = signal<string | null>(null);
  readonly origem = signal<string | null>(null);
  readonly alvo = signal<string | null>(null);
  readonly erros = signal<string[]>([]);

  readonly dependente = computed<CampoEstrutura | null>(
    () => this.dependentes().find((c) => c.chave === this.editando()) ?? null,
  );
  readonly opcoesVia = computed(() => {
    const dependente = this.dependente();
    if (!dependente) {
      return [];
    }
    return viasPossiveis(this.estrutura(), this.entidade(), dependente).map((v) => ({
      valor: v?.chave ?? SEM_VIA,
      rotulo: v ? `do item escolhido em "${v.nome}"` : 'neste mesmo formulário',
    }));
  });
  readonly opcoesOrigem = computed(() => {
    const dependente = this.dependente();
    const via = this.via();
    if (!dependente || via === null) {
      return [];
    }
    return origensPossiveis(this.estrutura(), this.entidade(), dependente, via === SEM_VIA ? null : via).map((c) => ({
      valor: c.chave,
      rotulo: c.nome,
    }));
  });
  readonly opcoesAlvo = computed(() => {
    const dependente = this.dependente();
    const via = this.via();
    const origem = this.origensCampos().find((c) => c.chave === this.origem());
    if (!dependente || !origem || via === null) {
      return [];
    }
    return alvosPossiveis(this.estrutura(), dependente, origem).map((c) => ({ valor: c.chave, rotulo: c.nome }));
  });
  readonly escolha = computed<EscolhaDependencia | null>(() => {
    const [via, origem, alvo] = [this.via(), this.origem(), this.alvo()];
    return via && origem && alvo ? { via: via === SEM_VIA ? null : via, origem, alvo } : null;
  });
  readonly frase = computed<TrechoFrase[] | null>(() => {
    const [dependente, escolha] = [this.dependente(), this.escolha()];
    return dependente && escolha ? fraseDependencia(this.estrutura(), this.entidade(), dependente, escolha) : null;
  });
  readonly exemplo = computed(() => {
    const [dependente, escolha] = [this.dependente(), this.escolha()];
    return dependente && escolha ? exemploDependencia(this.entidade(), dependente, escolha) : null;
  });

  private origensCampos(): CampoEstrutura[] {
    const dependente = this.dependente();
    const via = this.via();
    return dependente && via !== null
      ? origensPossiveis(this.estrutura(), this.entidade(), dependente, via === SEM_VIA ? null : via)
      : [];
  }

  fraseAtual(campo: CampoEstrutura): TrechoFrase[] | null {
    return fraseDaDependenciaAtual(this.estrutura(), this.entidade(), campo);
  }

  temCombinacoes(campo: CampoEstrutura): boolean {
    return viasPossiveis(this.estrutura(), this.entidade(), campo).length > 0;
  }

  abrir(campo: CampoEstrutura): void {
    this.erros.set([]);
    this.editando.set(campo.chave);
    const atual = campo.dependencia;
    this.via.set(atual ? (atual.via ?? SEM_VIA) : null);
    this.origem.set(atual?.origem ?? null);
    this.alvo.set(atual?.alvo ?? null);
    // Com uma opção só, já escolhe (menos cliques, e a frase aparece na hora).
    this.escolherUnicas();
  }

  escolherVia(via: string | null): void {
    this.via.set(via);
    this.origem.set(null);
    this.alvo.set(null);
    this.escolherUnicas();
  }

  escolherOrigem(origem: string | null): void {
    this.origem.set(origem);
    this.alvo.set(null);
    this.escolherUnicas();
  }

  private escolherUnicas(): void {
    if (this.via() === null && this.opcoesVia().length === 1) {
      this.via.set(this.opcoesVia()[0].valor);
    }
    if (this.origem() === null && this.opcoesOrigem().length === 1) {
      this.origem.set(this.opcoesOrigem()[0].valor);
    }
    if (this.alvo() === null && this.opcoesAlvo().length === 1) {
      this.alvo.set(this.opcoesAlvo()[0].valor);
    }
  }

  cancelar(): void {
    this.editando.set(null);
  }

  async salvar(): Promise<void> {
    const [dependente, escolha] = [this.dependente(), this.escolha()];
    if (!dependente || !escolha) {
      return;
    }
    const entidade = this.entidade();
    const desfecho = await this.alteracao.executar({
      grupo: this.estrutura().chave,
      entidade: entidade.chave,
      titulo: `Definir a dependência de "${dependente.nome}"`,
      chamar: (simular) =>
        this.admin.definirDependencia(
          this.estrutura().chave,
          entidade.chave,
          dependente.chave,
          { versao: entidade.versao, campoVia: escolha.via, campoOrigem: escolha.origem, campoAlvo: escolha.alvo },
          simular,
        ),
    });
    this.tratar(desfecho);
  }

  async remover(campo: CampoEstrutura): Promise<void> {
    const entidade = this.entidade();
    const desfecho = await this.alteracao.executar({
      grupo: this.estrutura().chave,
      entidade: entidade.chave,
      titulo: `Remover a dependência de "${campo.nome}"`,
      chamar: (simular) =>
        this.admin.excluirDependencia(this.estrutura().chave, entidade.chave, campo.chave, entidade.versao, simular),
    });
    this.tratar(desfecho);
  }

  private tratar(desfecho: Desfecho): void {
    this.erros.set([]);
    if (desfecho.status === 'gravado') {
      this.editando.set(null);
      this.alterada.emit(desfecho.estrutura);
    } else if (desfecho.status === 'conflito') {
      this.editando.set(null);
      this.alterada.emit(null);
    } else if (desfecho.status === 'invalido') {
      this.erros.set([...(desfecho.mensagem ? [desfecho.mensagem] : []), ...Object.values(desfecho.erros).flat()]);
    }
  }
}
