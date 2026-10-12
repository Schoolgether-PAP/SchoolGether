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
