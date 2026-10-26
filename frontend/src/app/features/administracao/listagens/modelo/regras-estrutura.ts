import { CampoEntrada, EntidadeEntrada } from '../../../../api/models';
import {
  CampoEstrutura,
  EntidadeEstrutura,
  EstruturaListagem,
} from '../../../../core/administracao/estrutura-listagem';
import { Campo, Entidade } from '../../../../core/metadados/modelo';
import { gerarChave } from '../../../../shared/chaves/chave';

// Regras que a administração de listagens tira da estrutura (funções puras, regras-estrutura.spec.ts). As mesmas do back-end
// (RN53 a RN55): a tela só oferece o que a API aceitaria; a API valida de novo.

/** Filha = tem um REFERENCIA obrigatório com CASCATA para outro tipo: vira seção no detalhe dele (docs/frontend.md). */
export function paiDe(entidade: EntidadeEstrutura): string | null {
  const campo = entidade.campos.find(
    (c) => c.ativo && c.tipo === 'REFERENCIA' && c.obrigatorio && c.acaoExclusao === 'CASCATA',
  );
  return campo?.entidadeReferenciada ?? null;
}

/** Onde o campo é usado (o que impede desativá-lo, RN69): exibição, unicidades e dependências, dele ou de outros campos. */
export function usosDoCampo(
  entidade: EntidadeEstrutura,
  campo: CampoEstrutura,
  estrutura: EstruturaListagem,
): string[] {
  const usos: string[] = [];
  if (entidade.campoExibicao === campo.chave) {
    usos.push('Exibição');
  }
  for (const restricao of entidade.restricoes.filter((r) => r.campos.includes(campo.chave))) {
    usos.push(`Unicidade (${restricao.campos.length > 1 ? 'composta' : 'simples'})`);
  }
  if (campo.dependencia) {
    usos.push('Dependência (filtra as opções)');
  }
  for (const outra of estrutura.entidades) {
    for (const c of outra.campos.filter((x) => x.dependencia)) {
      const d = c.dependencia!;
      const usado =
        (outra.chave === entidade.chave && (d.via === campo.chave || (!d.via && d.origem === campo.chave))) ||
        (d.via && viaAponta(outra, d.via) === entidade.chave && d.origem === campo.chave) ||
        (c.entidadeReferenciada === entidade.chave && d.alvo === campo.chave);
      if (usado && c.chave !== campo.chave) {
        usos.push(`Dependência de "${c.nome}"`);
      }
    }
  }
  return [...new Set(usos)];
}

function viaAponta(entidade: EntidadeEstrutura, via: string): string | null {
  return entidade.campos.find((c) => c.chave === via)?.entidadeReferenciada ?? null;
}

/** RN54: o campo de exibição (o rótulo do item nas referências) é um campo ativo, nem REFERENCIA nem TEXTO_LONGO. */
export function camposParaExibicao(entidade: EntidadeEstrutura): CampoEstrutura[] {
  return entidade.campos.filter((c) => c.ativo && c.tipo !== 'REFERENCIA' && c.tipo !== 'TEXTO_LONGO');
}

/** RN55: uma unicidade usa de 1 a 5 campos ativos, sem TEXTO_LONGO. */
export const MAXIMO_CAMPOS_UNICIDADE = 5;

export function camposParaUnicidade(entidade: EntidadeEstrutura): CampoEstrutura[] {
  return entidade.campos.filter((c) => c.ativo && c.tipo !== 'TEXTO_LONGO');
}

/** "uq_equipamento_nome": o nome sugerido para uma unicidade nova (no padrão das chaves). */
export function nomeDeUnicidade(entidade: string, campos: string[]): string {
  return gerarChave(['uq', entidade, ...campos].join('_'));
}

/**
 * RN54: os tipos de item que um campo REFERENCIA pode apontar: os ativos da mesma listagem. Um tipo GLOBAL só aponta para
 * globais (um registro global não pode apontar para registros de uma equipe, RN29); um POR EQUIPE aponta para qualquer um.
 */
export function entidadesReferenciaveis(
  estrutura: EstruturaListagem,
  entidade: EntidadeEstrutura,
): EntidadeEstrutura[] {
  return estrutura.entidades.filter((e) => e.ativo && (entidade.escopoEquipe || !e.escopoEquipe));
}

/**
 * A entidade como as telas de USO a verão (só os campos ativos), para a pré-visualização: o mesmo formulário genérico e as
 * mesmas colunas, sem gravar nada. `dependeDe` segue a RN21: o via, se houver; senão, a origem.
 */
export function entidadeParaPreVisualizacao(entidade: EntidadeEstrutura): Entidade {
  const campos: Campo[] = entidade.campos
    .filter((c) => c.ativo)
    .map((c) => ({
      chave: c.chave,
      nome: c.nome,
      tipo: c.tipo,
      obrigatorio: c.obrigatorio,
      ordem: c.ordem,
      ativo: true,
      tamanhoMaximo: c.tamanhoMaximo,
      precisao: c.precisao,
      escala: c.escala,
      opcoes: c.opcoes,
      entidadeReferenciada: c.entidadeReferenciada,
      acaoExclusao: c.acaoExclusao,
      dependeDe: c.dependencia ? (c.dependencia.via ?? c.dependencia.origem) : null,
    }));
  return {
    chave: entidade.chave,
    nome: entidade.nome,
    nomePlural: entidade.nomePlural,
    descricao: entidade.descricao,
    icone: entidade.icone,
    escopoEquipe: entidade.escopoEquipe,
    campoExibicao: entidade.campoExibicao,
    ordem: entidade.ordem,
    ativo: entidade.ativo,
    campos,
    restricoesUnicas: entidade.restricoes,
    equipesComAprovacao: [],
    criteriosAvaliacao: [],
  };
}

/** A ordem nova depois de arrastar e soltar (o mesmo que o moveItemInArray do CDK, sem mudar a lista original). */
export function moverItem<T>(lista: readonly T[], de: number, para: number): T[] {
  const nova = [...lista];
  const [item] = nova.splice(de, 1);
  nova.splice(Math.max(0, Math.min(para, nova.length)), 0, item);
  return nova;
}

/**
 * O corpo COMPLETO do PUT de entidade: na API, ausente = apagado (descrição, plural, ícone, campo de exibição), então a tela sempre manda
 * tudo o que a entidade já tem, mais o que mudou.
 */
export function corpoDaEntidade(entidade: EntidadeEstrutura, mudancas: Partial<EntidadeEntrada> = {}): EntidadeEntrada {
  return {
    versao: entidade.versao,
    nome: entidade.nome,
    nomePlural: entidade.nomePlural,
    icone: entidade.icone,
    descricao: entidade.descricao,
    ordem: entidade.ordem,
    escopoEquipe: entidade.escopoEquipe,
    campoExibicao: entidade.campoExibicao,
    ...mudancas,
  };
}

/** O corpo COMPLETO do PUT de campo (os atributos ausentes voltam ao padrão na API, RN53), mais o que mudou. */
export function corpoDoCampo(
  campo: CampoEstrutura,
  versaoEntidade: string,
  mudancas: Partial<CampoEntrada> = {},
): CampoEntrada {
  return {
    versao: versaoEntidade,
    nome: campo.nome,
    tipoDado: campo.tipo,
    obrigatorio: campo.obrigatorio,
    tamanhoMaximo: campo.tamanhoMaximo,
    precisao: campo.precisao,
    escala: campo.escala,
    opcoes: campo.tipo === 'LISTA' ? campo.opcoes : null,
    entidadeReferenciada: campo.entidadeReferenciada,
    acaoExclusao: campo.acaoExclusao,
    ...mudancas,
  };
}
