# Calendário escolar — colaboração com Codex, 2026-11-25

## Âmbito
Implementação local da tarefa 3, sem commit, push, publicação ou acesso a MySQL/contas de produção. O agente principal integra navegação e registo do cliente Web. Não foram alterados UiMessages, MemberWorkspace, InstitutionPanel, PlatformPanel nem Layout por este trabalhador.

## Entregue para integração
- Domain/Calendar: SchoolEvent e transições Pending → Published/Rejected/Cancelled, publicação administrativa direta e edição com versão.
- Application/Calendar: revalidação da conta atual, papéis fixos, consultas paginadas com intervalo máximo de 366 dias; professor escreve apenas propostas próprias pendentes; aluno só consulta publicados.
- Infrastructure/Calendar: configuração EF, consulta por instituição, unidade transacional, capacidade de escrita ligada à referência autorizada e transação, auditoria atómica e controlo de concorrência. DbContext inclui filtro global e bloqueio antecipado de escritas sem capacidade, antes dos restantes módulos.
- Api/Calendar: listagem, criação, edição, aprovação, rejeição e cancelamento. Erros ProblemDetails e recursos próprios. Contratos rejeitam campos desconhecidos para impedir publicação/tenant por pedido.
- Shared Calendar: contratos e CalendarApiClient autenticado.
- Web/Calendar/SchoolCalendar: agenda semanal/mensal, filtros, paginação, formulário e aprovação por papel; recursos próprios, datas em Europe/Lisbon, rejeição de horas locais ambíguas/inexistentes no formulário e limpeza quando a identidade muda.
- Limites: título/local 200 caracteres, detalhes 4000, duração positiva até 31 dias; armazenamento UTC, HTTP DateTimeOffset.

## Evidência executada
1. Testes HTTP escritos antes do módulo: falharam com endpoints inexistentes. Depois da implementação, detetaram campo Status inicialmente ignorado; contratos corrigidos para rejeitar campos desconhecidos.
2. `dotnet test tests/SchoolGether.Api.IntegrationTests --filter FullyQualifiedName~CalendarTests --no-restore -v q`: 4 aprovados, 0 falhas.
3. `dotnet test tests/SchoolGether.ApiClient.Tests --filter FullyQualifiedName~CalendarApiClientTests --no-restore -v q`: 2 aprovados, 0 falhas.
4. `dotnet build src/Clients/SchoolGether.Web --no-restore -v q`: sucesso, sem avisos/erros; executado depois da proteção de identidade/limpeza da agenda.
5. `dotnet build src/Backend/SchoolGether.Api --no-restore -v q`: sucesso, sem avisos/erros; último build depois da melhoria de identificador de auditoria abaixo.
6. Migração EF `20261125092333_SchoolCalendar` gerada com APPDATA isolado e ligação fictícia localhost, sem ligação a uma BD. Up cria apenas calendar_event e índice institucional/data, FK restrict. A versão final inclui ClientId e índice único instituição/ClientId. O snapshot apenas acrescenta a entidade calendar_event. `has-pending-model-changes` confirmou coerência depois da regeneração final.

## Continuidade obrigatória e limites
O identificador ClientId identifica os alvos de auditoria calendar.event:<guid>. A pedido do agente principal, regenerou-se a migração local ainda não aplicada (20261125092333_SchoolCalendar), recompilou-se a API sem avisos/erros e reexecutaram-se CalendarTests: 4 aprovados. O snapshot final está coerente (has-pending-model-changes sem alterações).

Não se verificou a aplicação da migração em MySQL nem o formulário no browser real; não se executaram suites alargadas dos restantes módulos nem MAUI. A interface impede repetição automática em 409 e mantém o rascunho; não há idempotência de criação, recorrência, notificações ou eventos destinados a turmas. Num erro de criação com resposta perdida, rever a agenda antes de repetir. Esta entrega não afirma disponibilidade online.

Próximo passo do agente principal: rever isolamento/guardas e executar preview integrado antes de commit/PR.
