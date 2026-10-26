import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  input,
  signal,
  untracked,
  viewChild,
} from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { ConfirmationService, MessageService } from 'primeng/api';
import { Button } from 'primeng/button';
import { InputText } from 'primeng/inputtext';
import { Message } from 'primeng/message';
import { MultiSelect } from 'primeng/multiselect';
import { Select } from 'primeng/select';
import { Skeleton } from 'primeng/skeleton';
import { AdministracaoService } from '../../../core/administracao/administracao.service';
import { DadosUsuario, EquipeAdmin, UsuarioAdmin } from '../../../core/administracao/modelo-administracao';
import { AuthService } from '../../../core/auth/auth.service';
import { Perfil } from '../../../core/auth/usuario';
import { tratarNoComponente } from '../../../core/erros/contexto-erros';
import { mensagemDoErro } from '../../../core/erros/mensagens-erro';
import { lerProblema } from '../../../core/erros/problema';
import { SenhaProvisoriaDialogComponent } from '../comum/senha-provisoria-dialog.component';
import {
  DESCRICOES_PERFIL,
  EXPLICACAO_ADMINISTRADOR,
  PERFIS,
  equipesParaSalvar,
  erroEquipes,
  exigeEquipes,
} from '../modelo/perfis';
import { avisoEdicaoPropria } from '../modelo/regras-administracao';
import { CabecalhoPaginaComponent } from '../../../shared/pagina/cabecalho-pagina.component';
import { FocoInicialDirective } from '../../../shared/acessibilidade/foco-inicial.directive';

type CampoUsuario = 'matricula' | 'nome' | 'perfil' | 'equipes';

/**
 * /configuracoes/usuarios/novo e /configuracoes/usuarios/:id/editar. Matrícula obrigatória no cadastro e somente leitura na edição; equipes
 * (só as ativas) obrigatórias para quem não é ADMINISTRADOR, e escondidas para ele (RN31). Os erros da API aparecem no campo
 * (400 por campo; 409 de matrícula repetida). Criado o usuário, a senha provisória aparece UMA vez no diálogo (ADR 0041).
 */
@Component({
  selector: 'app-formulario-usuario',
  imports: [
    FocoInicialDirective,
    CabecalhoPaginaComponent,
    ReactiveFormsModule,
    RouterLink,
    Button,
    InputText,
    Message,
    MultiSelect,
    Select,
    Skeleton,
    SenhaProvisoriaDialogComponent,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './formulario-usuario.component.html',
})
export class FormularioUsuarioComponent {
  /** Parâmetro da rota (nulo = novo usuário). */
  readonly id = input<string | undefined>(undefined);

  private readonly administracao = inject(AdministracaoService);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly confirmacao = inject(ConfirmationService);
  private readonly mensagens = inject(MessageService);
  private readonly dialogoSenha = viewChild.required(SenhaProvisoriaDialogComponent);

  readonly formulario = new FormGroup({
    matricula: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(20)] }),
    nome: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(150)] }),
    perfil: new FormControl<Perfil | null>(null, { validators: [Validators.required] }),
    equipes: new FormControl<string[]>([], { nonNullable: true }),
  });

  readonly perfil = toSignal(this.formulario.controls.perfil.valueChanges, { initialValue: null as Perfil | null });
  readonly original = signal<UsuarioAdmin | null>(null);
  readonly equipes = signal<EquipeAdmin[]>([]);
  readonly carregando = signal(true);
  readonly salvando = signal(false);
  readonly tentouSalvar = signal(false);
  /** Erros da API por campo (400 e 409), e os sem campo. */
  readonly errosServidor = signal<Partial<Record<CampoUsuario, string[]>>>({});
  readonly errosGerais = signal<string[]>([]);
  /** O que o usuário escolheu nas equipes, para o erro de "obrigatório" acompanhar a seleção. */
  private readonly equipesEscolhidas = toSignal(this.formulario.controls.equipes.valueChanges, {
    initialValue: [] as string[],
  });

  readonly edicao = computed(() => !!this.id());
  readonly perfis = PERFIS;
  readonly descricaoPerfil = computed(() => {
    const perfil = this.perfil();
    return perfil ? DESCRICOES_PERFIL[perfil] : 'Escolha o perfil para ver o que ele permite.';
  });
  readonly mostrarEquipes = computed(() => exigeEquipes(this.perfil()));
  readonly explicacaoAdministrador = EXPLICACAO_ADMINISTRADOR;
  /** Só equipes ATIVAS podem ser escolhidas; as inativas a que o usuário já pertence aparecem marcadas, para não sumirem. */
  readonly opcoesEquipe = computed(() => {
    const atuais = this.original()?.equipes.map((e) => e.id) ?? [];
    return this.equipes()
      .filter((e) => e.ativa || atuais.includes(e.id))
      .map((e) => ({ valor: e.id, rotulo: e.ativa ? e.nome : `${e.nome} (inativa)`, desabilitada: !e.ativa }));
  });
  readonly erroEquipesCliente = computed(() =>
    this.tentouSalvar() ? erroEquipes(this.perfil(), this.equipesEscolhidas()) : null,
  );

  constructor() {
    effect(() => {
      const id = this.id();
      untracked(() => void this.carregar(id));
    });
  }

  private async carregar(id: string | undefined): Promise<void> {
    this.carregando.set(true);
    try {
      const [equipes, usuario] = await Promise.all([
        this.administracao.todasAsEquipes(),
        id ? this.administracao.obterUsuario(id) : Promise.resolve(null),
      ]);
      this.equipes.set(equipes);
      this.original.set(usuario);
      if (usuario) {
        this.formulario.setValue({
          matricula: usuario.matricula,
          nome: usuario.nome,
          perfil: usuario.perfil,
          equipes: usuario.equipes.map((e) => e.id),
        });
        this.formulario.controls.matricula.disable();
      }
    } catch {
      // O interceptor já avisou (ex.: 404); volta para a lista.
      void this.router.navigate(['/configuracoes/usuarios']);
    } finally {
      this.carregando.set(false);
    }
  }

  /** Mensagens de um campo: as do cliente (depois de tocar ou tentar salvar) e as do servidor. */
  errosDo(campo: CampoUsuario): string[] {
    const controle = this.formulario.controls[campo];
    const cliente: string[] = [];
    if ((controle.dirty || this.tentouSalvar()) && controle.errors) {
      if (controle.hasError('required')) {
        cliente.push('Campo obrigatório.');
      }
      if (controle.hasError('maxlength')) {
        cliente.push(`Deve ter no máximo ${controle.errors['maxlength'].requiredLength} caracteres.`);
      }
    }
    if (campo === 'equipes' && this.erroEquipesCliente()) {
      cliente.push(this.erroEquipesCliente()!);
    }
    return [...cliente, ...(this.errosServidor()[campo] ?? [])];
  }

  descricaoDo(campo: CampoUsuario): string | null {
    return this.errosDo(campo).length > 0 ? `usuario-${campo}-erros` : null;
  }

  async salvar(): Promise<void> {
    this.tentouSalvar.set(true);
    this.formulario.markAllAsTouched();
    const perfil = this.formulario.controls.perfil.value;
    const escolhidas = this.formulario.controls.equipes.value;
    if (this.formulario.invalid || !perfil || erroEquipes(perfil, escolhidas)) {
      return;
    }
    const original = this.original();
    const aviso = original ? avisoEdicaoPropria(this.auth.usuario()?.id ?? null, original, perfil) : null;
    if (aviso && !(await this.confirmar(aviso))) {
      return;
    }

    const dados: DadosUsuario = {
      matricula: this.formulario.controls.matricula.value.trim(),
      nome: this.formulario.controls.nome.value.trim(),
      perfil,
      equipes: equipesParaSalvar(perfil, escolhidas),
    };
    this.errosServidor.set({});
    this.errosGerais.set([]);
    this.salvando.set(true);
    try {
      if (original) {
        await this.administracao.atualizarUsuario(original.id, dados, tratarNoComponente(400, 409));
        this.mensagens.add({ severity: 'success', summary: 'Usuário salvo.' });
        await this.depoisDeSalvarOProprio(original.id);
        void this.router.navigate(['/configuracoes/usuarios', original.id]);
      } else {
        const { usuario, senhaProvisoria } = await this.administracao.criarUsuario(dados, tratarNoComponente(400, 409));
        this.original.set(usuario);
        // Direto para o diálogo; ao fechar, vai para o detalhe (a senha não é guardada em lugar nenhum).
        this.dialogoSenha().mostrar(senhaProvisoria, `${usuario.nome} (${usuario.matricula})`);
      }
    } catch (erro) {
      this.mostrarErrosDaApi(erro);
    } finally {
      this.salvando.set(false);
    }
  }

  aoFecharSenha(): void {
    const criado = this.original();
    void this.router.navigate(criado ? ['/configuracoes/usuarios', criado.id] : ['/configuracoes/usuarios']);
  }

  /** Editou o próprio usuário: recarrega /auth/eu (perfil e equipes do menu); se deixou de ser administrador, sai da área. */
  private async depoisDeSalvarOProprio(id: string): Promise<void> {
    if (this.auth.usuario()?.id !== id) {
      return;
    }
    const atualizado = await this.auth.carregarUsuario();
    if (atualizado.perfil !== 'ADMINISTRADOR') {
      await this.router.navigate(['/']);
    }
  }

  private mostrarErrosDaApi(erro: unknown): void {
    const problema = lerProblema(erro);
    const porCampo: Partial<Record<CampoUsuario, string[]>> = {};
    const gerais: string[] = [];
    const campos: CampoUsuario[] = ['matricula', 'nome', 'perfil', 'equipes'];
    for (const [chave, mensagens] of Object.entries(problema.erros)) {
      const campo = campos.find((c) => c === chave);
      if (campo) {
        porCampo[campo] = mensagens;
      } else {
        gerais.push(...mensagens);
      }
    }
    // 409 CONFLITO_UNICIDADE: detalhes.campos diz qual campo (a matrícula).
    if (problema.codigo === 'CONFLITO_UNICIDADE') {
      const conflitantes = (problema.detalhes?.['campos'] as string[] | undefined) ?? ['matricula'];
      for (const chave of conflitantes) {
        const campo = campos.find((c) => c === chave);
        if (campo) {
          porCampo[campo] = [problema.detalhe ?? 'Valor já usado.'];
        }
      }
    } else if (problema.status !== 400) {
      // Pelo CÓDIGO (ex.: 409 ULTIMO_ADMINISTRADOR), nunca pelo texto da API.
      gerais.push(mensagemDoErro(problema.codigo, problema.status));
    }
    this.errosServidor.set(porCampo);
    this.errosGerais.set(gerais);
  }

  private confirmar(mensagem: string): Promise<boolean> {
    return new Promise((resolver) =>
      this.confirmacao.confirm({
        header: 'Mudar o seu próprio perfil',
        message: mensagem,
        icon: 'pi pi-exclamation-triangle',
        acceptLabel: 'Salvar mesmo assim',
        rejectLabel: 'Cancelar',
        acceptButtonProps: { severity: 'danger' },
        rejectButtonProps: { severity: 'secondary', text: true },
        accept: () => resolver(true),
        reject: () => resolver(false),
      }),
    );
  }
}
