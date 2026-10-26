import { FormControl, FormRecord } from '@angular/forms';
import { Campo, Entidade } from '../../core/metadados/modelo';
import { CorpoRegistro, Registro } from '../../core/registros/modelo-registro';
import { Problema } from '../../core/erros/problema';
import { validadoresDoCampo } from './validadores';
import { ValorCampo, valorDaApi, valorParaApi } from './valores';

/**
 * O formulário genérico: um FormControl por campo ativo da entidade, com os validadores dos metadados. As chaves
 * do FormRecord são as chaves dos campos. Funções puras (testadas em montador-formulario.spec.ts).
 */
export type FormularioRegistro = FormRecord<FormControl<ValorCampo>>;

export function criarControle(campo: Campo, valor: ValorCampo): FormControl<ValorCampo> {
  return new FormControl<ValorCampo>(valor, { validators: validadoresDoCampo(campo) });
}

/**
 * O layout do formulário: os campos CURTOS no grid de cima (na ordem dos metadados) e os TEXTO_LONGO embaixo, um por linha, na
 * largura toda. Assim os textos longos não espremem o grid nem deixam buracos entre os campos curtos.
 */
export function layoutDoFormulario(campos: Campo[]): { curtos: Campo[]; longos: Campo[] } {
  return {
    curtos: campos.filter((c) => c.tipo !== 'TEXTO_LONGO'),
    longos: campos.filter((c) => c.tipo === 'TEXTO_LONGO'),
  };
}

/** Sem registro: formulário vazio (booleanos em "não"). Com registro: os valores dele (edição). */
export function montarFormulario(entidade: Entidade, registro?: Registro | null): FormularioRegistro {
  const formulario: FormularioRegistro = new FormRecord<FormControl<ValorCampo>>({});
  for (const campo of entidade.campos) {
    formulario.addControl(campo.chave, criarControle(campo, valorDaApi(campo, registro?.dados[campo.chave])));
  }
  return formulario;
}

/**
 * Metadados recarregados no meio do preenchimento (a estrutura mudou na administração): o que a pessoa já digitou volta para os
 * campos que continuam existindo com o MESMO tipo. Campos novos começam vazios; os que sumiram são descartados.
 */
export function preservarValores(
  novo: FormularioRegistro,
  novaEntidade: Entidade,
  valoresAntigos: Record<string, ValorCampo>,
  camposAntigos: Campo[],
): void {
  for (const campo of novaEntidade.campos) {
    const antigo = camposAntigos.find((c) => c.chave === campo.chave);
    const controle = novo.controls[campo.chave];
    if (antigo && antigo.tipo === campo.tipo && controle && campo.chave in valoresAntigos) {
      controle.setValue(valoresAntigos[campo.chave]);
      controle.markAsDirty();
    }
  }
}

/** O corpo do POST/PUT com TODOS os campos (o PUT substitui o registro inteiro, RN15). Inclui os desabilitados. */
export function corpoDoFormulario(entidade: Entidade, formulario: FormularioRegistro): CorpoRegistro {
  const valores = formulario.getRawValue();
  const corpo: CorpoRegistro = {};
  for (const campo of entidade.campos) {
    corpo[campo.chave] = valorParaApi(campo, valores[campo.chave] ?? null);
  }
  return corpo;
}

/**
 * Os erros de um 400 (`errors` por campo, RN05) vão para baixo de cada campo (erro "servidor"); os que não são de
 * nenhum campo do formulário (ex.: "equipeId" sem select, "$") voltam para aparecer no topo.
 */
export function aplicarErrosDoServidor(formulario: FormularioRegistro, problema: Problema): string[] {
  const gerais: string[] = [];
  for (const [chave, mensagens] of Object.entries(problema.erros)) {
    const controle = formulario.controls[chave];
    if (controle) {
      controle.setErrors({ ...(controle.errors ?? {}), servidor: mensagens });
      controle.markAsTouched();
    } else {
      gerais.push(...mensagens);
    }
  }
  return gerais;
}

/** 409 CONFLITO_UNICIDADE: marca os campos da restrição (detalhes.campos) com a mensagem do servidor. */
export function marcarUnicidade(formulario: FormularioRegistro, problema: Problema): string[] {
  const campos = (problema.detalhes?.['campos'] as string[] | undefined) ?? [];
  const mensagem = problema.detalhe ?? 'Já existe um item com este valor.';
  const marcados: string[] = [];
  for (const chave of campos) {
    const controle = formulario.controls[chave];
    if (controle) {
      controle.setErrors({ ...(controle.errors ?? {}), servidor: [mensagem] });
      controle.markAsTouched();
      marcados.push(chave);
    }
  }
  return marcados;
}
