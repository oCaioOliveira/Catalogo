import { campo, entidade } from '../../../testes/metadados-de-teste';
import {
  nomeEmFrase,
  campoDoPai,
  colunasPadrao,
  ehEntidadeFilha,
  entidadeInicial,
  filhasDe,
  montarMenu,
  nomePlural,
  podeOrdenarPor,
  referenciaComoLink,
} from './regras-metadados';

describe('regras-metadados', () => {
  const relatorio = entidade('relatorio_teste', [campo('bateria', 'TEXTO'), campo('contexto', 'TEXTO_LONGO')], {
    ordem: 8,
  });
  const cenario = entidade(
    'cenario_teste',
    [
      campo('relatorio_teste', 'REFERENCIA', {
        obrigatorio: true,
        acaoExclusao: 'CASCATA',
        entidadeReferenciada: 'relatorio_teste',
      }),
    ],
    { ordem: 9 },
  );
  const linkOpcional = entidade('link_opcional', [
    campo('relatorio_teste', 'REFERENCIA', {
      obrigatorio: false,
      acaoExclusao: 'CASCATA',
      entidadeReferenciada: 'relatorio_teste',
    }),
  ]);
  const projeto = entidade(
    'projeto',
    [campo('sigla', 'REFERENCIA', { obrigatorio: true, acaoExclusao: 'RESTRINGIR', entidadeReferenciada: 'sigla' })],
    { ordem: 3 },
  );
  const comunidade = entidade('comunidade', [campo('nome', 'TEXTO')], { escopoEquipe: false, ordem: 1 });

  it('filha é quem tem REFERENCIA obrigatória com CASCATA', () => {
    expect(ehEntidadeFilha(cenario)).toBeTrue();
    expect(campoDoPai(cenario)?.chave).toBe('relatorio_teste');
  });

  it('REFERENCIA opcional com CASCATA, ou obrigatória com RESTRINGIR, não faz filha', () => {
    expect(ehEntidadeFilha(linkOpcional)).toBeFalse();
    expect(ehEntidadeFilha(projeto)).toBeFalse();
  });

  it('filhasDe devolve as filhas do pai com o campo que aponta para ele', () => {
    const filhas = filhasDe(relatorio, [relatorio, cenario, linkOpcional, projeto]);
    expect(filhas.map((f) => f.entidade.chave)).toEqual(['cenario_teste']);
    expect(filhas[0].campoPai.chave).toBe('relatorio_teste');
  });

  it('o menu separa principais (por equipe) e apoio (globais), na ordem, sem filhas', () => {
    const menu = montarMenu([cenario, relatorio, comunidade, projeto]);
    expect(menu.principais.map((e) => e.chave)).toEqual(['projeto', 'relatorio_teste']);
    expect(menu.apoio.map((e) => e.chave)).toEqual(['comunidade']);
  });

  it('as colunas padrão deixam TEXTO_LONGO de fora', () => {
    expect(colunasPadrao(relatorio)).toEqual(['bateria']);
  });

  it('as colunas padrão incluem URL (só o TEXTO_LONGO fica de fora)', () => {
    expect(colunasPadrao(entidade('link', [campo('texto', 'TEXTO'), campo('url', 'URL')]))).toEqual(['texto', 'url']);
  });

  it('a tela inicial é a primeira entidade do menu, pela ordem dos metadados (principais antes do apoio)', () => {
    expect(entidadeInicial([comunidade, projeto, relatorio, cenario])?.chave).toBe('projeto');
    expect(entidadeInicial([comunidade, cenario])?.chave).toBe('comunidade');
    expect(entidadeInicial([])).toBeNull();
  });

  it('plural: o nomePlural; sem ele, o nome', () => {
    expect(nomePlural(entidade('rtc', [], { nome: 'RTC', nomePlural: 'RTCs' }))).toBe('RTCs');
    expect(nomePlural(entidade('rtc', [], { nome: 'RTC' }))).toBe('RTC');
  });

  describe('referências e ordenação', () => {
    const ambiente = entidade('ambiente', [campo('nome', 'TEXTO')], { escopoEquipe: false });
    const semExibicao = entidade('sem_exibicao', [campo('codigo', 'TEXTO')], { campoExibicao: null });
    const paraProjeto = campo('projeto', 'REFERENCIA', { entidadeReferenciada: 'projeto' });
    const paraAmbiente = campo('ambiente', 'REFERENCIA', { entidadeReferenciada: 'ambiente' });
    const paraSemExibicao = campo('x', 'REFERENCIA', { entidadeReferenciada: 'sem_exibicao' });
    const paraInvisivel = campo('y', 'REFERENCIA', { entidadeReferenciada: 'nao_vejo' });
    const todas = [projeto, ambiente, semExibicao];

    it('referência para entidade por equipe é link; para cadastro de apoio global (ou que não vejo), texto', () => {
      expect(referenciaComoLink(paraProjeto, todas)).toBeTrue();
      expect(referenciaComoLink(paraAmbiente, todas)).toBeFalse();
      expect(referenciaComoLink(paraInvisivel, todas)).toBeFalse();
      expect(referenciaComoLink(campo('nome', 'TEXTO'), todas)).toBeFalse();
    });

    it('ordena por REFERENCIA só se a apontada tem campo de exibição (RN19); TEXTO_LONGO nunca', () => {
      expect(podeOrdenarPor(paraProjeto, todas)).toBeTrue();
      expect(podeOrdenarPor(paraAmbiente, todas)).toBeTrue();
      expect(podeOrdenarPor(paraSemExibicao, todas)).toBeFalse();
      expect(podeOrdenarPor(paraInvisivel, todas)).toBeFalse();
      expect(podeOrdenarPor(campo('contexto', 'TEXTO_LONGO'), todas)).toBeFalse();
      expect(podeOrdenarPor(campo('url', 'URL'), todas)).toBeTrue();
      expect(podeOrdenarPor(campo('vazao', 'DECIMAL'), todas)).toBeTrue();
    });
  });

  it('nome no meio de uma frase: minúsculas, menos as siglas', () => {
    expect(nomeEmFrase('Relatório de Teste')).toBe('relatório de teste');
    expect(nomeEmFrase('RTC')).toBe('RTC');
    expect(nomeEmFrase('Endpoint de Serviço')).toBe('endpoint de serviço');
    expect(nomeEmFrase('Link da URL')).toBe('link da URL');
  });
});
