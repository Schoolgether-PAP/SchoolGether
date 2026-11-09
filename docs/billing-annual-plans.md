# Anuidades e lugares docentes

Entrega local de 2026-11-09. Não publicada nem aplicada ao Aiven.

O catálogo `2026-11-09-v1` define Essencial (1 000 EUR/20 docentes), Profissional (2 500 EUR/50 docentes) e Institucional (4 000 EUR/100 docentes). Os identificadores técnicos `essential/evolution/campus` são conservados para ligações existentes; os nomes comerciais dos planos novos são distintos dos nomes antigos. Valores apenas de demonstração, sem cobrança, vencimento ou renovação automática.

## Contrato e compatibilidade

`PlanResponse` usa `price`, `billingPeriod` (`Monthly` ou `Annual`) e `capacityUnit` (`Accounts` ou `Teachers`); `accountLimit` é o número de lugares da unidade indicada. `usedAccounts` conserva o nome do campo existente, mas conta a unidade da subscrição. Clientes e servidor devem ser publicados coordenadamente: a antiga interface `monthlyPrice` foi substituída. Não publicar a API nova isoladamente com o site antigo.

A migração `AnnualTeacherPlans` renomeia `monthly_price` para `price`, sem transformar valores, e acrescenta `billing_period=Monthly` e `capacity_unit=Accounts` às linhas legadas. Novas subscrições copiam as condições anuais do catálogo e iniciam a ocupação docente em zero. Um preço antigo não é reinterpretado como anuidade; a leitura do plano vem da subscrição guardada, não do catálogo atual.

Criar Teacher reserva um lugar atómico na transação da conta. Student e InstitutionAdmin não consomem lugares Teachers. Accounts conserva a contagem anterior, incluindo o responsável. Falhas de criação/auditoria fazem rollback da reserva; desativação conserva o lugar. Não foi criada eliminação/restauro ou mudança de papel que possa contornar a quota. Não há alteração retroativa de planos nem gestão simulada de compras de módulos nesta entrega.

Sem subscrição, conserva-se o comportamento legado existente. O calendário, os materiais e a manutenção são descritos na apresentação como desenvolvimento futuro. Não confundir o suporte SaguiLearn existente com os futuros tickets de manutenção internos à escola.

## Publicação futura

Requer backup, ensaio de migração, revisão/CI e atualização coordenada do site/API. A migração local de inscrições permanece num ramo separado e deve ser reconciliada no snapshot EF quando integrada. Não há alteração na base online nesta entrega.

Verificações e limitações registadas em superpowers/plans/2026-11-09-teacher-focus.md.
