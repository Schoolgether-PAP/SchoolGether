# API base — Fase 0, etapa 6

## Executar e verificar

Na raiz do repositório, depois de [preparar o MySQL local](database.md):

```powershell
dotnet run --project src/Backend/SchoolGether.Api --launch-profile http
```

Manter esse terminal aberto. O perfil `http` executa a API em desenvolvimento,
em `http://localhost:5265`. Abrir `http://localhost:5265/swagger` no navegador.
O documento OpenAPI está em `http://localhost:5265/openapi/v1.json`.
O Swagger permite escolher um endpoint, carregar em **Try it out** e **Execute**.
Tanto a interface como o documento só são disponibilizados em `Development`.

Noutro terminal PowerShell:

```powershell
Invoke-RestMethod http://localhost:5265/api/v1/health
Invoke-RestMethod http://localhost:5265/api/v1/health/ready
```

| Endpoint | O que verifica | Resposta esperada |
|---|---|---|
| `GET /api/v1/health` | A API consegue responder, sem consultar a base de dados | HTTP 200, `{"status":"Healthy"}` |
| `GET /api/v1/health/ready` | A ligação ao MySQL funciona e não existem migrações pendentes | HTTP 200, `{"status":"Healthy"}`; HTTP 503 se a verificação falhar |

As respostas de diagnóstico não devem ser guardadas em cache. A verificação de
prontidão tem um limite de cinco segundos e usa o contexto EF Core registado em
Infrastructure, através de uma interface em Application. Não expõe a cadeia de
ligação nem detalhes internos dos erros.

Se `/ready` devolver 503, confirmar que o serviço MySQL está iniciado e que a
ligação privada está configurada conforme o guia da base de dados. Se faltarem
migrações, parar a API com `Ctrl+C`, executar o comando seguinte e voltar a iniciá-la:

```powershell
dotnet ef database update --project src/Backend/SchoolGether.Infrastructure --startup-project src/Backend/SchoolGether.Api
```

A API não aplica migrações automaticamente ao iniciar. `/ready` verifica a ligação
e as migrações registadas; não valida toda a estrutura ou todos os dados das tabelas.

## Rotas, contratos e erros

Os controllers usam o prefixo `/api/v1`. O contrato `HealthResponse` está em
`SchoolGether.Contracts`, para poder ser partilhado com os clientes.
O endpoint de exemplo `WeatherForecast` foi removido do backend.

Erros usam `application/problem+json`, com `status`, `title`, `instance` e `traceId`.
Erros de validação também incluem `errors`, por campo. Os títulos comuns estão em
recursos de texto em português. Exceções inesperadas devolvem uma mensagem genérica
com HTTP 500; os detalhes permanecem nos registos do servidor.

Não existem ainda endpoints de gestão de instituições ou de login. A etapa seguinte
é Identity, com autenticação, papéis e isolamento entre instituições.

## CORS e HTTPS

Em desenvolvimento são permitidas as origens do site local: `http://localhost:5227`
e `https://localhost:7256`. A política permite métodos e cabeçalhos dos pedidos dessas
origens e não ativa credenciais de cookies. CORS é uma regra do navegador e não
substitui a autenticação nem impede chamadas diretas à API.

Em produção, a lista de origens começa vazia. Configurar `Cors__AllowedOrigins__0`
com a origem HTTPS do site publicado, por exemplo `https://exemplo.github.io`.
Usar apenas esquema, domínio e porta, sem caminho ou barra final. Acrescentar outras
origens com os índices `1`, `2`, etc., quando necessário.

Fora de desenvolvimento, a API redireciona HTTP para HTTPS. O alojamento deve
disponibilizar HTTPS e fornecer a configuração necessária para esse redirecionamento.

## Testes

```powershell
dotnet test tests/SchoolGether.Api.IntegrationTests/SchoolGether.Api.IntegrationTests.csproj --configuration Release
```

Os testes verificam as respostas HTTP, validação, falhas controladas, CORS e Swagger.
Executam a API em memória com uma verificação de base de dados substituta e não
usam as credenciais locais. Os controllers que provocam falhas e validam pedidos
existem apenas na aplicação de teste.

A dependência `Swashbuckle.AspNetCore.SwaggerUI` acrescenta a interface visual do
Swagger. O documento continua a ser gerado pelo `Microsoft.AspNetCore.OpenApi`,
que já fazia parte do projeto.

## Referências

- [Erros em APIs ASP.NET Core](https://learn.microsoft.com/aspnet/core/web-api/handle-errors?view=aspnetcore-10.0)
- [Testes de integração ASP.NET Core](https://learn.microsoft.com/aspnet/core/test/integration-tests?view=aspnetcore-10.0)
- [CORS em ASP.NET Core](https://learn.microsoft.com/aspnet/core/security/cors?view=aspnetcore-10.0)
