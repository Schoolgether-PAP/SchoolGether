# Painel administrativo — plano de implementação

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Entregar a base administrativa A aprovada: master/permissões, instituições, auditoria e dashboard individual persistente com dados reais.

**Architecture:** Manter o monólito modular existente. Identity gere privilégios globais; Institutions gere estado institucional; Audit recebe eventos por interface e participa na transação da alteração. Contracts transporta DTOs e ApiClient reutiliza a sessão existente; Blazor apresenta apenas áreas implementadas e autorizadas.

**Tech Stack:** .NET 10, EF Core 10 Oracle/MySQL 8, ASP.NET Core controllers, Blazor WASM, xUnit; sem dependências adicionais.

**Spec:** `docs/superpowers/specs/2026-10-12-platform-admin-design.md`, incluindo revisão v3 aprovada na conversa.

## Global Constraints

- .NET 10; Clean Architecture, módulos e dependências definidos no AGENTS.md.
- Uma instituição por conta; `PlatformAdmin` global, sem instituição.
- API `/api/v1`, paginação, DTOs, `ProblemDetails`, validação e `CancellationToken`.
- Interface/documentação em português; textos em recursos; código/tabelas em inglês, colunas snake_case.
- Todos os administradores podem atuar em qualquer instituição; só as capacidades variam.
- Só o master gere administradores; não pode bloquear a própria conta ou transferir propriedade nesta entrega.
- Nunca expor/gravar em auditoria palavras-passe, tokens, chaves ou cadeias de ligação.
- Persistência de layout por conta; alterações de privilégios prevalecem sobre preferências guardadas.
- Suporte, conteúdos, finanças, avaliações e operação técnica avançada não são implementados nem simulados no produto A.
- Sem publicação, push/merge ou alteração automática das contas/bases existentes. Migração e promoção do master ficam como ações explícitas do operador.
- A app conserva login/perfil; painel administrativo web nesta entrega, conforme especificação aprovada.

## Review Focus

- JWT ainda válido após revogação de capacidade/desativação: impedir operação no pedido seguinte (tarefa 1).
- Suspender/reabrir uma instituição com sessões existentes: recusar pedidos durante suspensão e preservar sanções individuais (tarefa 2).
- Falha de auditoria após alteração: rollback, sem sucesso sem histórico (tarefas 1 e 2).
- Preferência desatualizada/invalidada ou de outra conta: conflito explicado, sem acesso cruzado nem dados de blocos sem autorização (tarefa 2).
- Falha/latência/logout durante operações web: não aplicar respostas antigas, não perder rascunhos recuperáveis e não confundir 403/409 com sessão expirada (tarefas 3 e 4).

## Interfaces transversais

Criar `src/Shared/SchoolGether.Contracts/Platform/PlatformContracts.cs` em 1/2.
Tipos partilhados definidos aqui são a fonte de nomes para todos os implementadores:

- `PlatformPermissions`: constantes string `InstitutionsRead`, `InstitutionsWrite`, `InstitutionsSuspend`, `AuditRead`; lista `All` com esses valores. Gestão de administradores é só master, nunca capacidade delegável.
- `PagedResponse<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize)`.
- `PlatformContextResponse(int AccountId, bool IsMaster, string[] Permissions, long RowVersion)`.
- `PlatformAdminResponse(int Id, string Email, string Name, bool IsMaster, bool IsActive, string[] Permissions, long RowVersion)`.
- `CreatePlatformAdminRequest`: Email, Password, Name, Permissions. Validar limites da Identity existente; permissões vazias por omissão.
- `UpdatePlatformAdminRequest`: Name, IsActive, Permissions, RowVersion, Reason. Não aceitar alteração de master.
- `AuditResponse`: Id, CreatedAt UTC, ActorId, ActorName, InstitutionId nullable, Action, Target, Result, Reason, Before/After sanitizados nullable.
- `PlatformInstitutionResponse`: Id, Name, IsSuspended, SuspensionReason nullable, CreatedAt UTC, AccountsCount, RowVersion.
- `PlatformInstitutionDetailResponse`: Institution (`PlatformInstitutionResponse`), Administrators (`IReadOnlyList<AccountResponse>`).
- `CreatePlatformInstitutionRequest`: Name, Reason.
- `UpdatePlatformInstitutionRequest`: Name, RowVersion, Reason.
- `SetInstitutionSuspensionRequest`: IsSuspended, RowVersion, Reason.
- `CreateInstitutionAdminRequest`: Email, Password, Name, Reason (instituição vem da rota).
- `InstitutionTrendPoint(string Label, int Count)`; `PlatformDashboardResponse`: InstitutionsTotal/ActiveInstitutions/SuspendedInstitutions/AccountsTotal nullable int, InstitutionsTrend (`IReadOnlyList<InstitutionTrendPoint>`), RecentActions (`IReadOnlyList<AuditResponse>`). Omissão por falta de permissão, não zero inventado.
- `DashboardWidget(string Id, string Size, bool Visible)`; `DashboardPreferenceResponse(int SchemaVersion, long RowVersion, IReadOnlyList<DashboardWidget> Widgets)` e `SaveDashboardPreferenceRequest` com os mesmos campos.

DTOs request podem ser classes com atributos em vez de records, mantendo os nomes/propriedades. Respostas records. Não referenciar Contracts em Application: modelos de aplicação equivalentes, mapeamento nos controllers. Catálogo inicial de blocos: `institutions-trend`, `institutions-status`, `recent-actions`; larguras `small`, `wide`, `full`; SchemaVersion=1, RowVersion=0 significa ainda sem preferências gravadas. Ordem da lista define ordem visual. Máximo 3, sem duplicados/IDs desconhecidos. Defaults: trend wide, status small, recent-actions full; todos visíveis quando autorizados.

## Task 1: Privilégios globais, master e auditoria transacional

**Files:**
- Create: `Domain/Audit/AuditEntry.cs`, `Application/Audit/IAuditWriter.cs` e modelos, sob `src/Backend/SchoolGether.*`.
- Create: `Application/Platform/PlatformAuthorization.cs`, `IPlatformAdminDirectory.cs`, `PlatformAdminService.cs`, modelos; `Infrastructure/Platform/PlatformAdminDirectory.cs`, `AuditWriter.cs`, configuração/DI.
- Create: `Api/Platform/PlatformAdminsController.cs`, `PlatformExceptionHandler.cs`, `MasterSetupConsole.cs`.
- Create: `Contracts/Platform/PlatformContracts.cs` com os tipos de 1.
- Modify: Identity ApplicationUser/model/store/DI; DbContext; Program e recursos API; autorização de rotas antigas; IdentityFactory apenas para seed de master nos testes.
- Test: `tests/SchoolGether.Api.IntegrationTests/PlatformAdminTests.cs`, `AuditTests.cs`, `MasterSetupTests.cs`.

**Interfaces:**
- Produces: Application `PlatformAuthorization.RequireAsync(string permission, CancellationToken)` e `RequireMasterAsync(CancellationToken)`; consulta contexto validado no estado atual. `IAuditWriter.Append(AuditEvent)` participa no SaveChanges/transação do chamador; operação assíncrona separada para recusas/leituras sensíveis. Nomes concretos auxiliares ficam com o construtor e serão reportados.
- Produces HTTP `/api/v1/platform/context` GET, `/admins` GET/POST, `/admins/{id}` PUT, `/audit` GET, com os DTOs acima.
- Produces local `--promote-master`: Development apenas; pede ID e confirmação do mesmo ID, promove uma conta global existente ativa, recusa segundo master. Sem promoção implícita na migração/bootstrap.
- Consumes: IdentityService, ICurrentAccount, UserManager e DbContext existentes. Audit não lê tabelas Identity; actor é capturado pelo chamador/autoridade.

- [ ] Escrever testes HTTP: conta escolar 403; global sem capacidades não lista/edita clientes; master cria global com capacidades selecionadas e só master altera colegas; não pode desativar/editar o próprio master; capacidades desconhecidas 400; conta duplicada 409; versão antiga 409.
- [ ] Escrever testes de revogação imediata/desativação com o mesmo JWT; impedir contorno pelas rotas antigas de criação de instituições/contas. Seed de testes é master explícito; contas pessoais não são alteradas.
- [ ] Escrever teste de auditoria com dados antes/depois sem Password/SecurityStamp/token e rollback por falha injetada ao gravar auditoria. Histórico append-only; alterações/deleções via DbContext recusadas.
- [ ] Escrever testes do assistente: ID errado/conta escolar/segundo master recusados; promoção de global escolhido e auditoria; produção não executa assistente.
- [ ] Executar testes novos antes de implementar; registar falha esperada, implementar o mínimo e voltar a executar os testes novos e os testes Identity existentes.
- [ ] Adicionar propriedades protegidas master/ativo e capacidades com normalização/validação. Verificar estado atual em cada pedido autenticado relevante; não depender de capabilities no JWT. As rotas antigas usam a mesma autoridade, evitando escalada de privilégios.
- [ ] Adicionar auditoria global append-only, guardas de escrita e transações incluindo UserManager quando este grava antes de SaveChanges. Registar recusa e consulta/exportação sensível sanitizada sem recursão infinita da própria consulta do histórico.
- [ ] Acrescentar erros localizados 400/403/404/409 e Security Bearer OpenAPI para novas rotas. Gerar migração EF apenas da tarefa 1, sem aplicar na base pessoal.
- [ ] Verificar `dotnet test tests/SchoolGether.Api.IntegrationTests` e `dotnet test tests/SchoolGether.Domain.Tests`; documentar skip MySQL normal. Commit em português com os ficheiros desta tarefa.

## Task 2: Instituições globais, dashboard real e preferências individuais

**Files:**
- Modify: Domain Institutions/Institution, configuração, IInstitutionDirectory/InstitutionDirectory.
- Create: `Application/Institutions/PlatformInstitutionService.cs`, interface global correspondente, modelos; `Infrastructure/Institutions/PlatformInstitutionDirectory.cs`.
- Create: `Application/Platform/DashboardService.cs`, `IDashboardPreferenceStore.cs`, modelos; Infrastructure Platform preferências/model/configuração; Identity interface de leitura global de contagens/responsáveis, implementada dentro de Identity.
- Create: `Api/Platform/PlatformInstitutionsController.cs`, `PlatformDashboardController.cs`; compléter PlatformContracts; migration EF.
- Test: `PlatformInstitutionTests.cs`, `DashboardPreferenceTests.cs`, `PlatformDashboardTests.cs` em Api.IntegrationTests.

**Interfaces:**
- Consumes authority/audit de 1; DTOs transversais da secção Interfaces.
- HTTP `/api/v1/platform/institutions` GET(page/pageSize/search/isSuspended)/POST; `/{id}` GET/PUT; `/{id}/suspension` PUT; `/{id}/administrators` POST.
- HTTP `/api/v1/platform/dashboard` GET, `/dashboard/preferences` GET/PUT. Todas as operações exigem conta global ativa, capacidades conforme bloco/ação; preferências só da conta atual, sem accountId de cliente.
- `IInstitutionDirectory.ExistsAsync` passa a recusar suspensas, mantendo DeletedAt distinto. Suspensão deve invalidar login/refresh e cada pedido escolar autenticado com JWT anterior; reativação não altera lockout/deleted da conta.

- [ ] Escrever testes: administrador com read consulta instituições A/B; escolar não usa rotas globais; write não implica suspend; criar/renomear/suspender com motivo e auditoria, versão antiga 409, nomes/motivos vazios/longos 400; paginação inválida 400; pesquisa Unicode e filtros coerentes.
- [ ] Escrever testes de responsável: criação separada na instituição da rota, InstitutionAdmin, recusar instituição inexistente/suspensa e email duplicado; operação auditada e transacional. Leitura cruza módulos através de interfaces Identity, não leitura direta de Users/Members no módulo Institutions.
- [ ] Escrever testes de sessão antiga após suspensão, login/refresh recusados, reativação mantém lockout individual; dados/guardas escolares entre A/B preservados. Testar rollback de auditoria numa alteração institucional.
- [ ] Escrever testes de preferências: dois admins com layouts distintos, reload persiste, conflito rowversion 409 (incluindo duas criações concorrentes com expected=0), versão esquema/IDs/larguras/duplicados/limites inválidos 400; sem acesso cruzado. Permissão revogada omite dados mesmo com widget guardado.
- [ ] Escrever testes dashboard com datas sem dados/meses parciais: contagens reais, série de instituições criadas nos seis meses UTC, mês atual identificado como parcial; sem histórico de acessos/finanças/suporte inventado. Dados indisponíveis por permissão ficam null/lista vazia.
- [ ] Executar testes novos a falhar; implementar regras nas camadas corretas. Institutions e Identity expõem interfaces limitadas de leitura/global, sem desativar filtros escolares nem abrir escrita arbitrária.
- [ ] Implementar preferências validadas e concorrência, métricas agregadas autorizadas e consulta de recentes autorizada. Mutation+audit partilham transação; consultas sensíveis/refusadas têm auditoria conforme 1.
- [ ] Gerar migração tarefa 2, verificar modelo e execução de migrações em MySQL temporário quando o script existente o permitir sem credenciais pessoais. Não aplicar à base do utilizador automaticamente.
- [ ] Executar suites API/domínio, commit em português; reportar interfaces exatas para cliente e UI.

## Task 3: Cliente autorizado sem exposição de tokens

**Files:**
- Modify: `src/Shared/SchoolGether.ApiClient/Identity/IdentitySession.cs`.
- Create: `src/Shared/SchoolGether.ApiClient/Platform/PlatformApiClient.cs`.
- Test: `tests/SchoolGether.ApiClient.Tests/PlatformApiClientTests.cs`, `IdentitySessionTests.cs`.

**Interfaces:**
- Produces `IdentitySession.ExecuteAuthorizedAsync<T>(Func<string, CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default)`: obtém tokens, renova uma vez em 401, rejeita resposta de geração antiga após logout; 403/409 preservam sessão.
- `PlatformApiClient(HttpClient http, IdentitySession session)` expõe métodos async para todas as rotas 1/2, com DTOs de PlatformContracts, `CancellationToken` opcional. Não devolver token ao componente nem alterar DefaultRequestHeaders partilhados.
- Reutilizar `IdentityApiException`/ProblemDetails para StatusCode/TraceId, com leitura robusta de resposta vazia/malformada sem expor conteúdo sensível.

- [ ] Escrever testes com HTTP controlado para refresh+retry401, 403 sem refresh/clear, 409 preserva sessão, URI/paginação/payload corretos, cancelamento e resposta atrasada após logout não atualiza sessão nem retorna sucesso ao chamador.
- [ ] Executar os novos testes a falhar, implementar cliente e operação autorizada mantendo invariantes/locks existentes; token por HttpRequestMessage.
- [ ] Executar `dotnet test tests/SchoolGether.ApiClient.Tests`, todas as regressões de sessão, commit em português e reportar assinaturas.

## Task 4: Painel Blazor funcional e documentação de operação

**Files:**
- Create: `src/Clients/SchoolGether.Web/Platform/PlatformPanel.razor` + code-behind/CSS focados; componentes Dashboard, Institutions, Administrators, Audit, PreferencesEditor e Profile se necessário.
- Modify: Home, registo DI/MemorySessionRegistration, recursos UI, estilos/layout existentes apenas quando necessários.
- Create: `docs/platform-admin.md`, atualizar docs/client-login.md, docs/revisao-etapas.md e AGENTS.md.
- Verification: script Playwright em `.superpowers/verification/platform-admin/` com HTTP controlado sem contas pessoais.

**Interfaces:**
- Consumes PlatformApiClient/IdentitySession e DTOs de 1–3. Home apresenta PlatformPanel para PlatformAdmin; outros papéis mantêm perfil/login. Perfil e saída continuam acessíveis no shell administrativo.
- Dados reais de A: instituições, administradores, audit, indicadores/série institucional e layout. Navegação não inclui módulos futuros desativados a fingir funcionamento.
- Prototype v3 é referência visual apenas: manter logo/paleta; reutilizar hierarquia, barras/linhas legíveis, grelha 12 colunas, menu móvel/detalhe lateral. Sem importar JS sobreposto do protótipo para produto.

- [ ] Construir fixtures HTTP do navegador e cenários que falham na UI atual: login master abre dashboard; global limitado só vê ações autorizadas; gerir instituição/responsável/master/permissões e histórico; layout por conta guarda/cancela/reordena/redimensiona; 403/409 explicados.
- [ ] Implementar componentes pequenos, mensagens em resx, carregamento/erro/repetir/estado vazio, botões ocupados, formulário mantém rascunho em falha. Razão obrigatória em alterações administrativas; confirmação explícita em suspensão/desativação; não pedir confirmação para leituras/edição reversível.
- [ ] Editor de dashboard por rascunho, modelos compatíveis com catálogo A, Guardar/Cancelar/Escape/reposição; conflito mantém rascunho e oferece recarregar sem gravar silenciosamente. Não renderizar blocos sem permissão. Sem zero fictício ou falha apresentada como vazio.
- [ ] Listas paginadas, pesquisa/filtro, colunas opcionais/densidade; detalhe lateral conserva lista/filtros e mostra responsáveis/histórico. Modal/gaveta com foco, Escape, retorno de foco, navegação por teclado; formulários sem overflow a 390 px. Sessão nova/saída limpa estados/preferências de memória da conta anterior.
- [ ] Gráficos de instituições criadas com unidades, rótulos, período efetivo/parcial e tabela; sem comparação falsa, gráficos financeiros ou atividade fictícia. Preferências carregadas do servidor após entrar.
- [ ] Executar build Web Release e suites existentes; verificar navegador desktop/390 px, teclado, retry, cancelamento, 401/403/409, criação e mudança de estado, recursos/logo. Verificar builds MAUI Debug Windows/Android porque ApiClient é partilhado, sem alegar teste nativo.
- [ ] Documentar migração e promoção explícita do master, capacidades e limitações A, sem credenciais. Atualizar estado factual AGENTS/docs; commit em português.

## Fecho

- [ ] Revisão independente de cada tarefa (especificação e qualidade), correções e verificação focada antes da tarefa seguinte.
- [ ] Revisão ampla do conjunto desde o commit base `47af731`, incluindo segurança, transações, isolamento, testes significativos e UI real.
- [ ] Corrigir resultados importantes; executar verificação final apropriada e documentar o que não foi executado.
- [ ] Entrega local com instruções concretas para migração/promoção e teste. Push/PR só quando autorizado.

## Autorrevisão do plano

Cobertura A mapeada para 1–4; B/C/D continuam fora. DTOs fixos em Interfaces,
Application sem dependência de Contracts; 1 fornece autoridade/auditoria, 2
consome-as, 3 fornece sessão/cliente a 4. Cinco condições de Review Focus têm
testes nas tarefas indicadas. Decisão de execução do agente: subagentes sequenciais
com revisão por tarefa, adequada às fronteiras dos módulos; o utilizador aprovou
prosseguir com a implementação, não é necessária nova autorização de trabalho.
