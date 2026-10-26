import { Campo, Entidade } from '../../../core/metadados/modelo';
import { Registro } from '../../../core/registros/modelo-registro';
import { vazio } from '../../../shared/formulario/validadores';

/**
 * Como o detalhe mostra os campos de um registro (funções puras, leitura-registro.spec.ts):
 *   - curtos: os preenchidos que não são TEXTO_LONGO, no grid, na ordem dos metadados;
 *   - longos: os TEXTO_LONGO preenchidos, um por linha, na largura toda (com as quebras de linha);
 *   - vazios: TODOS os campos sem valor, de qualquer tipo. Não ocupam espaço: aparecem numa linha só ("Não preenchidos: …").
 * Vazio = sem valor, ou texto só com espaços (RN03). BOOLEANO false é um valor ("Não").
 */
export interface LeituraRegistro {
  curtos: Campo[];
  longos: Campo[];
  vazios: Campo[];
}

export function leituraDoRegistro(entidade: Entidade, registro: Registro): LeituraRegistro {
  const preenchidos = entidade.campos.filter((c) => !vazio(registro.dados[c.chave]));
  return {
    curtos: preenchidos.filter((c) => c.tipo !== 'TEXTO_LONGO'),
    longos: preenchidos.filter((c) => c.tipo === 'TEXTO_LONGO'),
    vazios: entidade.campos.filter((c) => vazio(registro.dados[c.chave])),
  };
}

/** "Contexto, Objetivo, Conclusão": os nomes dos campos vazios, para a linha discreta do detalhe. */
export function listaDeNomes(campos: Campo[]): string {
  return campos.map((c) => c.nome).join(', ');
}
