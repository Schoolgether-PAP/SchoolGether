# Revisão das etapas e próximos passos

Revisão de 2026-10-11, ramo `feature/identity`, código em `427b9c8`.
Organização por dependências e critérios de conclusão, sem estimativas de tempo.

## Resultado

A base local está funcional, mas as etapas anteriores ainda não estão todas
encerradas. Faltam a integração dos PRs de base e a confirmação do fluxo
institucional no MySQL. O ajuste de arquitetura foi concluído localmente em
2026-10-12, conforme o acompanhamento abaixo.
Login na app/site e publicação pertencem à etapa seguinte.

| Área | Evidência | Estado |
|---|---|---|
| SDK e estrutura | .NET 10.0.401; projetos e referências presentes | Preparados |
| CI | Últimas execuções dos ramos dos PRs #2/#3 concluídas com sucesso | Funciona para o código enviado; Identity ainda sem CI remoto |
| MySQL local | Duas migrações aplicadas; modelo EF coincide com a última migração | Confirmado |
| API base | Compilação em Release através dos testes; diagnósticos 200 | Confirmado |
| Testes | 18 do domínio + 46 da API, sem falhas ou testes ignorados | Confirmado |
| Identity | JWT, papéis, isolamento, rotação/revogação cobertos por testes SQLite | Implementado; demonstração MySQL ainda incompleta |
| Site | Release sem erros ou avisos | Base compila; interface de login ainda por desenvolver |
| App Windows | Debug sem erros ou avisos | Base compila; interface de login ainda por desenvolver |
| App Android | Debug sem erros ou avisos | Base compila; interface de login ainda por desenvolver |
| Alojamento | Aiven, API pública e site publicado ainda por configurar/confirmar | Pendente para a demonstração alojada |

O primeiro administrador global, o seu login e a criação da instituição de ID 2
foram confirmados pelo utilizador. Não foi repetido nenhum login com credenciais
reais nesta auditoria. O erro 409 apresentado na criação de uma conta institucional
corresponde a um e-mail já utilizado, conforme o comportamento documentado.

A API na porta habitual 5265 não respondeu durante a revisão. Para verificar o
arranque e a base real, foi iniciada temporariamente uma instância na porta 5279;
`/api/v1/health` e `/api/v1/health/ready` devolveram 200/Healthy. Essa instância foi
encerrada no fim, sem criar ou alterar contas.

## Revisão das normas

Encontrada uma violação de prioridade P2: em
`src/Backend/SchoolGether.Infrastructure/Identity/IdentityStore.cs`,
`ToAccountAsync` consulta diretamente `db.Institutions`. O AGENTS.md exige que um
módulo use a interface do outro. Já existe `IInstitutionDirectory.ExistsAsync`;
o ajuste é usar essa interface para verificar se a instituição está ativa.
Não foi alterado código de produto nesta revisão.

### Acompanhamento de 2026-10-12

A consulta direta foi substituída por `IInstitutionDirectory.ExistsAsync`, sem
alterar o esquema nem acrescentar dependências. Dois testes HTTP novos verificam
que login e refresh rejeitam uma instituição indisponível segundo o módulo
Institutions, mesmo com a linha ainda presente na base, e mantêm o acesso da outra
instituição. Antes da correção, ambos falharam com 200 em vez de 401; depois,
passaram na suite completa: **18 testes de domínio + 48 da API**, sem falhas nem
testes ignorados. O EF confirmou ausência de alterações pendentes no modelo.

O utilizador confirmou que ainda não terminou a criação/login de InstitutionAdmin.
O guia `identity.md` inclui agora os passos para retomar esse teste sem recriar a
instituição de ID 2. A tabela inicial e a consulta remota acima referem-se à
auditoria de 2026-10-11; os builds dos clientes não foram repetidos nesta correção
exclusiva do backend. O fluxo MySQL e a revisão/integração no GitHub continuam
pendentes.

O projeto MAUI conserva alvos iOS/MacCatalyst do modelo inicial, embora as
plataformas decididas sejam Android e Windows. Alinhar essa configuração na
preparação da etapa 8. Os exemplos Counter/Weather/Class1 também permanecem para
limpeza nessa etapa.

## Revisão dos requisitos

### Confirmações posteriores no ambiente local — 2026-10-12

O utilizador confirmou criação/login do administrador da instituição 2, criação
de docente/aluno, perfil e listagem com o administrador, listagem recusada (403)
para docente/aluno e renovação/logout (200/204/401). As capturas seguintes
confirmam perfil e listagem (200) de outro administrador também associado à
instituição 2. O nome “instituição B” é o nome da conta; esse caso ainda não
comprova isolamento entre duas instituições diferentes.

Foram executados novamente os 18 testes de domínio e 48 da API, todos aprovados.
Na API real em `localhost:5265`, funcionamento e prontidão devolveram 200;
perfil e listagem sem autenticação devolveram 401. Estas verificações não criaram
contas nem alteraram dados no MySQL. A suite HTTP continua a usar SQLite isolado;
uma execução automática completa dos mesmos fluxos com o provider MySQL exige
um ambiente de teste próprio. O isolamento manual entre duas instituições
distintas continua pendente.

- Etapa 4: CI integrado; os jobs adicionais de testes fazem parte dos ramos seguintes.
- Etapa 5: ambiente local concluído; Aiven pendente para publicação.
- Etapa 6: API base implementada e verificada; PR #3 ainda aberto.
- Etapa 7: implementação e testes presentes; falta terminar a demonstração MySQL
  descrita em `identity.md` e enviar/rever o ramo.
- Etapa 8: login e sessões nos clientes, depois publicação; ainda por implementar.

As afirmações antigas no AGENTS.md sobre o primeiro administrador e login estavam
desatualizadas e foram corrigidas com base nas confirmações do utilizador.
Os testes relacionais SQLite não substituem a demonstração com o provider MySQL.

## Ordem de trabalho

Atualização de 2026-10-12: o GitHub confirmou os PRs #2 e #3 integrados, nos
commits `eb38f87` e `ce69c62`, respetivamente. As referências remotas locais foram
atualizadas. As menções anteriores a PRs abertos são o retrato da auditoria de
2026-10-11. O ramo Identity permanece local e será atualizado antes do seu PR.

### 1. Fechar a revisão de Identity

- [x] Corrigir o acesso entre módulos identificado acima.
- [x] Executar novamente os testes afetados de autenticação.
- Preservar as decisões existentes: uma instituição por conta, contas criadas por
  administradores, API partilhada, MAUI e Blazor.

### 2. Terminar a demonstração no MySQL local

Iniciar a API na raiz do repositório:

```powershell
dotnet run --project src/Backend/SchoolGether.Api --launch-profile http
```

Abrir `http://localhost:5265/swagger`. Seguir o guia `identity.md`:

- [ ] Entrar como PlatformAdmin e autorizar o Swagger com o novo access token.
- [ ] Criar um InstitutionAdmin com e-mail diferente do administrador global,
      usando `institutionId: 2` para a instituição já criada; esperar 201.
- [ ] Entrar com a nova conta e trocar o token em Authorize.
- [ ] Confirmar em `GET /api/v1/identity/me` o papel e a instituição.
- [ ] Criar Teacher e Student nessa instituição; esperar 201.
- [ ] Listar os membros e confirmar os dados da instituição autenticada.
- [ ] Confirmar 403 ao tentar criar uma conta noutra instituição.
- [ ] Criar outra instituição e o seu administrador com o PlatformAdmin;
      confirmar que as listagens não mostram membros da outra instituição.
- [ ] Renovar uma sessão com refresh; usar os novos tokens devolvidos.
- [ ] Fazer logout com o refresh token atual; esperar 204; tentar renovar com
      esse token revogado e confirmar 401.

Tokens e palavras-passe permanecem privados. Guardar como evidência apenas os
códigos de resposta e os resultados funcionais, sem capturas de credenciais.

### 3. Integrar o trabalho no GitHub

Consulta remota de 2026-10-11:

- [PR #2 — base de dados](https://github.com/Schoolgether-PAP/SchoolGether/pull/2):
  aberto, CI bem-sucedido, sem revisões registadas.
- [PR #3 — API base](https://github.com/Schoolgether-PAP/SchoolGether/pull/3):
  aberto, CI bem-sucedido, sem revisões registadas.
- `feature/identity`: apenas local; falta enviar e abrir o PR.

Pedir revisão a outro elemento da equipa, integrar #2 e depois #3, atualizar o
ramo de Identity com `origin/develop` e resolver eventuais diferenças. Depois
enviar Identity, abrir PR para `develop`, aguardar o CI e a revisão da equipa.
Esta auditoria não fez push, abriu PRs nem efetuou merges.

### 4. Desenvolver o login nos clientes

Primeira entrega funcional: entrar no site e na app com uma conta existente,
ver o próprio perfil e sair.

- Definir os ecrãs e estados: entrada, envio, erro, sessão ativa e sessão expirada.
- Implementar sessão e renovação serializada através do ApiClient partilhado;
  escolher o armazenamento apropriado a cada cliente.
- Implementar as páginas Blazor e os ecrãs MAUI com XAML/MVVM.
- Configurar a ligação da app à API em Windows e Android; o localhost do emulador
  ou do telemóvel não é o localhost do computador.
- Confirmar as permissões na API mesmo quando a interface esconde ações.
- Testar entrada, erro de credenciais, renovação, saída, navegação e acessibilidade.

### 5. Publicar e avançar por módulo

Configurar Aiven, alojamento da API e GitHub Pages, com HTTPS, configuração privada
e CORS das origens publicadas. Confirmar o login dos dois clientes na API alojada.

Depois seguir as fases do AGENTS.md: Academic e Library; atividades/avaliação e
pontos; competências, gamificação e portefólio; Stand e leitura offline.
Em cada módulo, concluir um fluxo utilizável na API, site e app antes de ampliar
a funcionalidade. Acessibilidade e testes acompanham o desenvolvimento.

## Comandos usados na revisão

```powershell
dotnet test tests/SchoolGether.Domain.Tests/SchoolGether.Domain.Tests.csproj --configuration Release
dotnet test tests/SchoolGether.Api.IntegrationTests/SchoolGether.Api.IntegrationTests.csproj --configuration Release
dotnet build src/Clients/SchoolGether.Web/SchoolGether.Web.csproj --configuration Release
dotnet build src/Clients/SchoolGether.Mobile/SchoolGether.Mobile.csproj --configuration Debug -f net10.0-windows10.0.19041.0
dotnet build src/Clients/SchoolGether.Mobile/SchoolGether.Mobile.csproj --configuration Debug -f net10.0-android
dotnet ef migrations list --project src/Backend/SchoolGether.Infrastructure --startup-project src/Backend/SchoolGether.Api --configuration Release --no-build
dotnet ef migrations has-pending-model-changes --project src/Backend/SchoolGether.Infrastructure --startup-project src/Backend/SchoolGether.Api --configuration Release --no-build
dotnet list tests/SchoolGether.Api.IntegrationTests/SchoolGether.Api.IntegrationTests.csproj package --vulnerable --include-transitive
```

O último comando não identificou pacotes vulneráveis no projeto consultado,
incluindo dependências transitivas, nas fontes NuGet atuais. Não foram testados
ecrãs reais de login, execução da app em dispositivo/emulador ou alojamento público.
