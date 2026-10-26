import { Injectable, inject } from '@angular/core';
import { ConfirmationService, MessageService } from 'primeng/api';
import { AuthService } from '../../../core/auth/auth.service';
import { AdministracaoService } from '../../../core/administracao/administracao.service';
import { EquipeAdmin, UsuarioAdmin } from '../../../core/administracao/modelo-administracao';
import { membrosAtivos, membrosSemOutraEquipeAtiva } from '../modelo/regras-administracao';

/**
 * As ações com confirmação da administração, usadas na listagem e no detalhe. Os erros da API (ex.: 409 ULTIMO_ADMINISTRADOR,
 * 409 USUARIOS_SEM_EQUIPE) viram aviso no interceptor, com a mensagem do `codigo` (core/erros/mensagens-erro.ts).
 */
@Injectable({ providedIn: 'root' })
export class AcoesAdministracaoService {
  private readonly confirmacao = inject(ConfirmationService);
  private readonly mensagens = inject(MessageService);
  private readonly administracao = inject(AdministracaoService);
  private readonly auth = inject(AuthService);

  /** Confirma e redefine: devolve a senha provisória para o diálogo (nulo = cancelou ou deu erro). */
  async redefinirSenha(usuario: UsuarioAdmin): Promise<string | null> {
    const confirmou = await this.confirmar({
      cabecalho: 'Redefinir senha',
      mensagem:
        `Gerar uma nova senha provisória para ${usuario.nome} (${usuario.matricula})? A senha atual deixa de funcionar ` +
        'na hora, e ele terá de trocar a nova no próximo acesso.',
      aceitar: 'Redefinir senha',
    });
    if (!confirmou) {
      return null;
    }
    try {
      return await this.administracao.redefinirSenha(usuario.id);
    } catch {
      return null;
    }
  }

  /**
   * Ativa ou desativa, com confirmação. Desativar o PRÓPRIO usuário pede uma segunda confirmação. Devolve o usuário atualizado
   * (nulo = cancelou ou a API recusou).
   */
  async alterarAtivacao(usuario: UsuarioAdmin, ativo: boolean): Promise<UsuarioAdmin | null> {
    const souEu = this.auth.usuario()?.id === usuario.id;
    const confirmou = await this.confirmar(
      ativo
        ? {
            cabecalho: 'Ativar usuário',
            mensagem: `Ativar ${usuario.nome} (${usuario.matricula})? Ele volta a entrar no sistema com a senha atual.`,
            aceitar: 'Ativar',
          }
        : {
            cabecalho: 'Desativar usuário',
            mensagem:
              `Desativar ${usuario.nome} (${usuario.matricula})? Ele perde o acesso em até 1 minuto, inclusive numa sessão ` +
              'aberta. O usuário e o histórico dele não são apagados, e você pode reativá-lo depois.',
            aceitar: 'Desativar',
            perigo: true,
          },
    );
    if (!confirmou) {
      return null;
    }
    if (!ativo && souEu) {
      const reforcada = await this.confirmar({
        cabecalho: 'Desativar o SEU usuário',
        mensagem:
          'Você está desativando o seu próprio usuário. Você será desconectado e não conseguirá entrar de novo até que outro ' +
          'administrador o reative. Tem certeza?',
        aceitar: 'Sim, desativar o meu usuário',
        perigo: true,
      });
      if (!reforcada) {
        return null;
      }
    }
    try {
      const atualizado = await this.administracao.alterarAtivacaoUsuario(usuario.id, ativo);
      this.mensagens.add({ severity: 'success', summary: ativo ? 'Usuário ativado.' : 'Usuário desativado.' });
      if (souEu && !ativo) {
        this.auth.sair();
      }
      return atualizado;
    } catch {
      return null;
    }
  }

  /**
   * Ativa ou desativa uma equipe. Antes de desativar, conta os membros: quantos perdem o acesso aos itens da equipe e quantos
   * ficariam sem NENHUMA equipe ativa (nesse caso a API recusa, RN31, e a tela já diz isso).
   */
  async alterarAtivacaoEquipe(equipe: EquipeAdmin, ativa: boolean): Promise<EquipeAdmin | null> {
    let opcoes: OpcoesConfirmacao;
    if (ativa) {
      opcoes = {
        cabecalho: 'Ativar equipe',
        mensagem: `Ativar a equipe ${equipe.nome}? Os membros voltam a ver e alterar os itens dela.`,
        aceitar: 'Ativar',
      };
    } else {
      const membros = await this.administracao.membrosDaEquipe(equipe.id);
      const afetados = membrosAtivos(membros).length;
      const semEquipe = membrosSemOutraEquipeAtiva(membros, equipe.id).length;
      opcoes = {
        cabecalho: 'Desativar equipe',
        mensagem: mensagemDesativarEquipe(equipe.nome, afetados, semEquipe),
        aceitar: 'Desativar',
        perigo: true,
        bloqueado: semEquipe > 0,
      };
    }
    if (!(await this.confirmar(opcoes))) {
      return null;
    }
    try {
      const atualizada = await this.administracao.alterarAtivacaoEquipe(equipe.id, ativa);
      this.mensagens.add({ severity: 'success', summary: ativa ? 'Equipe ativada.' : 'Equipe desativada.' });
      return atualizada;
    } catch {
      return null;
    }
  }

  private confirmar(opcoes: OpcoesConfirmacao): Promise<boolean> {
    return new Promise((resolver) =>
      this.confirmacao.confirm({
        header: opcoes.cabecalho,
        message: opcoes.mensagem,
        icon: opcoes.perigo ? 'pi pi-exclamation-triangle' : 'pi pi-question-circle',
        acceptLabel: opcoes.aceitar,
        rejectLabel: opcoes.bloqueado ? 'Fechar' : 'Cancelar',
        acceptVisible: !opcoes.bloqueado,
        acceptButtonProps: { severity: opcoes.perigo ? 'danger' : undefined },
        rejectButtonProps: { severity: 'secondary', text: true },
        closeOnEscape: true,
        accept: () => resolver(true),
        reject: () => resolver(false),
      }),
    );
  }
}

interface OpcoesConfirmacao {
  cabecalho: string;
  mensagem: string;
  aceitar: string;
  perigo?: boolean;
  /** Só informa: sem o botão de confirmar (a API recusaria). */
  bloqueado?: boolean;
}

/** O texto da confirmação de desativar uma equipe (função pura, testada em regras-administracao.spec.ts). */
export function mensagemDesativarEquipe(nome: string, afetados: number, semEquipe: number): string {
  const base =
    `Desativar a equipe ${nome}? Enquanto ela estiver inativa, os membros perdem o acesso aos itens dela ` +
    `(${afetados} usuário(s) ativo(s) afetado(s)). Os itens NÃO são apagados e voltam a aparecer ao reativar.`;
  if (semEquipe === 0) {
    return `${base} Nenhum usuário fica sem equipe ativa.`;
  }
  return (
    `${base}\n\n${semEquipe} usuário(s) ficariam sem NENHUMA equipe ativa e, portanto, sem acesso ao sistema. ` +
    'Por isso a desativação não é permitida: inclua esses usuários em outra equipe antes (veja os membros no detalhe da equipe).'
  );
}
