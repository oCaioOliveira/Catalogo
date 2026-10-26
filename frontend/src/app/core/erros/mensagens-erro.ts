import { SEM_CONEXAO } from './problema';

/**
 * Mensagem amigável para cada `codigo` de erro da API. ÚNICO lugar destes textos, alinhado com
 * docs/api/codigos-de-erro.md: código novo no back-end → uma linha aqui. Decida sempre pelo código,
 * nunca pelo texto que a API manda em `title`/`detail`.
 */
export const MENSAGENS_ERRO: Record<string, string> = {
  VALIDACAO: 'Há campos com problemas. Confira as mensagens.',
  EXPORTACAO_MUITO_GRANDE: 'A exportação passaria do limite de linhas. Use mais filtros.',
  REQUISICAO_INVALIDA: 'A requisição foi recusada pelo servidor.',
  NAO_AUTENTICADO: 'Sua sessão expirou. Entre de novo.',
  CREDENCIAIS_INVALIDAS: 'Matrícula ou senha inválidas.',
  SEM_PERMISSAO: 'Você não tem permissão para esta operação.',
  TROCA_DE_SENHA_OBRIGATORIA: 'Troque a sua senha para continuar.',
  GRUPO_NAO_ENCONTRADO: 'Área de gestão não encontrada.',
  ENTIDADE_NAO_ENCONTRADA: 'Tipo de item não encontrado.',
  CAMPO_NAO_ENCONTRADO: 'Campo não encontrado.',
  REGISTRO_NAO_ENCONTRADO: 'O item não está disponível: ele pode ter sido excluído.',
  EQUIPE_NAO_ENCONTRADA: 'Equipe não encontrada.',
  USUARIO_NAO_ENCONTRADO: 'Usuário não encontrado.',
  ROTA_NAO_ENCONTRADA: 'Endereço da API não encontrado.',
  METODO_NAO_PERMITIDO: 'Operação não permitida.',
  TIPO_DE_CONTEUDO_NAO_SUPORTADO: 'Formato de dados não suportado.',
  CONFLITO_UNICIDADE: 'Já existe um item com esses valores.',
  CONFLITO_CONCORRENCIA: 'Outra pessoa alterou este item depois que você o abriu.',
  EXCLUSAO_BLOQUEADA: 'O item não pode ser excluído porque outros itens dependem dele.',
  ENTIDADE_INATIVA: 'Este tipo de item está desativado: os registros são somente leitura.',
  ULTIMO_ADMINISTRADOR:
    'Não é possível desativar nem rebaixar o último administrador ativo. Promova outro usuário a administrador antes.',
  USUARIOS_SEM_EQUIPE:
    'Alguns usuários ficariam sem nenhuma equipe ativa. Inclua-os em outra equipe antes de desativar esta.',
  IMPORTACAO_NAO_ENCONTRADA: 'Importação não encontrada.',
  IMPORTACAO_SITUACAO_INVALIDA: 'Esta operação não vale mais para esta importação.',
  IMPORTACAO_DADOS_MUDARAM: 'Os dados mudaram desde a prévia. Nada foi importado: confira a prévia atualizada.',
  IMPORTACAO_NAO_PODE_SER_DESFEITA:
    'A importação não pode ser desfeita: itens criados por ela foram alterados ou são usados por outros itens.',
  IMPORTACAO_ARQUIVO_EXPIRADO: 'O arquivo desta importação já foi apagado (fica guardado por 7 dias). Envie de novo.',
  MUITAS_TENTATIVAS: 'Muitas tentativas de login. Aguarde um minuto e tente de novo.',
  ERRO_INTERNO: 'Erro inesperado no servidor.',
  [SEM_CONEXAO]: 'Sem conexão com a API. Verifique se ela está no ar e tente de novo.',
};

/** Texto para um código; código desconhecido: uma mensagem pelo status HTTP. */
export function mensagemDoErro(codigo: string, status: number): string {
  if (MENSAGENS_ERRO[codigo]) {
    return MENSAGENS_ERRO[codigo];
  }
  if (status >= 500) {
    return MENSAGENS_ERRO['ERRO_INTERNO'];
  }
  if (status === 404) {
    return 'Não encontrado.';
  }
  if (status === 403) {
    return MENSAGENS_ERRO['SEM_PERMISSAO'];
  }
  return 'Não foi possível concluir a operação.';
}
