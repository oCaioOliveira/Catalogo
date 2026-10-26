import { chamar } from './api';

/**
 * LIMPEZA dos dados de E2E (equipes, usuários e listagens criados pelos testes). TODA suíte que cria equipes ou usuários chama
 * `limparE2E` no INÍCIO (desmonta as sobras de execuções interrompidas) e no FIM (desmonta o que ela criou), mesmo com falha.
 * Só mexe no que tem nome de teste: equipes "E2E-…" ou "Equipe E2E-…", usuários com matrícula "e2e-…" e listagens com "_e2e_" na
 * chave. As equipes e os usuários de desenvolvimento (Alfa, Beta, dev.*) nunca são tocados.
 *
 * A ordem importa (cada passo segue mesmo se o anterior falhar):
 *   1. pendentes aprovados (a aprovação não desliga com pendentes, RN33);
 *   2. aprovação e modelos de avaliação desligados, em cada listagem em que a equipe está habilitada;
 *   3. usuários desativados (a API não desativa a ÚNICA equipe ativa de alguém, RN31);
 *   4. equipe desativada e removida das listagens (RN90: a equipe volta a não ver nada se for reativada).
 * Nada é excluído: equipes e usuários são desativados (RN31), e os itens ficam (RN92).
 */

/** Equipes criadas pelos testes: "E2E-ABC123 Squad", "E2E-ABC123 T", "Equipe E2E-ABC123". */
export const NOME_EQUIPE_E2E = /^(Equipe )?E2E-[A-Z0-9]+( |$)/;
/** Usuários criados pelos testes (o prefixoUnico em minúsculas). */
export const MATRICULA_E2E = /^e2e-/;
/** Listagens criadas pelos testes: inventario_e2e_…, tasks_e2e_… */
export const CHAVE_LISTAGEM_E2E = /_e2e_/;

export interface ResultadoLimpeza {
  equipes: number;
  usuarios: number;
  listagens: number;
  /** Habilitações de equipes de teste (já inativas) tiradas das listagens. */
  habilitacoes: number;
}

interface EquipeNaListagem {
  id: string;
  nome: string;
  habilitada: boolean;
}

async function tentar(contexto: string, passo: () => Promise<unknown>): Promise<void> {
  try {
    await passo();
  } catch (erro) {
    console.warn(`[limpeza E2E] ${contexto}:`, erro);
  }
}

/** As equipes habilitadas numa listagem passam a ser as atuais + `incluir` − `tirar` (PUT com a lista completa e a versão). */
export async function mudarEquipesDaListagem(
  admin: string,
  grupo: string,
  mudanca: { incluir?: string[]; tirar?: string[] },
): Promise<void> {
  const atual = await chamar<{ versao: string; equipes: EquipeNaListagem[] }>(
    admin,
    'GET',
    `/admin/metadados/grupos/${grupo}/equipes`,
  );
  if (atual.status !== 200) {
    throw new Error(`GET equipes de ${grupo}: ${atual.status}`);
  }
  const novas = new Set(atual.corpo.equipes.filter((e) => e.habilitada).map((e) => e.id));
  mudanca.incluir?.forEach((id) => novas.add(id));
  mudanca.tirar?.forEach((id) => novas.delete(id));
  const { status, corpo } = await chamar(admin, 'PUT', `/admin/metadados/grupos/${grupo}/equipes`, {
    versao: atual.corpo.versao,
    equipes: [...novas],
  });
  if (status !== 200) {
    throw new Error(`PUT equipes de ${grupo}: ${status} ${JSON.stringify(corpo)}`);
  }
}

/** Habilita a equipe do teste numa listagem (RN90: equipe nova não é habilitada em nenhuma). */
export function habilitarNaListagem(admin: string, grupo: string, equipeId: string): Promise<void> {
  return mudarEquipesDaListagem(admin, grupo, { incluir: [equipeId] });
}

/** Desmonta UMA equipe de teste, na ordem descrita no topo. Devolve quantos usuários foram desativados. */
export async function desmontarEquipe(admin: string, id: string): Promise<number> {
  const detalhe = await chamar<{ listagens: { chave: string }[] }>(admin, 'GET', `/equipes/${id}`);
  const listagens = (detalhe.corpo?.listagens ?? []).map((l) => l.chave);

  await tentar(`pendentes da equipe ${id}`, async () => {
    const fila = await chamar<{ itens: { id: string; grupo: string; entidade: { chave: string } }[] }>(
      admin,
      'GET',
      `/aprovacoes/pendentes?equipeId=${id}&tamanho=100`,
    );
    for (const item of fila.corpo?.itens ?? []) {
      await chamar(
        admin,
        'POST',
        `/grupos/${item.grupo}/entidades/${item.entidade.chave}/registros/${item.id}/aprovacao`,
        {
          decisao: 'APROVAR',
          justificativa: 'Limpeza do teste automatizado.',
        },
      );
    }
  });

  for (const grupo of listagens) {
    await tentar(`configurações da equipe ${id} em ${grupo}`, async () => {
      const estrutura = await chamar<{ entidades: { chave: string; escopoEquipe: boolean }[] }>(
        admin,
        'GET',
        `/admin/metadados/grupos/${grupo}`,
      );
      for (const entidade of (estrutura.corpo?.entidades ?? []).filter((e) => e.escopoEquipe)) {
        const base = `/equipes/${id}/configuracoes/grupos/${grupo}/entidades/${entidade.chave}`;
        const aprovacao = await chamar<{ ativa: boolean }>(admin, 'GET', `${base}/aprovacao`);
        if (aprovacao.corpo?.ativa) {
          await chamar(admin, 'PUT', `${base}/aprovacao`, { ativa: false });
        }
        const modelos = await chamar<{ id: string; ativo: boolean }[]>(admin, 'GET', `${base}/avaliacao/modelos`);
        for (const modelo of (Array.isArray(modelos.corpo) ? modelos.corpo : []).filter((m) => m.ativo)) {
          await chamar(admin, 'PATCH', `${base}/avaliacao/ativacao`, { ativo: false, modeloId: modelo.id });
        }
      }
    });
  }

  let usuarios = 0;
  await tentar(`membros da equipe ${id}`, async () => {
    const membros = await chamar<{ itens: { id: string; matricula: string }[] }>(
      admin,
      'GET',
      `/usuarios?equipeId=${id}&ativo=true&tamanho=100`,
    );
    for (const usuario of (membros.corpo?.itens ?? []).filter((u) => MATRICULA_E2E.test(u.matricula))) {
      const { status } = await chamar(admin, 'PATCH', `/usuarios/${usuario.id}/ativacao`, { ativo: false });
      if (status < 400) {
        usuarios++;
      }
    }
  });

  await tentar(`ativação da equipe ${id}`, async () => {
    const { status } = await chamar(admin, 'PATCH', `/equipes/${id}/ativacao`, { ativa: false });
    if (status >= 400) {
      throw new Error(`PATCH /equipes/${id}/ativacao: ${status}`);
    }
  });

  for (const grupo of listagens) {
    await tentar(`equipe ${id} na listagem ${grupo}`, () => mudarEquipesDaListagem(admin, grupo, { tirar: [id] }));
  }
  return usuarios;
}

/**
 * Desmonta TUDO o que é de teste e está ativo: equipes (com os membros), usuários soltos (ex.: o do login.spec, criado na Equipe
 * Alfa), listagens e as equipes de teste já inativas que continuam habilitadas em listagens ativas. Escreve no console o que fez.
 */
export async function limparE2E(admin: string): Promise<ResultadoLimpeza> {
  const resultado: ResultadoLimpeza = { equipes: 0, usuarios: 0, listagens: 0, habilitacoes: 0 };

  const equipes = await chamar<{ itens: { id: string; nome: string }[] }>(
    admin,
    'GET',
    '/equipes?ativa=true&busca=E2E-&tamanho=100',
  );
  for (const equipe of (equipes.corpo?.itens ?? []).filter((e) => NOME_EQUIPE_E2E.test(e.nome))) {
    resultado.usuarios += await desmontarEquipe(admin, equipe.id);
    resultado.equipes++;
  }

  await tentar('usuários soltos', async () => {
    for (;;) {
      const pagina = await chamar<{ itens: { id: string; matricula: string }[] }>(
        admin,
        'GET',
        '/usuarios?ativo=true&busca=e2e-&tamanho=100',
      );
      const soltos = (pagina.corpo?.itens ?? []).filter((u) => MATRICULA_E2E.test(u.matricula));
      let desativados = 0;
      for (const usuario of soltos) {
        const { status } = await chamar(admin, 'PATCH', `/usuarios/${usuario.id}/ativacao`, { ativo: false });
        if (status < 400) {
          desativados++;
        }
      }
      resultado.usuarios += desativados;
      // Um usuário que a API não deixa desativar (ex.: o único administrador) não pode travar o laço.
      if (soltos.length < 100 || desativados === 0) {
        break;
      }
    }
  });

  const listagens = await chamar<{ chave: string; ativo: boolean; versao: string }[]>(
    admin,
    'GET',
    '/admin/metadados/grupos',
  );
  for (const grupo of listagens.corpo ?? []) {
    if (grupo.ativo && CHAVE_LISTAGEM_E2E.test(grupo.chave)) {
      await tentar(`listagem ${grupo.chave}`, async () => {
        const { status } = await chamar(admin, 'PATCH', `/admin/metadados/grupos/${grupo.chave}/ativacao`, {
          versao: grupo.versao,
          ativo: false,
        });
        if (status < 400) {
          resultado.listagens++;
        }
      });
    } else if (grupo.ativo) {
      await tentar(`equipes de teste na listagem ${grupo.chave}`, async () => {
        const atual = await chamar<{ equipes: EquipeNaListagem[] }>(
          admin,
          'GET',
          `/admin/metadados/grupos/${grupo.chave}/equipes`,
        );
        const tirar = (atual.corpo?.equipes ?? [])
          .filter((e) => e.habilitada && NOME_EQUIPE_E2E.test(e.nome))
          .map((e) => e.id);
        if (tirar.length > 0) {
          await mudarEquipesDaListagem(admin, grupo.chave, { tirar });
          resultado.habilitacoes += tirar.length;
        }
      });
    }
  }

  if (resultado.equipes + resultado.usuarios + resultado.listagens + resultado.habilitacoes > 0) {
    console.log(
      `[limpeza E2E] desativadas ${resultado.equipes} equipe(s), ${resultado.usuarios} usuário(s) e ` +
        `${resultado.listagens} listagem(ns); ${resultado.habilitacoes} equipe(s) de teste tirada(s) das listagens.`,
    );
  }
  return resultado;
}
