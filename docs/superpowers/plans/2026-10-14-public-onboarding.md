# Página pública e adesão institucional — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Visitar o site público, escolher um plano fictício, criar instituição/responsável e configurar a comunidade, com quotas verificadas no servidor.

**Architecture:** Manter o monólito modular e a API partilhada. Application coordena Identity, Institutions, Billing e Audit por interfaces numa transação. O site usa os DTOs/ApiClient e a sessão em memória existentes; a navegação usa parâmetros na raiz para conservar o alojamento estático.

**Tech Stack:** .NET 10, ASP.NET Core controllers, Oracle MySql.EntityFrameworkCore 10.x, Blazor WebAssembly, xUnit e Playwright já disponíveis. Sem dependências novas.

**Spec:** `docs/superpowers/specs/2026-10-13-public-onboarding-design.md`.

**Estado:** Utilizador aprovou a proposta e o plano em 2026-10-14 com «Está aprovado». Execução escolhida pelo agente: implementação e revisão por tarefa, conforme opção recomendada apresentada. Aprovação permite implementação local; publicação continua dependente dos testes/CI e revisão do colega. Não confundir o plano aprovado com implementação concluída.

## Global Constraints

- Preços mensais em EUR: Essencial 59; Evolução 149; Campus 299. Limites escolhidos: 100, 350 e 1150 contas.
- Catálogo no servidor com IDs `essential`, `evolution`, `campus` e versão inicial `2026-10-14-v1`.
- Conta-se cada conta institucional não eliminada, incluindo contas inativas; o primeiro responsável ocupa uma vaga. PlatformAdmin não ocupa vaga.
- Pagamentos fictícios; sem cartão, cobrança, fatura ou contrato real. Identificar funcionalidades pendentes.
- Uma instituição por conta. Registo público cria apenas uma nova instituição e o primeiro InstitutionAdmin.
- Rotas `/api/v1`, DTOs em Contracts, erros ProblemDetails e recursos PT; controllers sem regras de negócio.
- Backend por módulo; não ler tabelas de outro módulo diretamente. Clientes dependem apenas de Contracts/ApiClient.
- Subscrição e perfil têm institution_id, filtros/guardas e colunas comuns. Filtros normais não são desligados pelo registo público.
- `Onboarding:Enabled` falso por omissão. Limites propostos: 3 registos/minuto/IP e 20/minuto por processo, sem fila; corpo até 16 KiB.
- Instâncias de teste próprias; não usar a base Aiven ou credenciais pessoais. Migrações por EF; publicação só após revisão/CI.
- App conserva login/perfil nesta entrega; novos percursos primeiro no site, conforme escolha do utilizador.

## Review Focus

1. API temporariamente indisponível: a apresentação/login continuam acessíveis e não aparecem preços antigos como se fossem atuais — tarefa 5.
2. E-mail com diferenças de maiúsculas/espaços ou concorrência: uma só conta e instituição, sem conversão de conta existente — tarefa 2.
3. Conta trocada/saída durante um pedido: resposta tardia não restaura sessão, dados ou rascunhos da conta anterior — tarefas 5/6.
4. Instituição antiga sem perfil/plano: continua utilizável, sem plano ou bloqueio arbitrário; configuração tem controlo de concorrência — tarefa 4.
5. Navegação direta, recarregamento e histórico sob `/SchoolGether/`: entrada/adesão funcionam sem depender de novas rotas físicas — tarefa 5.

## Contratos partilhados fixados pelo plano

Cada tarefa cria os modelos de Application que consome, sem depender de Contracts no backend Application. DTOs da API mapeiam esses modelos.

- `PlanResponse(string Id, string Name, decimal MonthlyPrice, string Currency, int AccountLimit)` e `PlanCatalogResponse(string Version, bool RegistrationEnabled, IReadOnlyList<PlanResponse> Plans)` em Contracts/Billing.
- `RegisterInstitutionRequest(string PlanId, string CatalogVersion, string OwnerName, string Email, string Password, string InstitutionName, string InstitutionType, string? Locality, bool AcceptDemonstration)` e `RegistrationResponse(int InstitutionId, int OwnerId, string Email)` em Contracts/Onboarding. Sem ID de instituição existente, papel ou preço no pedido.
- Tipos institucionais estáveis: `School`, `Faculty`, `Polytechnic`, `TrainingCenter`, `Other`; labels PT só na interface.
- `InstitutionSetupResponse(int InstitutionId, string Name, string InstitutionType, string? Locality, string? AcademicYear, bool IsComplete, long RowVersion, string SuggestedAcademicYear)` e `SaveInstitutionSetupRequest(string Name, string InstitutionType, string? Locality, string AcademicYear, long RowVersion)` em Contracts/Institutions.
- `InstitutionSubscriptionResponse(string Status, PlanResponse? Plan, string? CatalogVersion, int UsedAccounts)` em Contracts/Billing. Status `Demonstration` ou `None`.

### Task 1: Catálogo público e regras do domínio

**Files:** Criar `src/Backend/SchoolGether.Domain/Billing/SubscriptionPlan.cs`, `src/Backend/SchoolGether.Application/Billing/PlanCatalog.cs`, `src/Shared/SchoolGether.Contracts/Billing/BillingContracts.cs`, `src/Backend/SchoolGether.Api/Billing/PlansController.cs`; modificar `src/Backend/SchoolGether.Api/Program.cs` para registo do módulo; testes `tests/SchoolGether.Domain.Tests/Billing/SubscriptionPlanTests.cs` e `tests/SchoolGether.Api.IntegrationTests/PlanCatalogTests.cs`.

**Interfaces:** Produz `PlanCatalog.GetPlans(): IReadOnlyList<SubscriptionPlan>` e `PlanCatalog.RequirePlan(string planId, string version): SubscriptionPlan`. Modelo do plano tem ID, nome, preço decimal, moeda e limite; catálogo imutável, sem preço enviado pelo cliente.

- [ ] Escrever testes `Catalog_has_confirmed_prices_and_capacities` (59/149/299; 100/350/1150; EUR), `Unknown_plan_is_rejected`, `Outdated_catalog_requires_review` e `Public_catalog_does_not_require_database_or_authentication`.
- [ ] Executar testes Domain/API com filtro `SubscriptionPlanTests|PlanCatalogTests`; confirmar falha pelos tipos/rota inexistentes, não por configuração externa.
- [ ] Implementar catálogo/contratos/controller GET `/api/v1/billing/plans`; estado do interruptor vem da configuração do servidor; não devolver dados de cliente. Os limites inválidos ou preço negativo não constituem planos válidos.
- [ ] Executar novamente os mesmos testes; confirmar catálogo público 200 com registo desligado e os valores exatos.
- [ ] Rever alterações e guardar commit `Adiciona catálogo de planos de demonstração`.

### Task 2: Registo institucional transacional

**Files:** Criar `Domain/Billing/DemonstrationSubscription.cs`, `Domain/Institutions/InstitutionProfile.cs` nos projetos backend correspondentes; `Application/Onboarding/OnboardingService.cs`, `OnboardingModels.cs`, `IOnboardingUnitOfWork.cs`; `Infrastructure/Onboarding/OnboardingUnitOfWork.cs`, `OnboardingWriteScope.cs`; `Infrastructure/Identity/OnboardingAccountProvisioner.cs`; `Infrastructure/Institutions/InstitutionRegistrationStore.cs`; `Infrastructure/Billing/SubscriptionStore.cs`, `SubscriptionConfiguration.cs`; `Infrastructure/Institutions/InstitutionProfileConfiguration.cs`; `Contracts/Onboarding/OnboardingContracts.cs`; `Api/Onboarding/OnboardingController.cs`, `OnboardingExceptionHandler.cs`, `OnboardingRegistration.cs`. Modificar DbContext, IdentityRegistration, Institution e ApplicationUser para associação do próprio criador; recursos API. Criar teste `tests/SchoolGether.Api.IntegrationTests/OnboardingTests.cs`; estender IdentityFactory com configuração de onboarding e substituição de dependência de auditoria para simular falha.

**Interfaces:** Produz `OnboardingService.RegisterAsync(RegisterInstitution command, CancellationToken): Task<Registration>`. Consome PlanCatalog, `IOnboardingAccountProvisioner.CreateOwnerAsync(string name, string email, string password, CancellationToken): Task<RegisteredOwner>` e `LinkInstitutionAsync(int ownerId, int institutionId, CancellationToken): Task`; `IInstitutionRegistrationStore.CreateAsync(string name, string type, string? locality, int ownerId, CancellationToken): Task<int>`; `ISubscriptionStore.CreateAsync(int institutionId, SubscriptionPlan plan, string catalogVersion, int ownerId, CancellationToken): Task`; IAuditWriter existente. `RegisteredOwner` contém ID/nome/e-mail, nunca hash ou token. `IOnboardingUnitOfWork.ExecuteAsync<T>(Func<CancellationToken, Task<T>>, CancellationToken): Task<T>` usa o mesmo contexto/transação das dependências.

- [ ] Escrever `Signup_creates_only_institution_owner_and_demonstration_subscription`, `Invalid_signup_leaves_no_rows`, `Duplicate_email_does_not_convert_account_or_create_institution`, `Failure_writing_audit_rolls_back_user_and_institution`, `Anonymous_write_scope_cannot_target_existing_institution`, `Authenticated_signup_is_refused` e `Disabled_signup_returns_503_without_writes`. Verificar papel InstitutionAdmin, IsPlatformAdmin falso, criador igual ao owner e ocupação inicial 1.
- [ ] Executar API com filtro `OnboardingTests`; confirmar falha 404/ausência dos contratos antes do código.
- [ ] Implementar validações/coordenação em Application. A infraestrutura cria owner antes da instituição, associa o criador e permite apenas inserção da pertença exata na instituição recém-criada. Não inventar principal global ou ID 0. Subscrição nasce com UsedAccounts=1, perfil pendente; limite/version do plano vem do catálogo. Registo retorna 201 sem sessão.
- [ ] Adicionar rate limit dedicado encadeado IP/global, corpo limitado, interruptor e erros PT. Não confiar em forwarded headers arbitrários; configurar só o que o alojamento realmente fornece. Respostas privadas/registo sem cache público.
- [ ] Gerar migração `PublicInstitutionOnboarding` por `dotnet ef migrations add` com os projetos Infrastructure/Api; revisar operações e snapshot, sem aplicar ao ambiente online. Testar email normalizado concorrente em instância MySQL temporária.
- [ ] Executar API com filtro `OnboardingTests`; verificar rollback com falha de auditoria, registo anónimo restrito e preservação dos dados antigos. Guardar commit `Implementa adesão institucional transacional`.

### Task 3: Quotas atómicas nas criações de contas

**Files:** Criar `Application/Billing/IInstitutionAccountCapacity.cs`, `Infrastructure/Billing/InstitutionAccountCapacity.cs`; modificar `Infrastructure/Identity/IdentityStore.cs`, registos de DI e subscrição da tarefa 2; testes `tests/SchoolGether.Api.IntegrationTests/InstitutionCapacityTests.cs` e cenário MySQL temporário.

**Interfaces:** `IInstitutionAccountCapacity.ReserveAsync(int institutionId, CancellationToken): Task`. O serviço usa reserva atómica no contador `UsedAccounts` da subscrição com condição `< AccountLimit`, dentro da mesma transação de criação da conta. Ausência de subscrição mantém comportamento legado. Registo inicial usa a pertença dedicada da tarefa 2 e não reserva duas vezes a primeira vaga.

- [ ] Escrever `One_remaining_seat_allows_only_one_concurrent_creation`, `Duplicate_or_invalid_user_does_not_consume_seat`, `Global_admin_creation_obeys_same_capacity` e `Legacy_institution_without_subscription_still_accepts_account`.
- [ ] Executar API com filtro `InstitutionCapacityTests`; confirmar que a falha evidencia ausência da quota, não uma colisão entre e-mails de teste.
- [ ] Reservar dentro da transação existente de IdentityStore.CreateAsync, antes de guardar a nova conta. Atualização condicional do contador/versão deve ser atómica no MySQL; rollback da conta devolve a vaga. Billing não lê diretamente a tabela de Identity. Não depender de contagem de leitura potencialmente desatualizada ou de bloqueio em memória por processo.
- [ ] Executar os testes e o cenário concorrente MySQL com conexões independentes. Verificar que reservas/contas/subscrição coincidem e o segundo pedido recebe 409 com mensagem de capacidade. Documentar que qualquer futura eliminação/restauro deve atualizar a reserva na mesma transação.
- [ ] Guardar commit `Aplica limites de contas por instituição`.

### Task 4: Configuração institucional e consulta de subscrição

**Files:** Criar `Application/Institutions/InstitutionSetupService.cs`, `InstitutionSetupModels.cs`, `IInstitutionSetupStore.cs`; `Infrastructure/Institutions/InstitutionSetupStore.cs`; `Api/Institutions/InstitutionSetupController.cs`; estender contratos Institutions/Billing e recursos API; testes `tests/SchoolGether.Api.IntegrationTests/InstitutionSetupTests.cs`.

**Interfaces:** `InstitutionSetupService.GetAsync(CancellationToken): Task<InstitutionSetup>` e `SaveAsync(SaveInstitutionSetup command, CancellationToken): Task<InstitutionSetup>`. A conta/instituição vem de ICurrentAccount e IdentityService.GetMeAsync; o store atua só nesse ID. `ISubscriptionStore.FindForInstitutionAsync(int institutionId, CancellationToken): Task<InstitutionSubscription?>` retorna DTO interno com plano/contador, sem ler contas de Identity. Para legado sem subscrição, usar a listagem/contagem através de interface Identity, nunca a tabela diretamente.

- [ ] Escrever `Own_institution_setup_is_persisted`, `Other_tenant_or_student_cannot_read_or_change_setup`, `Stale_version_preserves_setup`, `Legacy_missing_profile_has_explicit_pending_state` e `Suspension_rejects_existing_token`. Ano válido 2026/2027; rejeitar 2026/2028, formato diferente e início fora de 1900..2199.
- [ ] Executar API com filtro `InstitutionSetupTests`; confirmar falha antes de criar rotas/serviço.
- [ ] Implementar GET/PUT `/api/v1/institutions/current/setup` e GET `/api/v1/institutions/current/subscription`; papel exclusivo InstitutionAdmin e estado atual no servidor. Nome/configuração/conclusão e evento numa transação. RowVersion do contrato é a versão da Institution, também quando não há perfil: toda gravação de configuração incrementa essa versão e usa a concorrência EF, mesmo sem mudar o nome. Isto protege contra uma renomeação simultânea no painel global. Perfil inexistente usa inserção única; conflito concorrente 409. Configuração concluída mantém CompletedAt; editar não reabre o assistente. Adicionar `Global_rename_conflicts_with_stale_setup` aos testes.
- [ ] Executar testes e verificar assinatura OpenAPI Bearer nas rotas privadas. Preservar filtros tenant e operações globais; guardar commit `Adiciona configuração inicial da instituição`.

### Task 5: ApiClient, página pública e adesão no site

**Files:** Criar `src/Shared/SchoolGether.ApiClient/Onboarding/OnboardingApiClient.cs`, `Institutions/InstitutionApiClient.cs`, `Billing/PlanApiClient.cs`; modificar `Web/Identity/MemorySessionRegistration.cs`; criar `Web/Public/PublicLanding.razor`, `PublicLanding.razor.css`, `RegistrationWizard.razor`, `RegistrationWizard.razor.css`, `PublicNavigation.cs`; extrair login/perfil de `Web/Pages/Home.razor` para `Web/Identity/IdentityPage.razor`; modificar Home/MainLayout e recursos UI. Testes `tests/SchoolGether.ApiClient.Tests/OnboardingApiClientTests.cs`, `InstitutionApiClientTests.cs`; criar cenário Playwright local `.superpowers/verification/public-onboarding/verify-public.cjs`.

**Interfaces:** `PlanApiClient.GetCatalogAsync(CancellationToken): Task<PlanCatalogResponse>` e `OnboardingApiClient.RegisterAsync(RegisterInstitutionRequest, CancellationToken): Task<RegistrationResponse>` são públicos e sem retry automático. `InstitutionApiClient.GetSetupAsync`, `SaveSetupAsync`, `GetSubscriptionAsync`, `GetMembersAsync(int page, int pageSize, CancellationToken)` e `CreateUserAsync(CreateUserRequest, CancellationToken)` usam IdentitySession.ExecuteAuthorizedAsync e ApiResponseReader existentes.

- [ ] Escrever testes de HTTP para rotas/DTOs, ausência de Authorization no registo, 400/409/429/503 e ausência de retry POST. Preparar fixture de navegador para público, preços/capacidades exatos, seleção/validação, retorno entre passos, registo→uma tentativa de login→área, resultado desconhecido e login falhado após criação.
- [ ] Executar ApiClient com filtro `OnboardingApiClientTests|InstitutionApiClientTests` e cenário de navegador contra o site ainda antigo; confirmar falhas relevantes.
- [ ] Implementar clientes e página usando os contratos fixados acima. Home escolhe `view` da query; raiz sempre pública, entrada separada e área por papel. Preservar identidade visual e painel global; não copiar scripts do protótipo para a gestão de sessão. Nenhuma senha em URL/localStorage/log; clearing ao cancelar/sair e após tentativa de login. Tratamento de resposta perdida não reenvia o registo automaticamente.
- [ ] Verificar explicitamente indisponibilidade da API, catálogo atualizado em 409, recarregamento/histórico com base `/SchoolGether/`, query desconhecida, saída durante registo/login e sessão antiga. UI com foco/aria, alvo de 44 px mínimo e leitura a 390 px.
- [ ] Executar testes de clientes/navegador, inspecionar capturas desktop/mobile e guardar commit `Adiciona página pública e adesão guiada`.

### Task 6: Área institucional mínima e verificação da entrega

**Files:** Criar `Web/Institutions/InstitutionPanel.razor`, `InstitutionSetupForm.razor`, `InstitutionMembers.razor`, `InstitutionMemberForm.razor` e CSS por responsabilidade; recursos PT; `docs/public-onboarding.md`; atualizar AGENTS/guia de publicação. Cenário `.superpowers/verification/public-onboarding/verify-institution.cjs`; estender `tests/SchoolGether.Api.IntegrationTests/MySqlIdentityTests.cs` e `scripts/Test-MySqlIdentity.ps1` se necessário para as verificações isoladas da migração/quotas.

**Interfaces:** Consome InstitutionApiClient da tarefa 5. Componentes têm CancellationTokenSource de lifetime e são keyed pelo ID da conta; invalidar estado anterior antes de iniciar pedidos de nova sessão. InstitutionPanel recebe callback `EventCallback SignOut`; a raiz privada conserva PlatformPanel e IdentityPage para os outros papéis.

- [ ] Preparar cenário de navegador: perfil pendente→assistente→gravação→novo login sem repetição, utilizadores paginados, criação Teacher/Student, e-mail existente/capacidade 409, conflito de configuração com rascunho preservado e 401/suspensão. Fixar que criar utilizador nunca apresenta opção global/instituição diferente.
- [ ] Executar cenário antes de criar os componentes; confirmar ausência da área/ações.
- [ ] Implementar Resumo, Utilizadores, Configuração, Perfil e Sair. Plano sem subscrição mostra estado explícito. Criação limpa senha e recarrega ocupação/lista; não alegar envio de convite. Páginas educativas completas permanecem futuras; resposta tardia após logout não repõe dados.
- [ ] Executar `dotnet test --artifacts-path .superpowers/test-artifacts --verbosity quiet`, builds Release de API/Web, `dotnet ef migrations has-pending-model-changes` e `scripts/Test-MySqlIdentity.ps1`. Inspecionar relatórios: cenário MySQL realmente executado e aprovado em execução própria; skip normal não é aprovação.
- [ ] Executar os dois cenários de navegador e regressões existentes de login/painel global; inspecionar capturas desktop/390 px. Compilar MAUI Windows/Android em Debug para verificar contratos partilhados; execução nativa permanece verificação separada.
- [ ] Revisão independente da entrega contra especificação/normas; corrigir problemas e repetir só as verificações afetadas. Guardar commit `Conclui área institucional e verifica adesão`.
- [ ] Preparar PR para develop com evidência, limites e restrições; publicação e ativação online ficam após CI e revisão do colega. Não afirmar que o registo já está publicado antes destas ações.

## Auto-revisão do plano

Cobertura: público/catálogo (1/5), registo/atomicidade/privacidade (2), capacidade (3), configuração/legado (4), clientes/navegação (5), equipa/regressões/publicação (6). Interfaces de DTOs e clientes estão fixadas acima; Application mantém modelos próprios. Os cinco focos de revisão têm testes nas tarefas indicadas. Nenhum requisito de suporte, offline protegido ou app institucional foi acrescentado a esta entrega.

A implementação de contadores fica como escolha técnica do agente: capacidade não pode depender de uma contagem concorrente vulnerável; a reserva pertence a Billing e integra a mesma transação da conta. Alterar esta técnica exige preservar os testes de quota/rollback/concorrência, sem mudar os limites escolhidos pelo utilizador.
