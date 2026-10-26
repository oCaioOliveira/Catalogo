// Dados de exemplo para DESENVOLVIMENTO, criados PELA API (nunca por SQL direto: assim valem as validações, o
// RegistroValor e as chaves únicas). IDEMPOTENTE: pode rodar várias vezes; o que já existe não é duplicado.
//
//   CATALOGO_ADMIN_SENHA=... CATALOGO_SENHA_DEV=... npm run dados-dev
//
// Variáveis de ambiente (as senhas NUNCA ficam no código):
//   CATALOGO_API             URL da API (padrão http://localhost:5080)
//   CATALOGO_ADMIN_MATRICULA administrador que roda o script (padrão "admin", criado com o comando criar-admin)
//   CATALOGO_ADMIN_SENHA     senha desse administrador (obrigatória)
//   CATALOGO_SENHA_DEV       senha dos usuários de exemplo dev.* (obrigatória; 10+ caracteres, letras e números)
//
// Cria: Equipe Alfa e Equipe Beta; um usuário de cada perfil (dev.externo, dev.interno, dev.gestor,
// dev.administrador) e dev.duas (INTERNO nas duas equipes); comunidades, siglas, ambientes e status (globais);
// projetos, endpoints e RTCs por equipe; 30 relatórios com 1 a 4 cenários e alguns links; e, para o PAINEL GERENCIAL,
// 60 relatórios de histórico espalhados de nov/2025 a out/2026 (5 por mês), com 4 ambientes, os 3 status, os 4 tipos e
// medidas variadas (o percentil 95 cai ao longo dos meses; Produção tem mais vazão). Só ACRESCENTA: nada é alterado nem apagado.

const API = (process.env.CATALOGO_API ?? 'http://localhost:5080').replace(/\/$/, '');
const ADMIN = process.env.CATALOGO_ADMIN_MATRICULA ?? 'admin';
const SENHA_ADMIN = process.env.CATALOGO_ADMIN_SENHA;
const SENHA_DEV = process.env.CATALOGO_SENHA_DEV;
const GRUPO = 'testes_desempenho';

if (!SENHA_ADMIN || !SENHA_DEV) {
  console.error('Defina CATALOGO_ADMIN_SENHA e CATALOGO_SENHA_DEV (veja o cabeçalho deste arquivo).');
  process.exit(1);
}

// ---- HTTP ----

async function chamar(metodo, caminho, { token, corpo, consulta } = {}) {
  const url = new URL(`${API}/api/v1${caminho}`);
  for (const [nome, valor] of Object.entries(consulta ?? {})) {
    url.searchParams.set(nome, String(valor));
  }
  const resposta = await fetch(url, {
    method: metodo,
    headers: { 'Content-Type': 'application/json', ...(token ? { Authorization: `Bearer ${token}` } : {}) },
    body: corpo === undefined ? undefined : JSON.stringify(corpo),
  });
  const texto = await resposta.text();
  const dados = texto ? JSON.parse(texto) : null;
  if (!resposta.ok) {
    const erro = new Error(
      `${metodo} ${caminho}: ${resposta.status} ${dados?.codigo ?? ''} ${JSON.stringify(dados?.errors ?? dados?.detail ?? '')}`,
    );
    erro.status = resposta.status;
    throw erro;
  }
  return dados;
}

async function entrar(matricula, senha) {
  return (await chamar('POST', '/auth/login', { corpo: { matricula, senha } })).token;
}

// ---- Gerador pseudoaleatório com semente fixa: os mesmos valores a cada execução. ----

let semente = 20261021;
function aleatorio() {
  semente = (semente * 1103515245 + 12345) % 2147483648;
  return semente / 2147483648;
}
const entre = (minimo, maximo) => minimo + Math.floor(aleatorio() * (maximo - minimo + 1));
const umDe = (lista) => lista[entre(0, lista.length - 1)];

// ---- Registros idempotentes ----

/** O registro da entidade com `campo = valor` (igualdade, sem diferenciar maiúsculas), ou null. */
async function procurar(token, entidade, campo, valor, equipeId) {
  const consulta = { [campo]: valor, tamanho: 1 };
  if (equipeId) {
    consulta.equipeId = equipeId;
  }
  const pagina = await chamar('GET', `/grupos/${GRUPO}/entidades/${entidade}/registros`, { token, consulta });
  return pagina.itens[0] ?? null;
}

const contagem = { criados: 0, existentes: 0 };

/** Cria o registro se não existir um com o mesmo valor no campo de busca. Devolve { id, novo }. */
async function garantir(token, entidade, campoBusca, dados, equipeId) {
  const existente = await procurar(token, entidade, campoBusca, dados[campoBusca], equipeId);
  if (existente) {
    contagem.existentes++;
    return { id: existente.id, novo: false };
  }
  const corpo = equipeId ? { ...dados, equipeId } : dados;
  const criado = await chamar('POST', `/grupos/${GRUPO}/entidades/${entidade}/registros`, { token, corpo });
  contagem.criados++;
  return { id: criado.id, novo: true };
}

// ---- Equipes e usuários ----

async function garantirEquipe(token, nome) {
  // Busca pelo nome: com muitas equipes (ex.: as desativadas dos testes E2E), ela pode não estar na primeira página.
  const equipes = await chamar('GET', '/equipes', { token, consulta: { busca: nome, tamanho: 100 } });
  const existente = equipes.itens.find((e) => e.nome.toLowerCase() === nome.toLowerCase());
  if (existente) {
    return existente.id;
  }
  return (await chamar('POST', '/equipes', { token, corpo: { nome } })).id;
}

async function garantirUsuario(token, matricula, nome, perfil, equipes) {
  const usuarios = await chamar('GET', '/usuarios', { token, consulta: { busca: matricula, tamanho: 100 } });
  if (usuarios.itens.some((u) => u.matricula.toLowerCase() === matricula)) {
    console.log(`  usuário ${matricula}: já existe (a senha não foi alterada)`);
    return;
  }
  const criado = await chamar('POST', '/usuarios', { token, corpo: { matricula, nome, perfil, equipes } });
  // A senha provisória vale uma vez: troca pela de desenvolvimento (RN25).
  const tokenUsuario = await entrar(matricula, criado.senhaProvisoria);
  await chamar('POST', '/auth/trocar-senha', {
    token: tokenUsuario,
    corpo: { senhaAtual: criado.senhaProvisoria, novaSenha: SENHA_DEV },
  });
  console.log(`  usuário ${matricula} (${perfil}): criado`);
}

// ---- Execução ----

async function principal() {
  console.log(`API: ${API}`);
  const token = await entrar(ADMIN, SENHA_ADMIN);

  console.log('Equipes e usuários');
  const alfa = await garantirEquipe(token, 'Equipe Alfa');
  const beta = await garantirEquipe(token, 'Equipe Beta');
  await garantirUsuario(token, 'dev.externo', 'Érica Externa', 'EXTERNO', [alfa]);
  await garantirUsuario(token, 'dev.interno', 'Ícaro Interno', 'INTERNO', [alfa]);
  await garantirUsuario(token, 'dev.gestor', 'Gabriela Gestora', 'GESTOR', [alfa]);
  await garantirUsuario(token, 'dev.administrador', 'Augusto Administrador', 'ADMINISTRADOR', []);
  await garantirUsuario(token, 'dev.duas', 'Diana das Duas Equipes', 'INTERNO', [alfa, beta]);

  console.log('Cadastros de apoio (globais)');
  const comunidades = [];
  for (const nome of ['Pagamentos', 'Cartões', 'Canais Digitais']) {
    comunidades.push((await garantir(token, 'comunidade', 'nome', { nome })).id);
  }
  const siglas = [];
  for (const [i, codigo] of ['PAG', 'PIX', 'CRT', 'DEB', 'APP', 'WEB'].entries()) {
    siglas.push(
      (
        await garantir(token, 'sigla', 'codigo_sigla', {
          comunidade: comunidades[Math.floor(i / 2)],
          codigo_sigla: codigo,
        })
      ).id,
    );
  }
  const ambientes = [];
  for (const [nome, servidor] of [
    ['Desenvolvimento', 'mq-dev.interno'],
    ['Homologação', 'mq-hml.interno'],
    ['Pré-produção', 'mq-pre.interno'],
  ]) {
    ambientes.push((await garantir(token, 'ambiente', 'nome', { nome, servidor_gerenciador_filas: servidor })).id);
  }
  const status = [];
  for (const descricao of ['Aprovado', 'Aprovado com ressalvas', 'Reprovado']) {
    status.push((await garantir(token, 'status_resultado', 'descricao', { descricao })).id);
  }

  console.log('Projetos, endpoints e RTCs por equipe');
  const porEquipe = {};
  for (const [equipeId, sufixo, indiceSiglas] of [
    [alfa, 'Alfa', [1, 2]],
    [beta, 'Beta', [3, 4]],
  ]) {
    const projetos = [];
    for (const [n, nome] of ['Transferências', 'Consultas'].entries()) {
      const projeto = (
        await garantir(
          token,
          'projeto',
          'nome',
          {
            sigla: siglas[indiceSiglas[n]],
            nome: `${nome} ${sufixo}`,
            url_projeto: `https://git.interno/${nome.toLowerCase()}-${sufixo.toLowerCase()}`,
          },
          equipeId,
        )
      ).id;
      const endpoints = [];
      for (const [servico, metodo] of [
        ['enviar', 'POST'],
        ['consultar', 'GET'],
        ['cancelar', 'DELETE'],
      ]) {
        const nomeServico = `${servico}-${nome.toLowerCase()}-${sufixo.toLowerCase()}`;
        endpoints.push(
          (
            await garantir(
              token,
              'endpoint_servico',
              'nome_servico',
              {
                projeto,
                nome_servico: nomeServico,
                metodo,
                endpoint_url_fila: `/api/${nomeServico}`,
                ativo: servico !== 'cancelar',
              },
              equipeId,
            )
          ).id,
        );
      }
      projetos.push({ id: projeto, endpoints });
    }
    const rtcs = [];
    for (let n = 1; n <= 4; n++) {
      rtcs.push(
        (
          await garantir(
            token,
            'rtc',
            'rtc_externo_id',
            { rtc_externo_id: `RTC-${sufixo.toUpperCase()}-${100 + n}` },
            equipeId,
          )
        ).id,
      );
    }
    porEquipe[equipeId] = { projetos, rtcs, sufixo };
  }

  console.log('Relatórios, cenários e links');
  const tipos = ['Carga', 'Estresse', 'Resistência', 'Pico'];
  const pessoas = ['Ana Souza', 'Bruno Lima', 'Carla Dias', 'Diego Rocha'];
  for (let n = 1; n <= 30; n++) {
    const equipeId = n % 3 === 0 ? beta : alfa;
    const { projetos, rtcs, sufixo } = porEquipe[equipeId];
    const projeto = umDe(projetos);
    // Datas espalhadas nos últimos 6 meses, sempre as mesmas (semente fixa).
    const data = new Date(Date.UTC(2026, 4, 1, 12, 0, 0) + entre(0, 170) * 86400000 + entre(0, 8) * 3600000);
    const bateria = `Bateria ${String(n).padStart(2, '0')} ${sufixo}`;
    const relatorio = await garantir(
      token,
      'relatorio_teste',
      'bateria_testes',
      {
        projeto: projeto.id,
        rtc: umDe(rtcs),
        ambiente: umDe(ambientes),
        status_resultado: umDe(status),
        data_execucao: data.toISOString(),
        bateria_testes: bateria,
        tipo_teste: umDe(tipos),
        executado_por: umDe(pessoas),
        contexto: `Execução ${n} do plano de testes de desempenho.`,
        objetivo: 'Validar a vazão e o tempo de resposta sob carga.',
        conclusao: n % 4 === 0 ? 'Resultado dentro do esperado.' : null,
      },
      equipeId,
    );

    // Cenários e links só junto com um relatório NOVO (se o relatório já existia, eles também).
    if (relatorio.novo) {
      const quantidade = entre(1, 4);
      for (let c = 1; c <= quantidade; c++) {
        await chamar('POST', `/grupos/${GRUPO}/entidades/cenario_teste/registros`, {
          token,
          corpo: {
            equipeId,
            relatorio_teste: relatorio.id,
            endpoint_servico: umDe(projeto.endpoints),
            numero_cenario: c,
            usuarios_virtuais: umDe([50, 100, 200, 500, 1000]),
            tempo_medio_resposta_ms: entre(80, 900),
            percentil_95_ms: entre(200, 2500),
            vazao_media_tps: Math.round(aleatorio() * 30000) / 100,
            taxa_erro_percentual: Math.round(aleatorio() * 500) / 100,
          },
        });
        contagem.criados++;
      }
      for (let l = 1; l <= entre(0, 2); l++) {
        await chamar('POST', `/grupos/${GRUPO}/entidades/link/registros`, {
          token,
          corpo: {
            equipeId,
            relatorio_teste: relatorio.id,
            texto_exibicao: `Painel de monitoração ${l}`,
            url: `https://grafana.interno/d/teste-${n}-${l}`,
          },
        });
        contagem.criados++;
      }
    }
  }

  await historicoDoPainel(token, { alfa, beta, porEquipe, ambientes, status });

  console.log(`Pronto: ${contagem.criados} registros criados, ${contagem.existentes} já existiam.`);
}

// ---- Histórico para o painel gerencial ----

// Gerador próprio, com outra semente: os valores do histórico não dependem de quantos relatórios acima já existiam.
let sementeHistorico = 20251101;
function aleatorioHistorico() {
  sementeHistorico = (sementeHistorico * 1103515245 + 12345) % 2147483648;
  return sementeHistorico / 2147483648;
}
const entreH = (minimo, maximo) => minimo + Math.floor(aleatorioHistorico() * (maximo - minimo + 1));
const umDeH = (lista) => lista[entreH(0, lista.length - 1)];

/**
 * 60 relatórios com data de execução em cada um dos 12 meses de nov/2025 a out/2026 (5 por mês, datas FIXAS: rodar de novo
 * não duplica, porque cada um é procurado pelo nome da bateria antes de ser criado). Equipe Alfa em 2 de cada 3.
 */
async function historicoDoPainel(token, { alfa, beta, porEquipe, ambientes, status }) {
  console.log('Histórico de 12 meses para o painel');
  const producao = (
    await garantir(token, 'ambiente', 'nome', { nome: 'Produção', servidor_gerenciador_filas: 'mq-prd.interno' })
  ).id;
  const todosAmbientes = [...ambientes, producao];
  const tipos = ['Carga', 'Estresse', 'Resistência', 'Pico'];
  const pessoas = ['Ana Souza', 'Bruno Lima', 'Carla Dias', 'Diego Rocha', 'Elisa Prado'];

  for (let mes = 0; mes < 12; mes++) {
    const ano = mes < 2 ? 2025 : 2026;
    const numeroMes = ((10 + mes) % 12) + 1; // nov/2025 ... out/2026
    for (let n = 1; n <= 5; n++) {
      const indice = mes * 5 + n;
      const equipeId = indice % 3 === 0 ? beta : alfa;
      const { projetos, rtcs, sufixo } = porEquipe[equipeId];
      const projeto = umDeH(projetos);
      const ambiente = umDeH(todosAmbientes);
      // Em outubro/2026, só até o dia 20 (antes de hoje).
      const dia = entreH(1, numeroMes === 10 && ano === 2026 ? 20 : 28);
      const data = new Date(Date.UTC(ano, numeroMes - 1, dia, entreH(11, 20), 0, 0));
      const bateria = `Histórico ${ano}-${String(numeroMes).padStart(2, '0')} #${n} ${sufixo}`;
      const relatorio = await garantir(
        token,
        'relatorio_teste',
        'bateria_testes',
        {
          projeto: projeto.id,
          rtc: umDeH(rtcs),
          ambiente,
          status_resultado: umDeH([status[0], status[0], status[1], status[2]]),
          data_execucao: data.toISOString(),
          bateria_testes: bateria,
          tipo_teste: umDeH(tipos),
          executado_por: umDeH(pessoas),
          contexto: `Execução mensal ${indice} do histórico de desempenho.`,
          objetivo: 'Acompanhar a evolução do tempo de resposta e da vazão.',
        },
        equipeId,
      );
      if (!relatorio.novo) {
        continue;
      }
      // O percentil 95 e o tempo médio melhoram ao longo do ano; Produção tem mais vazão.
      const tendencia = 1 - mes * 0.04;
      for (let c = 1; c <= entreH(2, 4); c++) {
        await chamar('POST', `/grupos/${GRUPO}/entidades/cenario_teste/registros`, {
          token,
          corpo: {
            equipeId,
            relatorio_teste: relatorio.id,
            endpoint_servico: umDeH(projeto.endpoints),
            numero_cenario: c,
            usuarios_virtuais: umDeH([50, 100, 200, 500, 1000]),
            tempo_medio_resposta_ms: Math.round(entreH(120, 600) * tendencia),
            percentil_95_ms: Math.round(entreH(600, 2200) * tendencia),
            vazao_media_tps: Math.round((ambiente === producao ? 250 : 120) * (0.6 + aleatorioHistorico()) * 100) / 100,
            taxa_erro_percentual: Math.round(aleatorioHistorico() * 400) / 100,
          },
        });
        contagem.criados++;
      }
    }
  }
}

principal().catch((erro) => {
  console.error(`Falhou: ${erro.message}`);
  process.exit(1);
});
