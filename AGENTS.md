# AGENTS.md — SchoolGether

Contexto do projeto para agentes de código (Codex e outros). Ler antes de alterar código.

## 1. Resumo

**SchoolGether** (antes SchoolTogether) é uma plataforma educativa para instituições de ensino, com foco em faculdades e politécnicos: **app móvel + site + API partilhada**, todos sobre a mesma base de dados.

Identidade confirmada pelo utilizador em 2026-10-13: **SaguiLearn é a empresa que desenvolve e disponibiliza o produto SchoolGether**. Apresentar a empresa nas áreas institucionais/Sobre nós e conservar SchoolGether como nome da aplicação. Isto não confirma constituição jurídica, firma registada, NIF ou CAE oficialmente atribuídos; os textos de objetivo/objeto social e enquadramentos CAE são propostas para o projeto.

- Projeto da PAP (Técnico de Informática de Gestão), equipa de 3 estudantes, a aprender enquanto desenvolve.
- Sem objetivo comercial por agora: o objetivo é um **produto funcional, demonstrável e bem estruturado**.
- Prioridades: código claro, simples e bem explicado. Preferir soluções simples a soluções "espertas".

## 2. Funcionalidades (MVP da PAP)

- Login e perfis (administrador da instituição, docente, aluno), com várias instituições.
- Instituições, cursos, unidades curriculares e inscrições.
- Biblioteca: materiais próprios e livros de exemplo (editoras parceiras só em demonstração).
- Testes e atividades. **O docente define a avaliação, as regras e os pontos por atividade**, dentro de limites definidos pela instituição.
- Competências com níveis de domínio, gamificação (XP, níveis, medalhas, sequências), portefólio e credenciais.
- Avaliação dos materiais pelos alunos.
- Stand: partilha gratuita de materiais entre instituições.
- Modo offline na app (primeiro só leitura de materiais descarregados) e acessibilidade (WCAG 2.2 AA). Requisito de 2026-10-14: materiais protegidos devem permitir leitura autorizada dentro da app, sem disponibilizar um ficheiro comum na pasta Downloads nem exportação/partilha livre. Todos os materiais disponibilizados pela SaguiLearn/SchoolGether são protegidos; nos materiais disponibilizados pelo professor, ele escolhe protegido ou não protegido, respeitando os direitos existentes. Não protegido não significa público nem comercialização autorizada. A referência Netflix exprime a experiência pretendida, não uma equivalência técnica de DRM. Ver `docs/offline-rights.md`; arquitetura, formatos e regras de licença ainda por definir.
- Site com as mesmas funcionalidades da app.

Fora do âmbito por agora: iOS, tutor com IA, notificações push, LTI/Moodle, acordos reais com editoras, faturação real.

## 3. Stack e decisões fixas (não alterar sem discussão)

| Tema | Decisão |
|---|---|
| Versão | .NET 10 em todos os projetos |
| API | ASP.NET Core 10 Web API (controllers), **API única** para app e site |
| Arquitetura | Clean Architecture, monólito modular (um módulo por área funcional) |
| App | .NET MAUI nativo (XAML + MVVM), Android e Windows; SQLite local para offline |
| Site | Blazor WebAssembly standalone, publicado como ficheiros estáticos no GitHub Pages (sem renderização no servidor) |
| Base de dados | MySQL 8; EF Core 10 com o provider da Oracle (`MySql.EntityFrameworkCore` 10.x). Não usar MariaDB/XAMPP |
| Chaves | Inteiros auto-incrementais; coluna `client_id` (GUID) nas entidades criadas offline |
| Ficheiros | Interface `IFileStorage`, implementação em disco local |
| Autenticação | JWT de curta duração + refresh token; papéis `PlatformAdmin`, `InstitutionAdmin`, `Teacher`, `Student` |
| Multi-instituição | `institution_id` em todas as tabelas de dados de cliente + filtro global no EF Core |
| Contas | Uma instituição por conta. Professores/alunos e restantes responsáveis são criados pelos administradores. Implementado localmente em 2026-10-15: adesão pública cria apenas a nova instituição e o primeiro `InstitutionAdmin`, com registo desativado por omissão e publicação dependente de revisão/CI. `PlatformAdmin` é global, sem instituição |
| Alojamento (demonstração) | API no MonsterASP.NET (gratuito), MySQL no Aiven (gratuito), site no GitHub Pages |
| Pontos e regras | Tabelas `activity`, `grading_rule` (JSON versionado) e `reward`; `xp_event` guarda a versão da regra usada |

## 4. Estrutura da solução

```
SchoolGether/
├── src/
│   ├── Backend/
│   │   ├── SchoolGether.Domain/          # entidades, regras, eventos de domínio
│   │   ├── SchoolGether.Application/     # casos de uso, validações, interfaces
│   │   ├── SchoolGether.Infrastructure/  # EF Core + MySQL, ficheiros, e-mail, login externo
│   │   └── SchoolGether.Api/             # endpoints, autenticação, middleware, Swagger
│   ├── Shared/
│   │   ├── SchoolGether.Contracts/       # DTOs, enums e constantes partilhados
│   │   └── SchoolGether.ApiClient/       # clientes HTTP tipados (app e site)
│   └── Clients/
│       ├── SchoolGether.Mobile/          # .NET MAUI
│       └── SchoolGether.Web/             # Blazor WebAssembly
├── tests/                                # Domain, Api.IntegrationTests e ApiClient.Tests; Application por criar
├── docs/                                 # documentação e decisões (ADR)
└── SchoolGether.slnx
```

Dentro de cada projeto do backend, organizar o código **por módulo** (por exemplo `Library/`, `Assessments/`), não por tipo técnico.

### Regras de dependência

- `Domain` não depende de nada.
- `Application` depende de `Domain`.
- `Infrastructure` depende de `Application` e `Domain`.
- `Api` depende de `Application`, `Infrastructure` (só para registar serviços) e `Contracts`.
- `Contracts` não depende de nada.
- `ApiClient` depende de `Contracts`.
- `Mobile` e `Web` dependem **só** de `ApiClient` e `Contracts`. **Nunca** referenciam Domain, Application ou Infrastructure.
- Os clientes nunca falam com a base de dados: tudo passa pela API.
- Um módulo não lê as tabelas de outro diretamente: usa a interface do outro módulo ou reage a eventos.

### Módulos

Identity, Institutions, Billing, Academic, Library, Assessments, Competencies, Gamification, Portfolio, Stand, Reviews, Integrations, Notifications, Sync, Audit. Cada módulo regista-se com `AddXModule()`.

## 5. Convenções de código

- Código, nomes de tabelas e colunas em **inglês** (`snake_case` na base de dados); interface e documentação em **português**, com os textos em ficheiros de recursos.
- Colunas comuns: `id`, `created_at`, `updated_at`, `created_by`, `deleted_at` (eliminação lógica), `row_version`.
- API REST em JSON com versão no caminho (`/api/v1/...`), paginação em listas, erros em `ProblemDetails`, validação de entrada em todos os pedidos.
- DTOs ficam em `Contracts`. Nunca devolver entidades da base de dados nos endpoints.
- Sem lógica de negócio nos controllers: ficam em `Application` e `Domain`.
- Nullable ativo, `async` com `CancellationToken` nas operações de I/O.
- MAUI: MVVM com CommunityToolkit.Mvvm, `SemanticProperties` em todos os controlos interativos, contraste mínimo 4,5:1, alvos de toque de 44 a 48 px.
- Migrações sempre por EF Core, nunca alterações manuais à base de dados.
- Testes: xUnit; testes unitários do domínio e da aplicação (regras de competências e de pontos), testes de integração da API, e um teste automático de **isolamento entre instituições**.

## 6. Git e segurança

- Ramos: `main` (estável, protegido), `develop`, `feature/<modulo>-<descricao>`. Um pull request por tarefa, com o CI verde e revisão de outro elemento.
- Mensagens de commit curtas, em português.
- **O repositório é público. Nunca fazer commit de segredos**: `appsettings.Production.json`, ficheiros `.PublishSettings`, cadeias de ligação, chaves JWT. Em desenvolvimento usar *user-secrets*; em demonstração, variáveis de ambiente no servidor.
- Não adicionar dependências novas sem justificar; preferir o que já está no .NET.

## 7. Comandos úteis

```
dotnet build
dotnet build src/Backend/SchoolGether.Api/SchoolGether.Api.csproj
dotnet build src/Clients/SchoolGether.Web/SchoolGether.Web.csproj
dotnet test
dotnet run --project src/Backend/SchoolGether.Api
```

- A app MAUI só compila em Windows (precisa dos componentes MAUI e do SDK Android). O CI no GitHub compila a API e o site em Ubuntu.
- O `dotnet new sln` cria `.slnx` (formato novo do .NET 10).

## 8. Estado atual e continuidade

- Empresa: SaguiLearn; produto: SchoolGether. Identidade visual B e paleta da logo aprovadas; referência PHC GO. Dashboard individual por administrador, com dados reais e preferências persistidas.
- Entregues: Identity (login/refresh/logout/papéis/isolamento), painel global master/permissões/instituições/auditoria e adesão pública com configuração institucional e gestão inicial de Teacher/Student. Perfis atuais não substituem painéis académicos completos.
- Demonstração publicada: https://schoolgether-pap.github.io/SchoolGether/ e https://schoolgether-api.runasp.net/. PR6 integrado em develop (7f9354a); API/site publicados em 2026-10-15, seis migrações no Aiven. Onboarding ativado só na configuração privada, desativado por omissão no código. Segredos fora do Git. Evidência operacional em docs/deployment.md e docs/public-onboarding.md.
- Planos fictícios: 59/149/299 € por mês; capacidade de 100/350/1150 contas. Sem cobranças reais.
- PR #8 do suporte integrado em develop (5eb7305); CI do head revisto aprovado. Ativação online ainda depende dos requisitos operacionais. PR #9 do painel institucional integrado; visual atual, configuração e gestão inicial de utilizadores são a referência para os módulos académicos.
- Catálogo de cursos: PR #10 integrado em develop (`df42c406`), sem nova publicação. Unidades curriculares por curso implementadas localmente no backend/contratos/cliente/site, com programa próprio, pesquisa/paginação, arquivo/restauro e consulta sob curso arquivado. InstitutionAdmin gere só a própria instituição; UI conserva rascunhos em 409/revisão explícita e congela pedido/chave após criação incerta, inclusive retry403/429. Ver docs/academic-courses.md e docs/academic-course-units.md. Próximos incrementos: turmas e inscrições; ecrãs MAUI depois. Revisão/CI precedem integração e publicação.
- Suporte: Teacher abre/vê os próprios pedidos; InstitutionAdmin abre/vê os da sua instituição; Student sem acesso. Administradores globais dependem de support.read/support.manage (só master delega). Anexos privados, notas internas separadas, chat em tempo real, fecho definitivo e histórico. Support desativado por omissão; publicação só após CI/revisão e validações operacionais.
- Próxima sequência aprovada: rever/integrar unidades curriculares, avançar turmas e inscrições e desenvolver painéis completos de professores/alunos. Materiais/atividades, competências/gamificação/portefólio e offline em fases posteriores.
- Limites de evidência: compilações MAUI não demonstram execução nativa; testes HTTP controlados não demonstram alojamento; testes MySQL exigem ambiente temporário explícito. Nunca usar contas/base pessoais nas suites.
- Verificação local de suporte em 2026-10-17: oito migrações no Oracle MySQL temporário, preservação das seis publicadas e cinco cenários MySQL executados separadamente; suites locais, API/Web Release, MAUI Windows/Android Debug e modelo EF verificados. Dois contextos Chromium com API/MySQL reais verificaram LongPolling, reconexão/recuperação de fecho, mensagens e anexos privados; backup SQL+ficheiros com hashes, restauro isolado e cifragem local ensaiados. Pacote privado com Support desativado e runtime autocontido 10.0.13; runtime global local 10.0.12, alojamento por verificar. Nenhuma migração/publicação online nesta etapa. App, e-mail/push, sanções, faturação real e acesso assistido fora do âmbito; ver docs/deployment.md para requisitos operacionais.
- Detalhes históricos e verificações anteriores ficam em docs/identity.md, docs/client-login.md, docs/platform-admin.md, docs/public-onboarding.md, docs/deployment.md e histórico Git; não duplicar cronologias aqui.

## 9. Como trabalhar neste projeto

- A equipa está a aprender: explicar as decisões, fazer alterações pequenas e fáceis de rever.
- Sempre que uma tarefa for concluída e verificada, comunicar o resultado e indicar imediatamente o próximo passo concreto para dar continuidade ao projeto. Distinguir o trabalho que o agente executa das ações necessárias do utilizador; assumir os testes automatizados e evitar repetições manuais desnecessárias no Swagger.
- Não mudar as decisões da secção 3 sem as discutir primeiro.
- Quando algo for ambíguo, perguntar antes de assumir.
- No desenvolvimento do site, envolver o utilizador nas escolhas de funcionamento, navegação e aparência; fazer perguntas concretas, uma de cada vez, antes de consolidar os ecrãs. Não considerar o tempo de desenvolvimento como variável de planeamento.
- Preferência do utilizador em 2026-10-20: executar apenas os testes necessários às alterações e aos riscos concretos. Não repetir suites amplas ou builds de plataformas não afetadas; verificar autorização/isolamento e migrações quando tocados. Protótipos visuais reversíveis dispensam testes automatizados.
- Ao criar uma funcionalidade nova: módulo nas quatro camadas do backend, contratos em `Contracts`, cliente em `ApiClient`, ecrãs na app e no site, e testes.
