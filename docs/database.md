# Base de dados local — Fase 0, etapa 5

Estado do ambiente de desenvolvimento: o assistente foi executado e a migração
`20261007225331_InitialInstitutions` foi aplicada com sucesso em 9 de outubro de 2026.
Não é necessário voltar a executar o assistente neste computador.

## Preparar o MySQL

Pré-requisitos: SDK .NET 10, MySQL Server 8.0 iniciado e acesso ao utilizador
`root`. O Workbench é uma ferramenta para consultar a base de dados; a API usa
o provider Oracle `MySql.EntityFrameworkCore` 10.0.9 para EF Core 10.

Na raiz `C:\PAP\SchoolGether`, abrir PowerShell e executar:

```powershell
.\scripts\Setup-LocalDatabase.ps1
```

Se o MySQL estiver noutra pasta, indicar o executável:

```powershell
.\scripts\Setup-LocalDatabase.ps1 -MySqlPath 'D:\MySQL\bin\mysql.exe'
```

O assistente pede a palavra-passe de `root` sem a mostrar, cria a base
`schoolgether` com `utf8mb4` e um utilizador `schoolgether_dev@localhost` com
permissões apenas nessa base. Gera a palavra-passe desse utilizador e guarda
a ligação em `ConnectionStrings:SchoolGether` nos user-secrets da API.
As permissões incluem criação e alteração de tabelas para executar migrações
no ambiente local. Em produção, separar o utilizador da aplicação do utilizador
que aplica as migrações.

O assistente não altera a palavra-passe de um utilizador existente e não cria
tabelas. Se terminar com erro, verificar o estado no Workbench antes de repetir.
Se a configuração já estiver concluída, avançar diretamente para as migrações.

## Aplicar migrações

```powershell
dotnet tool restore
dotnet ef database update --project src/Backend/SchoolGether.Infrastructure --startup-project src/Backend/SchoolGether.Api
```

O manifesto local fixa `dotnet-ef` em 10.0.9 para acompanhar o provider e as
ferramentas EF. A factory de design lê os user-secrets da API e as variáveis de
ambiente; estas últimas têm prioridade. A API em Development também lê os
user-secrets. A ligação nunca deve ser colocada nos appsettings versionados.

Para verificar as migrações conhecidas sem aceder ao servidor:

```powershell
dotnet ef migrations list --no-connect --project src/Backend/SchoolGether.Infrastructure --startup-project src/Backend/SchoolGether.Api
```

Para criar uma nova migração após alterar entidades ou configurações:

```powershell
dotnet ef migrations add NomeDaAlteracao --project src/Backend/SchoolGether.Infrastructure --startup-project src/Backend/SchoolGether.Api --output-dir Persistence/Migrations
```

As alterações de estrutura devem sempre passar por migrações EF, conforme o
AGENTS.md. Não editar as tabelas manualmente no Workbench.

## Confirmar no Workbench

Atualizar a lista **Schemas** e abrir `schoolgether`. O EF guarda as migrações
aplicadas na tabela `__EFMigrationsHistory`. Consultar essa tabela para confirmar
o nome e a versão aplicados.

A primeira migração cria `institution`: `id` inteiro auto-incremental, `name`
com até 200 caracteres e os campos `created_at`, `updated_at`, `created_by`,
`deleted_at` e `row_version`. As datas são guardadas em UTC. `created_by` é
opcional nesta etapa; a referência aos utilizadores entra com Identity.

O domínio incrementa `row_version` ao alterar o nome ou eliminar logicamente;
o EF utiliza essa coluna para detetar alterações concorrentes. O filtro global
exclui instituições eliminadas das consultas normais.

`institution` representa a própria instituição. O seu `id` será referenciado
como `institution_id` nas futuras tabelas de dados de cliente. O isolamento
entre instituições entra com Identity e será verificado por testes específicos.
Esta etapa não expõe endpoints de instituições nem implementa autenticação.

## Se o utilizador já existir e faltar a ligação

Usar **Manage User Secrets** no projeto da API no Visual Studio e configurar
`ConnectionStrings:SchoolGether` com a ligação do utilizador existente. Usar
`Server=localhost;Port=3306;Database=schoolgether;User=schoolgether_dev;Password=...;`,
substituindo a palavra-passe apenas no ficheiro privado de user-secrets.
Se a palavra-passe desse utilizador se perdeu, redefini-la através do Workbench
com acesso de root e atualizar o segredo privado.

Os user-secrets são uma configuração local de desenvolvimento; não estão
encriptados e não devem ser copiados para o repositório.

## Aiven e demonstração

A ligação Aiven será configurada depois de validar o ambiente local. No servidor,
usar `ConnectionStrings__SchoolGether` e a configuração TLS exigida pelo serviço.
Não executar o assistente local contra Aiven nem publicar credenciais.

## Referências

- [Provider Oracle no NuGet](https://www.nuget.org/packages/MySql.EntityFrameworkCore/10.0.9)
- [Criação do DbContext pelas ferramentas EF](https://learn.microsoft.com/en-us/ef/core/cli/dbcontext-creation)
- [User-secrets em desenvolvimento](https://learn.microsoft.com/en-us/aspnet/core/security/app-secrets?view=aspnetcore-10.0)
