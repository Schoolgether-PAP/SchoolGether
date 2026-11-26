# Fiabilidade das áreas pedagógicas

Objetivo: corrigir os três problemas confirmados na revisão de 26/11, sem alterar permissões nem publicar uma entrega incompleta.

Base: `feature/learning-materials`, `9ff7277`. Execução autorizada pelo utilizador; trabalho neste ramo, sem sobrepor o ramo do colega.

- [x] Reproduzir o HTTP 429 e reutilizar as sessões autenticadas no teste dos materiais.
- [x] Acrescentar regressões de paginação e de recuperação de criação após desmontar o componente, tanto para materiais como para planificações.
- [x] Conservar o pedido imutável por sessão; limpar ao mudar de conta; avisar antes de sair da aplicação com uma criação pendente.
- [x] Confirmar página/ano apenas depois de carregar com sucesso; conservar os resultados e o contexto anteriores numa falha.
- [x] Disponibilizar o cenário Materials no ensaio MySQL e executá-lo numa base descartável.
- [x] Executar suites e compilações relevantes; revisão independente; atualizar estado, plano anterior e registo de colaboração; commit/push e PR.

Fora deste incremento: ficheiros privados, atividades, entregas, avaliação, MAUI e publicação. Continuam pendentes, não devem ser apresentados como concluídos por estas correções.
