# Áreas de trabalho SchoolGether

Pedido aprovado: menus laterais recolhíveis, modo escuro, configuração institucional completa durante a adesão, apresentação profissional, suporte e calendário funcionais e áreas pedagógicas de professores e alunos. Cada entrega acompanha um registo em colaboracao-ia/registos e é enviada para um ramo de trabalho no GitHub.

## Regras

- Manter .NET 10, Blazor WASM, API única e EF Core/MySQL compatível TiDB. Sem serviços novos nem cobranças reais.
- Preservar permissões, isolamento por instituição e condições das subscrições existentes.
- Empresa SaguiLearn; produto SchoolGether. Português de Portugal nos recursos e na documentação.
- A adesão recolhe ano letivo e dados da instituição antes de criar atomicamente a conta, instituição e subscrição. Resumo de pagamento sem recolha de cartões; indicação explícita de que não há cobrança no passo de confirmação.
- Professores propõem eventos; InstitutionAdmin publica/aprova. Alunos consultam eventos publicados da própria escola. Sem novo papel de funcionário nesta entrega.
- Professores consultam contextos atribuídos, planificam aulas, publicam materiais e atividades; alunos consultam contextos de inscrição, materiais e atividades e entregam trabalhos. Feedback privado por aluno.
- Avaliação depende do percurso e turma, conforme investigação pedida pelo utilizador em 25/11. Não impor 0–20 nem converter percentagens em classificações oficiais automaticamente. Registos de atividades não equivalem a certificação oficial.
- Modo escuro respeita preferência do sistema e permite escolha persistida. Recolher menu no computador mantém acesso e rótulos acessíveis; no telemóvel manter menu próprio.
- Suporte SaguiLearn mantém-se distinto das avarias da escola. Não afirmar ativação online sem verificação operacional.
- Remover linguagem de teste generalizada; manter informação necessária para não sugerir cobranças ou funcionalidades inexistentes.

## Verificação

Testes focados de regras, permissões e isolamento; compilações API/Web; migrações verificadas sem usar produção como ambiente descartável. Verificação visual desktop/móvel para navegação e temas. Publicação é distinta de commit/push e só se declara depois de confirmada.
