import {
  alvosPossiveis,
  camposDependentes,
  exemploDependencia,
  fraseDependencia,
  origensPossiveis,
  viasPossiveis,
} from './dependencias';
import { estruturaDesempenho } from './estrutura-de-teste';

describe('construtor de dependências (só combinações válidas, RN55)', () => {
  const estrutura = estruturaDesempenho();
  const cenario = estrutura.entidades.find((e) => e.chave === 'cenario')!;
  const relatorio = estrutura.entidades.find((e) => e.chave === 'relatorio')!;
  const endpoint = cenario.campos.find((c) => c.chave === 'endpoint')!;

  it('só campos REFERENCIA ativos podem depender', () => {
    expect(camposDependentes(cenario).map((c) => c.chave)).toEqual(['relatorio', 'endpoint']);
  });

  it('via: só os que levam a alguma origem com alvo; sem via, só se o próprio item tiver origem', () => {
    // No cenário, nenhum outro campo do próprio item aponta para "projeto": não há opção "sem via".
    expect(viasPossiveis(estrutura, cenario, endpoint).map((v) => v?.chave ?? null)).toEqual(['relatorio']);
  });

  it('origem: na entidade do via, só as que têm alvo na entidade apontada', () => {
    // Relatório tem projeto (endpoint também tem projeto) e ambiente (endpoint não tem ambiente).
    expect(origensPossiveis(estrutura, cenario, endpoint, 'relatorio').map((c) => c.chave)).toEqual(['projeto']);
  });

  it('alvo: só campos da entidade apontada que apontam para a mesma entidade da origem', () => {
    const origem = relatorio.campos.find((c) => c.chave === 'projeto')!;
    expect(alvosPossiveis(estrutura, endpoint, origem).map((c) => c.chave)).toEqual(['projeto']);
    const ambiente = relatorio.campos.find((c) => c.chave === 'ambiente')!;
    expect(alvosPossiveis(estrutura, endpoint, ambiente)).toEqual([]);
  });

  it('sem via: origem no próprio item, nunca o próprio campo dependente', () => {
    // Em "relatorio", ambiente poderia depender de projeto? Não há alvo (ambiente não tem projeto): nada a oferecer.
    const ambienteDoRelatorio = relatorio.campos.find((c) => c.chave === 'ambiente')!;
    expect(viasPossiveis(estrutura, relatorio, ambienteDoRelatorio)).toEqual([]);
    expect(origensPossiveis(estrutura, cenario, endpoint, null).map((c) => c.chave)).not.toContain('endpoint');
  });

  it('a frase e o exemplo explicam a regra em português', () => {
    const frase = fraseDependencia(estrutura, cenario, endpoint, {
      via: 'relatorio',
      origem: 'projeto',
      alvo: 'projeto',
    });
    expect(frase.map((t) => t.texto).join('')).toBe(
      'As opções de Endpoint mostram só os itens de Endpoints de Serviço cujo Projeto é igual ao Projeto do item ' +
        'escolhido em Relatório.',
    );
    expect(frase.filter((t) => t.destaque).map((t) => t.texto)).toEqual([
      'Endpoint',
      'Endpoints de Serviço',
      'Projeto',
      'Projeto',
      'Relatório',
    ]);
    expect(exemploDependencia(cenario, endpoint, { via: 'relatorio', origem: 'projeto', alvo: 'projeto' })).toContain(
      '"Endpoint" fica desabilitado até alguém escolher "Relatório"',
    );
  });
});
