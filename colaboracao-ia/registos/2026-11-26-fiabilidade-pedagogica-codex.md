# 2026-11-26 — Fiabilidade de materiais e planificações

- Autor humano / ferramenta: Nicolas_Santos / Codex.
- Base: `feature/learning-materials` `9ff7277`; ramo `feature/learning-workspace-reliability`.
- Pedido: corrigir os problemas da revisão e atualizar o registo de colaboração.
- Estado: correções deste incremento concluídas e verificadas; restantes funcionalidades do plano continuam pendentes.

## Alterações e razão

1. O teste HTTP dos materiais excedia 10 autenticações/minuto e recebia 429. Conserva agora o token do administrador da preparação e reutiliza os tokens dos restantes papéis. Não foi alterado o rate limit nem contornada a autorização do servidor.
2. `Load` recebe a página pretendida e confirma página/ano/contextos/resultados apenas depois do sucesso. Uma falha deixa a página confirmada intacta; a repetição pede a página correta.
3. `PendingLearningCreation<T>` conserva o pedido imutável na sessão quando o componente é desmontado. Reabrir repõe o formulário, a chave e o conteúdo originais. Desmontar durante um POST marca o resultado como incerto. Respostas da sessão antiga não limpam estado da nova.
4. Um refresh de perfil não muda a sessão: conservar os pedidos quando geração e identidade são iguais. Limpar em logout/mudança de geração, conta, papel ou instituição. Não gravar conteúdos pedagógicos no armazenamento persistente do navegador.
5. `NavigationLock` nos componentes e no painel avisa nas saídas externas e impede abandonar internamente a aplicação enquanto há uma criação pendente. A navegação entre áreas do painel continua disponível com recuperação do pedido. Abandonar uma página após aceitar o aviso, recarregar ou sair da sessão perde o estado em memória; não há promessa de recuperação entre sessões.
6. Acrescentada suite de componentes com os clientes HTTP reais e fronteira HTTP controlada; nenhuma dependência externa nova. Incluída na solução e no CI. Acrescentado cenário `Materials` ao ensaio MySQL.
7. Consolidado `ESTADO.md`, atualizado o resumo atual de `AGENTS.md` e corrigidas as tarefas antigas que ainda apresentavam inscrições, calendário e materiais como ausentes.

## Ficheiros

- `src/Clients/SchoolGether.Web/Learning/PendingLearningCreation.cs` — estado de criação por sessão.
- `src/Clients/SchoolGether.Web/Learning/TeachingMaterials.razor` e `LessonPlans.razor` — paginação e recuperação de criação.
- `src/Clients/SchoolGether.Web/Identity/MemorySessionRegistration.cs` e `MemberWorkspace.razor` — registo de dependências e proteção de saída global.
- `tests/SchoolGether.Web.Tests/` — oito regressões nos handlers dos componentes reais, sem substituir o comportamento por mocks de estado.
- `tests/SchoolGether.Api.IntegrationTests/LearningMaterialsHttpTests.cs` — autenticações reutilizadas.
- `scripts/Test-MySqlIdentity.ps1` — cenário Materials.
- `.github/workflows/ci.yml` e `SchoolGether.slnx` — nova suite.
- `AGENTS.md`, `colaboracao-ia/ESTADO.md` e planos de 25/11 e 26/11 — estado e continuidade.

## Verificação

- Reproduzido o teste HTTP falhado antes da correção. Reproduzidas seis falhas Web antes da implementação (página avançava, pesquisa reiniciava página após erro e pedido desaparecia ao desmontar).
- Revisão independente encontrou perda em refresh de perfil: acrescentadas duas regressões, observadas a falhar e depois a passar. Revisão final sem novos findings acionáveis no escopo.
- `dotnet test tests/SchoolGether.Domain.Tests/SchoolGether.Domain.Tests.csproj --verbosity quiet`: 119 aprovados.
- `dotnet test tests/SchoolGether.Application.Tests/SchoolGether.Application.Tests.csproj --verbosity quiet`: 7 aprovados.
- `dotnet test tests/SchoolGether.ApiClient.Tests/SchoolGether.ApiClient.Tests.csproj --verbosity quiet`: 93 aprovados.
- `dotnet test tests/SchoolGether.Api.IntegrationTests/SchoolGether.Api.IntegrationTests.csproj --verbosity quiet`: 344 aprovados, 13 cenários MySQL excluídos da execução normal.
- `dotnet test tests/SchoolGether.Web.Tests/SchoolGether.Web.Tests.csproj --configuration Release --verbosity quiet`: 8 aprovados após as últimas correções.
- `./scripts/Test-MySqlIdentity.ps1 -Scenario Materials -ExpectedTestCount 1`: 1 aprovado num Oracle MySQL 8 temporário; migrações, publicação, revogação e isolamento. Instância e dados temporários removidos. TRX privado em `C:\PAP\artifacts\learning-reliability-2026-11-26-mysql`.
- API e Web Release: sem avisos/erros. `dotnet ef migrations has-pending-model-changes` com configuração de desenho: nenhuma alteração pendente do modelo, sem ligação à produção.
- Navegador local em vista estreita/modo escuro com API fictícia: login, navegação móvel, paginação falhada mantém 1, nova tentativa avança para 2; criar material com resposta 503 após guardar; Perfil/Atualizar; reabrir Materiais recupera título/pedido; Guardar confirma um único registo. Evidência visual privada em `C:\PAP\artifacts\learning-reliability-mobile-2026-11-26.png`.
- Servidores e separador de verificação encerrados. Nenhuma configuração local temporária foi incluída no código.
- `git diff --check`: sem erros. Primeira tentativa .NET bloqueada pelo acesso à configuração NuGet no sandbox; execução autorizada fora do sandbox resolveu o impedimento.

## Limites e próximos passos

Ficheiros privados de materiais, atividades, entregas, feedback, configuração de avaliação por percurso/turma e áreas pedagógicas MAUI continuam por implementar. Suporte operacional online e inspeção visual global de todos os painéis continuam pendentes. O ensaio MySQL não comprova compatibilidade integral com TiDB; não foram alterados dados online.

Integrar este incremento com o ramo dos materiais após CI remoto e revisão da equipa. Depois implementar ficheiros privados, seguindo as permissões/quotas e sem reutilizar cegamente a limpeza de anexos do suporte.

## Integração e publicação

Preparado para commit/push neste ramo, com registo no mesmo commit. Confirmar o SHA e PR no histórico remoto. Não integrado nos ramos estáveis, não publicado e sem migrações na produção. Não confundir as verificações locais com o estado do CI remoto.
