# Disciplinas e turmas por ano

## Intenção
PR11 integrado em develop8fe24bf7868d84be3fa7ae26ef2a6aa87ceb070d. O utilizador escolheu selecionar as disciplinas de cada turma e esclareceu que o10.ºano de Informática não terá as mesmas matérias/livros do11.º. UI profissional/simples delegada; site antes da app; testes focados, sem tempo como variável.

Este incremento entrega organização por ano e turmas, preparando inscrições (incremento seguinte). Não implementa biblioteca, livros, atribuição de docentes, inscrições, notas, horários, promoções de ano ou ecrãs nativos. Materiais/livros futuros terão de ser autorizados pelo contexto de turma/disciplina/ano, nunca apenas pelo curso. Sem publicação/migração online.

## Dois anos distintos
- `StudyYear` é ano de escolaridade/curricular:10,11 ou1 de licenciatura. Decisão técnica: inteiro1..20, comum aos tipos de instituição existentes.
- `AcademicYear` é período letivo, formatoAAAA/AAAA com anos consecutivos1900..2199 (regra existente). Curso, StudyYear e AcademicYear ficam imutáveis após criar turma.
- Disciplina (`CourseUnit`) ganha StudyYear opcional. Registos existentes mantêm null/«Por organizar»; não adivinhar o ano nem copiar programas. Podem ser consultados/editados, mas não selecionados para turma enquanto sem ano. O mesmo curso pode ter Matemática10 e Matemática11 como disciplinas distintas, com códigos/programas próprios. A unicidade de código do curso não muda.
- Alterar o ano da disciplina só é permitido antes de qualquer utilização em turma, inclusive referência arquivada/removida historicamente. Depois, criar outra disciplina para outro ano; editar programa/nome segue regras existentes.

## Compatibilidade das disciplinas
Create/UpdateCourseUnitRequest recebem `int? StudyYear=null` no fim. Create(null) continua válido (por organizar); Update(null/omitido) **preserva o ano atual**, não o apaga. Campo atribuído não volta a null. CourseUnitResponse acrescenta StudyYear opcional depois de Replay, preservando chamadas posicionais existentes. Query opcional StudyYear filtra número exato, ausência mantém todos. Novo cliente ListForYearAsync para picker, sem quebrar ListAsync existente.

Fingerprint de criações antigas null mantém exatamente o algoritmo anterior (arrayCode/Name/Syllabus). Nova criação com ano inclui esse ano numa representação canónica distinta. O fingerprint é imutável: um replay antigo sem ano continua válido após atribuir ano10. Não regravar hashes históricos. UnitYearInUse409 deve ser explicado com mensagem pública PT, preservando rascunho.

Preflight: verificar referências históricas só quando StudyYear efetivamente muda; programa/nome continuam editáveis quando o ano foi omitido ou é igual ao atual. Bind usa referências/identidades capturadas, nunca Id0 como autorização. O ensaio concorrente sincroniza as leituras antes da gravação e exige apenas um vencedor e nenhuma turma10 ligada a disciplina11.

## Turmas
Dentro de Cursos, ação Turmas abre lista contextual e Voltar aos cursos. Campos: códigoASCII1..30(normalizado como curso), nome1..200, StudyYear obrigatório1..20, AcademicYear obrigatório, seleção1..100 disciplinas distintas do mesmo curso/ano. Nenhuma disciplina de11.º/por organizar/de outro curso aparece no picker do10.º. Novas seleções exigem disciplina ativa; referências já selecionadas que entretanto foram arquivadas podem ser mantidas ou removidas, nunca adicionadas de novo por outra turma. Detalhes marcam disciplinas arquivadas, sem perder histórico/programa do catálogo.

Lista Ativas/Arquivadas, pesquisa código/nome<=150, filtrosStudyYear/AcademicYear opcionais, páginas20(máx100), total servidor, Iddesc. Novo/Detalhes/Editar/Arquivar(confirmar)/Restaurar. Edição só código/nome/seleção; não move curso/ano. Código único `(institution_id,course_id,study_year,academic_year,code)`, inclusive arquivadas. Sem DELETE. Curso arquivado permite consulta, não mutações; restaurar curso/turma não restaura disciplinas.

Só InstitutionAdmin ativo no tenant; revalidar Identity/Institutions e relações a cada operação. Teacher/Student/globalPlatformAdmin sem bypass.401 sessão inválida;403 papel;404 curso/turma/disciplina estrangeira ou relação errada;400 formato/duplicados/limites;409 estados/versões/código/chave/ano de disciplina em uso. DTOstrictJSON sem tenant/ator e sem relação mutável no update.

Class usa metadados comuns UTC/clientId/createdBy/deletedAt/RowVersion e fingerprint inicial normalizado com IDs ordenados (ordem da seleção não muda o replay). Mesma chave+autor+curso+payload inicial200 sem nova gravação/audit, mesmo após alteração de seleção/arquivo de turma ou curso. Outra chave/autor/payload409. RowVersion em edição/arquivo/restauro; draft409 e aceitação explícita de versão como módulos atuais. Criação incerta congela payload/key inclusive após403/429; só sucesso ou abandono explícito resolve.

## Consistência e persistência
Class e seleção são um agregado. `AcademicClassUnit` retém links removidos por desativação lógica, reativando a mesma relação se voltar à seleção; uniqueClassId/UnitId, sem apagar histórico. Estados/metadados das ligações são internos, não manipuláveis diretamente pela API. Novas ligações não precisam de ClientId próprio: são derivadas da operação agregada com ClientId de turma.

Capacidade própria `AuthorizedClassScope` e `ClassWriteScope` interno ligam ator/tenant/operação/transação e referências exatas do pai, turma, links e disciplinas elegíveis. Capturar identidades no bind, tratar chaves EF temporárias sem aceitar referências arbitrárias. Só paiRowVersion/UpdatedAt pode mudar; disciplinas consultadas não são modificadas. Exclusividade com todos os escopos antigos antes de retornos; prescan de classes/links impede bypass. Não ampliar Support/Onboarding/Courses/Units.

Cada mutação efetiva de turma toca versão/UpdatedAt do curso ativo, na mesma transação de turma/links/audit. Usar métodoCourse.TouchClasses com mesmas condições de TouchUnits. Isto serializa criação/edição de turma com arquivo de curso/disciplina ou mudança de ano da disciplina. UnitService verifica referências de turma dentro da sua transação antes de mudarStudyYear. Replay não toca pai. Falha audit reverte tudo. Recuperação de criação após unique/concurrency rollback reconsultaClientId e só devolve replay correspondente. Audit target `academic.class:{ClientId:D}`, sem texto livre/pesquisa/programa no log; recusas independentes seguras.

Uma migraçãoEF `AcademicClasses` (11.ª) acrescenta StudyYear nullable emCourseUnit, tabelaacademic_class e academic_class_unit. FKcompostaClasse(institution_id,course_id)→CourseAKexistente; link(institution_id,course_id,class_id)→ClassAK e link(institution_id,course_id,unit_id)→UnitAK; Restrict, índices únicos acima eClientIdporinstituição. A coerência deStudyYear é garantida por serviço/capacidade/barreira do pai, não por FKnullable. Preservar dez migrações/dados/FPantigos; não preencher ano automaticamente.

## UX e verificação
Manter paleta/SegoeUI/shell. Disciplinas mostram ano ou Por organizar, editor permite atribuir ano e explica bloqueio após uso. Turma escolhe curso contextual, StudyYear/AcademicYear e disciplinas paginadas/pesquisáveis; seleção sobrevive a páginas. Anoletivo pode sugerir configuração institucional pela API existente, sem ler tabelas de outro módulo. Rascunho preservado ao falhar, nunca perder seleção por resposta tardia; sessão/contexto/diálogo e cancelamento como existentes.390px, teclado/Escape/foco. Nenhuma contagem fictícia de alunos.

Verificação proporcional: Domain/HTTP novos para compatibilidadeYear/fingerprint/replay, elegibilidade10vs11/null, CRUD/softlinks, tenant/roles/strictJSON/versões/guardas/rollback; só regressões existentes nomeadas de guardas/ciclo de unidades afetados. APIRelease+EF; um Oracle temporário10→11 com preservação/null/legacyreplay/FK/uniques/atomicidade e corrida **determinística** por barreira entre leituras antes do SaveChanges (StudyYearchangevsClasscreate e replay concorrente). Depois WebRelease+uma jornada Chromiumcontrolada de unidadesYear e turmas, pickerpáginas, conflitos/incerteza/contextos/readonly/390/logout. Não repetir core/MySQL/MAUI após UI inalterada nesses módulos. Revisão core e final cliente+integração separadas, sem reler core aprovado; CI/colega antesmerge/publicação.
