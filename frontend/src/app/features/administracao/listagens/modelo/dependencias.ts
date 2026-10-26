import {
  CampoEstrutura,
  EntidadeEstrutura,
  EstruturaListagem,
} from '../../../../core/administracao/estrutura-listagem';

/**
 * Construtor guiado de dependências (RN08, RN21, RN55), em funções puras (dependencias.spec.ts). Uma dependência diz: "em
 * `dependente`, só vale escolher itens cujo `alvo` seja igual à `origem`", lida no próprio item (sem via) ou no item escolhido
 * no `via` (um salto). Cada select só oferece combinações que a API aceita:
 *   - dependente: campo REFERENCIA ativo;
 *   - via (opcional): outro REFERENCIA ativo da mesma entidade;
 *   - origem: REFERENCIA ativo da entidade do via (ou desta, sem via, e diferente do dependente);
 *   - alvo: REFERENCIA ativo da entidade apontada pelo dependente, que aponta para a MESMA entidade que a origem.
 * Ciclos (um valor que depende de si mesmo por outras dependências) a API recusa com a explicação no campo.
 */
export interface EscolhaDependencia {
  via: string | null;
  origem: string;
  alvo: string;
}

function entidadePorChave(estrutura: EstruturaListagem, chave: string | null): EntidadeEstrutura | null {
  return estrutura.entidades.find((e) => e.chave === chave) ?? null;
}

function referenciasAtivas(entidade: EntidadeEstrutura | null): CampoEstrutura[] {
  return (entidade?.campos ?? []).filter((c) => c.ativo && c.tipo === 'REFERENCIA');
}

/** Os campos que podem ter as opções filtradas: os REFERENCIA ativos. */
export function camposDependentes(entidade: EntidadeEstrutura): CampoEstrutura[] {
  return referenciasAtivas(entidade);
}

/** Os alvos: campos da entidade apontada pelo dependente que apontam para a mesma entidade da origem. */
export function alvosPossiveis(
  estrutura: EstruturaListagem,
  dependente: CampoEstrutura,
  origem: CampoEstrutura,
): CampoEstrutura[] {
  return referenciasAtivas(entidadePorChave(estrutura, dependente.entidadeReferenciada)).filter(
    (c) => c.entidadeReferenciada === origem.entidadeReferenciada,
  );
}

/** As origens: no item escolhido no via (ou neste item, sem via), só as que têm algum alvo possível. */
export function origensPossiveis(
  estrutura: EstruturaListagem,
  entidade: EntidadeEstrutura,
  dependente: CampoEstrutura,
  via: string | null,
): CampoEstrutura[] {
  const campoVia = via ? entidade.campos.find((c) => c.chave === via) : null;
  const base = campoVia ? entidadePorChave(estrutura, campoVia.entidadeReferenciada) : entidade;
  return referenciasAtivas(base)
    .filter((c) => via !== null || c.chave !== dependente.chave)
    .filter((c) => alvosPossiveis(estrutura, dependente, c).length > 0);
}

/** Os vias: null (a origem está neste mesmo item) e os outros REFERENCIA que levam a alguma origem válida. */
export function viasPossiveis(
  estrutura: EstruturaListagem,
  entidade: EntidadeEstrutura,
  dependente: CampoEstrutura,
): (CampoEstrutura | null)[] {
  const vias = referenciasAtivas(entidade)
    .filter((c) => c.chave !== dependente.chave)
    .filter((c) => origensPossiveis(estrutura, entidade, dependente, c.chave).length > 0);
  return origensPossiveis(estrutura, entidade, dependente, null).length > 0 ? [null, ...vias] : vias;
}

/** Um trecho da frase: texto comum ou um nome em destaque (negrito na tela). */
export interface TrechoFrase {
  texto: string;
  destaque?: boolean;
}

/**
 * A regra em português: "As opções de **Endpoint** mostram só os itens de **Endpoints de Serviço** cujo **Projeto** é igual ao
 * **Projeto** do item escolhido em **Relatório**." Sem via: "... igual ao valor escolhido em **Projeto** neste mesmo formulário."
 */
export function fraseDependencia(
  estrutura: EstruturaListagem,
  entidade: EntidadeEstrutura,
  dependente: CampoEstrutura,
  escolha: EscolhaDependencia,
): TrechoFrase[] {
  const apontada = entidadePorChave(estrutura, dependente.entidadeReferenciada);
  const nomeApontada = apontada ? (apontada.nomePlural ?? apontada.nome) : (dependente.entidadeReferenciada ?? '');
  const via = escolha.via ? entidade.campos.find((c) => c.chave === escolha.via) : null;
  const baseOrigem = via ? entidadePorChave(estrutura, via.entidadeReferenciada) : entidade;
  const nome = (e: EntidadeEstrutura | null, chave: string) => e?.campos.find((c) => c.chave === chave)?.nome ?? chave;
  const destaque = (texto: string): TrechoFrase => ({ texto, destaque: true });
  const inicio: TrechoFrase[] = [
    { texto: 'As opções de ' },
    destaque(dependente.nome),
    { texto: ' mostram só os itens de ' },
    destaque(nomeApontada),
    { texto: ' cujo ' },
    destaque(nome(apontada, escolha.alvo)),
  ];
  return via
    ? [
        ...inicio,
        { texto: ' é igual ao ' },
        destaque(nome(baseOrigem, escolha.origem)),
        { texto: ' do item escolhido em ' },
        destaque(via.nome),
        { texto: '.' },
      ]
    : [
        ...inicio,
        { texto: ' é igual ao valor escolhido em ' },
        destaque(nome(baseOrigem, escolha.origem)),
        { texto: ' neste mesmo formulário.' },
      ];
}

/** Como o campo se comporta no formulário (RN21), para o exemplo abaixo da frase. */
export function exemploDependencia(
  entidade: EntidadeEstrutura,
  dependente: CampoEstrutura,
  escolha: EscolhaDependencia,
): string {
  const chaveOrigemNoFormulario = escolha.via ?? escolha.origem;
  const origem = entidade.campos.find((c) => c.chave === chaveOrigemNoFormulario)?.nome ?? chaveOrigemNoFormulario;
  return (
    `No formulário de "${entidade.nome}", "${dependente.nome}" fica desabilitado até alguém escolher "${origem}". ` +
    `Trocar "${origem}" limpa "${dependente.nome}", e a busca passa a mostrar só as opções que combinam.`
  );
}

/** A frase de uma dependência já gravada (a mesma do construtor), ou null se o campo não tem dependência. */
export function fraseDaDependenciaAtual(
  estrutura: EstruturaListagem,
  entidade: EntidadeEstrutura,
  dependente: CampoEstrutura,
): TrechoFrase[] | null {
  return dependente.dependencia ? fraseDependencia(estrutura, entidade, dependente, dependente.dependencia) : null;
}
