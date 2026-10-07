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
O código atual ainda contém os exemplos dos modelos; o login e a base de dados
serão implementados nas próximas etapas.

## Integração contínua

O workflow [CI](.github/workflows/ci.yml) restaura e compila a API e o site em
Release, em dois jobs independentes num ambiente Ubuntu com .NET 10. As referências
dos projetos também compilam as cinco bibliotecas de backend e partilhadas.

É executado nos pushes para `main`, `develop` e `feature/**`, nos pull requests
para `main` e `develop`, e manualmente no separador **Actions** quando o workflow
estiver no ramo predefinido. A app MAUI é validada localmente em Windows.

Ainda não existem projetos de testes; nesta etapa o CI verifica a compilação.
Os testes xUnit devem entrar no workflow quando forem criados os primeiros módulos.
O workflow não publica a API nem o site.

Consultar [o guia de CI](docs/ci.md) para o fluxo de pull requests e diagnóstico de falhas.
