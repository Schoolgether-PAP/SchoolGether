# Adesão pública e área institucional mínima

Implementação local de 2026-10-15 no ramo `feature/public-onboarding-implementation`. Esta entrega ainda depende de revisão independente do ramo, CI e revisão de outro elemento da equipa antes de integrar/publicar. A demonstração publicada anteriormente continua descrita em [deployment.md](deployment.md); esta implementação não ativa o registo online.

## Percurso no site

A raiz apresenta SchoolGether, desenvolvido pela SaguiLearn, e o catálogo da API. As queries `?view=entrar`, `?view=aderir&plan=essential` e `?view=area` funcionam sob a base estática `/SchoolGether/`, sem exigir novas rotas no GitHub Pages. A raiz permanece pública com sessão ativa. Preços e capacidades vêm do servidor: Essencial 59 €/100 contas, Evolução 149 €/350, Campus 299 €/1150; catálogo inicial `2026-10-14-v1`. São valores mensais de demonstração em EUR, sem cartão, cobrança, fatura ou contrato real.

A adesão revê plano, responsável, instituição e consentimento. O servidor cria numa transação a nova instituição, o primeiro InstitutionAdmin, perfil pendente, subscrição e evento de auditoria. Não permite escolher instituição existente, papel global, preço ou capacidade no pedido. Uma conta pertence a uma instituição. O responsável inicial ocupa uma vaga; todas as contas institucionais não eliminadas contam, incluindo as inativas. PlatformAdmin fica fora da capacidade.

Após a adesão há uma única tentativa de entrada automática. Resultado de registo desconhecido após falha de rede oferece entrada e não repete o POST. Conflito de catálogo exige rever as condições e aceitar novamente. Senhas nunca são guardadas no URL, localStorage ou sessionStorage; a sessão web permanece em memória.

## Área institucional

InstitutionAdmin abre exclusivamente a sua instituição. Perfil pendente apresenta a configuração com nome, tipo, localidade opcional e ano letivo. O ano segue `AAAA/AAAA`, anos consecutivos e início entre 1900 e 2199; a sugestão vem do relógio do servidor e muda em setembro. É apenas um rótulo, sem criar cursos ou inscrições. Instituições antigas sem perfil mostram estado pendente e plano explicitamente «Sem subscrição»; consultar não cria perfis em massa. A navegação permite consultar o resumo, membros ou próprio perfil antes de configurar.

Guardar conclui perfil/ano/nome e auditoria na mesma transação. Nome global e configuração partilham a versão da instituição. Um 409 conserva o rascunho e oferece «Recarregar configuração», que substitui explicitamente o rascunho pelos dados do servidor. A conclusão é consultada no servidor em entradas seguintes, e o instante inicial permanece em edições posteriores.

O painel oferece Resumo (instituição, estado, ano, plano e ocupação), Utilizadores paginados, Configuração, O meu perfil e Sair. Só cria Teacher/Student; não tem seletor de instituição ou papéis administrativos. A senha inicial deve ser comunicada fora da plataforma: não é enviado convite nem existe alteração obrigatória de senha. Falhas mantêm os campos não sensíveis; sucesso/falha HTTP limpam a senha. Após criar, são pedidas novamente lista e ocupação; se uma leitura falhar, há mensagem/repetição em vez de inventar dados. Componentes são isolados pela conta, cancelam pedidos ao sair e ignoram respostas de sessões anteriores.

PlatformAdmin conserva o painel global; Teacher/Student conservam entrada/perfil/saída. A app mantém login/perfil nesta entrega. Cursos, turmas, disciplinas, importação, remoção/inativação de contas, outros responsáveis, mudança de plano, pagamentos reais e painéis educativos completos permanecem futuros.

## API e operação

- `GET /api/v1/billing/plans`: catálogo e disponibilidade de registo.
- `POST /api/v1/onboarding/institutions`: nova instituição e responsável; sem autenticação.
- `GET/PUT /api/v1/institutions/current/setup`: configuração da instituição da sessão.
- `GET /api/v1/institutions/current/subscription`: plano/capacidade ou estado None.
- `GET/POST /api/v1/identity/users`: membros paginados/criação segundo permissões existentes.

`Onboarding:Enabled` é falso por omissão. Mantê-lo falso até revisão/CI, aplicação controlada das migrações e verificação do IP efetivo no proxy do alojamento. O limite é 3 registos/minuto/IP e 20/minuto/processo, sem fila, corpo até 16 KiB. O código usa RemoteIpAddress e não confia diretamente em X-Forwarded-For. Confirmar o comportamento real do alojamento antes de ativar; não adicionar confiança indiscriminada em cabeçalhos. Os limites por processo não equivalem a uma quota distribuída entre instâncias.

Novas migrações EF: `20261014093325_PublicInstitutionOnboarding` e `20261014210542_InstitutionSetupCompletion`. Aplicação operacional exige credenciais próprias e cópia de segurança; nunca alterar o esquema manualmente ou usar a credencial SQL da API para migrar. Não foram aplicadas nesta entrega à base pessoal/Aiven.

## Evidência local e limites

Em 2026-10-15, suite Debug completa: 31 Domain + 53 ApiClient + 181 API aprovados, 0 falhas, 4 cenários MySQL explicitamente ignorados na execução normal. API/Web Release e modelo EF verificados separadamente; modelo sem alterações pendentes. As verificações MySQL reais foram executadas com Oracle MySQL 8 em instâncias temporárias próprias, removidas no fim:

```powershell
dotnet test --artifacts-path .superpowers/test-artifacts --verbosity quiet
./scripts/Test-MySqlIdentity.ps1 -Scenario Identity -ArtifactsPath .superpowers/test-artifacts
./scripts/Test-MySqlIdentity.ps1 -Scenario Onboarding -ArtifactsPath .superpowers/test-artifacts
./scripts/Test-MySqlIdentity.ps1 -Scenario InstitutionCapacity -ArtifactsPath .superpowers/test-artifacts
./scripts/Test-MySqlIdentity.ps1 -Scenario InstitutionSetup -ArtifactsPath .superpowers/test-artifacts
```

Cada cenário MySQL executou/aprovou 1 teste, com 0 skips: migrações/isolamento/sessões, unicidade concorrente de e-mail, última vaga concorrente, upgrade dos quatro esquemas anteriores preservando contas/membros/instituições/suspensão/auditoria/layout e conflitos concorrentes de configuração/nome global/instante de conclusão. Um skip normal não substitui estas execuções.

Chromium com HTTP controlado passou adesão pública, área institucional e regressões de entrada/painel global sob `/SchoolGether/`, incluindo teclado/foco, erros, 409/429/401, cancelamento, double-click, respostas tardias e capturas desktop/390 px inspecionadas. Todas as chamadas `/api/**` foram interceptadas; não usaram a API/contas pessoais ou o alojamento online. Os cenários e logs ficam no workspace ignorado `.superpowers/verification/public-onboarding` e `.superpowers/sdd/2026-10-14-public-onboarding`.

MAUI Windows (`net10.0-windows10.0.19041.0`) e Android (`net10.0-android`) compilaram em Debug com 0 avisos/erros, verificando compatibilidade dos contratos; execução nativa/SecureStorage real e distribuição continuam verificações separadas. Não houve publicação, ativação online, testes de carga no alojamento nem faturação real. CI e revisão do colega continuam gates externos.
