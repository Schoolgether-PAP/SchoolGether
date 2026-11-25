# Contexto do projeto

## Produto e público

SaguiLearn é a empresa concebida na PAP de Informática de Gestão; SchoolGether é o produto. A plataforma apoia instituições de ensino, com foco no trabalho dos professores e apoio aos alunos. O projeto é demonstrativo: não há cobranças reais. A constituição jurídica e as projeções financeiras académicas não comprovam uma empresa ou atividade comercial registada.

O objetivo é facilitar a preparação e planificação de aulas, organização pedagógica, partilha de materiais dos docentes e comunicação escolar. O levantamento incluiu pesquisa e um questionário a professores. Não inventar resultados quantitativos desse questionário: as respostas não estão incluídas nesta pasta.

## Decisões aprovadas

- Sem catálogo de livros fornecidos pela empresa; os materiais serão adicionados pelos professores.
- Calendário partilhado: direção e funcionários autorizados publicam; professores propõem eventos para aprovação. As permissões detalhadas ainda precisam de especificação.
- Comunicação de avarias para a manutenção da escola é um módulo adicional, distinto do suporte da SaguiLearn.
- Planos anuais demonstrativos: Essencial 1 750 €, Profissional 2 950 €, Institucional 4 000 €, mais IVA; limites atuais de 20/50/100 professores. Alunos e responsáveis incluídos. Não alterar subscrições antigas retroativamente.
- Suporte comercial previsto por pacotes de horas. Não inventar preços de módulos ou implementar pagamentos sem nova especificação.
- Disciplinas criadas dentro do curso; programas/matérias dependem do ano de escolaridade. Cada turma seleciona as disciplinas do respetivo curso e ano. Uma matrícula por aluno, curso e ano letivo foi a regra escolhida; confirmar o estado de implementação antes de a usar.
- Materiais docentes, planificação, calendário completo e manutenção não devem aparecer como funcionalidades operacionais só por existirem numa demonstração visual.

## Arquitetura

| Área | Decisão |
|---|---|
| Plataforma | .NET 10 |
| Backend | API ASP.NET Core, monólito modular, Clean Architecture |
| Web | Blazor WebAssembly e páginas institucionais estáticas |
| App | .NET MAUI, XAML/MVVM; Windows e Android; SQLite para o objetivo de offline |
| Persistência | Modelo MySQL; EF Core e provider MySQL da Oracle |
| Demonstração online | API MonsterASP.NET; TiDB compatível com MySQL; GitHub Pages |
| Autenticação | JWT de curta duração e refresh token |
| Papéis | PlatformAdmin, InstitutionAdmin, Teacher e Student |
| Isolamento | institution_id e filtro global; validar autorização no servidor |

`Domain` não depende de outras camadas; `Application` depende do domínio; `Infrastructure` implementa persistência/serviços; `Api` expõe contratos. Web e Mobile dependem apenas dos projetos partilhados `ApiClient` e `Contracts`, nunca da persistência. Ambos usam a mesma API. Migrações por EF Core; sem alterações manuais de esquema.

Estrutura: `src/Backend/`, `src/Shared/`, `src/Clients/`, `tests/`, `docs/`. Código/identificadores em inglês; interface e documentação em português de Portugal; textos de interface nos recursos.

## Identidade e utilização

SaguiLearn nas páginas da empresa; SchoolGether no produto. Public Sans, verde floresta e ameixa como identidade; superfícies brancas/neutras nos painéis. Site público explica a empresa, depois o produto, com páginas próprias de empresa e contactos. Contacto público: saguilearn@gmail.com. Usar apenas a morada e os contactos já aprovados no site, sem inventar dados.

Pedido visual mais recente: cabeçalho e botões compactos, textos menores e bem distribuídos, sem faixa decorativa SaguiLearn; três cartões de planos verticais lado a lado no computador, empilhados no telemóvel; login neutro e navegação consistente nos painéis. Manter legibilidade, foco por teclado, estados e adaptação a 390 px. Evitar decoração genérica e métricas fictícias.
