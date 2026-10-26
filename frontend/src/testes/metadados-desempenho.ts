import { Entidade } from '../app/core/metadados/modelo';
import { campo, entidade } from './metadados-de-teste';

/** As entidades de Testes de Desempenho que o painel usa, como vêm de GET .../entidades (mesmas chaves e nomes do seed). */
export function entidadesDesempenho(): Entidade[] {
  const ref = (chave: string, nome: string, apontada: string) =>
    campo(chave, 'REFERENCIA', { nome, entidadeReferenciada: apontada });
  return [
    entidade('ambiente', [campo('nome', 'TEXTO', { nome: 'Nome' })], { nome: 'Ambiente', escopoEquipe: false }),
    entidade('status_resultado', [campo('descricao', 'TEXTO', { nome: 'Descrição' })], {
      nome: 'Status do Resultado',
      escopoEquipe: false,
    }),
    entidade('projeto', [campo('nome', 'TEXTO', { nome: 'Nome' })], { nome: 'Projeto' }),
    entidade(
      'endpoint_servico',
      [ref('projeto', 'Projeto', 'projeto'), campo('nome_servico', 'TEXTO', { nome: 'Nome do serviço' })],
      { nome: 'Endpoint de Serviço', campoExibicao: 'nome_servico' },
    ),
    entidade(
      'relatorio_teste',
      [
        ref('projeto', 'Projeto', 'projeto'),
        ref('ambiente', 'Ambiente', 'ambiente'),
        ref('status_resultado', 'Status', 'status_resultado'),
        campo('data_execucao', 'DATA_HORA', { nome: 'Data de execução' }),
        campo('bateria_testes', 'TEXTO', { nome: 'Bateria de testes' }),
        campo('tipo_teste', 'TEXTO', { nome: 'Tipo de teste' }),
        campo('conclusao', 'TEXTO_LONGO', { nome: 'Conclusão' }),
      ],
      { nome: 'Relatório de Teste', campoExibicao: 'bateria_testes' },
    ),
    entidade(
      'cenario_teste',
      [
        campo('relatorio_teste', 'REFERENCIA', {
          nome: 'Relatório',
          entidadeReferenciada: 'relatorio_teste',
          obrigatorio: true,
          acaoExclusao: 'CASCATA',
        }),
        ref('endpoint_servico', 'Endpoint', 'endpoint_servico'),
        campo('numero_cenario', 'INTEIRO', { nome: 'Número do cenário' }),
        campo('usuarios_virtuais', 'INTEIRO', { nome: 'Usuários virtuais' }),
        campo('tempo_medio_resposta_ms', 'INTEIRO', { nome: 'Tempo médio (ms)' }),
        campo('percentil_95_ms', 'INTEIRO', { nome: 'Percentil 95 (ms)' }),
        campo('vazao_media_tps', 'DECIMAL', { nome: 'Vazão média (TPS)', escala: 2 }),
        campo('taxa_erro_percentual', 'DECIMAL', { nome: 'Taxa de erro (%)', escala: 2 }),
      ],
      { nome: 'Cenário de Teste', campoExibicao: 'numero_cenario' },
    ),
  ];
}

export function entidadeDesempenho(chave: string): Entidade {
  return entidadesDesempenho().find((e) => e.chave === chave)!;
}
