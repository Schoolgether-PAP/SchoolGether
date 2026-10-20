# Academic Courses Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development. Two vertical slices; focused tests per user instruction.

**Goal:** gerir cursos reais da própria instituição, com interface simples, isolamento e alterações seguras.
**Architecture:** módulo Academic nas camadas existentes; contratos e cliente tipado partilhados, Web apenas neste incremento. Sem dependências novas.
**Tech Stack:** .NET10, EFCore10/OracleMySQL8, BlazorWASM, xUnit e Chromium existentes.
**Spec:** docs/superpowers/specs/2026-10-20-academic-courses-design.md

## Global Constraints
- Apenas InstitutionAdmin ativo e a própria instituição;401/403/404/409 corretos. Revalidar identidade e não aceitar tenant/ator no corpo.
- Código normalizado1..30/nome200/descrição1000; lista20máx100; estado ativo/arquivado; sem apagar ou libertar códigos históricos.
- ClientId inicial estável, fingerprint imutável+autor e índices únicos; versões optimistas e auditoria atómica; escopoEF por referência/transação.
- Português em recursos; paleta/SegoeUI/nav existentes; desktop390/teclado/foco; sem dados/métricas fictícios.
- Testes focados e artefactos temporários próprios. Não repetir backend/MAUI/MySQL de módulos inalterados. Sem cloud/deploy/dados pessoais.

## Review Focus
IDs de outro tenant e claims obsoletos; retry incerto após edição/arquivo; código arquivado ainda ocupado;409 depois de mudança concorrente; logout com resposta tardia e estado do formulário.

## Interfaces comuns
- Application/Academic: AcademicAuthorization com RequireAsync(operation,courseId?,ct) produz AuthorizedAcademicScope de construtor interno; ActorId/ActorName/InstitutionId/Operation/CourseId são imutáveis e derivados da sessão revalidada.
- AcademicCourseService: ListAsync(CourseQuery,ct),GetAsync(id,ct),CreateAsync(CreateCourse,ct),UpdateAsync(id,UpdateCourse,ct),ArchiveAsync(id,rowVersion,ct),RestoreAsync(id,rowVersion,ct). Models Application sem referência a Contracts. IAcademicCourseStore/IUnitOfWork só operam no escopo autorizado; módulos externos via suas interfaces.
- Contratos em Contracts/Academic: CreateCourseRequest(Guid ClientId,string Code,string Name,string? Description), UpdateCourseRequest(long RowVersion,string Code,string Name,string? Description), CourseStateRequest(long RowVersion), CourseResponse(int Id,int InstitutionId,Guid ClientId,string Code,string Name,string? Description,bool IsActive,long RowVersion,DateTime CreatedAt,DateTime? UpdatedAt,bool Replay=false), CoursePageResponse(Items,Total,Page,PageSize). Atributos de validação e JsonUnmappedMemberHandling.Disallow nos pedidos.
- Rotas /api/v1/academic/courses: GET lista (page/pageSize/search/status=Active|Archived), POST cria (201; replay200), GET/{id}, PUT/{id}, POST/{id}/archive e POST/{id}/restore. Status lista defaultActive, defaultpage1/pageSize20. DTOs não incluem tenant no pedido nem campos internos.
- ApiClient/Academic/AcademicCourseApiClient usa IdentitySession.ExecuteAuthorizedAsync e ApiResponseReader; factories/JSON novos por tentativa. Sem nova persistência de tokens; métodos correspondem às rotas.

### Task 1: catálogo académico persistido e API autorizada
Files: Domain/Academic/Course.cs(+exception), Application/Academic autorização/models/ports/service, Infrastructure/Academic store/configuration/write scope/UoW/registration, Persistence DbContext+migração, Api/Academic controller/handler/registration/resources, Contracts/Academic, testes CourseTests/AcademicCourseTests/MySqlAcademicTests e cenário Academic no runner existente.
- [ ] TDD focado: regras de campos/estado/versão; HTTP autorização/isolamento/strictJSON, pesquisa/paginação/CRUD/arquivo/restauro, unicidade incluindo arquivado, replay estável e conflito, versão concorrente, guardas de referência/tenant/módulo, auditoria/rollback. Sem testes duplicados da implementação.
- [ ] Implementar escopo/migração/DI e erros/auditoria seguros; manter filtros/guardas antigos. Reutilizar padrões sem copiar o módulo Support inteiro.
- [ ] Cenário Oracle temporário único Academic: migrar8→9 preservando dados existentes, índices por tenant/código/chave, concorrência e versão. Nunca usarAiven/serviço pessoal. Atualizar runner ValidateSet/filter, preservar limpeza.
- [ ] Executar só testes novos/afetados, APIRelease e EFhas-pending-model-changes. Relatório curto com comandos/contagens e interfaces concretas; commit português e revisão deste núcleo antesTask2.

### Task 2: cursos no painel institucional e cliente tipado
Files: ApiClient/Academic/AcademicCourseApiClient.cs+DI, Web/Academic componentes/formulário/CSS, InstitutionPanel nav, recursos e documentação academic-courses.md/AGENTS.
- [ ] Consumir contratos entregues sem Domain/Application nos clientes; preservar novo visual e áreas existentes. UI Ativos/Arquivados, pesquisa, edição/arquivo/restauro e ações reais.
- [ ] Tratar 409 e entrega incerta de criação sem perder rascunho/duplicar, cancelamento/session generation e erro recuperável. Campos não mudam silenciosamente após pedido incerto.
- [ ] WebRelease + uma jornada Chromiumcontrolada: vazio/lista/paginação/pesquisa, criação/retry/código duplicado, edição409/revisão explícita, arquivo/restauro, falha/repetir, logout tardio e390teclado/Escape/foco. Capturar/inspecionar desktop390. Sem repetir todoMySQL/backend/MAUI após UI inalterada nesses módulos.
- [ ] Documentar limites/próximo incremento (unidades curriculares/turmas/inscrições), commit e revisão final única do delta cliente+integração de contratos; núcleo já revisto só é relido por risco concreto. Corrigir achados, abrir PR, CI e revisão do colega antesmerge/publicação.
