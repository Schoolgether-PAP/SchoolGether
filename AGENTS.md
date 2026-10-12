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
| Contas | Uma instituição por conta; criadas pelos administradores. `PlatformAdmin` é global, sem instituição |
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

## 8. Estado atual

- Feito: conta e organização no GitHub, repositório criado, solução com os 8 projetos, build local sem erros, primeiro commit em `main`.
- Concluído: Etapa 4 — integração contínua (GitHub Actions), PR #1 integrado em `develop`.
- Concluído localmente: Etapa 5 — contexto EF Core, entidade Institution, ligação privada em user-secrets e primeira migração aplicados ao MySQL local. Testes de domínio de instituições criados. A configuração Aiven permanece pendente.
- PR #2 da base de dados integrado em `develop`, confirmado no GitHub em 2026-10-12.
- Concluído localmente: Etapa 6 — Swagger/OpenAPI em desenvolvimento, rotas `/api/v1`, `ProblemDetails`, CORS e diagnósticos de funcionamento/prontidão. Testes HTTP criados e incluídos no CI. Ligação real ao MySQL e migrações confirmadas por `/api/v1/health/ready`. Ver `docs/api.md`.
- Integrado pelo PR #4: login JWT, refresh com rotação/revogação, papéis, criação de instituições/contas por administradores, filtro e guardas de escrita em `institution_member`. Credenciais e sessões são metadados globais de autenticação; dados institucionais ficam no perfil filtrado. O primeiro administrador é criado por assistente local, sem credenciais no repositório. Ver `docs/identity.md`.
- Identity tem testes HTTP com SQLite relacional isolado e um cenário automático com o provider Oracle numa instância MySQL 8 temporária. O utilizador confirmou na base local administrador global, instituição (ID 2), administrador institucional, docente/aluno, perfil, listagens/permissões e refresh/logout. O isolamento entre instituições de IDs distintos foi depois confirmado automaticamente em MySQL, sem usar nem alterar as contas existentes. Ver `scripts/Test-MySqlIdentity.ps1` e `docs/identity.md`.
- Auditoria de 2026-10-11: 18 testes de domínio e 46 da API passaram; o site compilou em Release e a base MAUI para Windows e Android em Debug. Modelo EF sem alterações pendentes; diagnósticos de funcionamento/prontidão devolveram 200 numa execução temporária da API. Estes resultados não demonstram um login funcional nos clientes. Ver `docs/revisao-etapas.md`.
- PRs #2, #3 e #4 integrados em `develop`. PR #4 (Identity) aprovado por outro elemento da equipa e integrado em 2026-10-12, commit `93462d6`. `develop` local atualizado por fast-forward; preparação da próxima entrega no ramo `feature/client-login`.
- Ajuste concluído localmente em 2026-10-12: `IdentityStore.ToAccountAsync` usa `IInstitutionDirectory`. Verificação após integrar develop: 18 testes de domínio + 48 testes da API aprovados. O cenário adicional MySQL foi executado separadamente e aprovado, com migrações reais, permissões, isolamento e sessões; a instância temporária foi removida. Na suite normal/CI esse cenário fica explicitamente ignorado, pois requer o ambiente preparado pelo script local. Não confundir esse skip com a execução MySQL aprovada. API local em 5265 respondeu 200 nos diagnósticos e 401 em perfil/listagem sem autenticação na verificação anterior.
- Por limpar quando relevante: ficheiros `Class1.cs`. As páginas Counter/Weather, navegação e dados de exemplo do site foram removidos; o exemplo WeatherForecast da API já tinha sido removido.
- Implementado localmente no ramo `feature/client-login`: entrada, próprio perfil e saída no site e na app, com gestão partilhada de sessão. Visual B, nome SchoolGether e logo/paleta escolhidos pelo utilizador; primeira versão após entrada delegada para corrigir ao seu gosto. Site sem persistência de tokens; app com refresh no SecureStorage. Sem alterações ao esquema/backend. Ver `docs/client-login.md` e especificação/plano em `docs/superpowers/`.
- Verificação de 2026-10-12: 18 testes de domínio, 48 da API e 23 dos clientes aprovados; 1 cenário MySQL explicitamente ignorado na suite normal. Site Release e app Debug Windows/Android compilados sem avisos/erros. Cenários Chromium com HTTP controlado verificaram entrada, perfil, renovação, saída, erros, teclado e 390 px. Native UI/SecureStorage real, ligação do emulador e manifesto Release gerado não foram verificados. Não confundir compilações/fixtures com execução real nas plataformas ou alojamento.
- Próximo: revisão final local, revisão visual do utilizador, envio do ramo/PR para develop quando autorizado e revisão do colega. Aiven, API HTTPS e GitHub Pages continuam pendentes; ainda não há publicação desta entrega nem CI remoto confirmado para o quinto job.

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
- Sempre que uma tarefa for concluída e verificada, comunicar o resultado e indicar imediatamente o próximo passo concreto para dar continuidade ao projeto. Distinguir o trabalho que o agente executa das ações necessárias do utilizador; assumir os testes automatizados e evitar repetições manuais desnecessárias no Swagger.
- Não mudar as decisões da secção 3 sem as discutir primeiro.
- Quando algo for ambíguo, perguntar antes de assumir.
- No desenvolvimento do site, envolver o utilizador nas escolhas de funcionamento, navegação e aparência; fazer perguntas concretas, uma de cada vez, antes de consolidar os ecrãs. Não considerar o tempo de desenvolvimento como variável de planeamento.
- Ao criar uma funcionalidade nova: módulo nas quatro camadas do backend, contratos em `Contracts`, cliente em `ApiClient`, ecrãs na app e no site, e testes.
