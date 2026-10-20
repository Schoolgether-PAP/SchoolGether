# Cursos da instituição — primeira entrega académica

## Intenção e âmbito
O utilizador pediu continuação após integrar o painel institucional e delegou uma interface profissional, simples e fácil de navegar. Escolha do agente: começar por um catálogo de cursos gerido pelo InstitutionAdmin da própria instituição. Preservar a stack, marca, sessões e painel existentes. Sem publicar nem usar contas/base online nesta entrega.
Cursos são catálogo institucional, não cópias por ano letivo; turmas, unidades curriculares e inscrições com ano letivo ficam para incrementos seguintes. Teacher, Student e PlatformAdmin não ganham acesso a este módulo inicial; acesso assistido global é trabalho separado.

## Dados e comportamento
- Course: Id inteiro, InstitutionId, ClientId(Guid), Code, Name, Description opcional, IsActive, RowVersion, CreatedAt/UpdatedAt UTC, CreatedBy, DeletedAt e CreationFingerprint interno imutável. Estado inicial ativo, versão1.
- Código1..30 caracteres ASCII letras/dígitos/ponto/hífen/underscore; primeiro carácter alfanumérico. Trim+maiúsculas no servidor. Único por instituição, incluindo cursos arquivados. Nome trim obrigatório até200; descrição trim opcional até1000 (branca=null). Não expor fingerprint.
- Criar, pesquisar por código/nome, consultar, editar, arquivar com confirmação e restaurar. Nenhuma eliminação física ou perda de histórico. Editar curso arquivado é recusado; restaurar permite voltar a editar.
- Lista paginada20 (máximo100), ordemId descendente, pesquisa até150, separadores Ativos/Arquivados e total calculados no servidor. Sem disciplinas/alunos/indicadores fictícios.
- Edição/arquivo/restauro exigem RowVersion do servidor; alterações confirmadas avançam a versão, conflito409 preserva o rascunho e exige revisão explícita. Não repetir mutações silenciosamente com uma versão nova.
- Criação tem ClientId estável nas repetições incertas. Mesma chave, autor e payload inicial normalizado devolvem o curso existente sem nova gravação/auditoria, mesmo editado/arquivado entretanto. Chave com payload/autor diferente devolve409. Guardar SHA256 de representação canónica dos campos iniciais; unicidade InstitutionId+ClientId. A alteração de conteúdo após resultado incerto exige decisão explícita/nova chave; não reenviar automaticamente.

## Autorização, persistência e auditoria
Revalidar conta/role/instituição via IIdentityStore a cada operação; conta/instituição suspensa não pode usar o módulo. Sem confiar em institutionId/ator do corpo. DTOs rejeitam propriedades desconhecidas.401 sessão inválida,403 papel sem acesso,404 ID fora do âmbito/desconhecido,409 versão/código/chave duplicados,400 validação.
Filtro globalEF por instituição e papel, mais guarda de escrita Academic com escopo autorizado interno ligado à transação e à referência exata do curso. Não aceitar troca de tenant/autor/ClientId/fingerprint, escrita noutro módulo nem alterações fora de escopo. Não alargar os bypasses de Support/Onboarding. Unicidade por índice e concorrência por tokenEF; não usar só verificações prévias.
MigraçãoEF AcademicCourses, FKinstituição restritiva e índices únicos (InstitutionId,Code)/(InstitutionId,ClientId). Criar/editar/arquivar/restaurar e auditoria na mesma transação; falha não deixa gravação parcial. Recusas relevantes com auditoria independente segura, sem corpo/query/tokens. Reutilizar IAuditWriter e alvo estável academic.course:ClientId para criação, sem depender de Id ainda0. Contratos e clientes não recebem entidadesEF.

## Interface
Entrada Cursos no painel da instituição, lista/pesquisa/paginação, Novo curso, formulário/detalhe e ações de editar/arquivar/restaurar. Reutilizar diálogos acessíveis existentes e recursos portugueses. Campos curtos, estados vazios com ação, falhas recuperáveis, progresso/bloqueio contra submissão simultânea. Arquivo com confirmação;409 não perde texto. Cancelar leituras/callbacks ao sair/trocar conta; não guardar dados/tokens no storage do navegador. Desktop/390px, teclado/Escape/foco44px, paleta e SegoeUI atuais.

## Verificação necessária
Só o que afeta esta entrega: regras de domínio/HTTP do novo módulo (validação, papéis/tenant, CRUD/arquivo/restauro, duplicados/replay, versões, paginação, guardas e auditoria atómica); cenário OracleMySQL temporário para migração8→9/preservação e índices/concorrência. APIRelease e EFmodelo/migração. Depois clienteWeb e uma jornada Chromiumcontrolada para lista/criação/edição/409/resultado incerto/arquivo/restauro/erros/menu390/saída tardia, com capturas. Sem repetir suites de suporte, cenáriosMySQL anteriores, backups ou MAUI inalterados. CI obrigatório do repositório permanece normal.
