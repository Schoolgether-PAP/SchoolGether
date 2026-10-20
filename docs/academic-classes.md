# Disciplinas por ano e turmas

Entrega local de 2026-10-21, após as unidades curriculares do PR #11 integrado em `develop` (`8fe24bf`). Em **Cursos**, **Turmas** abre a lista do curso e **Voltar aos cursos** regressa à lista anterior. A ação **Unidades curriculares** e os detalhes do curso continuam disponíveis. Mantém a paleta, Segoe UI e o diálogo existentes.

## Anos e seleção

**Ano curricular / escolaridade** (`StudyYear`, 1–20) identifica as matérias, como 10.º e 11.º; **ano letivo** (`AcademicYear`, anos consecutivos entre 1900 e 2199, por exemplo 2026/2027) identifica o período. As disciplinas existentes ficam **Por organizar**, sem adivinhar o ano. Podem receber um ano no editor; um ano atribuído não pode voltar a vazio. A API mantém compatibilidade: criar com ano null continua válido e atualizar com ano null/omitido preserva o ano atribuído.

Depois da primeira utilização de uma disciplina numa turma, inclusive uma ligação removida ou arquivada, o ano fica fixo. O nome e o programa continuam editáveis. O erro 409 explica em português que é necessário criar outra disciplina para outro ano, conserva o rascunho e exige revisão da versão atual antes de nova gravação.

A turma tem código, nome, ano curricular, ano letivo e entre 1 e 100 disciplinas. Curso e anos ficam imutáveis depois de criar; editar altera apenas código, nome e seleção. O código é único dentro do curso/ano curricular/ano letivo, inclusive para turmas arquivadas. O seletor consulta apenas disciplinas ativas do curso e do ano exato, com pesquisa, programas como texto escapado e páginas de 20. Disciplinas de outro ano, curso ou por organizar não são oferecidas. A seleção mantém-se entre páginas e pesquisas. Mudar o ano de uma turma nova com seleção exige escolher explicitamente entre manter o ano/seleção e limpar a seleção para o novo ano.

Os detalhes preservam as disciplinas selecionadas que entretanto foram arquivadas e assinalam esse estado. Na edição, essas referências podem ser mantidas ou removidas; não entram no seletor de novas disciplinas. Arquivar uma turma exige confirmação. Restaurar a turma ou o curso não restaura disciplinas. Curso arquivado permite apenas consulta.

## Cliente, acesso e recuperação

`AcademicClassApiClient` expõe List/Get/Create/Update/Archive/Restore nas rotas `/api/v1/academic/courses/{courseId}/classes`. A lista filtra estado, pesquisa, ano curricular e ano letivo; o total e a paginação vêm do servidor. `AcademicCourseUnitApiClient.ListForYearAsync(courseId, studyYear, page=1, search=null, ct)` consulta ativas do ano exato; `ListWithYearAsync` serve o catálogo e o método `ListAsync` anterior mantém a assinatura e comportamento. Cada tentativa autorizada usa um novo pedido/conteúdo HTTP, `IdentitySession` e `ApiResponseReader`.

Só InstitutionAdmin ativo da própria instituição pode gerir estes dados; a API revalida ator, tenant e relações. Os pedidos não transportam instituição/autor e a edição de turma não permite mudar curso/anos. As ligações removidas mantêm histórico interno. A 11.ª migração `20261020225655_AcademicClasses` acrescenta StudyYear nullable e turmas/ligações, preservando as dez anteriores e os fingerprints de criações legadas. O núcleo foi verificado e revisto separadamente no commit `1b744d94`.

Um 409 na edição/arquivo/restauro conserva os campos e a seleção e bloqueia nova mutação até **Rever versão atual** e **Usar versão revista**. Aceitar a comparação conserva o rascunho; a gravação seguinte usa a versão revista. A criação incerta congela pedido e chave mesmo após 403/429. Apenas replay/sucesso ou abandono explícito resolve o bloqueio. Não há troca automática de chave ou versão.

Os contextos e o seletor têm chaves por curso/ano; sair do contexto ou terminar a sessão cancela consultas. A geração da sessão e as verificações nos callbacks impedem aplicar respostas antigas. Pesquisa/página falhada mantém a consulta e página para **Repetir**. Busy impede escritas simultâneas. O diálogo conserva teclado, Escape e retorno do foco; a criação incerta bloqueia o fecho.

## Verificação e limites

Web Release sem avisos/erros e jornada Chromium com o site real e API controlada verificaram: disciplinas 10/11/legadas, erro de ano em uso com rascunho/revisão, isolamento do seletor e limite 0/100/101, programas escapados, seleção entre páginas, mudança de ano confirmada, pesquisa/página/retry, edição/arquivo/restauro, seleção arquivada retida, 409 e revisão explícita, criação incerta/403/429/replay/abandono, curso arquivado, respostas tardias por ano/curso/logout, 390 px, teclado/Escape/foco e ausência de overflow. Capturas desktop, mobile, seletor/editor e curso arquivado foram inspecionadas. Evidência local em `C:/PAP/artifacts/academic-classes/`; dois pontos de espera do guião foram corrigidos e a jornada retomada nos cenários pendentes, sem repetir suites do núcleo.

Esta evidência demonstra integração cliente/UI; não demonstra execução nativa ou alojamento. Não foram repetidos backend/Domain/MySQL/MAUI nem efetuados migração/publicação online. Não inclui inscrições, contagens de alunos, docentes, biblioteca ou livros. Materiais futuros terão autorização pelo contexto de turma/disciplina/ano, para que o 10.º e o 11.º possam ter matérias e livros diferentes. Próximo incremento: inscrições; ecrãs nativos depois. Revisão final do delta cliente/contratos, CI e revisão de colega precedem integração/publicação.
