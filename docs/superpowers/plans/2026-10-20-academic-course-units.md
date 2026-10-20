# Academic Course Units Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development to implement task-by-task. Steps use checkbox syntax.

**Goal:** gerir unidades curriculares próprias de cada curso no painel institucional.
**Architecture:** estender Academic nas quatro camadas; unidade subordinada ao curso com capacidade de escrita própria e versão do pai como barreira ao arquivo concorrente. Cliente tipado e área contextual no site; sem dependências novas nem mudanças em contas/alojamento.
**Tech Stack:** .NET10, Oracle EF10/MySQL8, Blazor WASM, xUnit, Chromium local.
**Spec:** docs/superpowers/specs/2026-10-20-academic-course-units-design.md

## Global Constraints

Recursos PT, código inglês/SQLsnake_case, Domain sem dependências/Application sóDomain, DTOs emContracts. Site antes da app; InstitutionAdmin próprio tenant, sem bypass global. Curso/unidade/ator/clientkey imutáveis. Testes focados, base temporária, sem dados pessoais/deploy. Código30/nome200/programa5000 apóstrim; pesquisa150/página20máx100.

## Review Focus

1. Unidade de outro curso/tenant e mudança escondida de relação:404/guardas/FK; coberto Task1.
2. Arquivo do pai versus edição da unidade: uma versão ganha, sem escrita sob pai arquivado; Task1 Oracle.
3. Retry de criação depois de pai arquivado ou recusa403/429: replay seguro sem nova chave implícita; Tasks1/2.
4. Falha audit não deixa versão do pai avançada nem unidade alterada; Task1 SQL/HTTP.
5. Navegar para outro curso/sair com pedido pendente não repinta contexto antigo; Task2 browser.

## Interfaces comuns

Domain `CourseUnit` (nome evita conflito com UnitOfWork), métodos Create/IsReplayOf/Update/Archive/Restore como Course, incluindo CourseId e Syllabus em vez de Description. `Course.TouchUnits(long version,DateTime now)` valida ativo+UTC+versão e avança metadados do agregado.

Application `AuthorizedCourseUnitScope` (internal ctor, ActorId/ActorName/InstitutionId/Operation/CourseId/UnitId); `AcademicAuthorization.RequireUnitAsync(string operation,int courseId,int? unitId=null,CancellationToken ct=default)` reusa a revalidação existente sem quebrar RequireAsync. `AcademicCourseUnitService.ListAsync(int courseId,CourseUnitQuery query,ct)`, `GetAsync(int courseId,int unitId,ct)`, `CreateAsync(int courseId,CreateCourseUnit request,ct)`, `UpdateAsync(int courseId,int unitId,UpdateCourseUnit request,ct)`, `ArchiveAsync(int courseId,int unitId,long rowVersion,ct)`, `RestoreAsync(...)`. Novas portas `IAcademicCourseUnitStore` e `ICourseUnitOfWork`; modelos internos sem Contracts; escolher métodos de store mínimos para localizar/bind pai+unidade e replay por key no tenant.

Contracts/Academic records:
- `CreateCourseUnitRequest(Guid ClientId,string Code,string Name,string? Syllabus)`
- `UpdateCourseUnitRequest(long RowVersion,string Code,string Name,string? Syllabus)`
- `CourseUnitStateRequest(long RowVersion)`
- `CourseUnitResponse(int Id,int InstitutionId,int CourseId,Guid ClientId,string Code,string Name,string? Syllabus,bool IsActive,long RowVersion,DateTime CreatedAt,DateTime? UpdatedAt,bool Replay=false)`
- `CourseUnitPageResponse(IReadOnlyList<CourseUnitResponse> Items,int Total,int Page,int PageSize)`

Rotas exatas na spec. Sem courseId/bodytenant; requeststrictJSON. `AcademicCourseUnitApiClient` ListAsync(courseId,page,search,archived,ct), GetAsync(courseId,unitId,ct), CreateAsync(courseId,request,ct), UpdateAsync(courseId,unitId,request,ct), ArchiveAsync/RestoreAsync(courseId,unitId,rowVersion,ct); usa sessão/leitor comum e request novo por tentativa.

### Task 1: unidade, autorização, API, persistência e migração

**Files:** criar Domain/Academic/CourseUnit.cs; Application/Academic/AcademicCourseUnitService.cs e CourseUnitModels.cs; Infrastructure/Academic/CourseUnitPersistence.cs; Api/Academic/AcademicCourseUnitController.cs; Contracts/Academic/CourseUnitContracts.cs. Alterar Domain/Academic/Course.cs, AcademicAuthorization.cs, DbContext/DIAcademic/AcademicExceptionHandler/recursosAPI/configCourse conforme necessário; migração e snapshot gerados. Testes novos CourseUnitTests.cs, AcademicCourseUnitTests.cs, AcademicCourseUnitGuardTests.cs, MySqlAcademicUnitTests.cs e runner Scenario AcademicUnits. Dividir ficheiros por responsabilidade se necessário; não reestruturar módulos não afetados.

**Consumes:** Course/AcademicAuthorization/IdentityStore/AuditWriter e padrões existentes aprovados no PR10.
**Produces:** contratos/rotas acima e `AddAcademicModule` estendido; relatório das assinaturas entregues para Task2.

- [ ] Testes HTTP RED antesproduto: rotas401/201 ausentes; depois Domain/HTTP implementados para casos concretos da spec.
- [ ] Implementar capacidade Units transacional e guardas exatas curso+unidade; mutação toca versão do pai; replay não toca nem exige pai ativo. Ampliar checks de exclusividade, nunca Allowall dos escopos antigos.
- [ ] Gerar AcademicCourseUnits com EF. FK composta e únicos; revalidação de role/tenant e relações cadaop; nova escrita limitada à unidade e pai autorizados e audit correspondente.
- [ ] Domain/HTTP focados + apenas dois checks existentes de guarda/cicloCourse afetados. API Release/EF pending-model. Um Oracle AcademicUnits de9→10, com preservação, índices/FK/UTC/replay, corridas e rollback real.
- [ ] Commit português/árvore limpa; relatório .superpowers/sdd/2026-10-20-academic-course-units/task-1-report.md. Gate revisão spec+quality uma leitura do delta; corrigir achados com testes focados.

### Task 2: unidades no painel, cliente e verificação visual

**Files:** criar ApiClient/Academic/AcademicCourseUnitApiClient.cs; Web/Academic/AcademicCourseUnits.razor(.css) e CourseUnitEditor.razor(.css). Alterar AcademicCourses para abrir contexto e regressar à lista, DIWeb/recursosPT; base AcademicView só se preciso para mensagens corretas, sem quebrar curso. docs/academic-course-units.md e AGENTS estado conciso.

**Consumes:** DTOs e endpoints Task1, IdentitySession.ExecuteAuthorizedAsync, ApiResponseReader, PlatformDialog e shell existente.
**Produces:** UI funcional curso→unidades→curso, sem novo menu global redundante; novos/editar/arquivar/restaurar/programa, pesquisa/status/paginaçãoreais.

- [ ] Preservar visual atual e acessibilidade390/teclado/foco/Escape. Carregar pai no contexto; arquivado consulta apenas. Sem métricas inventadas nem associações docentes ainda.
- [ ] Draft409+revisão explícita; freeze ClientId/payload em entrega incerta inclusive retry403/429; nova chave só abandonoexplícito. Cancelar geração/contexto/troca curso/logout; impedirduplosubmit; erro/retry mantém query/página pedida.
- [ ] Web Release e uma jornada Chromiumcontrolada dos riscos Task2; screenshotsdesktop/390inspecionadas. Apenas repetição focada sefalha/correção concreta. Não repetir Domain/backend/MySQL/MAUI.
- [ ] Documentar limites/próximo turmas/inscrições; commit português/relatórioTask2. Revisão final única delta cliente+integração (núcleo járevisto semrepetição), PRdevelop/CI/colega antesmerge/publicação.
