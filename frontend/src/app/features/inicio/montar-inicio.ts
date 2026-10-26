import { Entidade } from '../../core/metadados/modelo';
import { classeIcone, montarMenu, nomePlural } from '../../core/metadados/regras-metadados';
import { Recente } from '../../core/registros/modelo-registro';

// A página Início de uma área de gestão (seção "Início" de docs/frontend.md). Funções puras, sem Angular: montar-inicio.spec.ts.

/** Um atalho para um tipo de item principal: ícone, nome no plural e se o usuário pode criar. A quantidade chega depois. */
export interface Atalho {
  entidade: Entidade;
  nome: string;
  /** A descrição do tipo de item (metadados), ou null. O card mostra até 2 linhas, com o texto completo na dica. */
  descricao: string | null;
  /** "Novo: Relatório de Teste" (o singular), para o nome acessível do botão. */
  rotuloNovo: string;
  icone: string;
  podeCriar: boolean;
}

/** Um item de "Continuar de onde parou": o tipo (singular), o rótulo e o link para o detalhe. */
export interface ItemRecente {
  id: string;
  tipo: string;
  icone: string;
  rotulo: string;
  link: string[];
  ocorridoEm: string;
}

/** Os atalhos: os tipos de item PRINCIPAIS (por equipe e não filhos), na ordem do menu. */
export function atalhosDaArea(entidades: Entidade[], podeCriar: (entidade: Entidade) => boolean): Atalho[] {
  return montarMenu(entidades).principais.map((entidade) => ({
    entidade,
    nome: nomePlural(entidade),
    descricao: entidade.descricao?.trim() || null,
    rotuloNovo: `Novo: ${entidade.nome}`,
    icone: classeIcone(entidade),
    podeCriar: podeCriar(entidade),
  }));
}

/** Os recentes com o tipo de item de cada um. Um tipo que o usuário não vê mais na lista de metadados fica de fora. */
export function itensRecentes(grupo: string, recentes: Recente[], entidades: Entidade[]): ItemRecente[] {
  return recentes.flatMap((r) => {
    const entidade = entidades.find((e) => e.chave === r.entidade);
    if (!entidade) {
      return [];
    }
    return [
      {
        id: r.id,
        tipo: entidade.nome,
        icone: classeIcone(entidade),
        rotulo: r.rotulo?.trim() || '(sem rótulo)',
        link: ['/', grupo, entidade.chave, r.id],
        ocorridoEm: r.ocorridoEm,
      },
    ];
  });
}

/** "Bom dia", "Boa tarde" ou "Boa noite", pela hora local, com o primeiro nome. */
export function saudacao(nome: string | null | undefined, hora: number): string {
  const periodo = hora < 5 ? 'Boa noite' : hora < 12 ? 'Bom dia' : hora < 18 ? 'Boa tarde' : 'Boa noite';
  const primeiro = (nome ?? '').trim().split(/\s+/)[0];
  return primeiro ? `${periodo}, ${primeiro}` : periodo;
}

/** "1 item", "12 itens" (com separador de milhar). */
export function textoQuantidade(quantidade: number): string {
  return quantidade === 1 ? '1 item' : `${quantidade.toLocaleString('pt-BR')} itens`;
}
