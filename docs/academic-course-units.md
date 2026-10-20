# Unidades curriculares por curso

Unidades curriculares do PR #11 integrado em `develop` (`8fe24bf`). Em **Cursos**, a ação **Unidades curriculares** abre o catálogo desse curso; **Voltar aos cursos** regressa à lista anterior. O código e o nome do curso mantêm o contexto visível, sem um novo menu global. O incremento local de 2026-10-21 acrescenta ano curricular e turmas; ver [Disciplinas por ano e turmas](academic-classes.md).

Cada disciplina pertence a um único curso e tem código, nome e programa próprios. A lista tem pesquisa por código/nome, separadores Ativas/Arquivadas e páginas de 20 resultados. Permite criar, consultar o programa, editar, arquivar com confirmação e restaurar. Não inclui ECTS, semestres, docentes, turmas, inscrições, contagens fictícias ou dados de exemplo.

## Regras e acesso

O código tem 1–30 caracteres ASCII: começa por letra/número e admite também ponto, hífen e underscore. O servidor remove espaços exteriores e normaliza maiúsculas. O código é único no curso, mesmo arquivado; pode existir noutro curso. Nome obrigatório até 200 caracteres; programa opcional até 5000, apresentado como texto simples escapado. A disciplina nunca muda de curso ou instituição.

Só um InstitutionAdmin ativo gere os dados da própria instituição; o servidor revalida identidade, papel, instituição e relação entre curso/unidade em cada operação. Teacher, Student e PlatformAdmin não têm acesso direto. Curso arquivado permite consultar todas as unidades e os programas, mas impede criar, editar, arquivar ou restaurar unidades. Restaurar o curso conserva o estado individual das unidades.

As rotas são `/api/v1/academic/courses/{courseId}/units`: GET lista, POST criar, GET/PUT `/{unitId}` e POST `/{unitId}/archive|restore`. O corpo não aceita curso, instituição ou autor. `AcademicCourseUnitApiClient` utiliza `IdentitySession.ExecuteAuthorizedAsync`, um pedido HTTP novo por tentativa e `ApiResponseReader`.

## Conflitos e recuperação

Um 409 na edição/arquivo/restauro conserva o rascunho e bloqueia novas mutações. **Rever versão atual** lê a unidade e o curso; **Usar versão revista** confirma a comparação sem substituir o rascunho. Só uma submissão posterior usa a versão revista. Um primeiro 409 definitivo na criação mantém os campos editáveis.

Uma criação sem confirmação congela o payload e o ClientId. **Repetir a mesma criação** mantém ambos, inclusive depois de uma recusa 403 ou 429. O replay pode recuperar a criação mesmo após arquivo do curso. Apenas sucesso ou **Abandonar tentativa e alterar conteúdo** resolve esse bloqueio; o abandono avisa que a unidade anterior pode já existir. Não há troca silenciosa de chave nem repetição automática com uma versão nova.

O contexto do curso e os diálogos usam chaves de componente. Sair do contexto, terminar a sessão ou destruir o componente cancela os pedidos; a geração de sessão impede aceitar respostas tardias. As mutações não se sobrepõem. Pesquisa/paginação com falha conservam a consulta e a página solicitadas para **Repetir**. O diálogo existente conserva teclado, Escape e retorno do foco; fechar fica bloqueado durante escrita ou criação incerta.

Cada alteração efetiva de unidade avança também a versão do curso pai na mesma transação da unidade e auditoria. Isto serializa alterações com o arquivo do curso e pode exigir rever um formulário de curso que já estivesse aberto. Replay não altera o pai. A 10.ª migração, `20261020092303_AcademicCourseUnits`, acrescenta a tabela e FK composta de instituição/curso, preservando as nove migrações anteriores.

## Verificação e limites

O núcleo, autorização, transações e migração foram verificados na Task 1 (`23f3c9d`), incluindo Domain/HTTP focados, API Release, modelo EF e Oracle MySQL temporário de nove para dez migrações. Não foram repetidos na etapa da interface.

A Task 2 verificou Web Release, sem avisos/erros, e uma jornada Chromium com o site real e respostas HTTP controladas: contexto entre cursos, programa escapado, CRUD, duplicado 409, revisão explícita, criação incerta seguida de 403/429/replay/abandono, curso arquivado, restauro independente, pesquisa/página/retry, respostas tardias no contexto e logout, 390 px, teclado/Escape/foco e ausência de overflow. Capturas desktop, mobile, editor e curso arquivado foram inspecionadas. Evidência local: `C:/PAP/artifacts/academic-course-units/`.

Esta jornada demonstra integração cliente/UI, sem provar alojamento ou execução nativa. Sem repetição de suites antigas, builds MAUI, contas pessoais, migração online ou publicação. Revisão do delta, CI e revisão de colega precedem integração/publicação. Próximos incrementos: turmas e inscrições; ecrãs MAUI depois.
