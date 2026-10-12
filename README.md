# SchoolGether

Plataforma educativa para instituições de ensino (PAP).

O SchoolGether partilha uma API entre o site Blazor WebAssembly e a app .NET MAUI.
As decisões de arquitetura, o âmbito e as regras de trabalho estão em [AGENTS.md](AGENTS.md).

## Preparar o ambiente

- Instalar o SDK .NET 10 estável. O `global.json` mantém a solução na versão 10,
  permitindo os SDKs estáveis mais recentes dessa versão.
- Para a app, usar Windows com os componentes MAUI e o SDK Android.
- A API e o site podem ser compilados sem instalar os componentes MAUI.

Na raiz do repositório:

```powershell
dotnet restore src/Backend/SchoolGether.Api/SchoolGether.Api.csproj
dotnet build src/Backend/SchoolGether.Api/SchoolGether.Api.csproj --configuration Release --no-restore
dotnet restore src/Clients/SchoolGether.Web/SchoolGether.Web.csproj
dotnet build src/Clients/SchoolGether.Web/SchoolGether.Web.csproj --configuration Release --no-restore
```

Para executar em desenvolvimento, abrir dois terminais:

```powershell
dotnet run --project src/Backend/SchoolGether.Api --launch-profile http
dotnet run --project src/Clients/SchoolGether.Web --launch-profile http
```

A API usa `http://localhost:5265` e o site usa `http://localhost:5227`.
O código atual ainda contém exemplos dos modelos. A primeira migração MySQL
está preparada; o login será implementado nas próximas etapas.

## Integração contínua

O workflow [CI](.github/workflows/ci.yml) restaura e compila a API e o site em
Release, em dois jobs independentes num ambiente Ubuntu com .NET 10. As referências
dos projetos também compilam as cinco bibliotecas de backend e partilhadas.

É executado nos pushes para `main`, `develop` e `feature/**`, nos pull requests
para `main` e `develop`, e manualmente no separador **Actions** quando o workflow
estiver no ramo predefinido. A app MAUI é validada localmente em Windows.

O job **Testes do domínio** executa os testes xUnit de instituições. O job da API
também verifica se o modelo EF corresponde às migrações guardadas, sem aceder ao
MySQL. Os testes de aplicação e de integração serão acrescentados nas próximas etapas.
O workflow não publica a API nem o site.

Consultar [o guia de CI](docs/ci.md) para o fluxo de pull requests e diagnóstico de falhas.

## Base de dados local

Consultar [o guia de MySQL e migrações](docs/database.md) para criar a base local,
configurar a ligação privada da API e aplicar as migrações EF Core.

Para executar os testes existentes:

```powershell
dotnet test tests/SchoolGether.Domain.Tests/SchoolGether.Domain.Tests.csproj --configuration Release
```
