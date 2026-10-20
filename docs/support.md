# Suporte: implementação e validação

O módulo está desativado por omissão (`Support:Enabled=false`). Está implementado no site para atendimento global, InstitutionAdmin e Teacher: domínio, HTTP autorizado, anexos privados, avisos SignalR e cliente partilhado. Student não tem acesso. Não publicar nem ativar apenas por estes testes locais.

## Canal ao vivo

O hub autenticado está em `/api/v1/support/live`. Disponibiliza apenas `JoinTicket(int ticketId)` e `LeaveTicket(int ticketId)`. O evento `SupportChanged` contém `{ ticketId, rowVersion }`; o conteúdo é sempre obtido pela API HTTP autorizada.

Cada ligação conserva só a identidade autenticada (ID, instituição e papel), validade do token e inscrições em memória. A conta, a instituição, o papel e as permissões globais são novamente consultados em cada invocação e imediatamente antes de cada entrega. Inscrições públicas e internas são separadas. A fila global é inscrita automaticamente apenas com `support.read`; recebe um único aviso mesmo quando a mesma ligação acompanha o pedido. Uma nota interna não produz qualquer aviso escolar. Ligações revogadas ou expiradas são removidas e encerradas.

Os avisos são posteriores ao commit. Falhas do transporte não anulam uma operação HTTP já persistida. Repetições idempotentes e escritas rejeitadas não produzem avisos. O registo de ligações é local ao processo; a entrega pode perder-se durante uma interrupção/reinício. A recuperação persistente usa `GET /api/v1/support/tickets/{id}/messages?afterId={lastId}` e uma nova leitura do pedido para obter estado, versão e capacidades, incluindo fecho durante a interrupção.

## Transportes e dependência

O navegador deve usar **SignalR LongPolling com Authorization**, sem `access_token` na URL. Não existe autenticação por token de query em qualquer rota. O servidor também permite WebSockets nativos autenticados por cabeçalho; SSE está desativado. As origens de navegador são comparadas com `Cors:AllowedOrigins`, incluindo pedidos WebSocket, negociação, polling e DELETE. Clientes nativos sem `Origin` continuam sujeitos à autenticação e autorização.

O servidor usa SignalR do framework ASP.NET Core. ApiClient e testes usam o cliente oficial `Microsoft.AspNetCore.SignalR.Client` **10.0.13**: a dependência implementa negociação, LongPolling e reconexão nas duas plataformas, mantendo um único protocolo com o servidor. O pacote dos testes de integração também ensaia transportes reais em TestServer.

A versão planeada 10.0.12 foi substituída pelo mínimo corrigido 10.0.13 porque o restauro sinalizou `NU1903` em `Microsoft.AspNetCore.SignalR.Common` (negação de serviço em stateful reconnect). A decisão segue o [aviso oficial GHSA-334f-vvp4-7g6p](https://github.com/advisories/GHSA-334f-vvp4-7g6p). Não há supressão de avisos. Stateful reconnect está desativado; usar reconexão normal e cursor HTTP. O pacote corrigido no teste não demonstra que o runtime do alojamento esteja atualizado.

Referências: [autenticação e revalidação SignalR](https://learn.microsoft.com/en-us/aspnet/core/signalr/authn-and-authz?view=aspnetcore-10.0), [segurança, origens e tokens](https://learn.microsoft.com/en-us/aspnet/core/signalr/security?view=aspnetcore-10.0).

## Continuidade e publicação

O ApiClient obtém tokens via `IdentitySession.ExecuteAuthorizedAsync`, em cabeçalhos por pedido. `SupportLiveClient` fixa LongPolling em ambas as plataformas e usa reconexão normal; `StartAsync(ticketId)` reinscreve a conversa após reconectar. `StartQueueAsync()` abre apenas a ligação: o servidor autoriza/inscreve automaticamente a fila global, sem um ID fictício. Consumidores da fila devem reler a lista HTTP após `Connected` e avisos `Changed`; o aviso não é uma linha nem uma métrica da fila.

`SupportConversationState` recupera mensagens por `AfterId`, antigas por `BeforeId`, deduplica e relê a versão/capacidades do pedido. Conserva até 500 mensagens em memória e faz no máximo 20 páginas de recuperação por atualização; `RecoveryPending` indica recuperação ainda incompleta. `HasOlderMessages` permite voltar a ler o histórico que saiu da janela. A versão vem sempre do servidor. Falha inicial do canal ao vivo fica em `LiveError`; as operações HTTP permanecem disponíveis.

Cada `SupportUpload` recebe nome, dimensão e fábrica `OpenReadAsync(ct)`. O consumidor Web deve usar `IBrowserFile.OpenReadStream(SupportApiClient.MaximumFileSize, ct)` numa fábrica nova por tentativa; não partilhar o mesmo stream. O cliente valida contagem/dimensões, verifica os bytes efetivamente lidos e descarta streams/pedidos a cada tentativa de renovação. `SupportDownload` é dono da resposta/stream e deve ser usado com `await using`; logout também o descarta.

O rascunho é preparado por `SetDraftAsync`: a impressão local inclui conteúdo dos anexos sem guardar cópias em Contracts. Texto/anexos preservam `ClientId` e fingerprint numa repetição explícita. `DeliveryUncertain` exige decisão explícita para substituir conteúdo (`replaceUncertain:true`); não há reenvio automático nem fila offline. Anexos alterados fisicamente são rejeitados antes de completar o multipart. Em 409, o rascunho fica conservado e o estado relê pedido/mensagens para revisão. Não editar/repetir enquanto `IsSending`. `Clear()` cancela leituras, desliga o hub e remove referências locais a mensagens/rascunho/fábricas de ficheiros; a geração da sessão invalida respostas e callbacks tardios. Eventos de estado podem chegar fora do contexto UI: os componentes fazem dispatch para o seu contexto e traduzem falhas através dos recursos.

Os registos Web (scoped) e MAUI (singleton da sessão) estão disponíveis sem novos ecrãs nativos. O canal partilhado acompanha uma conversa ou a fila; os ecrãs devem coordenar essa seleção e chamar `Clear()` ao sair da conversa. Não ativar stateful reconnect nem introduzir query bearer.

Os testes locais da tarefa 4 usam contas fictícias, SQLite temporário com ligações independentes e TestServer: LongPolling, WebSockets nativos, cabeçalhos, expiração, DELETE e isolamento. Não demonstram execução no browser nem no IIS/MonsterASP. A tarefa 8 deve verificar runtime/SDK corrigidos (10.0.13 ou superior na linha .NET 10), browser e transportes/CORS/DELETE reais, e que os registos de aplicação e do host não expõem tokens/queries. Os níveis atuais de aplicação mantêm `Microsoft.AspNetCore` em Warning; não habilitar registo de URLs/query sensíveis. A supressão de queries IIS W3C ainda não foi demonstrada, motivo pelo qual o browser não usa query tokens nesta entrega.

## Atendimento global no site

A área Suporte aparece na navegação global com `support.read` (ou master); as capacidades de cada pedido devolvidas pelo servidor controlam resposta, gestão e fecho. A fila apresenta 20 pedidos por página, pesquisa e filtros; `GET /api/v1/support/tickets?status=Active` reúne `Open`, `InProgress` e `AwaitingClient` antes de contar/paginar. `Active` é apenas um filtro de consulta, não um estado de domínio nem uma transição. Os filtros explícitos dos quatro estados continuam disponíveis na API; o separador Histórico usa `Closed`.

A conversa distingue mensagens para o cliente de notas internas. O fecho exige resumo e confirmação, e torna o pedido apenas consultável. Conflitos 409 conservam texto/anexos e exigem revisão antes de novo envio. Falhas de transporte permitem repetir a mesma entrega idempotente; alterar um envio incerto exige confirmação porque o anterior pode já ter sido entregue. Rascunhos ficam apenas em memória e a troca de conversa pede confirmação para os descartar. O download passa pelo cliente autenticado, com libertação do stream e do URL temporário.

No desktop, fila, conversa e detalhes têm deslocamento limitado ao espaço de trabalho. A 390 px, fila e detalhe alternam com um botão Voltar à fila e reposição do foco. O estado mostrado corresponde à ligação real; falhas do tempo real permitem continuar por HTTP e usar Atualizar dados. Não existem métricas ou disponibilidade de operadores simuladas.

## Verificação local de entrega — 2026-10-17

O cenário `Support` de `scripts/Test-MySqlIdentity.ps1` usa exclusivamente Oracle MySQL 8 em loopback, datadir/porta/credencial aleatórios e a base de teste validada pela fixture. Migra das seis migrações já publicadas para as oito atuais, conservando contas, membros, auditoria, layout e subscrição fictícia. Verifica isolamento HTTP/download/hub, nota interna invisível, criação/envio idempotentes e índices únicos, reserva concorrente de quota em pedidos diferentes e corrida envio/fecho com apenas um vencedor. Um concorrente idempotente pode receber 409; repetir explicitamente a mesma chave recupera o resultado único. Os quatro cenários anteriores (Identity, Onboarding, InstitutionCapacity e InstitutionSetup) também foram executados em instâncias próprias, um teste aprovado em cada cenário.

Comandos reproduzíveis, a partir da raiz do repositório:

```powershell
./scripts/Test-MySqlIdentity.ps1 -Scenario Support -ArtifactsPath C:/PAP/artifacts/support-task8/mysql-build -EvidencePath C:/PAP/artifacts/support-task8
dotnet test tests/SchoolGether.Domain.Tests/SchoolGether.Domain.Tests.csproj -c Release
dotnet test tests/SchoolGether.Application.Tests/SchoolGether.Application.Tests.csproj -c Release
dotnet test tests/SchoolGether.ApiClient.Tests/SchoolGether.ApiClient.Tests.csproj -c Release
dotnet test tests/SchoolGether.Api.IntegrationTests/SchoolGether.Api.IntegrationTests.csproj -c Release
dotnet ef migrations has-pending-model-changes --project src/Backend/SchoolGether.Infrastructure --startup-project src/Backend/SchoolGether.Api --configuration Release
```

Na matriz local: Domain 84, Application 7, ApiClient 85 e integração 274 aprovados; os cinco testes MySQL são ignorados normalmente e executados separadamente pelo script. API/Web Release e MAUI Debug Windows/Android compilaram sem avisos/erros; EF sem alterações pendentes. Compilar MAUI não demonstra execução nativa. A publicação Web indica ausência do workload opcional `wasm-tools`; o pacote produzido foi usado no ensaio browser, sem instalar workloads globais.

Os artefactos privados ficam em `C:/PAP/artifacts/support-task8/`, fora do Git. A máquina tem runtime global 10.0.12; o pacote API autocontido e o runtime privado para o ensaio real usam 10.0.13, sem alterar a instalação global. Esta preparação não demonstra a versão do runtime do MonsterASP.NET. O CI/revisão de todo o ramo e os testes de alojamento continuam etapas separadas antes de merge/publicação.

O ensaio Chromium usa dois BrowserContexts independentes, o pacote Web Release e Kestrel/MySQL temporários reais, sem substituir respostas das rotas de negócio. O host estático de ensaio configura `Blazor.start({environment:"Development"})` e `Api:BaseUrl` para permitir HTTP exclusivamente local; a validação HTTPS do produto não foi alterada. Passaram criação com anexo, troca pública automática, ausência de nota/anexo internos na escola, download privado, LongPolling com cabeçalhos e sem query bearer, reconexão/recuperação por cursor, fecho durante interrupção, 390 px e logout/DELETE; sem erros JavaScript. A interrupção é aplicada ao contexto escolar; mensagens reais terminam polls pendentes antes de confirmar a reconexão. Capturas, pedidos sem tokens e prova de runtime/base real ficam nos artefactos privados. Isto substitui a limitação anterior do fixture de negociação 503 para a prova local, mantendo por verificar o alojamento.

`-EvidencePath` conserva o TRX antes da limpeza. `-BackupPath` no cenário Support conserva SQL + anexos e manifesto SHA-256, importa SQL numa segunda base do mesmo servidor temporário e compara contagens e hashes dos ficheiros restaurados. Após o ensaio, o conjunto é cifrado com DPAPI `CurrentUser`, a decifração é comparada byte a byte e as cópias em claro são removidas. O backup local só pode ser decifrado pelo mesmo utilizador Windows; não é um mecanismo portátil de recuperação do alojamento. Um backup operacional precisa do seu próprio armazenamento seguro e ensaio de recuperação.

## Limites e continuidade

Esta entrega disponibiliza suporte no site. Os ecrãs nativos da app, e-mail/push, sanções, faturação real e acesso assistido às contas ficam fora do âmbito. Não há reabertura, edição/eliminação do histórico nem fila offline de mensagens.

Após concluir suporte e a sua revisão/entrega, a etapa seguinte é desenvolver os painéis completos de instituições, docentes e alunos e a funcionalidade académica, discutindo com o utilizador a navegação e as escolhas de funcionamento. A área institucional mínima e os perfis atuais mantêm-se; não são painéis académicos completos.
