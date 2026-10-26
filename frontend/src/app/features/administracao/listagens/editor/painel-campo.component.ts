import { CdkDrag, CdkDragDrop, CdkDragHandle, CdkDropList } from '@angular/cdk/drag-drop';
import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  computed,
  effect,
  inject,
  input,
  output,
  signal,
  untracked,
} from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormArray, FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { Button } from 'primeng/button';
import { InputNumber } from 'primeng/inputnumber';
import { InputText } from 'primeng/inputtext';
import { Message } from 'primeng/message';
import { RadioButton } from 'primeng/radiobutton';
import { Select } from 'primeng/select';
import { ToggleSwitch } from 'primeng/toggleswitch';
import { Tooltip } from 'primeng/tooltip';
import { CampoEntrada } from '../../../../api/models';
import {
  CampoEstrutura,
  EntidadeEstrutura,
  EstruturaListagem,
} from '../../../../core/administracao/estrutura-listagem';
import { MetadadosAdminService } from '../../../../core/administracao/metadados-admin.service';
import { lerProblema } from '../../../../core/erros/problema';
import { Campo, TipoDado } from '../../../../core/metadados/modelo';
import { DescritoPorDirective } from '../../../../shared/acessibilidade/descrito-por.directive';
import { AlteracaoEstruturaService, Desfecho } from '../comum/alteracao-estrutura.service';
import { aplicarErros, errosDe, ligarChaveAoNome } from '../comum/chave-automatica';
import { corpoDoCampo, entidadesReferenciaveis, moverItem, usosDoCampo } from '../modelo/regras-estrutura';
import { ACOES_EXCLUSAO, LIMITES, TIPOS_CAMPO, opcoesDoTipo, tiposNaEdicao } from '../modelo/tipos-campo';

/**
 * O painel lateral de criar e editar um campo. As opções mudam conforme o tipo (tipos-campo.ts → opcoesDoTipo); depois de criado,
 * a chave não muda (RN51) e o tipo só muda pelas conversões permitidas (RN66, tiposNaEdicao). Toda gravação passa pela simulação,
 * inclusive o pedido de valor padrão ao criar um campo obrigatório ou tornar um obrigatório (RN60, RN61). "Excluir" só aparece
 * quando a API permitiria (exclusão simulada ao abrir, RN70); senão, a tela sugere desativar.
 */
@Component({
  selector: 'app-painel-campo',
  imports: [
    ReactiveFormsModule,
    CdkDropList,
    CdkDrag,
    CdkDragHandle,
    Button,
    InputNumber,
    InputText,
    Message,
    RadioButton,
    Select,
    ToggleSwitch,
    Tooltip,
    DescritoPorDirective,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './painel-campo.component.html',
})
export class PainelCampoComponent {
  readonly estrutura = input.required<EstruturaListagem>();
  readonly entidade = input.required<EntidadeEstrutura>();
  /** null = campo novo. */
  readonly campo = input<CampoEstrutura | null>(null);
  readonly alterada = output<EstruturaListagem | null>();
  readonly fechar = output<void>();

  private readonly admin = inject(MetadadosAdminService);
  private readonly alteracao = inject(AlteracaoEstruturaService);
  private readonly destruir = inject(DestroyRef);

  readonly nome = new FormControl('', {
    nonNullable: true,
    validators: [Validators.required, Validators.maxLength(150)],
  });
  readonly chave = new FormControl('', {
    nonNullable: true,
    validators: [Validators.required, Validators.pattern(/^[a-z0-9_]+$/), Validators.maxLength(100)],
  });
  readonly tipoDado = new FormControl<TipoDado>('TEXTO', { nonNullable: true });
  readonly obrigatorio = new FormControl(false, { nonNullable: true });
  readonly tamanhoMaximo = new FormControl<number | null>(LIMITES.tamanhoMaximo.padrao);
  readonly precisao = new FormControl<number | null>(LIMITES.precisao.padrao);
  readonly escala = new FormControl<number | null>(LIMITES.escala.padrao);
  readonly opcoes = new FormArray<FormControl<string>>([]);
  readonly entidadeReferenciada = new FormControl<string | null>(null);
  readonly acaoExclusao = new FormControl<'RESTRINGIR' | 'CASCATA'>('RESTRINGIR', { nonNullable: true });

  readonly tipoAtual = toSignal(this.tipoDado.valueChanges, { initialValue: this.tipoDado.value });
  readonly opcoesTipo = computed(() => opcoesDoTipo(this.tipoAtual()));
  readonly tipos = computed(() => {
    const campo = this.campo();
    const permitidos = campo ? tiposNaEdicao(campo.tipo) : TIPOS_CAMPO.map((t) => t.tipo);
    return TIPOS_CAMPO.filter((t) => permitidos.includes(t.tipo));
  });
  readonly nomesDosTipos = computed(() =>
    this.tipos()
      .map((t) => t.rotulo)
      .join(', '),
  );
  readonly explicacaoTipo = computed(() => TIPOS_CAMPO.find((t) => t.tipo === this.tipoAtual())?.explicacao ?? '');
  readonly referenciaveis = computed(() =>
    entidadesReferenciaveis(this.estrutura(), this.entidade()).map((e) => ({ chave: e.chave, nome: e.nome })),
  );
  readonly usos = computed(() => {
    const campo = this.campo();
    return campo ? usosDoCampo(this.entidade(), campo, this.estrutura()) : [];
  });
  readonly acoesExclusao = ACOES_EXCLUSAO;
  readonly limites = LIMITES;
  readonly errosDe = errosDe;

  readonly salvando = signal(false);
  readonly errosGerais = signal<string[]>([]);
  /** null = ainda conferindo; true = a API permitiria excluir; string[] = os motivos de não poder. */
  readonly exclusao = signal<true | string[] | null>(null);
  readonly podeExcluir = computed(() => this.exclusao() === true);
  readonly motivosSemExclusao = computed(() => {
    const exclusao = this.exclusao();
    return Array.isArray(exclusao) ? exclusao : null;
  });

  constructor() {
    ligarChaveAoNome(this.nome, this.chave, this.destruir);
    // Fora do rastreamento: o estado dos controles também é signal (ver aba-geral.component.ts).
    effect(() => {
      const campo = this.campo();
      untracked(() => this.preencher(campo));
    });
  }

  private preencher(campo: CampoEstrutura | null): void {
    this.errosGerais.set([]);
    this.nome.reset(campo?.nome ?? '');
    this.chave.reset(campo?.chave ?? '');
    this.tipoDado.reset(campo?.tipo ?? 'TEXTO');
    this.obrigatorio.reset(campo?.obrigatorio ?? false);
    this.tamanhoMaximo.reset(campo?.tamanhoMaximo ?? LIMITES.tamanhoMaximo.padrao);
    this.precisao.reset(campo?.precisao ?? LIMITES.precisao.padrao);
    this.escala.reset(campo?.escala ?? LIMITES.escala.padrao);
    this.opcoes.clear();
    for (const opcao of campo?.opcoes ?? []) {
      this.opcoes.push(new FormControl(opcao, { nonNullable: true }));
    }
    this.entidadeReferenciada.reset(campo?.entidadeReferenciada ?? null);
    this.acaoExclusao.reset(campo?.acaoExclusao ?? 'RESTRINGIR');
    if (campo) {
      this.chave.disable();
      this.entidadeReferenciada.disable();
      if (tiposNaEdicao(campo.tipo).length === 1) {
        this.tipoDado.disable();
      } else {
        this.tipoDado.enable();
      }
      void this.conferirExclusao(campo);
    } else {
      this.chave.enable();
      this.tipoDado.enable();
      this.entidadeReferenciada.enable();
      this.exclusao.set(null);
    }
  }

  /** Simula o DELETE: "Excluir" só aparece se a API permitiria (RN70). */
  private async conferirExclusao(campo: CampoEstrutura): Promise<void> {
    this.exclusao.set(null);
    try {
      const simulacao = await this.admin.excluirCampo(
        this.estrutura().chave,
        this.entidade().chave,
        campo.chave,
        this.entidade().versao,
        true,
      );
      this.exclusao.set(simulacao.permitido ? true : simulacao.impactos.map((i) => i.descricao));
    } catch (erro) {
      const detalhes = lerProblema(erro).detalhes?.['impactos'];
      this.exclusao.set(
        Array.isArray(detalhes)
          ? detalhes.map((i: { descricao?: string }) => i.descricao ?? '')
          : ['O campo está em uso.'],
      );
    }
  }

  // ---- Opções da LISTA ----

  adicionarOpcao(): void {
    this.opcoes.push(new FormControl('', { nonNullable: true }));
  }

  removerOpcao(indice: number): void {
    this.opcoes.removeAt(indice);
  }

  moverOpcao(de: number, para: number): void {
    if (para < 0 || para >= this.opcoes.length) {
      return;
    }
    const valores = moverItem(this.opcoes.getRawValue(), de, para);
    this.opcoes.setValue(valores);
    this.opcoes.markAsDirty();
  }

  aoSoltarOpcao(evento: CdkDragDrop<unknown>): void {
    this.moverOpcao(evento.previousIndex, evento.currentIndex);
  }

  // ---- Gravar ----

  /** O corpo do POST/PUT com SÓ os atributos do tipo (RN53). */
  private corpo(): CampoEntrada {
    const tipo = this.tipoDado.getRawValue();
    const opcoes = opcoesDoTipo(tipo);
    return {
      nome: this.nome.value.trim(),
      tipoDado: tipo,
      obrigatorio: this.obrigatorio.value,
      tamanhoMaximo: opcoes.tamanhoMaximo ? this.tamanhoMaximo.value : null,
      precisao: opcoes.precisaoEscala ? this.precisao.value : null,
      escala: opcoes.precisaoEscala ? this.escala.value : null,
      opcoes: opcoes.opcoesLista ? this.opcoes.getRawValue().map((o) => o.trim()) : null,
      entidadeReferenciada: opcoes.referencia ? this.entidadeReferenciada.getRawValue() : null,
      acaoExclusao: opcoes.referencia ? this.acaoExclusao.value : null,
    };
  }

  /** O campo como o formulário de uso o veria: o diálogo de valor padrão usa o MESMO componente do tipo. */
  private campoParaValorPadrao(): Campo {
    const corpo = this.corpo();
    return {
      chave: this.chave.getRawValue(),
      nome: corpo.nome ?? '',
      tipo: corpo.tipoDado as TipoDado,
      obrigatorio: true,
      ordem: 0,
      ativo: true,
      tamanhoMaximo: corpo.tamanhoMaximo ?? null,
      precisao: corpo.precisao ?? null,
      escala: corpo.escala ?? null,
      opcoes: corpo.opcoes ?? [],
      entidadeReferenciada: corpo.entidadeReferenciada ?? null,
      acaoExclusao: (corpo.acaoExclusao as Campo['acaoExclusao']) ?? null,
      dependeDe: null,
    };
  }

  async salvar(): Promise<void> {
    this.nome.markAsTouched();
    this.chave.markAsTouched();
    if (this.nome.invalid || (this.chave.enabled && this.chave.invalid)) {
      return;
    }
    const [grupo, entidade] = [this.estrutura().chave, this.entidade()];
    const campo = this.campo();
    const corpo = this.corpo();
    const ficaObrigatorio = corpo.obrigatorio === true && !(campo?.obrigatorio ?? false);
    this.salvando.set(true);
    const desfecho = await this.alteracao.executar({
      grupo,
      entidade: entidade.chave,
      titulo: campo ? `Alterar o campo "${corpo.nome}"` : `Criar o campo "${corpo.nome}"`,
      campoValorPadrao: ficaObrigatorio ? this.campoParaValorPadrao() : undefined,
      chamar: (simular, valorPadrao) =>
        campo
          ? this.admin.alterarCampo(
              grupo,
              entidade.chave,
              campo.chave,
              { ...corpoDoCampo(campo, entidade.versao, corpo), valorPadrao: valorPadrao as never },
              simular,
            )
          : this.admin.criarCampo(
              grupo,
              entidade.chave,
              { ...corpo, versao: entidade.versao, chave: this.chave.value, valorPadrao: valorPadrao as never },
              simular,
            ),
    });
    this.salvando.set(false);
    this.tratar(desfecho);
  }

  async excluir(): Promise<void> {
    const campo = this.campo();
    if (!campo) {
      return;
    }
    const [grupo, entidade] = [this.estrutura().chave, this.entidade()];
    const desfecho = await this.alteracao.executar({
      grupo,
      entidade: entidade.chave,
      titulo: `Excluir o campo "${campo.nome}"`,
      chamar: (simular) => this.admin.excluirCampo(grupo, entidade.chave, campo.chave, entidade.versao, simular),
    });
    this.tratar(desfecho);
  }

  private tratar(desfecho: Desfecho): void {
    this.errosGerais.set([]);
    if (desfecho.status === 'gravado') {
      this.alterada.emit(desfecho.estrutura);
      this.fechar.emit();
    } else if (desfecho.status === 'conflito') {
      this.alterada.emit(null);
      this.fechar.emit();
    } else if (desfecho.status === 'invalido') {
      const controles: Record<string, FormControl<unknown>> = {
        nome: this.nome,
        chave: this.chave,
        tipoDado: this.tipoDado,
        tamanhoMaximo: this.tamanhoMaximo,
        precisao: this.precisao,
        escala: this.escala,
        entidadeReferenciada: this.entidadeReferenciada,
        acaoExclusao: this.acaoExclusao,
      } as Record<string, FormControl<unknown>>;
      // Erros das opções da LISTA ("opcoes" ou "opcoes[1]") ficam junto do editor de opções.
      const erros = { ...desfecho.erros };
      const daLista = Object.keys(erros).filter((k) => k.startsWith('opcoes'));
      this.errosOpcoes.set(daLista.flatMap((k) => erros[k]));
      for (const k of daLista) {
        delete erros[k];
      }
      const gerais = aplicarErros(controles, erros);
      this.errosGerais.set(desfecho.mensagem ? [desfecho.mensagem, ...gerais] : gerais);
    }
  }

  readonly errosOpcoes = signal<string[]>([]);
}
