# Publicação de demonstração

## Endereços

- Site: https://schoolgether-pap.github.io/SchoolGether/
- API: https://schoolgether-api.runasp.net/
- Funcionamento: https://schoolgether-api.runasp.net/api/v1/health
- Base de dados: https://schoolgether-api.runasp.net/api/v1/health/ready

A raiz da API não é uma página do site. Swagger está disponível apenas em desenvolvimento.

## Versão publicada em 2026-10-13

Código da aplicação: `565801362f1a654ea1256edfa40d8ab00c76e655`, com os cinco jobs do CI aprovados. Pacote estático: ramo `gh-pages`, commit `b68ae453b8134ac1bf77b7c733d9d1407abd38a1`; a origem consta em `deployment-manifest.json`. O PR #5 mantém a revisão/integração do código separada da publicação dos artefactos.

MySQL Aiven no plano gratuito de 1 GB, independente da base local. As quatro migrações EF existentes foram aplicadas; a conta master online foi criada e promovida com autorização explícita. Não foram copiadas contas nem dados pessoais do desenvolvimento.

API .NET 10 no MonsterASP.NET, ambiente Production. Ligação SQL própria com permissões SELECT/INSERT/UPDATE/DELETE e SSL obrigatório; o utilizador operacional de migrações não é usado pela API. Chave JWT exclusiva e CORS limitado à origem `https://schoolgether-pap.github.io`.

O provider Oracle utiliza `SslMode=VerifyCA` e a CA do projeto Aiven. A cadeia e o hostname foram também verificados separadamente com OpenSSL; isto não constitui validação de hostname por `VerifyFull` em cada ligação do provider. Não desativar a validação TLS.

## Atualizar a demonstração

1. Rever o PR e o CI com outro elemento da equipa. Escolher o commit que será publicado.
2. Se houver novas migrações, aplicá-las por EF com a credencial operacional privada antes de publicar a API; nunca editar o esquema manualmente.
3. Gerar a API com `dotnet publish src/Backend/SchoolGether.Api -c Release`. Configurar variáveis do processo IIS: `ASPNETCORE_ENVIRONMENT`, `ConnectionStrings__SchoolGether`, `Jwt__PrivateKeyPem`, `Cors__AllowedOrigins__0` e `AllowedHosts`. Incluir a CA referenciada na ligação.
4. Publicar pelo perfil WebDeploy privado do website correto, utilizando o endpoint HTTPS com certificado válido. Não ativar opções que ignorem certificados nem enviar segredos para o Git.
5. Gerar o site com `dotnet publish src/Clients/SchoolGether.Web -c Release`. No pacote `wwwroot`, configurar `<base href="/SchoolGether/" />` e `Api:BaseUrl` com o endereço HTTPS da API. Incluir `.nojekyll` e preservar os bytes dos recursos com `.gitattributes` (`* -text`). Não reutilizar versões comprimidas anteriores de ficheiros alterados.
6. Atualizar apenas o ramo de artefactos `gh-pages`, preservando a sua história; não integrar este ramo no código-fonte. GitHub Pages publica a raiz desse ramo.
7. Verificar os dois diagnósticos, recursos WASM, login, painel, CORS, versão móvel e logout. Confirmar que um refresh token revogado recebe 401.

Perfis de publicação, segredos e relatórios operacionais locais ficam em `.superpowers/deployment/`, ignorada pelo Git. A equipa deve configurar os seus próprios acessos aos alojamentos; os clientes comunicam exclusivamente com a API.

## Verificação e limites

Na publicação inicial, os dois diagnósticos HTTPS devolveram 200. Chromium carregou a versão pública, entrou com o master, abriu o dashboard, verificou 390 px e saiu; logout devolveu 204 e a reutilização do refresh token devolveu 401. Sem erros JavaScript ou falhas de rede inesperadas. O isolamento entre instituições tem cobertura anterior automatizada em MySQL; não foi repetido criando instituições nesta base online.

Os planos gratuitos destinam-se à demonstração: podem suspender processos por inatividade e a primeira resposta pode demorar. No MonsterASP.NET, HTTPS foi ativado pelo utilizador após o upload. Pagamentos continuam fictícios; o módulo de suporte ainda não está implementado nesta versão.
