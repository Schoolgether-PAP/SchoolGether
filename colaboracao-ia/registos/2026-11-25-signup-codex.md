# 2026-11-25 — Adesão e configuração institucional

- Ferramenta: Codex (agente Task 1).
- Ramo/base: feature/school-workspaces, 0989d90.
- Estado: implementado e verificado automaticamente; revisão visual pendente.
- Objetivo: concluir a configuração da instituição atomicamente durante a adesão, incluindo ano letivo e resumo sem cobrança real.

## Alterações e razão

O pedido e comando de adesão recebem AcademicYear. O valor tem de respeitar a validação existente do domínio (1900/1901 a 2199/2200, anos consecutivos). Ausência, vazio ou formato inválido devolvem 400 sem criação parcial. O parâmetro opcional final mantém compatibilidade de compilação com consumidores existentes do contrato; pedidos antigos sem ano deixam de poder criar instituições incompletas.

O store chama InstitutionProfile.Complete antes da primeira inserção do perfil, dentro da transação existente. Conta, instituição, perfil completo, subscrição e auditoria conservam o rollback comum. Nenhuma capacidade de escrita anónima foi ampliada. Signup continua desativado por omissão, sem alteração das proteções de sessão/reenvio, preços ou subscrições anteriores. Não foi necessária migração: os campos já existem.

O wizard recolhe e valida o ano no passo institucional e mostra-o na confirmação. O resumo distingue valor de referência do plano e montante a pagar de 0 €, explica a simulação e não recolhe dados bancários/cartão. A confirmação e o consentimento explicitam ausência de cobrança real e renovação automática; removida a mensagem generalizada de demonstração deste wizard.

## Ficheiros

- src/Shared/SchoolGether.Contracts/Onboarding/OnboardingContracts.cs
- src/Backend/SchoolGether.Application/Onboarding/OnboardingModels.cs
- src/Backend/SchoolGether.Application/Onboarding/OnboardingService.cs
- src/Backend/SchoolGether.Application/Institutions/IInstitutionRegistrationStore.cs
- src/Backend/SchoolGether.Infrastructure/Institutions/InstitutionRegistrationStore.cs
- src/Backend/SchoolGether.Api/Onboarding/OnboardingController.cs
- src/Clients/SchoolGether.Web/Public/RegistrationWizard.razor
- src/Clients/SchoolGether.Web/Public/RegistrationWizard.razor.css
- src/Clients/SchoolGether.Web/Resources/UiMessages.resx (apenas recursos da adesão, incluindo cinco novas chaves SignupBilling/SignupPlan/SignupAmount/SignupNoAmount)
- tests/SchoolGether.Api.IntegrationTests/OnboardingTests.cs
- tests/SchoolGether.Api.IntegrationTests/MySqlOnboardingTests.cs
- tests/SchoolGether.Api.IntegrationTests/InstitutionSetupTests.cs (expectativa de configuração inicial completa)
- tests/SchoolGether.ApiClient.Tests/OnboardingApiClientTests.cs

## Verificação

- Teste red inicial Signup_creates: falhou em IsComplete (False), demonstrando comportamento anterior.
- dotnet test tests/SchoolGether.Api.IntegrationTests/SchoolGether.Api.IntegrationTests.csproj --filter 'FullyQualifiedName~OnboardingTests|FullyQualifiedName~InstitutionSetupTests|FullyQualifiedName~PlanCatalogTests' --no-restore -v minimal: 83 passaram, 1 MySQL excluído.
- dotnet test tests/SchoolGether.Api.IntegrationTests/SchoolGether.Api.IntegrationTests.csproj --filter FullyQualifiedName~InstitutionCapacityTests --no-build --no-restore -v minimal: 8 passaram, 1 MySQL excluído. Inclui conservação do preço mensal/quotas legadas.
- dotnet test tests/SchoolGether.ApiClient.Tests/SchoolGether.ApiClient.Tests.csproj --filter FullyQualifiedName~OnboardingApiClientTests -v minimal: 6 passaram, incluindo serialização do ano e ausência de retries automáticos.
- dotnet build src/Clients/SchoolGether.Web/SchoolGether.Web.csproj -v minimal: passou, 0 avisos/erros; inclui compilação ApiClient/Contracts. Os testes de integração compilaram API/Application/Infrastructure/Domain.
- Falhas ambientais iniciais: NuGet.Config inacessível no sandbox; execução elevada restaurou. Uma execução de testes no sandbox teve 40 aprovados e 2 falhas por EventLog Windows; repetição elevada passou todos os cenários selecionados.
- git diff --check dos ficheiros desta tarefa: sem problemas. O diff global contém mudanças simultâneas de outros agentes, que não foram alteradas por esta tarefa.

## Limitações, integração e próximo passo

MySQL temporário não configurado: cenários concorrentes reais MySQL não executados; teste correspondente atualizado para verificar perfil completo. Não foram executadas migrações, credenciais de produção, publicação ou chamadas a sistemas externos. Não foram executadas suites amplas ou MAUI, conforme pedido de verificação focada.

Inspeção do wizard em navegador desktop/móvel permanece para a integração. Coordenador deve rever o diff, verificar o fluxo visual e incluir estes ficheiros no commit do ramo. Commit, push, revisão externa, integração e publicação: não realizados por este agente, conforme instrução.
