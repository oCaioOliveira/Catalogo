import { UsuarioLogado } from '../../core/auth/usuario';
import { campo, entidade } from '../../../testes/metadados-de-teste';
import {
  ID_CRIAR_NOVO,
  ehOpcaoCriarNovo,
  opcaoCriarNovo,
  podeCriarRelacionado,
  valoresIniciaisDoTexto,
} from './criar-relacionado';

// Criar o item referenciado num diálogo, sem sair do formulário.
describe('criar item relacionado', () => {
  const alfa = { id: 'alfa', nome: 'Equipe Alfa' };
  const beta = { id: 'beta', nome: 'Equipe Beta' };
  const usuario = (perfil: UsuarioLogado['perfil']): UsuarioLogado =>
    ({
      id: 'u1',
      nome: 'Ana',
      matricula: 'ana',
      perfil,
      equipes: [alfa, beta],
      deveTrocarSenha: false,
    }) as UsuarioLogado;
  const rtc = entidade('rtc', [campo('rtc_externo_id', 'TEXTO', { tamanhoMaximo: 10 })], { nome: 'RTC' });
  const sigla = entidade('sigla', [campo('codigo', 'TEXTO')], { escopoEquipe: false });

  it('a opção mostra o tipo e o texto digitado (forma neutra) e é reconhecida pelo id especial', () => {
    expect(opcaoCriarNovo('  RTC-9 ', 'RTC')).toEqual({ id: ID_CRIAR_NOVO, rotulo: "+ Criar RTC 'RTC-9'" });
    expect(opcaoCriarNovo('', 'RTC').rotulo).toBe('+ Criar RTC');
    // Forma neutra: serve para tipos femininos também.
    expect(opcaoCriarNovo('ABC', 'Sigla').rotulo).toBe("+ Criar Sigla 'ABC'");
    expect(ehOpcaoCriarNovo(opcaoCriarNovo('x', 'RTC'))).toBeTrue();
    expect(ehOpcaoCriarNovo({ id: 'abc', rotulo: 'x' })).toBeFalse();
    expect(ehOpcaoCriarNovo(null)).toBeFalse();
  });

  it('o texto digitado preenche o campo de exibição, cortado no tamanho máximo', () => {
    expect(valoresIniciaisDoTexto(rtc, ' RTC-NOVO-123456 ')).toEqual({ rtc_externo_id: 'RTC-NOVO-1' });
    expect(valoresIniciaisDoTexto(rtc, '   ')).toEqual({});
    const porNumero = entidade('x', [campo('numero', 'INTEIRO')]);
    expect(valoresIniciaisDoTexto(porNumero, '12')).toEqual({});
  });

  it('cadastro de apoio (global): só o administrador cria', () => {
    expect(podeCriarRelacionado(usuario('ADMINISTRADOR'), sigla, [alfa], 'alfa')).toBeTrue();
    expect(podeCriarRelacionado(usuario('GESTOR'), sigla, [alfa], 'alfa')).toBeFalse();
  });

  it('por equipe: interno cria na equipe do formulário; externo só com a aprovação ativa nela', () => {
    expect(podeCriarRelacionado(usuario('INTERNO'), rtc, [alfa], 'alfa')).toBeTrue();
    expect(podeCriarRelacionado(usuario('INTERNO'), rtc, [alfa], 'beta')).toBeFalse();
    expect(podeCriarRelacionado(usuario('EXTERNO'), rtc, [alfa], 'alfa')).toBeFalse();
    const comAprovacao = { ...rtc, equipesComAprovacao: ['alfa'] };
    expect(podeCriarRelacionado(usuario('EXTERNO'), comAprovacao, [alfa], 'alfa')).toBeTrue();
  });

  it('sem a equipe do formulário escolhida ainda, basta poder criar em alguma', () => {
    expect(podeCriarRelacionado(usuario('ADMINISTRADOR'), rtc, [alfa, beta], null)).toBeTrue();
    expect(podeCriarRelacionado(usuario('INTERNO'), rtc, [], null)).toBeFalse();
  });
});
