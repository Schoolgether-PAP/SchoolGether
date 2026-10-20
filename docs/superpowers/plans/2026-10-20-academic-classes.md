# Academic Classes Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development, task-by-task. Steps use checkbox syntax.

**Goal:** separar disciplinas/turmas por ano curricular e ano letivo, preparando inscrições.
**Architecture:** estenderAcademic CourseUnit com ano opcional compatível; novo agregadoAcademicClass+softlinksCourseUnits, capacidade própria e versão do curso como barreira transacional. Cliente/site contextual, sem outras dependências.
**Tech Stack:** .NET10/OracleEF10/MySQL8/BlazorWASM/xUnit/Chromium.
**Spec:** docs/superpowers/specs/2026-10-20-academic-classes-design.md

## Global Constraints
RecursosPT/códigoinglês/SQLsnake_case, CleanArchitecture e stackfixas, semdependênciasnovas. InstitutionAdminprópriotenant revalidado; escoposexatos sembypass. Código30/nome200/StudyYear1..20/AcademicYearconsecutivo1900..2199/seleção1..100/pesquisa150/página20máx100. Ano/curso da turma imutáveis. Seminscrições/biblioteca/docentes/MAUI/cloud nestaentrega.

## Review Focus
1. Unidadeslegadas semStudyYear/replaysemYear após atribuição: preservarFPv1/updateomitido; Task1.
2. Disciplina10→11 enquanto turma10 é criada: barrieraconcorrênciadeterminística + histórico impede transferência apósuso; Task1.
3. Seleção deoutrocurso/tenant/ano/arquivada, alterações ocultas de links: guardas/FKs/elegibilidade; Task1.
4. Falhaaudit ouconcurrency rollback não deixa pai/turma/link parcialmentegravadonemgeraReplayerrado; Task1.
5. Picker trocaYear/página/contexto ou logouttardio: nãorecolocar seleção indevida, draft/freezeconservados; Task2.

## Interfaces comuns
Estender CourseUnit.Create/Update comStudyYearopcional após now (Update nullpreserva). Pedidos DTOUnit acrescentamStudyYearopcional nofim; resposta apósReplay; CourseUnitQuery acrescentaStudyYearopcional. ClientListAsync antigo permanece; `ListForYearAsync(int courseId,int studyYear,int page=1,string? search=null,CancellationToken ct=default)` sóativas.

Domain/Academic `AcademicClass`, `AcademicClassUnit`; `Course.TouchClasses(long version,DateTime now)` mesmo contratoativo/UTC/versiondeTouchUnits. Application `AuthorizedClassScope` internalctor/ActorId/ActorName/InstitutionId/Operation/CourseId/ClassId. AcademicAuthorization.RequireClassAsync(operation,courseId,classId?,ct) reusa revalidação. `AcademicClassService.ListAsync(int courseId,AcademicClassQuery query,ct)`, GetAsync(courseId,classId,ct), CreateAsync(courseId,CreateAcademicClass,ct), UpdateAsync(courseId,classId,UpdateAcademicClass,ct), ArchiveAsync/RestoreAsync(courseId,classId,long rowVersion,ct). Portas mínimas `IAcademicClassStore` e `IClassUnitOfWork`. IAcademicCourseUnitStore ganha consulta segura de referências paraYearchange dentrotransação; Application semContracts.

Contracts/Academic:
- `CreateAcademicClassRequest(Guid ClientId,string Code,string Name,int StudyYear,string AcademicYear,IReadOnlyList<int> UnitIds)`
- `UpdateAcademicClassRequest(long RowVersion,string Code,string Name,IReadOnlyList<int> UnitIds)`
- `AcademicClassStateRequest(long RowVersion)`
- `AcademicClassUnitResponse(int UnitId,string Code,string Name,int StudyYear,bool IsActive)`
- `AcademicClassResponse(int Id,int InstitutionId,int CourseId,Guid ClientId,string Code,string Name,int StudyYear,string AcademicYear,bool IsActive,long RowVersion,DateTime CreatedAt,DateTime? UpdatedAt,IReadOnlyList<AcademicClassUnitResponse> Units,bool Replay=false)`
- `AcademicClassListItemResponse` mesmos metadados, `int UnitCount` em vezUnits/Replay; contagem de links selecionados mesmo se disciplinaarquivada.
- `AcademicClassPageResponse(IReadOnlyList<AcademicClassListItemResponse> Items,int Total,int Page,int PageSize)`.

Routes `/api/v1/academic/courses/{courseId:int}/classes`: GETlista/POST201ouReplay200; GET/PUT `/{id:int}`, POST `/{id:int}/archive|restore`. Querypage/pageSize/search/status maisStudyYear?/AcademicYear?; ausentes=todos, Iddesc.

### Task1: core classes+ano das disciplinas+API/migração
Files: novos DomainAcademic/AcademicClass.cs (linksseparadosseajudar); ApplicationAcademic/AcademicClassService.cs, AcademicClassModels.cs; InfrastructureAcademic/ClassPersistence.cs; APIAcademic/AcademicClassController.cs; ContractsAcademic/AcademicClassContracts.cs. Alterar CourseUnit/domain/service/store/DTOs/controller/Yearquery; Course.TouchClasses; autorização/DI/DbContext/exceções/recursos/migraçãoEFsnapshot. Testes AcademicClassTests/Guards/DomainAcademicClassTests/CourseUnitStudyYearTests/MySqlAcademicClassTests e runnerScenarioAcademicClasses.
Consumes: módulosCourses/Units revistosPR11. Produces: interfacesacima e migração11AcademicClasses; relatórioTask1comassinaturasreais.
- [ ] REDrotas/compatYear antesproduto, implementarDomain/serviço/guardas/softlinks e preservarFPv1 eYearomitido. ClassreplayIDsordenados antesvalidarestadoatual. AtualizarYearindisciplinaapenassemqualquerreferênciahistórica.
- [ ] Exclusividade/prescan antesoldreturns; exactrefs eEFtemporarykeys; pai/turma/links/audit atómicos; Yearchange eUnitarchive serializadosatravésdeCourseversion. RecuperarReplayapósconcurrency/uniqueerro.
- [ ] GerarmigraçãoEF11; Domain/HTTP/guardas/rollback focados + sóregressõesoldUnitguard/lifecycleatingidos. UMOracleAcademicClasses10→11(inclFPv1null/preservação/FK/uniques/rollback ebarreiraconcorrência explícita). APIRelease/EFpendingmodel. Semsuitesantigascompletas/MAUI/Web/cloud.
- [ ] CommitPT/árvorelimpa/report .superpowers/sdd/2026-10-20-academic-classes/task-1-report.md. Revisãoscopedcore spec+quality antesTask2; corrigirsóriscosconcretos.

### Task2: client/UI unidadesYear+turmas e jornada focada
Files: AcademicClassApiClient/DI; estenderCourseUnitApiClientListForYear; WebAcademicClasses.razor(.css), AcademicClassEditor.razor(.css), pickercomponent sejustificado; AcademicCoursesnova ação/contexto, CourseUnitEditor/AcademicCourseUnitsYearfield/filterlabel; recursosPT/docsacademic-classes.md/AGENTSestado.
Consumes: contratosTask1eclients/IdentitySession/ApiResponseReader/PlatformDialog. Produces: funcionalidadesUIsembackenddeps.
- [ ] Ano/Pororganizardisciplina, editorYearnullable/preserve eajuda apósuso; turma comStudyYear eAcademicYearexplícitos (noedit decriação), pickerativas exatoano/cursopaginado+seleçãopreservada; arquivadasjáselecionadasmarcadassemseperder. Parentarquivadosóconsulta. Semcontagensalunosfalsas.
- [ ] Draft409+revisãoexplícita e mensagem públicaYearInUse; criaçãouncertaincongelaaté403429/replay/abandono. Requisição nova porattempt; query/páginaretry; @key/lifetimegeneration impederespostadeano/curso/sessãoantiga. PickerYearalteradorequerdecisãoexplícitapararesetseleção, nãotransferirIDs10para11silenciosamente.
- [ ] WebRelease + UMAjornadaChromiumcontrolada: atribuir10/11àsunidadeslegadas, replayoldcompat, filtros/pickernãooferece11not10/unassigned, criar/editar/softselection/archive/restore,1/100limites,409inclYearInUse,uncertain403429/replay/abandon, readonlyparent/retainedarchivedselection, search/page/retry, Year/contextrequestlate/logout,390teclado/Escape/foco. Inspecionar screenshots; sórerunfocadoapósfixconcreto, nãobackend/MySQL/MAUI.
- [ ] Docsconcisoslimites/próximoinscrições; commitPT/reportTask2. Revisãofinal única clientdelta+contratos, núcleoaprovadosócheckporrisconomeado. PRdevelop,CI/colega antesmerge/publicação.
