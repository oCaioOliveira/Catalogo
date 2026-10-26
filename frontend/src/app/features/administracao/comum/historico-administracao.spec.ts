import { MudancaDto } from '../../../api/models';
import { nomeCampoAdministracao, valorAdministracao } from './historico-administracao';

/** Uma mudança como vem da API (JSON qualquer em "de" e "para"). */
function mudanca(campo: string, de: unknown, para: unknown): MudancaDto {
  return { campo, de, para } as unknown as MudancaDto;
}

describe('histórico de usuários e equipes (RN32)', () => {
  const equipes = new Map([
    ['e1', 'Alfa'],
    ['e2', 'Beta'],
  ]);

  it('equipes pelo nome, perfil por extenso e situação', () => {
    expect(valorAdministracao(mudanca('equipes', ['e1'], ['e1', 'e2']), 'para', equipes)).toBe('Alfa, Beta');
    expect(valorAdministracao(mudanca('perfil', 'INTERNO', 'GESTOR'), 'para', equipes)).toBe('Gestor');
    expect(valorAdministracao(mudanca('ativo', true, false), 'para', equipes)).toBe('Inativo');
    expect(nomeCampoAdministracao('equipes')).toBe('Equipes');
  });

  it('senha: só o evento ("redefinida"), nunca um valor', () => {
    expect(valorAdministracao(mudanca('senha', null, 'redefinida'), 'para', equipes)).toBe('redefinida');
    // Mesmo que algo inesperado viesse no campo, a tela não o mostraria.
    expect(valorAdministracao(mudanca('senha', null, 'Segredo123456'), 'para', equipes)).toBe('(alterada)');
  });
});
