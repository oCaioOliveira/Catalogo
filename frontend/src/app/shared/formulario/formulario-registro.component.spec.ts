import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { MessageService } from 'primeng/api';
import { campo, entidade } from '../../../testes/metadados-de-teste';
import { AuthService } from '../../core/auth/auth.service';
import { UsuarioLogado } from '../../core/auth/usuario';
import { MetadadosService } from '../../core/metadados/metadados.service';
import { Registro } from '../../core/registros/modelo-registro';
import { RegistrosService } from '../../core/registros/registros.service';
import { FormularioRegistroComponent } from './formulario-registro.component';

// Criar o item referenciado num diálogo, sem perder o que já foi digitado no formulário principal.
describe('FormularioRegistroComponent: criar item relacionado', () => {
  const alfa = { id: 'alfa', nome: 'Equipe Alfa' };
  const rtc = entidade('rtc', [campo('rtc_externo_id', 'TEXTO', { nome: 'ID externo' })], { nome: 'RTC' });
  const sigla = entidade('sigla', [campo('codigo', 'TEXTO')], { nome: 'Sigla', escopoEquipe: false });
  const relatorio = entidade(
    'relatorio_teste',
    [
      campo('bateria', 'TEXTO', { nome: 'Bateria' }),
      campo('rtc', 'REFERENCIA', { nome: 'RTC', entidadeReferenciada: 'rtc' }),
      campo('sigla', 'REFERENCIA', { nome: 'Sigla', entidadeReferenciada: 'sigla' }),
    ],
    { nome: 'Relatório de Teste' },
  );

  async function montar(perfil: UsuarioLogado['perfil'], permitir = true) {
    const usuario: UsuarioLogado = {
      id: 'u',
      nome: 'Ana',
      matricula: 'ana',
      perfil,
      equipes: [alfa],
      deveTrocarSenha: false,
    };
    TestBed.configureTestingModule({
      providers: [
        provideNoopAnimations(),
        MessageService,
        { provide: AuthService, useValue: { usuario: signal(usuario) } },
        { provide: RegistrosService, useValue: {} },
        {
          provide: MetadadosService,
          useValue: { entidades: async () => [relatorio, rtc, sigla], equipesDaListagem: async () => [alfa] },
        },
      ],
    });
    const fixture = TestBed.createComponent(FormularioRegistroComponent);
    fixture.componentRef.setInput('grupo', 'testes');
    fixture.componentRef.setInput('entidade', relatorio);
    fixture.componentRef.setInput('focarAoAbrir', false);
    fixture.componentRef.setInput('permitirCriarRelacionado', permitir);
    fixture.detectChanges();
    await fixture.whenStable();
    return fixture.componentInstance;
  }

  const campoDe = (chave: string) => relatorio.campos.find((c) => c.chave === chave)!;

  it('a opção aparece para quem pode criar no tipo referenciado (cadastro de apoio, só o administrador)', async () => {
    const formulario = await montar('INTERNO');

    expect(formulario.criarNovoDe(campoDe('rtc'))).toBe('RTC');
    expect(formulario.criarNovoDe(campoDe('sigla'))).toBeNull();
    expect(formulario.criarNovoDe(campoDe('bateria'))).toBeNull();
  });

  it('dentro do diálogo (um nível só), a opção não aparece', async () => {
    const formulario = await montar('ADMINISTRADOR', false);

    expect(formulario.criarNovoDe(campoDe('rtc'))).toBeNull();
  });

  it('o diálogo abre com o texto digitado no campo de exibição e a equipe do item principal', async () => {
    const formulario = await montar('INTERNO');

    formulario.abrirCriacao(campoDe('rtc'), 'RTC-9');

    expect(formulario.criacao()?.entidade.chave).toBe('rtc');
    expect(formulario.criacao()?.valores).toEqual({ rtc_externo_id: 'RTC-9' });
    expect(formulario.criacao()?.equipe).toBe('alfa');
  });

  it('ao salvar, o item novo fica escolhido e o resto do formulário continua como estava', async () => {
    const formulario = await montar('INTERNO');
    formulario.formulario.controls['bateria'].setValue('Bateria 01');
    formulario.abrirCriacao(campoDe('rtc'), 'RTC-9');

    formulario.aoCriarRelacionado({
      id: 'novo',
      dados: { rtc_externo_id: 'RTC-9' },
      situacaoAprovacao: 'APROVADO',
    } as Registro);

    expect(formulario.criacao()).toBeNull();
    expect(formulario.formulario.controls['rtc'].value).toEqual({ id: 'novo', rotulo: 'RTC-9' });
    expect(formulario.formulario.controls['bateria'].value).toBe('Bateria 01');
  });

  it('um item que nasce pendente (EXTERNO com aprovação) já pode ser usado, marcado como pendente', async () => {
    const formulario = await montar('INTERNO');
    formulario.abrirCriacao(campoDe('rtc'), 'RTC-9');

    formulario.aoCriarRelacionado({
      id: 'p',
      dados: { rtc_externo_id: 'RTC-9' },
      situacaoAprovacao: 'PENDENTE',
    } as Registro);

    expect(formulario.formulario.controls['rtc'].value).toEqual({ id: 'p', rotulo: 'RTC-9 (pendente)' });
  });
});
