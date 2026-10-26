import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  ElementRef,
  Injector,
  OnInit,
  afterNextRender,
  computed,
  inject,
  input,
  linkedSignal,
  output,
  signal,
} from '@angular/core';
import { FormControl, FormRecord, ReactiveFormsModule, Validators } from '@angular/forms';
import { MessageService } from 'primeng/api';
import { Button } from 'primeng/button';
import { Dialog } from 'primeng/dialog';
import { Message } from 'primeng/message';
import { Select } from 'primeng/select';
import { rotuloComSituacao, rotuloSalvarEdicao, situacaoDe } from '../../core/aprovacao/regras-aprovacao';
import { AuthService } from '../../core/auth/auth.service';
import { tratarNoComponente } from '../../core/erros/contexto-erros';
import { metadadosDesatualizados } from '../../core/metadados/listagens';
import { MetadadosService } from '../../core/metadados/metadados.service';
import { mensagemDoErro } from '../../core/erros/mensagens-erro';
import { lerProblema } from '../../core/erros/problema';
import { EquipeDaListagem } from '../../core/metadados/listagens';
import { Campo, Entidade, OpcaoReferencia } from '../../core/metadados/modelo';
import { entidadeApontada, nomeEmFrase } from '../../core/metadados/regras-metadados';
import { aprovacaoAtiva, equipesParaCriar } from '../../core/permissoes/permissoes';
import { Registro } from '../../core/registros/modelo-registro';
import { RegistrosService } from '../../core/registros/registros.service';
import { environment } from '../../../environments/environment';
import { DescritoPorDirective } from '../acessibilidade/descrito-por.directive';
import { CampoFormularioComponent } from '../campos/campo-formulario.component';
import { BuscarOpcoes } from '../campos/campo-referencia.component';
import { formatarValor } from '../formatacao/formatar-valor';
import { podeCriarRelacionado, valoresIniciaisDoTexto } from './criar-relacionado';
import {
  FormularioRegistro,
  aplicarErrosDoServidor,
  corpoDoFormulario,
  layoutDoFormulario,
  marcarUnicidade,
  montarFormulario,
  preservarValores,
} from './montador-formulario';
import { vazio } from './validadores';
import { ValorCampo } from './valores';

/** O diálogo de "+ Criar <tipo>" aberto: o campo que vai receber o item, o tipo referenciado e o que vem preenchido. */
interface CriacaoRelacionada {
  campo: Campo;
  entidade: Entidade;
  valores: Record<string, ValorCampo>;
  equipe: string | null;
}

/** Um campo que já vem preenchido e travado (ex.: o relatório, ao criar um cenário no detalhe dele). */
export interface CampoFixo {
  chave: string;
  valor: OpcaoReferencia;
}

/**
 * O formulário GENÉRICO de criar e editar registros, montado a partir dos metadados da entidade (nada de
 * formulário escrito à mão por tipo de item). Usado na página (novo/editar) e no diálogo dos itens filhos.
 *
 *   - equipe (RN27, RN91): em entidade por equipe, select obrigatório para o administrador e para quem tem mais de uma
 *     equipe NESTA listagem (só elas são oferecidas); com uma equipe só, ela é usada e aparece como informação. Não muda na edição. O EXTERNO só escolhe entre as equipes
 *     dele com a aprovação ativa (RN34) e vê o aviso de que o item vai para a aprovação; editando um item rejeitado, o botão vira
 *     "Reenviar para aprovação" (RN35).
 *   - campos dependentes (RN21): desabilitados até a origem ser preenchida; mudar a origem limpa o valor.
 *   - erros do servidor: 400 vai para baixo de cada campo; 409 de unicidade marca os campos da restrição;
 *     409 de concorrência abre um aviso para recarregar.
 *   - metadados desatualizados (a estrutura mudou na administração; listagens.ts → metadadosDesatualizados): recarrega os metadados,
 *     remonta o formulário com o que já foi digitado e pede para revisar antes de salvar de novo.
 *   - criar item relacionado: nos selects de referência, "+ Criar <tipo>" abre um diálogo com ESTE MESMO formulário
 *     para o tipo referenciado (o texto digitado no campo de exibição, a equipe do item principal); ao salvar, o item novo fica
 *     escolhido no campo e o resto do formulário continua como estava. Só um nível: no diálogo, a opção não aparece.
 *   - foco no primeiro campo ao abrir; Enter salva (é um <form> com botão submit); na página, a barra de ações fica presa no rodapé.
 */
@Component({
  selector: 'app-formulario-registro',
  imports: [ReactiveFormsModule, Button, Dialog, Message, Select, CampoFormularioComponent, DescritoPorDirective],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './formulario-registro.component.html',
})
export class FormularioRegistroComponent implements OnInit {
  readonly grupo = input.required<string>();
  readonly entidade = input.required<Entidade>();
  /** Na edição, o registro lido (com a `versao`). Nulo = criação. */
  readonly registro = input<Registro | null>(null);
  readonly campoFixo = input<CampoFixo | null>(null);
  /** Equipe já decidida (ex.: a do registro pai, no diálogo de filhos). */
  readonly equipeFixa = input<string | null>(null);
  /**
   * Na criação, um campo que já vem PREENCHIDO mas pode ser alterado (ex.: o projeto, no "Novo Relatório de Teste" das seções de
   * relacionados do detalhe do projeto, RN94). As dependências dele valem como se a pessoa o tivesse escolhido.
   */
  readonly valorInicial = input<CampoFixo | null>(null);
  /** Na criação, a equipe sugerida (a do item de origem): escolhida se estiver entre as que o usuário pode usar. */
  readonly equipeInicial = input<string | null>(null);
  readonly prefixoId = input('formulario');
  /** Oferece "+ Criar <tipo>" nos selects de referência (falso dentro do próprio diálogo de criação: um nível só). */
  readonly permitirCriarRelacionado = input(true);
  /** Na criação, valores que já vêm preenchidos (ex.: o texto digitado no select, no diálogo de "+ Criar <tipo>"). */
  readonly valoresIniciais = input<Record<string, ValorCampo> | null>(null);
  /** Foca o primeiro campo ao abrir. */
  readonly focarAoAbrir = input(true);

  readonly salvo = output<Registro>();
  readonly cancelado = output<void>();

  private readonly auth = inject(AuthService);
  private readonly registros = inject(RegistrosService);
  private readonly metadados = inject(MetadadosService);
  private readonly destruir = inject(DestroyRef);
  private readonly elemento = inject<ElementRef<HTMLElement>>(ElementRef);
  private readonly injector = inject(Injector);
  private readonly mensagens = inject(MessageService);

  /** Vazio até o ngOnInit montar o formulário de verdade (os inputs só existem a partir daí). */
  formulario: FormularioRegistro = new FormRecord<FormControl<ValorCampo>>({});
  readonly equipe = new FormControl<string | null>(null);
  readonly equipesDisponiveis = signal<{ id: string; nome: string }[]>([]);
  readonly mostrarSelectEquipe = signal(false);
  readonly nomeEquipeUnica = signal<string | null>(null);
  /** A equipe escolhida, como signal (para o aviso de aprovação). */
  private readonly equipeEscolhida = signal<string | null>(null);

  readonly salvando = signal(false);
  readonly errosGerais = signal<string[]>([]);
  readonly conflitoConcorrencia = signal(false);
  readonly dadosCopiados = signal(false);
  /** Campos dependentes desabilitados porque a origem está vazia (chave → dica). */
  readonly dicas = signal<Record<string, string>>({});
  /** As entidades da área e as equipes do usuário nela: quem pode criar um item relacionado. */
  private readonly todas = signal<Entidade[]>([]);
  private readonly equipesNaListagem = signal<EquipeDaListagem[]>([]);
  /** O diálogo de "+ Criar <tipo>" (null = fechado). */
  readonly criacao = signal<CriacaoRelacionada | null>(null);
  private salvouAgora = false;

  readonly edicao = computed(() => this.registro() !== null);
  /** EXTERNO numa equipe com aprovação ativa: o aviso de que o item vai (ou volta) para a aprovação. Nulo = sem aviso. */
  readonly avisoAprovacao = computed(() => {
    const registro = this.registro();
    const equipeId = registro?.equipeId ?? this.equipeFixa() ?? this.equipeEscolhida();
    if (this.auth.usuario()?.perfil !== 'EXTERNO' || !aprovacaoAtiva(this.entidadeAtual(), equipeId)) {
      return null;
    }
    if (!registro) {
      return 'Este item será enviado para aprovação. Ele só fica visível para os outros depois de aprovado.';
    }
    return situacaoDe(registro.situacaoAprovacao) === 'REJEITADO'
      ? 'Ao reenviar, o item volta a ficar pendente de aprovação.'
      : null;
  });
  readonly rotuloSalvar = computed(() => {
    const registro = this.registro();
    // Botão com verbo e objeto ("Salvar relatório de teste"), não só "Salvar" (docs/design/guia-de-interface.md → "Textos").
    return registro
      ? rotuloSalvarEdicao(this.auth.usuario(), registro)
      : `Salvar ${nomeEmFrase(this.entidadeAtual().nome)}`;
  });
  /** A entidade em uso: a do input, ou a recarregada quando a API avisa que os metadados estavam desatualizados. */
  readonly entidadeAtual = linkedSignal(() => this.entidade());
  readonly campos = computed(() => this.entidadeAtual().campos);
  /** Curtos no grid de cima; TEXTO_LONGO embaixo, na largura toda (montador-formulario.ts). */
  readonly layout = computed(() => layoutDoFormulario(this.campos()));
  /** Formulário longo (mais de 6 campos, com curtos e textos longos): os dois grupos ganham título. */
  readonly comGrupos = computed(
    () => this.campos().length > 6 && this.layout().curtos.length > 0 && this.layout().longos.length > 0,
  );

  /** Busca das opções de cada campo REFERENCIA, criada uma vez (o input do componente de campo não muda). */
  private readonly buscas = new Map<string, BuscarOpcoes>();

  async ngOnInit(): Promise<void> {
    const inscricao = this.equipe.valueChanges.subscribe((valor) => this.equipeEscolhida.set(valor));
    this.destruir.onDestroy(() => inscricao.unsubscribe());
    this.montar(this.registro());
    if (this.focarAoAbrir()) {
      afterNextRender(() => this.focarPrimeiroCampo(), { injector: this.injector });
    }
    await this.prepararEquipe();
    if (this.permitirCriarRelacionado()) {
      const [todas, equipes] = await Promise.all([
        this.metadados.entidades(this.grupo()),
        this.metadados.equipesDaListagem(this.grupo()),
      ]);
      this.todas.set(todas);
      this.equipesNaListagem.set(equipes);
    }
  }

  /**
   * O nome do tipo referenciado por um campo, quando o usuário pode criar um item novo dele sem sair; senão, null. Na
   * edição também vale (ex.: trocar o RTC de um relatório por um que ainda não existe).
   */
  criarNovoDe(campo: Campo): string | null {
    if (!this.permitirCriarRelacionado() || campo.tipo !== 'REFERENCIA') {
      return null;
    }
    const referenciada = entidadeApontada(campo, this.todas());
    if (
      !referenciada ||
      !podeCriarRelacionado(this.auth.usuario(), referenciada, this.equipesNaListagem(), this.equipeDoRegistro())
    ) {
      return null;
    }
    return referenciada.nome;
  }

  /** "+ Criar <tipo>": abre o diálogo com o formulário do tipo referenciado, o texto digitado e a equipe deste item. */
  abrirCriacao(campo: Campo, texto: string): void {
    const entidade = entidadeApontada(campo, this.todas());
    if (!entidade) {
      return;
    }
    this.criacao.set({
      campo,
      entidade,
      valores: valoresIniciaisDoTexto(entidade, texto),
      equipe: entidade.escopoEquipe ? this.equipeDoRegistro() : null,
    });
  }

  /** O item novo fica escolhido no campo; o resto do formulário continua como estava. */
  aoCriarRelacionado(criado: Registro): void {
    const criacao = this.criacao();
    this.criacao.set(null);
    if (!criacao) {
      return;
    }
    const exibicao = criacao.entidade.campos.find((c) => c.chave === criacao.entidade.campoExibicao);
    const rotulo = exibicao ? formatarValor(exibicao, criado.dados[exibicao.chave]) || null : null;
    const controle = this.formulario.controls[criacao.campo.chave];
    controle?.setValue({ id: criado.id, rotulo: rotuloComSituacao(rotulo, criado.situacaoAprovacao) });
    controle?.markAsDirty();
    this.mensagens.add({
      severity: 'success',
      summary:
        situacaoDe(criado.situacaoAprovacao) === 'PENDENTE'
          ? `Item criado e enviado para aprovação. Ele já está escolhido em “${criacao.campo.nome}”.`
          : `Item criado e escolhido em “${criacao.campo.nome}”.`,
    });
    setTimeout(() =>
      this.elemento.nativeElement.querySelector<HTMLElement>(`#${this.prefixoId()}-${criacao.campo.chave}`)?.focus(),
    );
  }

  fecharCriacao(): void {
    const campo = this.criacao()?.campo;
    this.criacao.set(null);
    if (campo) {
      setTimeout(() =>
        this.elemento.nativeElement.querySelector<HTMLElement>(`#${this.prefixoId()}-${campo.chave}`)?.focus(),
      );
    }
  }

  /** Para o guard de "alterações não salvas". */
  temAlteracoesPendentes(): boolean {
    return !this.salvouAgora && (this.formulario.dirty || this.equipe.dirty);
  }

  buscaDe(campo: Campo): BuscarOpcoes {
    let busca = this.buscas.get(campo.chave);
    if (!busca) {
      busca = (texto: string) =>
        this.registros.opcoes(this.grupo(), this.entidadeAtual().chave, campo.chave, {
          busca: texto,
          dependeDe: campo.dependeDe
            ? idDoValor(this.formulario.controls[campo.dependeDe]?.getRawValue() ?? null)
            : undefined,
          equipeId: this.equipeDoRegistro(),
          tamanho: environment.tamanhoPaginaOpcoes,
        });
      this.buscas.set(campo.chave, busca);
    }
    return busca;
  }

  controleDe(campo: Campo): FormControl<ValorCampo> {
    return this.formulario.controls[campo.chave];
  }

  async salvar(): Promise<void> {
    this.errosGerais.set([]);
    // Tentou salvar: todos os erros aparecem (CampoFormularioComponent.mostrarErros).
    this.formulario.markAllAsTouched();
    Object.values(this.formulario.controls).forEach((c) => c.markAsDirty());
    this.equipe.markAsTouched();
    if (this.formulario.invalid || this.equipe.invalid) {
      this.errosGerais.set(['Corrija os campos destacados.']);
      this.focarPrimeiroErro();
      return;
    }

    const corpo = corpoDoFormulario(this.entidadeAtual(), this.formulario);
    const registro = this.registro();
    // Os erros de validação e de conflito são mostrados AQUI, no formulário (não viram toast).
    const contexto = tratarNoComponente(400, 409);
    this.salvando.set(true);
    try {
      let salvo: Registro;
      if (registro) {
        salvo = await this.registros.atualizar(
          this.grupo(),
          this.entidadeAtual().chave,
          registro.id,
          { ...corpo, versao: registro.versao },
          contexto,
        );
      } else {
        const equipeId = this.equipeDoRegistro();
        salvo = await this.registros.criar(
          this.grupo(),
          this.entidadeAtual().chave,
          equipeId ? { ...corpo, equipeId } : corpo,
          contexto,
        );
      }
      this.salvouAgora = true;
      this.formulario.markAsPristine();
      this.salvo.emit(salvo);
    } catch (erro) {
      this.tratarErro(erro);
    } finally {
      this.salvando.set(false);
    }
  }

  /** 409 de concorrência: relê o registro e remonta o formulário (as alterações locais se perdem). */
  async recarregar(): Promise<void> {
    const registro = this.registro();
    if (!registro) {
      return;
    }
    const atual = await this.registros.obter(this.grupo(), this.entidadeAtual().chave, registro.id);
    this.conflitoConcorrencia.set(false);
    this.dadosCopiados.set(false);
    this.errosGerais.set([]);
    // O input `registro` é do pai; a versão nova fica guardada aqui para o próximo PUT.
    registro.versao = atual.versao;
    registro.dados = atual.dados;
    this.montar(atual);
  }

  /** Copia os valores digitados (JSON) para a área de transferência, antes de recarregar. */
  async copiarDados(): Promise<void> {
    const texto = JSON.stringify(corpoDoFormulario(this.entidadeAtual(), this.formulario), null, 2);
    try {
      await navigator.clipboard.writeText(texto);
      this.dadosCopiados.set(true);
    } catch {
      this.errosGerais.set([
        'Não foi possível copiar automaticamente. Selecione e copie os valores antes de recarregar.',
      ]);
    }
  }

  private montar(registro: Registro | null): void {
    this.formulario = montarFormulario(this.entidadeAtual(), registro);
    const fixo = this.campoFixo();
    if (fixo) {
      const controle = this.formulario.controls[fixo.chave];
      controle?.setValue(fixo.valor);
      controle?.disable();
    }
    const inicial = this.valorInicial();
    if (inicial && !registro && !fixo) {
      this.formulario.controls[inicial.chave]?.setValue(inicial.valor);
    }
    if (!registro) {
      for (const [chave, valor] of Object.entries(this.valoresIniciais() ?? {})) {
        this.formulario.controls[chave]?.setValue(valor);
      }
    }
    this.ligarDependencias();
  }

  /** RN21: o campo dependente fica desabilitado até a origem ter valor; trocar a origem limpa o dependente. */
  private ligarDependencias(): void {
    for (const campo of this.campos().filter((c) => c.dependeDe)) {
      const origem = this.formulario.controls[campo.dependeDe!];
      const dependente = this.formulario.controls[campo.chave];
      const nomeOrigem = this.campos().find((c) => c.chave === campo.dependeDe)?.nome ?? campo.dependeDe!;
      if (!origem || !dependente) {
        continue;
      }
      const atualizar = () => {
        if (vazio(idDoValor(origem.getRawValue()))) {
          dependente.disable({ emitEvent: false });
          this.dicas.update((d) => ({ ...d, [campo.chave]: `Escolha antes: ${nomeOrigem}. As opções dependem dele.` }));
        } else {
          dependente.enable({ emitEvent: false });
          this.dicas.update((d) => {
            const demais = { ...d };
            delete demais[campo.chave];
            return demais;
          });
        }
      };
      atualizar();
      const inscricao = origem.valueChanges.subscribe(() => {
        dependente.setValue(null);
        dependente.markAsDirty();
        atualizar();
      });
      this.destruir.onDestroy(() => inscricao.unsubscribe());
    }
  }

  private async prepararEquipe(): Promise<void> {
    const usuario = this.auth.usuario();
    if (!this.entidadeAtual().escopoEquipe || this.edicao() || this.equipeFixa() || !usuario) {
      return;
    }
    // RN91: só as equipes do usuário NESTA listagem (para o ADMINISTRADOR, as habilitadas nela).
    const naListagem = await this.metadados.equipesDaListagem(this.grupo());
    this.equipesDisponiveis.set(equipesParaCriar(usuario, this.entidadeAtual(), naListagem));
    const equipes = this.equipesDisponiveis();
    if (usuario.perfil !== 'ADMINISTRADOR' && equipes.length === 1) {
      this.equipe.setValue(equipes[0].id);
      this.nomeEquipeUnica.set(equipes[0].nome);
    } else {
      this.equipe.setValidators(Validators.required);
      const sugerida = this.equipeInicial();
      if (sugerida && equipes.some((e) => e.id === sugerida)) {
        this.equipe.setValue(sugerida);
      }
      this.equipe.updateValueAndValidity();
      this.mostrarSelectEquipe.set(true);
    }
  }

  /** A equipe do registro: a do próprio registro (edição), a fixa (filhos) ou a escolhida. */
  private equipeDoRegistro(): string | null {
    return this.registro()?.equipeId ?? this.equipeFixa() ?? this.equipe.value;
  }

  private tratarErro(erro: unknown): void {
    const problema = lerProblema(erro);
    if (metadadosDesatualizados(problema, Object.keys(this.formulario.controls))) {
      void this.atualizarMetadados();
    } else if (problema.status === 400) {
      const gerais = aplicarErrosDoServidor(this.formulario, problema);
      if (problema.erros['equipeId'] && this.mostrarSelectEquipe()) {
        this.equipe.setErrors({ servidor: problema.erros['equipeId'] });
      }
      this.errosGerais.set(gerais.length > 0 ? gerais : ['Corrija os campos destacados.']);
      this.focarPrimeiroErro();
    } else if (problema.codigo === 'CONFLITO_CONCORRENCIA') {
      this.conflitoConcorrencia.set(true);
    } else if (problema.codigo === 'CONFLITO_UNICIDADE') {
      const marcados = marcarUnicidade(this.formulario, problema);
      this.errosGerais.set([problema.detalhe ?? mensagemDoErro(problema.codigo, problema.status)]);
      if (marcados.length > 0) {
        this.focarPrimeiroErro();
      }
    } else {
      this.errosGerais.set([problema.detalhe ?? mensagemDoErro(problema.codigo, problema.status)]);
    }
  }

  /** A estrutura mudou desde que a tela abriu: recarrega os metadados e remonta o formulário com o que já foi digitado. */
  private async atualizarMetadados(): Promise<void> {
    const anterior = this.entidadeAtual();
    const valores = this.formulario.getRawValue();
    this.metadados.invalidar(this.grupo());
    const nova = await this.metadados.entidade(this.grupo(), anterior.chave);
    if (!nova) {
      this.errosGerais.set([
        `"${anterior.nome}" foi desativado ou removido por um administrador enquanto você preenchia. Não é possível salvar.`,
      ]);
      return;
    }
    this.buscas.clear();
    this.entidadeAtual.set(nova);
    this.montar(this.registro());
    preservarValores(this.formulario, nova, valores, anterior.campos);
    this.errosGerais.set([
      'Os campos deste tipo de item foram alterados por um administrador enquanto você preenchia. O formulário foi ' +
        'atualizado: revise os campos e salve de novo.',
    ]);
  }

  /** O primeiro campo habilitado do formulário (texto, número, select...). */
  private focarPrimeiroCampo(): void {
    const primeiro = this.elemento.nativeElement.querySelector<HTMLElement>(
      'input:not([disabled]):not([type="hidden"]), textarea:not([disabled]), [role="combobox"]:not([aria-disabled="true"])',
    );
    primeiro?.focus();
  }

  private focarPrimeiroErro(): void {
    setTimeout(() => {
      const invalido = this.elemento.nativeElement.querySelector<HTMLElement>('[aria-invalid="true"]');
      invalido?.focus();
    });
  }

  mensagensEquipe(): string[] {
    if (!this.equipe.invalid || !this.equipe.touched) {
      return [];
    }
    return (this.equipe.errors?.['servidor'] as string[] | undefined) ?? ['Escolha a equipe.'];
  }
}

/** Valor que vai em "dependeDe": o id, se a origem é REFERENCIA; senão, o próprio valor. */
function idDoValor(valor: ValorCampo): string | undefined {
  if (valor === null || valor === undefined) {
    return undefined;
  }
  if (typeof valor === 'object' && 'id' in valor) {
    return valor.id;
  }
  return String(valor);
}
