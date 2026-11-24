# Reposicionamento e anuidades — plano de implementação

**Goal:** alinhar apresentação e novas adesões com a direção autorizada, preservando contratos de demonstração antigos.
**Architecture:** API e estrutura existentes .NET 10/Blazor; metadados de preço e capacidade explícitos; reserva por docente na transação de criação de conta.
**Spec:** ../specs/2026-11-09-teacher-focus.md
**Execução:** nesta sessão, alterações pequenas, sem dependências novas, verificação focada conforme preferência do utilizador.

## Restrições e revisão

Sem cobranças reais, sem migração de condições antigas, sem dados pessoais nos testes, sem remoção de migrações antigas. Garantir: período mensal legado; unidade Accounts legada; estudantes não consomem lugares Teachers; limite docente atómico; rollback em criação inválida; conflito para versão de catálogo anterior.

## Tarefa 1 — catálogo e subscrição

- [x] Teste HTTP do catálogo anual e rejeição de versão anterior; observar falha antes da implementação.
- [x] Domain/Billing: Price e BillingPeriod/CapacityUnit validados; snapshot de subscrição preserva condições e ocupação inicial apropriada.
- [x] Contracts/Billing, controllers e SubscriptionStore: preço/período/unidade explícitos.
- [x] Infrastructure/Billing e IdentityStore: reserva atómica por papel para os planos novos, comportamento antigo para Accounts.
- [x] Migração: renomear monthly_price para price e acrescentar campos com defaults legados; verificar SQL e base temporária.
- [x] Testes de catálogo, adesão, legado, quota por papel e isolamento.

## Tarefa 2 — apresentação e continuidade

- [x] Página pública e adesão: anuidades, unidades de capacidade, foco docente, materiais próprios e módulos futuros identificados como desenvolvimento.
- [x] Painel institucional: ocupação por professores ou contas conforme subscrição.
- [x] Atualizar AGENTS e documentação de biblioteca/adesão; assinalar especificações antigas como substituídas quando necessário.
- [x] Compilar site e verificar visual desktop/móvel sem contas reais.
- [x] Rever diff e relatar o que ficou verificado e o que continua pendente.

## Evidência de verificação

- API: 86 testes focados passaram; 2 cenários MySQL foram ignorados nessa execução.
- MySQL real temporário: AnnualPlans passou separadamente, sem skips, verificando migração do esquema publicado e preservação das condições antigas.
- Domínio: 13 testes de Billing passaram. ApiClient: 8 testes relevantes passaram.
- Web Release: compilação sem avisos ou erros.
- Navegador com API simulada: catálogo, resumo da adesão, subscrições anuais e mensais, desktop e largura 390px; sem erros JavaScript ou overflow horizontal. Não representa teste online.
- Revisão independente concluída; omissão de preço/período no painel corrigida e confirmada pelo revisor.
- git diff --check sem problemas. Não foi executada a suite completa nem o build MAUI, por não serem necessários a esta entrega.
- Sem publicação ou migração online. Materiais, planificação, calendário e manutenção continuam pendentes.
