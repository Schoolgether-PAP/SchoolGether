# Login nos clientes — plano de implementação

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development to implement this plan task-by-task. Steps use checkbox syntax for tracking.

**Goal:** Entrar, ver o próprio perfil e sair no site e na app com a API existente.

**Architecture:** Gestão de sessão independente da interface em ApiClient, usando armazenamento de refresh fornecido pelo cliente. Site Blazor com sessão em memória; app MAUI com refresh em SecureStorage. Nenhum cliente acede à base de dados.

**Tech Stack:** .NET 10, Blazor WASM, MAUI XAML/MVVM, CommunityToolkit.Mvvm, xUnit.

**Spec:** `docs/superpowers/specs/2026-10-12-client-login-design.md`.

**Estado em 2026-10-12:** implementação e verificações locais concluídas. Execução nativa, SecureStorage real, manifesto Release gerado, revisão visual, CI remoto e publicação permanecem pendentes, conforme docs/client-login.md e docs/revisao-etapas.md. Os checks abaixo registam o trabalho local e os limites documentados, sem afirmar distribuição validada.

## Global Constraints

- .NET 10, uma instituição por conta e contas criadas pelos administradores.
- Clientes continuam a depender apenas de Contracts e ApiClient.
- Não escrever palavras-passe, tokens ou respostas de autenticação em logs.
- A escolha visual é B, com nome SchoolGether e logo original; castanho, laranja, branco e cinzento claro.
- Textos em português nos recursos; código em inglês.
- Android e Windows; HTTP apenas em desenvolvimento; não desativar validação de certificados.
- Implementação local autorizada. Não fazer push, merge ou publicação automática.
- Ecrã após entrada: perfil real e saída; sem inventar módulos ou dados demonstrativos.

## Review Focus

1. Saída enquanto uma renovação está pendente: não restaurar a sessão nem o token guardado.
2. Pedidos simultâneos com token expirado: usar uma única renovação, sem reutilizar o token antigo.
3. Falha de rede: permitir recuperação e distinguir credenciais inválidas de indisponibilidade.
4. Falha de SecureStorage: não apresentar sucesso com sessão parcialmente persistida.
5. Cliente sem configuração de produção: não distribuir uma ligação HTTP de desenvolvimento como se fosse HTTPS.

### Task 1: Sessão partilhada e testes

**Files:**
- Create: `src/Shared/SchoolGether.ApiClient/Identity/IdentitySession.cs`
- Create: `src/Shared/SchoolGether.ApiClient/Identity/IRefreshTokenStore.cs`
- Create: `src/Shared/SchoolGether.ApiClient/Identity/MemoryRefreshTokenStore.cs`
- Create: `src/Shared/SchoolGether.ApiClient/Identity/SessionExpiredException.cs`
- Create: `tests/SchoolGether.ApiClient.Tests/SchoolGether.ApiClient.Tests.csproj`
- Create: `tests/SchoolGether.ApiClient.Tests/IdentitySessionTests.cs`
- Create: `tests/SchoolGether.ApiClient.Tests/SessionTestFixtures.cs`
- Modify: `SchoolGether.slnx`, `.github/workflows/ci.yml`

**Interfaces:**
- Consumes: `IdentityApiClient.LoginAsync(LoginRequest, CancellationToken)`, `RefreshAsync(RefreshRequest, CancellationToken)`, `GetMeAsync(string, CancellationToken)`, `LogoutAsync(RefreshRequest, CancellationToken)`.
- Produces: `IRefreshTokenStore.ReadAsync(CancellationToken) -> Task<string?>`, `WriteAsync(string?, CancellationToken) -> Task` (null remove).
- Produces: `IdentitySession(IdentityApiClient, IRefreshTokenStore, TimeProvider)`; `AccountResponse? Account { get; }`; `event Action? Changed`; `Task<AccountResponse> SignInAsync(LoginRequest, CancellationToken = default)`; `Task<AccountResponse?> RestoreAsync(CancellationToken = default)`; `Task<AccountResponse> GetProfileAsync(CancellationToken = default)`; `Task<bool> SignOutAsync(CancellationToken = default)` (true só se revogação remota confirmada).
- Produces: `MemoryRefreshTokenStore` for web and `SessionExpiredException` for rejected/absent authentication. UI translates errors rather than displaying internal exception text.

- [x] Escrever testes xUnit antes da implementação. Usar HttpMessageHandler controlado no limite HTTP e armazenamento em memória; não simular o próprio IdentitySession. Reutilizar versões de xUnit/Test SDK existentes.
- [x] Criar primeiro uma estrutura mínima que compile e observar falha funcional (ex.: `SignInLoadsProfileFromApi` espera perfil real em vez de estado vazio), depois implementar essa fatia.
- [x] Cobrir `SignInLoadsProfileFromApi`, `RejectedLoginDoesNotRetainAccount`, `ExpiredConcurrentRequestsRotateOnce`, `RejectedRefreshClearsSession`, `ProfileNetworkFailurePreservesRecoverableSession`, `LogoutClearsSessionWhenApiUnavailable`, `PendingRefreshCannotRestoreLoggedOutSession`, `StorageFailureDoesNotPublishAuthenticatedAccount`, `RestoreUsesSavedRefreshWithoutPassword`, `UnauthorizedProfileRetriesOnlyOnce`.
- [x] Assertar resultados observáveis: conta correta, Bearer correto no pedido, refresh novo persistido, perfil limpo após saída, número exato de pedidos quando há contrato de concorrência e repetição. Usar TaskCompletionSource para ordens de corrida, nunca sleeps como sincronização.
- [x] Implementar renovação serializada, no máximo uma repetição de perfil após 401, comparação de geração da sessão antes de publicar respostas atrasadas e armazenamento consistente com a conta. Login consulta `/me`; não confiar apenas na conta embebida na resposta de token.
- [x] Usar TimeProvider para expiração determinística. Não adicionar um handler genérico de autorização para rotas arbitrárias nesta entrega.
- [x] Executar `dotnet test tests/SchoolGether.ApiClient.Tests/SchoolGether.ApiClient.Tests.csproj -c Release`; esperado: todos aprovados. Adicionar job «Testes do cliente API» no CI com o mesmo comando.
- [x] Executar suites Domain e API existentes antes do commit; reportar separadamente o teste MySQL ignorado por falta do ambiente do script, sem alegar execução real.
- [x] Rever o diff e fazer commit apenas dos ficheiros desta tarefa: `Implementa gestão partilhada da sessão`.

### Task 2: Login B e perfil no site

**Files:**
- Create: `src/Clients/SchoolGether.Web/Identity/MemorySessionRegistration.cs`
- Create: `src/Clients/SchoolGether.Web/Resources/UiMessages.resx`
- Create: `src/Clients/SchoolGether.Web/UiMessages.cs`
- Create: `src/Clients/SchoolGether.Web/wwwroot/appsettings.json`
- Create: `src/Clients/SchoolGether.Web/wwwroot/appsettings.Development.json`
- Create: `src/Clients/SchoolGether.Web/wwwroot/images/schoolgether-logo.jpeg` (copiar original sem edição)
- Modify: `src/Clients/SchoolGether.Web/Program.cs`, `App.razor`, `_Imports.razor`, `Pages/Home.razor`, `Layout/MainLayout.razor`, `Layout/MainLayout.razor.css`, `wwwroot/css/app.css`, `wwwroot/index.html`
- Remove: `Pages/Counter.razor`, `Pages/Weather.razor`, `Layout/NavMenu.razor`, `Layout/NavMenu.razor.css`, `wwwroot/sample-data/weather.json` se deixarem de ter consumidores.
- Create: `docs/client-login.md`

**Interfaces:**
- Consumes: all Task 1 public interfaces; register scoped IdentitySession, IdentityApiClient and IRefreshTokenStore, with TimeProvider.System.
- Produces: site em `/` com formulário B quando sem sessão; perfil real e saída após entrada. Não necessita de nova rota pública ou endpoint da API.
- Configuration: `Api:BaseUrl`, Development `http://localhost:5265/`; produção sem valor deve mostrar erro de configuração localizado e nunca usar a origem dos ficheiros estáticos como API por omissão.

- [x] Antes da UI, preparar cenário de verificação de navegador que encontra formulário com etiquetas, envia conta de teste para API controlada, espera nome/papel/instituição e «Sair», confirma limpeza após saída. O cenário deve falhar no site de exemplo. Não usar credenciais pessoais.
- [x] Implementar o visual B escolhido, usando o protótipo apenas como referência de apresentação; não copiar a estrutura de comparação nem os campos readonly. Usar castanho-escuro `#36281F`, ação `#663A20`, destaque `#C87532`, branco e cinzento; campos com contorno visível, etiquetas e foco.
- [x] Login controla required/e-mail, password oculta e ação Mostrar/Ocultar, desativa envio durante pedido e limpa password após resposta. Mensagens diferentes para credenciais, rede, rate limit, sessão expirada e configuração; não mostrar exceções internas.
- [x] Perfil mostra nome/e-mail/papel traduzido/ID de instituição ou Administração da plataforma. Ação atualizar perfil usa gestão de sessão e permite testar renovação sem temporizador global. Saída limpa a interface mesmo sem rede e informa revogação não confirmada quando necessário.
- [x] Recursos incluem títulos, etiquetas, estados e mensagens; `html lang="pt-PT"`, título SchoolGether, logo original. Layout adapta-se a 390 px e teclado; não incluir registo/recuperação falsos.
- [x] Configurar origens já autorizadas `http://localhost:5227` e `https://localhost:7256`; não ampliar CORS. Documentar URLs e sessão em memória (atualizar separador exige nova entrada).
- [x] Executar build Release e cenário de navegador com contas isoladas. Usar fixtures controladas ou API em ambiente de teste, sem alterar base real. Confirmar login inválido, perfil, logout e rede; captar e inspecionar imagens desktop/mobile e verificar ausência de overflow.
- [x] Rever o diff e commit: `Implementa login e perfil no site`.

### Task 3: Login e perfil na app Android/Windows

**Files:**
- Create: `src/Clients/SchoolGether.Mobile/Identity/SecureRefreshTokenStore.cs`
- Create: `src/Clients/SchoolGether.Mobile/Identity/IdentityViewModel.cs`
- Create: `src/Clients/SchoolGether.Mobile/Resources/Strings/UiMessages.resx`
- Create: `src/Clients/SchoolGether.Mobile/UiMessages.cs`
- Create: `src/Clients/SchoolGether.Mobile/Resources/Images/schoolgether_logo.jpeg` (copiar original sem edição)
- Modify: `SchoolGether.Mobile.csproj`, `MauiProgram.cs`, `App.xaml.cs`, `AppShell.xaml`, `AppShell.xaml.cs`, `MainPage.xaml`, `MainPage.xaml.cs`, `Resources/Styles/Colors.xaml`, `Platforms/Android/AndroidManifest.xml` dentro do projeto Mobile.
- Create: `tests/SchoolGether.ApiClient.Tests/IdentityViewModelTests.cs` (ligar ficheiro MVVM puro ao projeto de testes ou refatorar só o necessário; não referenciar targets MAUI nos testes .NET).
- Modify: `docs/client-login.md`

**Interfaces:**
- Consumes: Task 1 IdentitySession. `SecureRefreshTokenStore : IRefreshTokenStore`, usando SecureStorage.Default com chave `schoolgether.refresh-token`.
- Produces: IdentityViewModel(IdentitySession) com propriedades/AsyncRelayCommands para email, password, busy/error, mostrar password, login, restore, refresh profile e logout; Account exposta através da sessão.
- Configuration: Debug Windows `http://localhost:5265/`, Debug Android emulator `http://10.0.2.2:5265/`. Release recebe `SchoolGetherApiBaseUrl` por propriedade de build HTTPS; erro explícito se ausente/inválida. HTTP Android permitido só em Debug, sem bypass TLS.

- [x] Adicionar CommunityToolkit.Mvvm com versão estável compatível verificada na fonte oficial; justificar por requisito existente AGENTS.md. Windows/Android são os únicos TargetFrameworks suportados.
- [x] Escrever testes de comportamento do ViewModel antes da implementação: `LoginPublishesProfile`, `InvalidLoginShowsError`, `ConcurrentSubmitIsIgnored`, `LogoutClearsVisibleProfile`, `RestoreNetworkFailureOffersRetry`. Usar sessão real com limite HTTP controlado, não mocks do ViewModel.
- [x] Implementar armazenamento protegido e comandos MVVM, garantir limpeza de password e atualização de propriedades no thread UI pelo consumidor MAUI. Tratar falha do armazenamento sem afirmar entrada bem-sucedida.
- [x] Implementar XAML nativo com logo, formulário e perfil, textos de recursos, SemanticProperties, alvos 48 e redimensionamento. Reutilizar a hierarquia de B em ecrã largo; uma coluna no telemóvel. Sem criar WebView para reutilizar o site.
- [x] Arranque tenta restore uma vez; falha de rede permite tentar novamente. Navegação após saída não mostra perfil antigo. Falhas de configuração têm mensagem utilizável, sem segredos.
- [x] Executar testes, `dotnet build src/Clients/SchoolGether.Mobile/SchoolGether.Mobile.csproj -c Debug -f net10.0-windows10.0.19041.0` e equivalente `-f net10.0-android`; não propagar TargetFrameworks globalmente aos projetos partilhados.
- [x] Confirmar HTTP desativado em Release e URL HTTPS obrigatória. Documentar arranque/localhost do emulador. Registar execução em Windows/Android disponível separadamente de compilação; não abrir firewall ou instalar no dispositivo pessoal sem necessidade.
- [x] Rever diff e commit: `Implementa login e perfil na app`.

### Task 4: Verificação integrada e documentação

**Files:** `AGENTS.md`, `docs/revisao-etapas.md`, `docs/client-login.md`, `README.md`, `docs/ci.md`; eventuais correções encontradas pertencem às tarefas responsáveis.

- [x] Executar suites de Domain, API e ApiClient; builds Site Release e MAUI Debug Windows/Android. Não usar `dotnet test` da solução inteira se provocar restore de workloads não suportados.
- [x] Confirmar que cliente inválido não expõe tokens, passwords ou segredos em HTML, logs e diff. Rever URLs públicas e callbacks eventuais sem publicar nada.
- [x] Pedir revisão independente de requisitos e qualidade do ramo completo; corrigir problemas materiais e repetir apenas verificações afetadas.
- [x] Documentar evidências reais e limitações (emulador/dispositivo/hosting), atualizar o estado atual sem confundir mockups, build e login real.
- [x] Commit de documentação validada. Preparar o próximo passo de envio/PR e explicar a revisão do colega; não publicar automaticamente.

## Revisão do plano

Tarefas 2 e 3 consomem exatamente o contrato da tarefa 1. Cada um dos cinco
riscos acima tem um cenário na tarefa responsável. Apenas o contexto dos
clientes é alterado; Identity/MySQL e respetivo esquema não precisam de mudar.
As preferências novas do utilizador serão incorporadas sem inventar requisitos
retroativos para a proposta B já escolhida.
