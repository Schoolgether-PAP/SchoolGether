# Inscrições em turmas

Incremento local sobre cursos e turmas do PR12. A demonstração publicada em 2026-10-21 conserva o código PR12 e onze migrações; as inscrições e a migração 12 ainda dependem de revisão, CI e integração/publicação próprias. Não foram usadas contas ou dados online na verificação deste incremento.

O responsável institucional ativo gere alunos da própria instituição através de **Cursos → Turmas → Inscrições**. O contexto mostra curso, turma, ano curricular e ano letivo. As listas Ativas/Canceladas pesquisam nome/e-mail, usam páginas de 20 e totais do servidor. Repetir uma consulta conserva a pesquisa aplicada e a página solicitada, independentemente do rascunho do campo.

Uma conta de aluno pode ter uma turma ativa por curso e ano letivo, mesmo que as turmas tenham anos curriculares diferentes. A escolha paginada apresenta apenas alunos disponíveis, excluindo no servidor os lugares ativos nesse curso/ano e todo o histórico na própria turma. Uma inscrição cancelada na mesma turma deve ser reativada em Canceladas. Inscrever não cria contas nem altera a capacidade do plano.

Cancelar exige confirmação e conserva o histórico; reativar exige aluno, curso e turma disponíveis. Curso/turma arquivados ocultam novas inscrições e reativações, mas permitem consultar e cancelar inscrições ativas. Um aluno indisponível continua no histórico, com indicação explícita. Se já não existir descrição acessível, apresenta-se «Aluno indisponível» e o identificador. Datas de criação/última alteração descrevem o registo atual; não se inventa uma cronologia de transições.

Um conflito 409 conserva seleção e contexto, bloqueia nova alteração e exige **Rever versão atual → Usar versão revista** antes de uma nova confirmação. A revisão faz apenas leituras. Numa criação com resultado incerto, ficam fixos o aluno e ClientId, inclusive após respostas 403/429 numa repetição. Só uma resposta bem-sucedida ou o abandono explícito liberta a tentativa. Um replay pode devolver uma inscrição já cancelada; a interface comunica esse estado e abre Canceladas. Abandonar avisa que a inscrição anterior pode existir, limpa a seleção e uma nova escolha usa outro ClientId.

Cada componente pertence a um contexto curso/turma/geração de sessão. Alterar contexto ou terminar sessão cancela leituras e invalida resultados tardios, mesmo que o transporte ignore cancelamento. As operações sobrepostas são bloqueadas; os diálogos seguem o comportamento existente de teclado, Escape e retorno de foco. A sessão usa a autorização partilhada, criando uma nova mensagem HTTP por repetição autorizada.

## API e permissões

Base: `/api/v1/academic/courses/{courseId}/classes/{classId}/enrolments`.

| Operação | Pedido |
|---|---|
| Listar | GET base, `page`, `pageSize=20`, `search`, `status=Active\|Cancelled` |
| Alunos elegíveis | GET base + `/eligible-students`, `page`, `pageSize=20`, `search` |
| Consultar | GET base + `/{id}` |
| Inscrever | POST base, `{ clientId, studentId }` |
| Cancelar / reativar | POST base + `/{id}/cancel` ou `/{id}/restore`, `{ rowVersion }` |

Somente InstitutionAdmin ativo da própria instituição, revalidado em cada operação. Teacher, Student e PlatformAdmin não acedem diretamente a esta gestão. O servidor determina instituição, autor e anos da turma; os corpos não transportam esses dados. Criação 201, replay 200; erros ProblemDetails 400/401/403/404/409/429 conforme validação, sessão, relação, conflito e limite. `AcademicEnrolmentApiClient` partilha os Contracts entre os clientes; esta entrega inclui o site, sem ecrãs nativos.

## Limites e continuidade

A jornada Chromium local usa respostas HTTP controladas com os contratos reais para verificar o cliente e a interface; não demonstra a base de dados, permissões do host ou publicação. O núcleo tem verificação HTTP/Domain/Oracle própria, documentada no relatório Task1, sem repetição nesta etapa.

Livros SaguiLearn estão incluídos nos planos sem custo adicional; catálogo, seleção por turma/disciplina/ano, leitor protegido e autorização de materiais são trabalho futuro em [library-selection.md](library-selection.md). Painéis completos de docentes/alunos e ecrãs MAUI também ficam para etapas próprias. Não há inscrições em lote, edição de contas ou transferência automática de turma neste incremento.
