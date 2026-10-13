# Administração global — fundação

As contas `PlatformAdmin` continuam globais e sem instituição. Uma conta ativa sem
permissões pode entrar e consultar o próprio contexto, mas não administra clientes.
As decisões de acesso consultam os dados atuais no servidor em cada operação;
revogar uma permissão ou desativar a conta afeta também um JWT ainda válido.

O master tem todas as capacidades e é o único que lista, cria e altera os outros
administradores. O painel não permite editar a conta master nem transferir essa
condição. As contas novas recebem apenas as permissões selecionadas.

| Permissão | Finalidade |
|---|---|
| `institutions.read` | Consulta administrativa de instituições |
| `institutions.write` | Criação/edição de instituições e contas institucionais |
| `institutions.suspend` | Suspensão/reativação de instituições |
| `audit.read` | Consulta do histórico administrativo |

Esta fundação disponibiliza as rotas seguintes; a gestão detalhada de instituições
e o dashboard pertencem à entrega seguinte.

| Método e rota | Acesso |
|---|---|
| `GET /api/v1/platform/context` | Conta global ativa |
| `GET /api/v1/platform/admins?page=1&pageSize=20` | Master |
| `POST /api/v1/platform/admins` | Master |
| `PUT /api/v1/platform/admins/{id}` | Master; conta alvo diferente do master |
| `GET /api/v1/platform/audit?page=1&pageSize=20&search=termo` | `audit.read` ou master |

A pesquisa do histórico é aplicada no servidor antes da paginação, por ação,
alvo, nome do ator e motivo, até 150 caracteres. As listas aceitam páginas de 1 a
100000 e tamanhos de 1 a 100. A alteração de administrador exige `RowVersion` atual
e `Reason`; uma versão desatualizada devolve 409. Outros erros são `ProblemDetails`
localizados, com 400, 401, 403 ou 404 conforme o caso. O OpenAPI documenta Bearer.

As rotas anteriores `POST /api/v1/institutions` e `POST /api/v1/identity/users`
exigem também `institutions.write` aos administradores globais. A administração
escolar conserva as regras existentes de isolamento e criação de docentes/alunos.
Como os contratos antigos não incluem motivo, a auditoria usa um motivo fixo do
servidor para essas operações.

## Preparação explícita pelo operador

A migração `PlatformAdministration` acrescenta estado ativo, permissões, um índice
único para o master e o histórico. Não promove contas nem concede permissões às
contas existentes. A migração foi gerada; aplicá-la à base desejada é uma ação
separada do operador, através do fluxo EF já documentado em `database.md`.

Depois de aplicar a migração, em Development, executar:

```powershell
dotnet run --project src/Backend/SchoolGether.Api -- --promote-master
```

O assistente pede o ID de uma conta global existente e ativa, e exige escrever o
mesmo ID novamente. Não escolhe a primeira conta, não cria outra, não altera a
palavra-passe e recusa um segundo master. Em Production não lê os dados de entrada
nem executa a promoção. O assistente anterior `--bootstrap-admin` continua a criar
apenas uma conta global inicial, sem condição master nem capacidades delegadas.

## Histórico e transações

Alterações administrativas e os respetivos eventos são gravados na mesma transação,
incluindo os primeiros `SaveChanges` do `UserManager`. Uma falha ao gravar o evento
faz rollback da alteração. Leituras sensíveis e recusas usam um contexto separado
para não gravar alterações pendentes do pedido recusado. A consulta do próprio
histórico produz um único evento, depois da leitura.

Os eventos guardam ator, instante UTC, ação, alvo, resultado, motivo, instituição
quando aplicável e referência do pedido. Os estados anterior/novo usam uma lista
positiva de campos administrativos; não são serializações de entidades Identity.
Não incluem palavras-passe, hashes, security stamps, tokens ou configurações privadas.
Não guardar segredos no motivo ou no nome de uma operação.

Não existe endpoint de edição/eliminação do histórico. O contexto EF recusa
alterações/remoções rastreadas e comandos `ExecuteUpdate`/`ExecuteDelete` sobre a
tabela de auditoria. Estas guardas pertencem à aplicação; a administração direta da
base pelo operador permanece fora do painel.

## Instituições globais, suspensão e dashboard

As rotas `GET /api/v1/platform/institutions` e `GET /{id}` exigem
`institutions.read`. A lista aceita `page=1`, `pageSize=20`, `search` (até 150
caracteres, nome/identificador) e `isSuspended` opcional, aplicados antes da
paginação. O detalhe inclui contas administrativas institucionais, sem credenciais.
A criação `POST`, a renomeação `PUT /{id}` e `POST /{id}/administrators` exigem
`institutions.write`. A criação da instituição e do responsável são operações
separadas: uma falha na conta não elimina a instituição já criada. A instituição
do responsável vem exclusivamente da rota e o papel é `InstitutionAdmin`.

`PUT /{id}/suspension` exige a capacidade distinta `institutions.suspend`.
Todas as alterações institucionais exigem motivo de 1 a 500 caracteres;
nome até 200 caracteres. Alterações enviam `rowVersion` atual; conflito devolve
409 e preserva os dados. Suspensão tem campos próprios, conserva os dados e
impede login, refresh e qualquer pedido escolar autenticado, incluindo JWT
anterior. Reativação não remove lockout nem eliminação individual.

`GET /api/v1/platform/audit` aceita também `institutionId` positivo opcional.
O filtro é exato, combinado com `search` e aplicado antes da contagem/paginação.
Continua a exigir `audit.read`, mesmo quando o chamador tem `institutions.read`.
O detalhe institucional não concede acesso implícito ao histórico.

`GET /api/v1/platform/dashboard` devolve indicadores autorizados, sem dados de
suporte, finanças ou histórico de utilização. `institutions.read` dá acesso à
fotografia atual de instituições não eliminadas (ativas/suspensas), ao total de
contas não eliminadas (globais e escolares, incluindo contas inativas/bloqueadas)
e à série de instituições criadas nos seis meses UTC até agora. Meses sem registos
têm zero; o mês atual tem «(parcial)» no rótulo. `audit.read` dá acesso às dez
operações administrativas mais recentes. Indicadores sem permissão ficam `null`
e listas ficam vazias; falhas de consulta continuam a ser erros, sem zeros falsos.
Os totais de contas por instituição incluem perfis/contas não eliminados, mesmo
quando a instituição está suspensa ou a conta tem uma sanção individual.

`GET/PUT /api/v1/platform/dashboard/preferences` só lê/grava a conta autenticada;
não aceita seleção de outra conta. Esquema 1, até três blocos distintos:
`institutions-trend`, `institutions-status`, `recent-actions`; larguras `small`,
`wide`, `full`. A ordem da lista define o layout, `visible` controla visibilidade
e uma lista vazia é válida. Os defaults são trend/wide, status/small e
recent-actions/full, visíveis quando autorizados. `rowVersion=0` identifica ausência
de preferências; a primeira gravação e cada atualização incrementam a versão.
Duas primeiras gravações concorrentes ou uma versão antiga produzem 409.
Permissões atuais filtram a resposta e os dados mesmo com um layout guardado.
Guardar preferências e o evento de auditoria também partilham a mesma transação.

A migração `20261012213240_PlatformInstitutionsAndDashboard` acrescenta os campos
de suspensão e a tabela de preferências por conta. Não altera privilégios nem
contas existentes. Foi validada, juntamente com a fundação administrativa, no
MySQL 8 temporário criado por `scripts/Test-MySqlIdentity.ps1`; a base pessoal
não foi usada. Para evitar DLLs bloqueadas por uma API em execução:

```powershell
./scripts/Test-MySqlIdentity.ps1 -ArtifactsPath .superpowers/test-artifacts
```

O script executa exatamente um cenário Oracle, verifica execução/aprovação e
remove a instância/dados temporários. A suite normal ignora explicitamente esse
cenário quando não existe ambiente temporário preparado.
