import { campo, entidade } from '../../../testes/metadados-de-teste';
import { Perfil, UsuarioLogado } from '../auth/usuario';
import {
  areaInicialDaFila,
  faixaDeSituacao,
  formatarAguardando,
  mostraSituacao,
  opcoesDaFila,
  rotuloComSituacao,
  rotuloSalvarEdicao,
  validarJustificativa,
} from './regras-aprovacao';

function usuario(perfil: Perfil, id = 'u', equipes = ['A']): UsuarioLogado {
  return {
    id,
    matricula: id,
    nome: id,
    perfil,
    deveTrocarSenha: false,
    equipes: equipes.map((e) => ({ id: e, nome: e })),
  };
}

describe('regras de aprovação na tela (RN33 a RN38)', () => {
  const comAprovacao = entidade('relatorio_teste', [campo('nome', 'TEXTO')], { equipesComAprovacao: ['A'] });
  const semAprovacao = entidade('relatorio_teste', [campo('nome', 'TEXTO')]);
  const item = (situacaoAprovacao: string, autor = 'u') => ({
    equipeId: 'A',
    situacaoAprovacao,
    criadoPor: { id: autor },
  });

  it('justificativa: obrigatória, de 10 a 1000 caracteres sem os espaços das pontas', () => {
    expect(validarJustificativa('')).toContain('Informe');
    expect(validarJustificativa('   curta   ')).toContain('pelo menos 10');
    expect(validarJustificativa('  dez letra  ')).toContain('tem 9'); // limite: 9
    expect(validarJustificativa('  dez letras  ')).toBeNull(); // limite: 10
    expect(validarJustificativa('a'.repeat(1000))).toBeNull();
    expect(validarJustificativa('a'.repeat(1001))).toContain('no máximo 1000');
  });

  it('sem aprovação na equipe, nada aparece (todo item é APROVADO)', () => {
    expect(mostraSituacao(semAprovacao, item('APROVADO'))).toBeFalse();
    expect(faixaDeSituacao(usuario('GESTOR'), semAprovacao, item('APROVADO'))).toBeNull();
    // Rejeitado de quando a aprovação estava ligada: continua mostrando a situação.
    expect(mostraSituacao(semAprovacao, item('REJEITADO'))).toBeTrue();
  });

  it('pendente: o GESTOR da equipe e o ADMINISTRADOR decidem; o INTERNO só vê', () => {
    expect(faixaDeSituacao(usuario('GESTOR'), comAprovacao, item('PENDENTE', 'x'))).toEqual({
      situacao: 'PENDENTE',
      titulo: 'Pendente de aprovação',
      podeDecidir: true,
      corrigirEReenviar: false,
    });
    expect(
      faixaDeSituacao(usuario('ADMINISTRADOR', 'a', []), comAprovacao, item('PENDENTE', 'x'))?.podeDecidir,
    ).toBeTrue();
    expect(faixaDeSituacao(usuario('INTERNO'), comAprovacao, item('PENDENTE', 'x'))?.podeDecidir).toBeFalse();
    expect(
      faixaDeSituacao(usuario('GESTOR', 'g', ['B']), comAprovacao, item('PENDENTE', 'x'))?.podeDecidir,
    ).toBeFalse();
  });

  it('rejeitado: o autor EXTERNO vê "corrija e reenvie", e o salvar vira "Reenviar para aprovação"', () => {
    const faixa = faixaDeSituacao(usuario('EXTERNO'), comAprovacao, item('REJEITADO'));
    expect(faixa?.titulo).toBe('Rejeitado — corrija e reenvie');
    expect(faixa?.corrigirEReenviar).toBeTrue();
    expect(rotuloSalvarEdicao(usuario('EXTERNO'), item('REJEITADO'))).toBe('Reenviar para aprovação');
    expect(rotuloSalvarEdicao(usuario('EXTERNO'), item('PENDENTE'))).toBe('Salvar alterações');
    expect(rotuloSalvarEdicao(usuario('GESTOR'), item('REJEITADO'))).toBe('Salvar alterações');
    // Outro EXTERNO (não autor) não corrige.
    expect(
      faixaDeSituacao(usuario('EXTERNO', 'outro'), comAprovacao, item('REJEITADO'))?.corrigirEReenviar,
    ).toBeFalse();
  });

  it('aprovado: sem ações', () => {
    expect(faixaDeSituacao(usuario('EXTERNO'), comAprovacao, item('APROVADO'))).toEqual({
      situacao: 'APROVADO',
      titulo: 'Aprovado',
      podeDecidir: false,
      corrigirEReenviar: false,
    });
  });

  it('opções de referência: itens não aprovados do próprio autor aparecem identificados', () => {
    expect(rotuloComSituacao('Bateria 1', 'APROVADO')).toBe('Bateria 1');
    expect(rotuloComSituacao('Bateria 1', 'PENDENTE')).toBe('Bateria 1 (pendente)');
    expect(rotuloComSituacao(null, 'REJEITADO')).toBe('(sem rótulo) (rejeitado)');
  });

  it('"aguardando há"', () => {
    const agora = new Date('2026-10-23T12:00:00Z');
    expect(formatarAguardando('2026-10-23T11:30:00Z', agora)).toBe('menos de 1 hora');
    expect(formatarAguardando('2026-10-23T11:00:00Z', agora)).toBe('1 hora');
    expect(formatarAguardando('2026-10-23T02:00:00Z', agora)).toBe('10 horas');
    expect(formatarAguardando('2026-10-22T11:00:00Z', agora)).toBe('1 dia');
    expect(formatarAguardando('2026-10-20T12:00:00Z', agora)).toBe('3 dias');
    expect(formatarAguardando('2026-06-01T12:00:00Z', agora)).toBe('4 meses');
  });
});

describe('opcoesDaFila (RN36, RN91)', () => {
  const t = { id: 't', nome: 'T' };
  const d = { id: 'd', nome: 'D' };
  const listagem = (chave: string, minhasEquipes: { id: string; nome: string }[], ativo = true) => ({
    chave,
    nome: chave,
    ativo,
    minhasEquipes,
    possuiEquipes: minhasEquipes.length > 0,
  });

  it('com uma listagem filtrada, só as equipes do usuário nela', () => {
    const opcoes = opcoesDaFila([listagem('testes', [t]), listagem('tasks', [d])], 'tasks');
    expect(opcoes.equipes).toEqual([d]);
    expect(opcoes.listagens.map((l) => l.chave)).toEqual(['testes', 'tasks']);
  });

  it('sem listagem, as equipes de todas, sem repetir e por nome; listagens sem equipe dele ou inativas ficam fora', () => {
    const opcoes = opcoesDaFila(
      [listagem('testes', [t, d]), listagem('tasks', [d]), listagem('nova', []), listagem('antiga', [t], false)],
      null,
    );
    expect(opcoes.equipes).toEqual([d, t]);
    expect(opcoes.listagens.map((l) => l.chave)).toEqual(['testes', 'tasks']);
  });

  it('a fila abre na área atual, a menos que a URL já diga a área ou "todas"', () => {
    const area = (chave: string, equipes = [{ id: 'a', nome: 'Alfa' }]) => ({
      chave,
      nome: chave,
      ativo: true,
      minhasEquipes: equipes,
      possuiEquipes: true,
    });
    const listagens = [area('testes'), area('tasks'), area('sem_equipe', [])];

    expect(areaInicialDaFila({}, 'tasks', listagens)).toBe('tasks');
    expect(areaInicialDaFila({ grupo: 'testes' }, 'tasks', listagens)).toBeNull();
    expect(areaInicialDaFila({ todas: '1' }, 'tasks', listagens)).toBeNull();
    expect(areaInicialDaFila({}, 'sem_equipe', listagens)).toBeNull();
    expect(areaInicialDaFila({}, null, listagens)).toBeNull();
  });
});
