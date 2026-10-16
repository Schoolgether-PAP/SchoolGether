# Suporte: implementação e validação

O módulo está desativado por omissão (`Support:Enabled=false`). As tarefas 1–4 implementam domínio, HTTP autorizado, anexos privados e avisos SignalR; os clientes e ecrãs são as tarefas seguintes. Não publicar nem ativar apenas por estes testes locais.

## Canal ao vivo

O hub autenticado está em `/api/v1/support/live`. Disponibiliza apenas `JoinTicket(int ticketId)` e `LeaveTicket(int ticketId)`. O evento `SupportChanged` contém `{ ticketId, rowVersion }`; o conteúdo é sempre obtido pela API HTTP autorizada.

Cada ligação conserva só a identidade autenticada (ID, instituição e papel), validade do token e inscrições em memória. A conta, a instituição, o papel e as permissões globais são novamente consultados em cada invocação e imediatamente antes de cada entrega. Inscrições públicas e internas são separadas. A fila global é inscrita automaticamente apenas com `support.read`; recebe um único aviso mesmo quando a mesma ligação acompanha o pedido. Uma nota interna não produz qualquer aviso escolar. Ligações revogadas ou expiradas são removidas e encerradas.

Os avisos são posteriores ao commit. Falhas do transporte não anulam uma operação HTTP já persistida. Repetições idempotentes e escritas rejeitadas não produzem avisos. O registo de ligações é local ao processo; a entrega pode perder-se durante uma interrupção/reinício. A recuperação persistente usa `GET /api/v1/support/tickets/{id}/messages?afterId={lastId}` e uma nova leitura do pedido para obter estado, versão e capacidades, incluindo fecho durante a interrupção.

## Transportes e dependência

O navegador deve usar **SignalR LongPolling com Authorization**, sem `access_token` na URL. Não existe autenticação por token de query em qualquer rota. O servidor também permite WebSockets nativos autenticados por cabeçalho; SSE está desativado. As origens de navegador são comparadas com `Cors:AllowedOrigins`, incluindo pedidos WebSocket, negociação, polling e DELETE. Clientes nativos sem `Origin` continuam sujeitos à autenticação e autorização.

O servidor usa SignalR do framework ASP.NET Core. O projeto de testes acrescenta o cliente oficial `Microsoft.AspNetCore.SignalR.Client` **10.0.13** para ensaiar os transportes reais em TestServer. É necessário para validar o protocolo e a autenticação sem inventar um cliente WebSocket. A tarefa 5 acrescentará o mesmo pacote ao ApiClient.

A versão planeada 10.0.12 foi substituída pelo mínimo corrigido 10.0.13 porque o restauro sinalizou `NU1903` em `Microsoft.AspNetCore.SignalR.Common` (negação de serviço em stateful reconnect). A decisão segue o [aviso oficial GHSA-334f-vvp4-7g6p](https://github.com/advisories/GHSA-334f-vvp4-7g6p). Não há supressão de avisos. Stateful reconnect está desativado; usar reconexão normal e cursor HTTP. O pacote corrigido no teste não demonstra que o runtime do alojamento esteja atualizado.

Referências: [autenticação e revalidação SignalR](https://learn.microsoft.com/en-us/aspnet/core/signalr/authn-and-authz?view=aspnetcore-10.0), [segurança, origens e tokens](https://learn.microsoft.com/en-us/aspnet/core/signalr/security?view=aspnetcore-10.0).

## Continuidade e publicação

Na tarefa 5, obter o token através da sessão existente; usar LongPolling explicitamente no browser, reconectar/reinscrever os pedidos e recuperar mensagens/estado por HTTP. Uma indisponibilidade live não impede envios HTTP. Limpar inscrições, cursores, rascunhos e callbacks ao mudar de sessão. Não ativar stateful reconnect nem introduzir query bearer.

Os testes locais da tarefa 4 usam contas fictícias, SQLite temporário com ligações independentes e TestServer: LongPolling, WebSockets nativos, cabeçalhos, expiração, DELETE e isolamento. Não demonstram execução no browser nem no IIS/MonsterASP. A tarefa 8 deve verificar runtime/SDK corrigidos (10.0.13 ou superior na linha .NET 10), browser e transportes/CORS/DELETE reais, e que os registos de aplicação e do host não expõem tokens/queries. Os níveis atuais de aplicação mantêm `Microsoft.AspNetCore` em Warning; não habilitar registo de URLs/query sensíveis. A supressão de queries IIS W3C ainda não foi demonstrada, motivo pelo qual o browser não usa query tokens nesta entrega.
