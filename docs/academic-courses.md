# Catálogo institucional de cursos

Entrega local de 2026-10-20. O painel institucional inclui **Cursos**, com separadores Ativos/Arquivados, pesquisa por código ou nome e páginas de 20 resultados. Permite criar, consultar, editar, arquivar com confirmação e restaurar. Os cursos pertencem ao catálogo da instituição e são independentes do ano letivo.

Só um InstitutionAdmin ativo pode gerir cursos da própria instituição. O servidor revalida conta, papel e instituição em cada operação; Teacher, Student e PlatformAdmin não têm acesso a este incremento. O cliente partilhado `AcademicCourseApiClient` utiliza `IdentitySession.ExecuteAuthorizedAsync`, pedidos HTTP novos por tentativa e `ApiResponseReader`, sem armazenar tokens ou cursos no navegador.

## Campos e histórico

Código obrigatório de 1–30 caracteres ASCII (letras, números, ponto, hífen ou underscore; primeiro carácter alfanumérico), nome obrigatório até 200 e descrição opcional até 1000. O servidor remove espaços exteriores e normaliza o código para maiúsculas. O código continua reservado nos cursos arquivados; não existe eliminação física nem libertação de códigos históricos.

A API fica em `/api/v1/academic/courses`, com GET da lista/detalhe, POST de criação, PUT de edição e POST de `/{id}/archive` ou `/{id}/restore`. A lista aceita `page`, `pageSize`, `search` e `status=Active|Archived`. Os pedidos não recebem instituição ou autor do cliente.

## Conflitos e criação sem confirmação

Edição, arquivo e restauro enviam a versão apresentada. Um 409 conserva o rascunho e bloqueia nova mutação: **Rever versão atual** consulta os dados atuais; **Usar versão revista** confirma a comparação sem substituir o texto escrito. Só uma ação posterior envia a nova versão. Um código duplicado na criação mantém os campos editáveis e explica que o código pode estar reservado nos arquivados.

Quando uma criação falha sem confirmação (rede, resposta inválida, cancelamento enquanto a sessão continua ativa ou erro 5xx), o formulário congela o pedido inicial e o ClientId. **Repetir a mesma criação** reenvia exatamente o mesmo pedido; o replay recupera o curso existente mesmo que já tenha sido alterado ou arquivado. Fechar/Escape ficam bloqueados até resolver ou escolher **Abandonar tentativa e alterar conteúdo**. Esta decisão explícita desbloqueia o rascunho e faz a próxima submissão usar uma chave nova; a interface avisa que o curso anterior pode já existir. Não há repetição automática após 409 nem troca silenciosa de conteúdo/chave.

As operações desativam submissões simultâneas. O componente cancela pedidos ao sair e verifica a geração de sessão antes de aceitar respostas/callbacks. O diálogo nativo existente conserva navegação por teclado, Escape e retorno do foco, com a exceção explícita da tentativa incerta descrita acima.

## Verificação e limites

Núcleo, migração e cenário Oracle MySQL temporário foram verificados na Task 1, commit `f39ba36f602f30ce106b03cfa45d6a1b09b24cf8`; não foram repetidos para a interface. Esta etapa verificou Web Release (zero avisos/erros) e uma jornada Chromium controlada sobre o site real e respostas HTTP sintéticas: vazio, CRUD, duplicado, revisão explícita de 409, retry idêntico após gravação sem resposta, abandono explícito, arquivo/restauro, paginação de 20, pesquisa codificada, erro recuperável, saída com resposta tardia, largura 390, teclado/Escape/retorno do foco e ausência de overflow. Capturas desktop e mobile inspecionadas; evidência local em `C:/PAP/artifacts/academic-courses/`.

A jornada controlada prova a integração cliente/UI; não prova alojamento ou execução nativa. Não foram executados testes antigos de suporte, novos cenários MySQL ou builds MAUI nesta etapa. Sem contas pessoais, migração online ou publicação. Revisão do delta, CI e revisão do colega precedem integração/publicação.

Próximos incrementos: unidades curriculares, turmas e inscrições com ano letivo; ecrãs MAUI próprios. Não existem métricas de alunos, turmas ou unidades neste catálogo inicial.
