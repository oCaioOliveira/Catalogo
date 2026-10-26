import { campo, entidade } from '../../../testes/metadados-de-teste';
import { Perfil, UsuarioLogado } from '../auth/usuario';
import {
  aprovacaoAtiva,
  equipesParaCriar,
  podeAdministrar,
  podeAvaliar,
  podeConfigurarEquipe,
  podeDecidir,
  podeVerAprovacoes,
  podeVerAvaliacoesIndividuais,
  podeCriar,
  podeEditar,
  podeExcluir,
  podeExportar,
  podeVerMinhaConta,
  podeVerPainel,
} from './permissoes';

function usuario(perfil: Perfil, equipes: string[] = ['A']): UsuarioLogado {
  return {
    id: 'u',
    matricula: 'm',
    nome: 'n',
    perfil,
    deveTrocarSenha: false,
    equipes: equipes.map((id) => ({ id, nome: id })),
  };
}

/** As equipes do usuário na listagem quando todas as dele estão habilitadas nela (o caso de antes da RN91). */
function equipesDe(u: UsuarioLogado | null): { id: string; nome: string }[] {
  return u?.equipes ?? [];
}

describe('permissoes (RN26, RN27)', () => {
  const porEquipe = entidade('relatorio_teste', [campo('nome', 'TEXTO')]);
  const global = entidade('comunidade', [campo('nome', 'TEXTO')], { escopoEquipe: false });
  const daA = { equipeId: 'A' };
  const daB = { equipeId: 'B' };

  it('EXTERNO só vê (e exporta)', () => {
    const externo = usuario('EXTERNO');
    expect(podeCriar(externo, porEquipe, equipesDe(externo))).toBeFalse();
    expect(podeEditar(externo, porEquipe, daA)).toBeFalse();
    expect(podeExcluir(externo, porEquipe, daA)).toBeFalse();
    expect(podeExportar(externo)).toBeTrue();
  });

  it('INTERNO cria e edita nas suas equipes, mas não exclui', () => {
    const interno = usuario('INTERNO');
    expect(podeCriar(interno, porEquipe, equipesDe(interno))).toBeTrue();
    expect(podeEditar(interno, porEquipe, daA)).toBeTrue();
    expect(podeEditar(interno, porEquipe, daB)).toBeFalse();
    expect(podeExcluir(interno, porEquipe, daA)).toBeFalse();
  });

  it('GESTOR cria, edita e exclui só nas suas equipes', () => {
    const gestor = usuario('GESTOR');
    expect(podeExcluir(gestor, porEquipe, daA)).toBeTrue();
    expect(podeExcluir(gestor, porEquipe, daB)).toBeFalse();
  });

  it('ADMINISTRADOR faz tudo, em qualquer equipe', () => {
    const admin = usuario('ADMINISTRADOR', []);
    // Para ele, as equipes "na listagem" são todas as habilitadas nela (RN91).
    expect(podeCriar(admin, porEquipe, [{ id: 'B', nome: 'B' }])).toBeTrue();
    expect(podeExcluir(admin, porEquipe, daB)).toBeTrue();
  });

  it('entidade global: só o ADMINISTRADOR altera', () => {
    expect(podeCriar(usuario('GESTOR'), global, equipesDe(usuario('GESTOR')))).toBeFalse();
    expect(podeEditar(usuario('GESTOR'), global, { equipeId: null })).toBeFalse();
    expect(podeExcluir(usuario('ADMINISTRADOR', []), global, { equipeId: null })).toBeTrue();
  });

  it('entidade desativada é somente leitura, até para o ADMINISTRADOR (RN75)', () => {
    const inativa = entidade('x', [], { ativo: false });
    expect(podeCriar(usuario('ADMINISTRADOR', []), inativa, equipesDe(usuario('ADMINISTRADOR', [])))).toBeFalse();
  });

  it('sem usuário logado, nada', () => {
    expect(podeCriar(null, porEquipe, equipesDe(null))).toBeFalse();
    expect(podeExportar(null)).toBeFalse();
  });

  it('RN91: cria só nas equipes dele NA listagem, não nas outras equipes dele', () => {
    const gestor = usuario('GESTOR', ['T', 'D']);
    expect(equipesParaCriar(gestor, porEquipe, [{ id: 'D', nome: 'D' }]).map((e) => e.id)).toEqual(['D']);
    expect(podeCriar(gestor, porEquipe, [])).toBeFalse();
  });

  it('RN93: listagem sem equipe habilitada, nem o ADMINISTRADOR cria itens por equipe (globais, sim)', () => {
    const admin = usuario('ADMINISTRADOR', []);
    expect(podeCriar(admin, porEquipe, [])).toBeFalse();
    expect(podeCriar(admin, global, [])).toBeTrue();
  });

  it('não-administrador sem equipe ativa não cria em entidade por equipe', () => {
    expect(podeCriar(usuario('INTERNO', []), porEquipe, equipesDe(usuario('INTERNO', [])))).toBeFalse();
  });

  it('painel gerencial: só GESTOR e ADMINISTRADOR (RN78)', () => {
    expect(podeVerPainel(usuario('GESTOR'))).toBeTrue();
    expect(podeVerPainel(usuario('ADMINISTRADOR', []))).toBeTrue();
    expect(podeVerPainel(usuario('INTERNO'))).toBeFalse();
    expect(podeVerPainel(usuario('EXTERNO'))).toBeFalse();
    expect(podeVerPainel(null)).toBeFalse();
  });

  it('administração de usuários e equipes: só o ADMINISTRADOR', () => {
    expect(podeAdministrar(usuario('ADMINISTRADOR', []))).toBeTrue();
    for (const perfil of ['GESTOR', 'INTERNO', 'EXTERNO'] as const) {
      expect(podeAdministrar(usuario(perfil)))
        .withContext(perfil)
        .toBeFalse();
    }
    expect(podeAdministrar(null)).toBeFalse();
  });

  it('Minha conta: qualquer perfil logado', () => {
    for (const perfil of ['ADMINISTRADOR', 'GESTOR', 'INTERNO', 'EXTERNO'] as const) {
      expect(podeVerMinhaConta(usuario(perfil)))
        .withContext(perfil)
        .toBeTrue();
    }
    expect(podeVerMinhaConta(null)).toBeFalse();
  });
});

describe('permissoes com aprovação e avaliação (RN33 a RN37, RN40, RN44, RN47)', () => {
  const semAprovacao = entidade('relatorio_teste', [campo('nome', 'TEXTO')]);
  const comAprovacaoEmA = entidade('relatorio_teste', [campo('nome', 'TEXTO')], { equipesComAprovacao: ['A'] });
  const externo = usuario('EXTERNO', ['A', 'B']);
  const doExterno = (situacaoAprovacao: string, equipeId = 'A') => ({
    equipeId,
    situacaoAprovacao,
    criadoPor: { id: 'u' },
  });

  it('a aprovação vale por equipe', () => {
    expect(aprovacaoAtiva(comAprovacaoEmA, 'A')).toBeTrue();
    expect(aprovacaoAtiva(comAprovacaoEmA, 'B')).toBeFalse();
    expect(aprovacaoAtiva(comAprovacaoEmA, null)).toBeFalse();
  });

  it('EXTERNO cria só onde a aprovação está ativa numa equipe dele, e o select mostra só essas equipes', () => {
    expect(podeCriar(externo, semAprovacao, equipesDe(externo))).toBeFalse();
    expect(podeCriar(externo, comAprovacaoEmA, equipesDe(externo))).toBeTrue();
    expect(equipesParaCriar(externo, comAprovacaoEmA, equipesDe(externo)).map((e) => e.id)).toEqual(['A']);
    expect(podeCriar(usuario('EXTERNO', ['B']), comAprovacaoEmA, equipesDe(usuario('EXTERNO', ['B'])))).toBeFalse();
    // Os outros perfis não mudam: todas as equipes deles.
    expect(
      equipesParaCriar(usuario('INTERNO', ['A', 'B']), comAprovacaoEmA, equipesDe(usuario('INTERNO', ['A', 'B']))).map(
        (e) => e.id,
      ),
    ).toEqual(['A', 'B']);
  });

  it('EXTERNO edita só os próprios, PENDENTES ou REJEITADOS; aprovado, não; nunca exclui', () => {
    expect(podeEditar(externo, comAprovacaoEmA, doExterno('PENDENTE'))).toBeTrue();
    expect(podeEditar(externo, comAprovacaoEmA, doExterno('REJEITADO'))).toBeTrue();
    expect(podeEditar(externo, comAprovacaoEmA, doExterno('APROVADO'))).toBeFalse();
    expect(podeEditar(externo, comAprovacaoEmA, { ...doExterno('PENDENTE'), criadoPor: { id: 'outro' } })).toBeFalse();
    expect(podeEditar(externo, comAprovacaoEmA, doExterno('PENDENTE', 'B'))).toBeFalse(); // sem aprovação na B
    expect(podeEditar(externo, semAprovacao, doExterno('PENDENTE'))).toBeFalse();
    expect(podeExcluir(externo, comAprovacaoEmA, doExterno('PENDENTE'))).toBeFalse();
  });

  it('INTERNO e GESTOR editam como antes, em qualquer situação', () => {
    expect(podeEditar(usuario('INTERNO'), comAprovacaoEmA, doExterno('APROVADO'))).toBeTrue();
    expect(podeEditar(usuario('GESTOR'), comAprovacaoEmA, doExterno('REJEITADO'))).toBeTrue();
  });

  it('decide o GESTOR da equipe do item ou o ADMINISTRADOR, só itens PENDENTES', () => {
    expect(podeDecidir(usuario('GESTOR', ['A']), doExterno('PENDENTE'))).toBeTrue();
    expect(podeDecidir(usuario('GESTOR', ['B']), doExterno('PENDENTE'))).toBeFalse();
    expect(podeDecidir(usuario('ADMINISTRADOR', []), doExterno('PENDENTE'))).toBeTrue();
    expect(podeDecidir(usuario('GESTOR', ['A']), doExterno('APROVADO'))).toBeFalse();
    expect(podeDecidir(usuario('INTERNO', ['A']), doExterno('PENDENTE'))).toBeFalse();
    expect(podeDecidir(externo, doExterno('PENDENTE'))).toBeFalse();
  });

  it('configurações da equipe e fila de aprovação: GESTOR e ADMINISTRADOR', () => {
    for (const [perfil, esperado] of [
      ['EXTERNO', false],
      ['INTERNO', false],
      ['GESTOR', true],
      ['ADMINISTRADOR', true],
    ] as const) {
      expect(podeConfigurarEquipe(usuario(perfil)))
        .withContext(perfil)
        .toBe(esperado);
      expect(podeVerAprovacoes(usuario(perfil)))
        .withContext(perfil)
        .toBe(esperado);
    }
  });

  it('avaliam e veem as avaliações individuais INTERNO, GESTOR e ADMINISTRADOR; o EXTERNO só o resumo', () => {
    expect(podeAvaliar(externo)).toBeFalse();
    expect(podeVerAvaliacoesIndividuais(externo)).toBeFalse();
    expect(podeAvaliar(usuario('INTERNO'))).toBeTrue();
    expect(podeVerAvaliacoesIndividuais(usuario('GESTOR'))).toBeTrue();
  });
});
