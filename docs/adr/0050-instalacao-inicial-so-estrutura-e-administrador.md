# 0050. Instalação inicial só com a estrutura e o administrador inicial, criado por script com o hash da senha

- Status: Aceito
- Data: 2026-10-26
- Substitui em parte: [0049](0049-schema-catalogo-e-pacotes-de-entrega.md) (o conteúdo dos pacotes e a criação do primeiro
  administrador). O schema `catalogo` e a entrega por pacotes continuam valendo.

## Contexto

O pacote de instalação inicial do 0049 levava, além da estrutura, a carga da área Testes de Desempenho, uma verificação e um rollback,
e deixava o primeiro administrador para o comando `criar-admin`, no servidor da aplicação, depois do deploy. O responsável pelo
sistema pediu uma entrega mais simples: só a estrutura e o próprio usuário administrador, para que ele configure o resto pela interface
logo depois da instalação, sem depender de rodar um comando no servidor.

## Decisão

- O pacote `2026-10-26-instalacao-inicial` tem só `01` (schema e papel), `02` (estrutura consolidada) e `03_administrador_inicial.sql`,
  e o documento para os DBAs se chama `README.md` (nome padrão das próximas entregas).
- O `03` é **gerado pela aplicação** (`criar-admin --gerar-sql`, caso de uso `GerarSqlAdministradorInicial`), com o mesmo
  `IHasherSenha` (PBKDF2): o script tem **só o hash** da senha provisória, o usuário nasce ADMINISTRADOR, ativo, sem equipe e com
  `DeveTrocarSenha = 1`, com a linha de criação no histórico, e o script para se a matrícula já existir.
- A senha provisória pedida tem menos caracteres do que a política exige. Ela é aceita **só** nesse modo e só com a opção explícita
  `--aceitar-senha-fora-da-politica`: a troca no primeiro acesso exige uma senha dentro da política, e a política não muda para
  nenhum outro caso (cadastro, troca, `criar-admin` no banco).
- A carga de áreas de gestão fica para quando for preciso, pelo gerador (`database/ferramentas/gerar_carga_area.sh`), que continua no
  projeto. Verificação e rollback deixam de ser obrigatórios nos pacotes.

## Consequências

- Um hash de senha fica num arquivo versionado. O risco é aceito porque a senha é provisória, de troca obrigatória antes de qualquer
  outra ação, e o hash é PBKDF2 com sal; o administrador deve fazer o primeiro acesso logo depois da instalação.
- O banco de produção nasce sem áreas de gestão: o administrador cria a área Testes de Desempenho (ou ela vem num pacote futuro, gerado).
- Sem verificação nem rollback no pacote, a conferência da instalação fica com as mensagens dos próprios scripts.
