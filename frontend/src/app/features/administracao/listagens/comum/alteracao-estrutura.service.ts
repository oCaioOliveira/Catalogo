import { Injectable, inject, signal } from '@angular/core';
import { MessageService } from 'primeng/api';
import { EstruturaListagem, Impacto, ResultadoAlteracao } from '../../../../core/administracao/estrutura-listagem';
import { mensagemDoErro } from '../../../../core/erros/mensagens-erro';
import { lerProblema } from '../../../../core/erros/problema';
import { Campo } from '../../../../core/metadados/modelo';
import { classificarSimulacao, impactosDoBloqueio, resumoDoImpacto } from '../modelo/simulacao';

/** Uma alteração de estrutura, do jeito que a tela a descreve para o executor. */
export interface Alteracao {
  /** A listagem (para os links dos exemplos e para invalidar os metadados). */
  grupo: string;
  /** O tipo de item cujos itens aparecem nos exemplos do relatório (link para cada item). */
  entidade?: string;
  /** "Alterar o campo Nome": título dos diálogos e do aviso de sucesso. */
  titulo: string;
  /** A chamada à API: a mesma, simulada ou não, com o valor padrão quando a tela o pediu. */
  chamar: (simular: boolean, valorPadrao?: unknown) => Promise<ResultadoAlteracao>;
  /** O campo que pode receber um valor padrão (criar campo obrigatório ou tornar obrigatório, RN60, RN61). */
  campoValorPadrao?: Campo;
}

export type Desfecho =
  | { status: 'gravado'; estrutura: EstruturaListagem | null }
  | { status: 'cancelado' }
  | { status: 'bloqueado' }
  | { status: 'invalido'; erros: Record<string, string[]>; mensagem: string | null }
  | { status: 'conflito' }
  | { status: 'erro' };

/** O que o diálogo mostra agora (DialogoAlteracaoComponent). */
export type EstadoDialogo =
  | { modo: 'confirmar'; titulo: string; impactos: Impacto[]; grupo: string; entidade?: string }
  | { modo: 'bloqueado'; titulo: string; impactos: Impacto[]; grupo: string; entidade?: string }
  | { modo: 'valor-padrao'; titulo: string; impacto: Impacto; campo: Campo; grupo: string }
  | { modo: 'conflito'; titulo: string };

/**
 * TODA alteração de estrutura passa por aqui (ADR 0044): primeiro a SIMULAÇÃO (?simular=true, RN56), e o resultado decide:
 *   - sem impacto: grava direto;
 *   - com impacto (ex.: itens que recebem o valor padrão): pede confirmação, com o resumo;
 *   - campo que vira obrigatório com itens vazios: pede o VALOR PADRÃO (com o componente do tipo do campo) e simula de novo;
 *   - bloqueado: mostra o relatório de impacto (quantidade, exemplos com link, orientação), sem confirmar;
 *   - 409 de versão: avisa que outra pessoa alterou a estrutura (quem chamou recarrega);
 *   - 400: devolve os erros por campo para a tela mostrar junto de cada controle.
 */
@Injectable({ providedIn: 'root' })
export class AlteracaoEstruturaService {
  private readonly mensagens = inject(MessageService);

  readonly dialogo = signal<EstadoDialogo | null>(null);
  private resolver: ((resposta: unknown) => void) | null = null;

  async executar(alteracao: Alteracao): Promise<Desfecho> {
    let valorPadrao: unknown = undefined;
    for (;;) {
      let simulacao: ResultadoAlteracao;
      try {
        simulacao = await alteracao.chamar(true, valorPadrao);
      } catch (erro) {
        return this.tratarErro(erro, alteracao);
      }
      const classificacao = classificarSimulacao(simulacao, !!alteracao.campoValorPadrao && valorPadrao === undefined);
      if (classificacao.tipo === 'pede-valor-padrao') {
        const valor = await this.perguntar({
          modo: 'valor-padrao',
          titulo: alteracao.titulo,
          impacto: classificacao.impacto,
          campo: alteracao.campoValorPadrao!,
          grupo: alteracao.grupo,
        });
        if (valor === undefined) {
          return { status: 'cancelado' };
        }
        valorPadrao = valor;
        continue;
      }
      if (classificacao.tipo === 'bloqueado') {
        await this.perguntar({
          modo: 'bloqueado',
          titulo: alteracao.titulo,
          impactos: classificacao.impactos,
          ...alvo(alteracao),
        });
        return { status: 'bloqueado' };
      }
      if (classificacao.tipo === 'com-impacto') {
        const confirmou = await this.perguntar({
          modo: 'confirmar',
          titulo: alteracao.titulo,
          impactos: classificacao.impactos,
          ...alvo(alteracao),
        });
        if (confirmou !== true) {
          return { status: 'cancelado' };
        }
      }
      break;
    }

    try {
      const resultado = await alteracao.chamar(false, valorPadrao);
      const efeitos = resultado.impactos.filter((i) => !i.bloqueia && i.quantidade > 0).map(resumoDoImpacto);
      this.mensagens.add({
        severity: 'success',
        summary: `${alteracao.titulo}: feito.`,
        detail: efeitos.join(' ') || undefined,
      });
      return { status: 'gravado', estrutura: resultado.estrutura };
    } catch (erro) {
      return this.tratarErro(erro, alteracao);
    }
  }

  /** A resposta do diálogo: true/false (confirmar), o valor padrão (ou undefined = cancelou) e "entendi" nos demais. */
  responder(resposta: unknown): void {
    const resolver = this.resolver;
    this.resolver = null;
    this.dialogo.set(null);
    resolver?.(resposta);
  }

  private perguntar(estado: EstadoDialogo): Promise<unknown> {
    this.resolver?.(undefined);
    this.dialogo.set(estado);
    return new Promise((resolve) => (this.resolver = resolve));
  }

  private async tratarErro(erro: unknown, alteracao: Alteracao): Promise<Desfecho> {
    const problema = lerProblema(erro);
    if (problema.status === 400) {
      const semCampo = Object.keys(problema.erros).length === 0;
      return {
        status: 'invalido',
        erros: problema.erros,
        mensagem: semCampo ? (problema.detalhe ?? mensagemDoErro(problema.codigo, problema.status)) : null,
      };
    }
    if (problema.codigo === 'CONFLITO_CONCORRENCIA') {
      await this.perguntar({ modo: 'conflito', titulo: alteracao.titulo });
      return { status: 'conflito' };
    }
    const impactos = impactosDoBloqueio(problema);
    if (impactos) {
      await this.perguntar({ modo: 'bloqueado', titulo: alteracao.titulo, impactos, ...alvo(alteracao) });
      return { status: 'bloqueado' };
    }
    this.mensagens.add({
      severity: 'error',
      summary: `${alteracao.titulo}: não foi possível.`,
      detail: problema.detalhe ?? mensagemDoErro(problema.codigo, problema.status),
    });
    return { status: 'erro' };
  }
}

function alvo(alteracao: Alteracao): { grupo: string; entidade?: string } {
  return { grupo: alteracao.grupo, entidade: alteracao.entidade };
}
