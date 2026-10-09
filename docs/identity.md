# Identity — Fase 0, etapa 7

## Decisões do projeto

- Cada conta institucional pertence a **uma instituição**. Um índice único no
  `user_id` de `institution_member` impede duas associações para a mesma conta.
- As contas são criadas por administradores. Não existe registo público.
- `PlatformAdmin` é a conta de administração global: cria instituições e as
  primeiras contas `InstitutionAdmin`. Não pertence a uma instituição.
- `InstitutionAdmin` cria docentes (`Teacher`) e alunos (`Student`) apenas na sua
  instituição. Não pode criar outros administradores nem escolher outra instituição.
- O login no site e na app é a etapa seguinte. Este ramo prepara a API e o cliente
  HTTP partilhado; a demonstração desta etapa é feita no Swagger.

## Preparar localmente

Na raiz do repositório, parar qualquer API aberta com `Ctrl+C`. Os comandos seguintes
usam a ligação MySQL privada já configurada na etapa anterior.

```powershell
cd C:\PAP\SchoolGether
dotnet run --project src/Backend/SchoolGether.Api --launch-profile http -- --setup-identity-key
dotnet ef database update --project src/Backend/SchoolGether.Infrastructure --startup-project src/Backend/SchoolGether.Api
dotnet run --project src/Backend/SchoolGether.Api --launch-profile http -- --bootstrap-admin
```

O primeiro comando gera uma chave RSA de 3072 bits e guarda-a em `Jwt:PrivateKeyPem`
nos user-secrets da API. Não mostra a chave nem a escreve no repositório. Se já
existir uma chave configurada, mantém-na. Cada programador prepara a sua chave local.

O segundo comando aplica a migração `IdentityAccounts`, acrescentando as tabelas
de autenticação e os membros das instituições. Não altera a tabela `institution`.

O terceiro comando pede o e-mail e a palavra-passe do primeiro administrador global.
Escolher uma palavra-passe privada com 12 a 128 caracteres, incluindo maiúscula,
minúscula, número e símbolo. A escrita mostra apenas asteriscos; Backspace apaga
o último carácter. Se a validação falhar, o assistente indica os requisitos em falta
ou o problema com o e-mail. Voltar a executar o comando para tentar novamente.
Se já existir um administrador global, nenhuma conta é alterada. Estes assistentes
só estão disponíveis em `Development` e terminam sem iniciar o servidor HTTP.

Depois, iniciar a API:

```powershell
dotnet run --project src/Backend/SchoolGether.Api --launch-profile http
```

Abrir `http://localhost:5265/swagger`. `/api/v1/health/ready` deve devolver 200.
Sem a nova migração aplicada, a prontidão devolve 503. Sem a chave privada, é
necessário executar o assistente de configuração antes de usar a API.

## Demonstração no Swagger

1. Em `POST /api/v1/identity/login`, usar **Try it out** com o e-mail e a palavra-passe
   escolhidos no assistente. A resposta 200 inclui `accessToken`, `refreshToken`,
   os prazos de validade e a conta. Não partilhar estes valores nem capturas dos mesmos.
2. Copiar apenas o `accessToken`, abrir **Authorize** no topo do Swagger, colar o valor
   e confirmar. O Swagger acrescenta o cabeçalho `Authorization: Bearer ...`.
3. Em `POST /api/v1/institutions`, enviar `{"name":"Instituição de demonstração"}`.
   A resposta 201 inclui o `id` da instituição.
4. Em `POST /api/v1/identity/users`, indicar e-mail, palavra-passe privada, nome,
   `role` igual a `InstitutionAdmin` e `institutionId` igual ao ID criado.
5. Fazer login com a nova conta. Substituir o token em **Authorize** pelo desta conta.
6. Criar um docente ou aluno em `POST /api/v1/identity/users`, com `role` igual a
   `Teacher` ou `Student`. O `institutionId` pode ser `null`: o servidor usa a
   instituição do administrador autenticado. Indicar outra instituição devolve 403.
7. Testar `GET /api/v1/identity/me` e `GET /api/v1/identity/users`. A listagem contém
   apenas os membros da instituição atual e admite `page` e `pageSize` (máximo 100).
8. Para testar outra instituição, entrar novamente como administrador global,
   criá-la e repetir os passos. O seu administrador não deve ver os membros da primeira.

Para renovar uma sessão, enviar `{"refreshToken":"VALOR_PRIVADO"}` para
`POST /api/v1/identity/refresh`. Guardar os **novos** tokens devolvidos e substituir
o access token no Swagger. O refresh token anterior deixa de poder ser usado.
O Swagger não renova a sessão automaticamente.

Para terminar, enviar o refresh token atual para `POST /api/v1/identity/logout`.
A resposta é 204. Um token inexistente também devolve 204. O logout revoga a renovação
daquela sessão; um access token já emitido pode continuar válido até expirar.

## Endpoints e permissões

| Endpoint | Permissão |
|---|---|
| `POST /api/v1/identity/login` | Público; verifica credenciais e bloqueio da conta |
| `POST /api/v1/identity/refresh` | Exige posse de um refresh token válido |
| `POST /api/v1/identity/logout` | Exige o refresh token da sessão a terminar |
| `GET /api/v1/identity/me` | Utilizador autenticado; devolve a própria conta |
| `POST /api/v1/identity/users` | Administrador global ou da instituição, com as restrições acima |
| `GET /api/v1/identity/users` | Administrador da instituição |
| `POST /api/v1/institutions` | Administrador global |

Pedidos inválidos devolvem 400; credenciais/sessões inválidas devolvem 401;
falta de permissão devolve 403; e-mail já utilizado devolve 409. Os erros usam
`ProblemDetails` em português, com `traceId`. Os endpoints de login, refresh e logout
partilham um limite de dez pedidos por minuto por endereço IP. O excesso devolve
429 com `Retry-After`. Cinco palavras-passe incorretas bloqueiam a conta durante cinco
minutos. Uma conta inexistente, bloqueada ou com palavra-passe errada usa a mesma mensagem.

## Implementação e isolamento

ASP.NET Core Identity trata das credenciais, normalização de e-mail, hashing de
palavras-passe e bloqueio. As credenciais e sessões são metadados globais de
autenticação (`user_account`, tabelas auxiliares Identity e `refresh_session`).
Não são usadas como listagem de dados de instituições.

Os dados institucionais ficam em `institution_member`, com `institution_id`,
eliminação lógica e filtro global EF. O contexto obtém a instituição exclusivamente
das claims do JWT validado. Sem instituição autenticada, o filtro não devolve membros.
As gravações também verificam a instituição; a criação pelo administrador global é
explicitamente permitida. Novas tabelas de dados institucionais devem seguir o mesmo
modelo. Operações SQL diretas, `ExecuteUpdate` e `IgnoreQueryFilters` exigem sempre
restrições explícitas e revisão, pois contornam parte das guardas do contexto.

A pesquisa global de uma conta e do seu perfil é limitada às operações internas de
autenticação: o login verifica a palavra-passe e o refresh identifica o utilizador
por uma sessão aleatória guardada no servidor. A listagem de membros não contorna
o filtro. Instituições desativadas e contas eliminadas não podem entrar ou renovar.

O access token é JWT, assinado com RS256, e dura dez minutos. A API verifica assinatura,
algoritmo, emissor, audiência e validade, com tolerância de quinze segundos.
O refresh token tem 256 bits aleatórios; apenas o seu hash SHA-256 é guardado.
A sessão tem validade absoluta de catorze dias. A renovação usa uma atualização
condicional e uma transação, criando um novo token e consumindo o anterior.
Reutilizar um token consumido revoga a família da sessão, incluindo a sua substituição.
Os clientes devem serializar a renovação, para evitar dois pedidos de refresh em paralelo.

As respostas de autenticação não são guardadas em cache e os contratos não incluem
hashes, chaves privadas nem security stamps. `IdentityApiClient` fornece login,
refresh, logout e consulta da própria conta para a próxima etapa dos clientes.

## Verificação e limites desta etapa

```powershell
dotnet test tests/SchoolGether.Domain.Tests --configuration Release
dotnet test tests/SchoolGether.Api.IntegrationTests --configuration Release
```

O CI executa estes testes sem MySQL nem credenciais reais. Os testes de Identity
usam SQLite relacional em memória para verificar transações, filtros, permissões,
JWT, bloqueio, renovação e revogação. A chave e as contas de teste são temporárias.
Isto não substitui verificar a migração e o fluxo acima no MySQL local; o provider
Oracle continua a ser usado pela aplicação. O teste de consumo condicional simula
um pedido com uma leitura anterior; não é um teste de carga concorrente de MySQL.

Antes da demonstração alojada, preparar a chave privada através das variáveis de
ambiente do servidor e usar HTTPS. A política de rotação das chaves, recuperação de
palavra-passe, convites, MFA, auditoria detalhada e configuração dos proxies do
alojamento serão tratadas nas próximas etapas. O emissor atual serve apenas os
clientes próprios do projeto; integrações externas exigirão um fluxo OAuth/OIDC.

## Dependências acrescentadas

- `Microsoft.AspNetCore.Identity.EntityFrameworkCore`: credenciais e persistência oficial Identity.
- `Microsoft.AspNetCore.Authentication.JwtBearer` e `System.IdentityModel.Tokens.Jwt`:
  validação e serialização de JWT com as bibliotecas Microsoft.
- Apenas nos testes: `Microsoft.EntityFrameworkCore.Sqlite` e
  `SQLitePCLRaw.bundle_e_sqlite3` 3.0.5. Esta versão do bundle evita a dependência
  nativa antiga assinalada pelo NuGet como vulnerável.

## Referências

- [Configuração de ASP.NET Core Identity](https://learn.microsoft.com/aspnet/core/security/authentication/identity-configuration?view=aspnetcore-10.0)
- [Validação de JWT Bearer](https://learn.microsoft.com/aspnet/core/security/authentication/configure-jwt-bearer-authentication?view=aspnetcore-10.0)
- [Filtros globais do EF Core](https://learn.microsoft.com/ef/core/querying/filters)
- [RFC 9700 — proteção dos refresh tokens](https://www.rfc-editor.org/rfc/rfc9700.html#section-4.14)
