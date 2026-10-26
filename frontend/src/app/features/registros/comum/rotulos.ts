import { Entidade } from '../../../core/metadados/modelo';
import { Registro } from '../../../core/registros/modelo-registro';
import { formatarValor } from '../../../shared/formatacao/formatar-valor';

/** O rótulo de um registro: o valor do campo de exibição da entidade, formatado (RN20). */
export function rotuloDoRegistro(entidade: Entidade, registro: Registro): string {
  const campo = entidade.campos.find((c) => c.chave === entidade.campoExibicao);
  const texto = campo ? formatarValor(campo, registro.dados[campo.chave]) : '';
  return texto || '(sem rótulo)';
}
