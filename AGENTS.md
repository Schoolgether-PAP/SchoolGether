# AGENTS.md — SchoolGether

Contexto do projeto para agentes de código (Codex e outros). Ler antes de alterar código.

## 1. Resumo

**SchoolGether** (antes SchoolTogether) é uma plataforma educativa para instituições de ensino, com foco em faculdades e politécnicos: **app móvel + site + API partilhada**, todos sobre a mesma base de dados.

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
- Modo offline na app (primeiro só leitura de materiais descarregados) e acessibilidade (WCAG 2.2 AA).
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
├── tests/                                # Domain criado; Application e Api.IntegrationTests por criar
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

## 8. Estado atual

- Feito: conta e organização no GitHub, repositório criado, solução com os 8 projetos, build local sem erros, primeiro commit em `main`.
- Em curso: Etapa 4 — integração contínua (GitHub Actions) via pull request para `develop`.
- Preparado localmente: Etapa 5 — contexto EF Core, entidade Institution, primeira migração e assistente MySQL; falta configurar a ligação privada e aplicar a migração local. Testes de domínio de instituições criados.
- Por limpar: ficheiros de exemplo dos modelos (`WeatherForecast`, `Class1.cs`, páginas `Counter` e `Weather` do site).

### Próximas etapas da Fase 0

5. Base de dados: MySQL local, Aiven e primeira migração.
6. API base: Swagger, `ProblemDetails`, versão `/api/v1`, CORS.
7. Identity: login, papéis e isolamento por instituição.
8. Login na app e no site, e publicação (MonsterASP.NET e GitHub Pages).

### Fases seguintes

- Fase 1: instituições, cursos, materiais, testes e pontos definidos pelo docente.
- Fase 2: competências, gamificação, avaliação de materiais e portefólio.
- Fase 3: stand, planos, integrações, offline e livros de exemplo.
- Fase 4: acessibilidade, testes de usabilidade, relatório e apresentação.

## 9. Como trabalhar neste projeto

- A equipa está a aprender: explicar as decisões, fazer alterações pequenas e fáceis de rever.
- Não mudar as decisões da secção 3 sem as discutir primeiro.
- Quando algo for ambíguo, perguntar antes de assumir.
- Ao criar uma funcionalidade nova: módulo nas quatro camadas do backend, contratos em `Contracts`, cliente em `ApiClient`, ecrãs na app e no site, e testes.
