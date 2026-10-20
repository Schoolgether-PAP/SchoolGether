# Unidades curriculares por curso

## Intenção e decisões

Continuação após PR10 integrado em `develop` (`df42c40675fa3433f0e3409de1a237a53670cf7f`). O utilizador confirmou: cada disciplina é criada dentro de cada curso e tem programa próprio. Delegou uma interface profissional, simples e fácil de navegar; testes apenas necessários às alterações. Site primeiro, app depois. Mantêm-se .NET10, Clean Architecture, Oracle EF10/MySQL8, Blazor WASM, recursos PT e paleta SchoolGether.

Decisões do implementador: este incremento gere o catálogo, sem semestres, ECTS, horas, docentes, turmas, inscrições, notas, materiais ou dados fictícios. Código, nome e programa bastam para definir a disciplina. O catálogo continua independente do ano letivo. Uma disciplina nunca muda de curso/tenant; a mesma disciplina noutro curso é outro registo. Não há publicação nem migração online nesta entrega.

## Comportamento

- InstitutionAdmin ativo gere só a própria instituição; Identity/Institutions são revalidados em cada operação como nos cursos. Teacher, Student e PlatformAdmin continuam sem acesso académico direto.
- Em Cursos, ação «Unidades curriculares» abre a lista do curso, com nome/código do curso, «Voltar aos cursos», Ativas/Arquivadas, pesquisa por código/nome, paginação20 e Novo/Detalhes/Editar/Arquivar/Restaurar. Mantém o menu institucional existente.
- Curso inexistente/estrangeiro ou unidade que não pertence ao curso devolve404 sem fuga de dados. Curso arquivado permite consulta, mas qualquer nova mutação de unidade devolve409. Restaurar o curso não muda os estados individuais das unidades. Arquivar preserva dados; não há DELETE.
- Código trim+uppercase ASCII, 1..30, começa por letra/número e segue letras/números/ponto/hífen/underscore; único no curso mesmo arquivado. Nome trim obrigatório1..200. Programa (`Syllabus`) opcional, trim, até5000 caracteres; branco vira null, texto simples escapado (sem HTML). Limites após trim.
- Metadados comuns Id/institution_id/course_id/client_id/created_by/created_at/updated_at/deleted_at/row_version, UTC; ativa/versão1 na criação. Tenant/autor/curso/chave/fingerprint são imutáveis. DTOs strictJSON não aceitam tenant/autor nem curso no corpo.
- RowVersion na edição/arquivo/restauro;409 preserva rascunho e exige revisão explícita antes de nova submissão. Sem retry com versão nova silenciosa.
- ClientId estável na criação; unicidade `(institution_id,client_id)`. Replay só do autor original com o payload normalizado inicial, incluindo course_id; mesma chave com outro curso/payload/autor409. Replay devolve estado atual, mesmo depois de editar/arquivar unidade ou arquivar curso, sem nova alteração/auditoria. Resultado incerto congela chave/payload inclusive após retry403/429; só sucesso ou abandono explícito resolve.

## Atomicidade e autorização de escrita

Usar uma capacidade específica de unidades (`AuthorizedCourseUnitScope`) e um `CourseUnitWriteScope` interno. Vincular referências exatas do curso pai e unidade, ator/tenant/operação e transação atual. Não ampliar bypasses de Support/Onboarding/Courses. Rejeitar referências adicionais, curso/tenant/autor/chave trocados, operações fora do escopo e escrita de outros módulos.

Decisão técnica: cada mutação efetiva da unidade avança também RowVersion/UpdatedAt do curso pai, mantendo-o ativo. `Course.TouchUnits(version,now)` valida UTC/versão/estado. O token EF do pai serializa edição das unidades com arquivo do curso, sem SQL específico por provider; se o arquivo do pai ganhar a corrida, a unidade não é gravada. Se a unidade ganhar, o arquivo que usou a versão anterior recebe409. Isto pode exigir rever um formulário de curso aberto antes de uma alteração de unidade; evita gravar novas alterações sob um curso arquivado. Replay não toca o pai.

Curso+unidade+auditoria são gravados na mesma transação; falha de qualquer um reverte todos, incluindo versões. Audit target seguro `academic.unit:{ClientId:D}`; sem programa/nome/código livre no log. Recusas com contexto independente seguro. Guardas antigas continuam restritas; prescan impede qualquer bypass para CourseUnit. CourseUnitScope não pode coexistir com outro escopo.

Preflight: TouchUnits recebe a versão do pai carregado dentro da transação; a guarda permite no pai apenas RowVersion/UpdatedAt e exige estado ativo antes/depois. O prescan de exclusividade antecede todos os retornos das guardas antigas. Se uma criação concorrente falhar por unicidade **ou concorrência do pai**, após rollback procurar novamente ClientId no tenant: só devolver200 se autor/curso/payload inicial coincidirem, mesmo com pai entretanto arquivado; caso contrário409. Um retry posterior com a mesma chave continua a recuperar um vencedor já confirmado.

## Persistência e API

Migração EF `AcademicCourseUnits` (10.ª): tabela `academic_course_unit`; unique `(institution_id,course_id,code)` e `(institution_id,client_id)`; FK composta `(institution_id,course_id)` para chave alternativa `(institution_id,id)` de academic_course, Restrict. Preservar as nove migrações existentes e os seus dados; converter DateTime paraUTC como nos cursos.

Rotas `/api/v1/academic/courses/{courseId:int}/units`: GET lista, POST criar201/replay200, GET/PUT `/{unitId:int}`, POST `/{unitId:int}/archive` e `/restore`. Query `page=1,pageSize=20,search?,status=Active|Archived`; pageSize1..100, pesquisa<=150, ordemIddesc e totalservidor.401 sessão inválida,403 papel,404 relação/tenant inexistente,400 inválido,409 estado/versão/chave/código. ProblemDetails PT. Sem dependências novas.

## Verificação proporcional

1. Novos testes Domain/HTTP focados: limites/programa/estado/replay; CRUD/search/paginação; papéis/tenant/relação de curso/strictJSON; versão e pai arquivado; guardas referência/transação/crossmodule; rollback curso+unidade+audit. Pelo risco da guarda partilhada, repetir apenas AcademicGuardTests e o cenário HTTP de ciclo de curso existente, não as suites antigas completas.
2. Um cenário Oracle temporário `AcademicUnits`: upgrade9→10 com preservação, UTC/FK/unicidade por curso inclusive arquivados, replay após mutações/arquivo do pai e corridas de criação/versão/arquivo pai; rollback real por falha audit. Sem Aiven ou contas pessoais.
3. API Release + EF modelo; depois Web Release e uma jornada Chromium controlada: navegação curso↔unidades/isolamento de contexto, programa/criar/editar/conflito explícito/uncertainty403/429/arquivo/restauro, curso arquivadoreadonly, pesquisa/página/retry,390teclado/foco/Escape e logout tardio. Não repetir backend/MySQL/MAUI após UI que não os altera.
4. Revisão independente por tarefa; revisão final combinada do delta cliente e integração de contratos, preservando a revisão do núcleo e consultando-o só por risco concreto. CI normal, revisão de colega e merge precedem publicação.
