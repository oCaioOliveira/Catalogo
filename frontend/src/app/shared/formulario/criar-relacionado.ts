import { UsuarioLogado } from '../../core/auth/usuario';
import { Entidade, OpcaoReferencia } from '../../core/metadados/modelo';
import { EquipeOferecida, equipesParaCriar, podeCriar } from '../../core/permissoes/permissoes';
import { ValorCampo } from './valores';

// "Criar item relacionado sem sair do formulário": a opção "+ Criar <tipo>" no select de referência, o
// pré-preenchimento do diálogo e quem pode criar. Funções puras, sem Angular: criar-relacionado.spec.ts.

/** O id da opção especial: nunca é um registro de verdade (os ids da API são GUIDs). */
export const ID_CRIAR_NOVO = '__criar_novo__';

/**
 * A opção "+ Criar RTC" (ou "+ Criar RTC 'RTC-9'", com o que foi digitado), sempre no fim da lista. Forma neutra: o nome do tipo
 * vem como está, sem "novo"/"nova" (os metadados não dizem o gênero: "+ Criar Sigla 'ABC'").
 */
export function opcaoCriarNovo(texto: string, nomeTipo: string): OpcaoReferencia {
  const digitado = texto.trim();
  return {
    id: ID_CRIAR_NOVO,
    rotulo: digitado ? `+ Criar ${nomeTipo} '${digitado}'` : `+ Criar ${nomeTipo}`,
  };
}

export function ehOpcaoCriarNovo(valor: unknown): boolean {
  return typeof valor === 'object' && valor !== null && (valor as { id?: unknown }).id === ID_CRIAR_NOVO;
}

/**
 * O que vem preenchido no diálogo: o texto digitado vai para o CAMPO DE EXIBIÇÃO do tipo referenciado, quando ele é de texto
 * (cortado no tamanho máximo). Sem texto, ou com outro tipo de campo de exibição, nada é preenchido.
 */
export function valoresIniciaisDoTexto(entidade: Entidade, texto: string): Record<string, ValorCampo> {
  const digitado = texto.trim();
  const campo = entidade.campos.find((c) => c.chave === entidade.campoExibicao);
  if (!digitado || !campo || (campo.tipo !== 'TEXTO' && campo.tipo !== 'TEXTO_LONGO' && campo.tipo !== 'URL')) {
    return {};
  }
  return { [campo.chave]: campo.tamanhoMaximo ? digitado.slice(0, campo.tamanhoMaximo) : digitado };
}

/**
 * A opção "Criar novo" só aparece se o usuário pode criar no tipo referenciado (RN26, RN27: cadastros de apoio só o
 * ADMINISTRADOR; RN34: o EXTERNO só onde a aprovação está ativa). Num tipo POR EQUIPE, o item novo nasce na equipe do formulário
 * principal: se ela já é conhecida, o usuário precisa poder criar nela; senão, em alguma das equipes dele na área.
 */
export function podeCriarRelacionado(
  usuario: UsuarioLogado | null,
  referenciada: Entidade,
  equipesNaListagem: readonly EquipeOferecida[],
  equipeDoFormulario: string | null,
): boolean {
  if (!podeCriar(usuario, referenciada, equipesNaListagem)) {
    return false;
  }
  if (!referenciada.escopoEquipe || !equipeDoFormulario) {
    return true;
  }
  return equipesParaCriar(usuario, referenciada, equipesNaListagem).some((e) => e.id === equipeDoFormulario);
}
