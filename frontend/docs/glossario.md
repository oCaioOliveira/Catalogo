# Glossário: termos da tela × termos do código

> **Cópia.** A fonte é o `docs/glossario.md` do repositório do back-end. Termo novo ou mudado: altere lá primeiro e copie para cá
> no mesmo dia.

**Na interface, sempre os termos da coluna "Na tela".** O código, o banco e a API mantêm os nomes históricos (coluna "No código").
Mensagens, títulos, rótulos e textos de ajuda usam só os da tela.

| Na tela | No código | Definição |
|---|---|---|
| Área de gestão | `Grupo` (rota `/:grupo`, API `/grupos/{grupo}`) | O agrupamento de tipos de item de um assunto (ex.: Testes de Desempenho), habilitado por equipe (RN90). |
| Tipo de item | `Entidade` | Um tipo de cadastro dentro da área (ex.: Relatório de Teste, Projeto), com os seus campos. |
| Item | `Registro` | Um cadastro concreto de um tipo de item. |
| Campo | `Campo` | Uma informação de um tipo de item, com tipo de dado (texto, data, lista, referência...). |
| Cadastro de apoio | entidade global (`escopoEquipe = false`) | Tipo de item compartilhado por todas as equipes (ex.: Ambiente, Status), no grupo "Cadastros de apoio" do menu (RN27). |
| Item filho | entidade filha (campo `REFERENCIA` obrigatório com `CASCATA`; `filhasDe`) | Item que só existe dentro de outro e é excluído junto com ele; aparece no detalhe do pai. |
| Item relacionado | relacionados (`ListarRelacionados`, RN94) | Item de outro tipo que aponta para este por um campo de referência; listado no detalhe. |
| Equipe | `Equipe` | Grupo de pessoas dono dos itens; cada pessoa vê os itens das suas equipes. |
| Perfil | `Perfil` (`EXTERNO`, `INTERNO`, `GESTOR`, `ADMINISTRADOR`) | O que a pessoa pode fazer no sistema (RN26). |
