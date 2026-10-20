# Publicação de demonstração

## Endereços

- Site: https://schoolgether-pap.github.io/SchoolGether/
- API: https://schoolgether-api.runasp.net/
- Funcionamento: https://schoolgether-api.runasp.net/api/v1/health
- Base de dados: https://schoolgether-api.runasp.net/api/v1/health/ready

A raiz da API não é uma página do site. Swagger está disponível apenas em desenvolvimento.

## Versão atual — 2026-10-15

O PR #6 foi revisto por outro elemento e integrado em `develop`, commit `7f9354af6bc8028164dc31d2647dc36b3d8c9c97`. A árvore publicada coincide com o head revisto `97e57aa`, cujos cinco jobs do CI passaram. API e site foram gerados em Release a partir do commit integrado. O ramo de artefactos `gh-pages` foi atualizado para `bb7c657`, preservando a história; `deployment-manifest.json` no site identifica o commit de origem.

As migrações EF `20261014093325_PublicInstitutionOnboarding` e `20261014210542_InstitutionSetupCompletion` foram aplicadas ao Aiven com a credencial operacional. Antes delas foi criado um backup consistente, cifrado localmente e verificado após decifração; a ligação do cliente de backup validou CA e hostname com `VERIFY_IDENTITY`. As contagens de instituições e contas ficaram iguais após as migrações. A API conserva a sua credencial SQL de execução, sem permissões de migração.

A adesão pública está ativa na demonstração (`Onboarding__Enabled=true` apenas na configuração privada do alojamento). O valor por omissão do código continua falso. A raiz do site apresenta a empresa/produto e os planos; a entrada tem percurso separado. Ver [public-onboarding.md](public-onboarding.md).

Antes de ativar, uma sonda foi comparada com os registos W3C do website correto no MonsterASP.NET: o IP registado correspondeu ao IP de saída conhecido e diferiu do `X-Forwarded-For` de teste. O pacote utiliza IIS `inprocess`; a aplicação usa `RemoteIpAddress` e não lê esse cabeçalho diretamente. Não foram adicionadas regras genéricas de confiança em proxies. Esta verificação é específica deste alojamento/configuração e deve ser repetida se a infraestrutura mudar.

Verificação online: funcionamento/prontidão 200, catálogo e três planos, entrada do master/dashboard/saída, adesão 201 e entrada automática como InstitutionAdmin, configuração/ano letivo persistentes, criação/listagem de docente e aluno e ocupação 3/100. Chromium verificou desktop e 390 px; saída devolveu 204 e reutilizar um refresh revogado devolveu 401. Docente/aluno entraram com os próprios perfis e receberam 403 nas operações administrativas testadas; configuração sem sessão devolveu 401. Sem erros JavaScript na verificação final.

Foi conservada uma instituição claramente identificada como «Teste técnico PR 6», com responsável, docente e aluno fictícios (`example.invalid`). Não foram eliminados dados anteriores nem promovidas contas existentes. Estas verificações não substituem os testes locais de isolamento entre duas instituições, concorrência ou carga, nem demonstram a execução nativa da app. Os relatórios operacionais, capturas e credenciais cifradas estão na pasta privada ignorada `.superpowers/deployment/`; não publicar os seus conteúdos no Git.

## Histórico — publicação inicial de 2026-10-13

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

## Operar a adesão pública

O processo de ativação desta entrega foi concluído conforme a secção da versão atual. Para outras instalações, manter `Onboarding__Enabled=false` até CI, revisão por colega, escolha do commit, backup, migrações e validação do IP efetivo. Não copiar os segredos da demonstração para outro ambiente.

Aplicar novas migrações por EF com a credencial operacional privada e cópia de segurança, preservando os dados existentes. Os testes de migrações/legado/concorrência usam instâncias temporárias Oracle MySQL 8; a aplicação ao Aiven é uma etapa operacional separada.

Validar no alojamento o endereço efetivo usado por `RemoteIpAddress`: a adesão limita 3/minuto/IP e 20/minuto/processo, sem fila, corpo máximo de 16 KiB. A aplicação não lê X-Forwarded-For diretamente; configurar confiança no proxy só após confirmar a infraestrutura. Não ativar com todos os utilizadores indistintamente identificados pelo mesmo proxy nem confiar em cabeçalhos arbitrários. Os limites por processo não são distribuídos entre instâncias.

Publicar API e ficheiros estáticos com base `/SchoolGether/`, configurar a origem CORS/API HTTPS e verificar catálogo/desativação, navegação por query/reload, entrada, configuração, capacidade, 390 px e saída. Só após estas verificações e validação controlada do IP/proxy deve o operador ativar `Onboarding__Enabled=true`. Pagamentos permanecem fictícios. Os 265 testes normais, quatro cenários MySQL reais e fixtures Chromium são evidência local; distinguir esses resultados do CI e das verificações da demonstração.

Durante esta publicação, MySQL (TCP 21290) e WebDeploy (TCP 8172) expiravam numa rede Wi-Fi e passaram numa ligação móvel autorizada. Se voltar a acontecer, comparar as ligações com `Test-NetConnection` antes de alterar credenciais ou código; não desativar o firewall/antivírus como tentativa de correção.

## Suporte — pacote local, ainda sem publicação

Em 2026-10-17 foi preparado localmente um pacote API Release autocontido `win-x64`, com runtime .NET/ASP.NET Core 10.0.13 e `Support:Enabled=false`, e o pacote estático Web Release. O `web.config` do pacote privado fixa `maxAllowedContentLength=16777216`; não foi aplicado ao alojamento. A pasta privada local está fora de `wwwroot`, com criação/leitura/remoção pelo utilizador local e espaço livre verificados; a identidade IIS e persistência por WebDeploy/reinício continuam por verificar. A máquina conserva o runtime global 10.0.12; não foi atualizado. Os pacotes, manifestos SHA-256, ensaio conjunto SQL+ficheiros/restauro cifrado DPAPI e logs estão em `C:/PAP/artifacts/support-task8/`, ignorados pelo Git. Não foram enviados para MonsterASP.NET/GitHub Pages nem aplicadas migrações ao Aiven. A versão online continua a indicada acima, com seis migrações e sem suporte publicado. Os resultados locais constam em [support.md](support.md); não representam CI nem validação do alojamento.

Antes de qualquer publicação/ativação de suporte, o operador deve cumprir estes requisitos e guardar evidência da instalação de destino:

1. Rever o ramo completo, corrigir os problemas relevantes, obter CI verde e revisão por colega e escolher o commit aprovado. Não alterar a versão estável enquanto estes requisitos não forem satisfeitos.
2. Parar escritas durante a cópia conjunta SQL + diretório privado de anexos, guardar um manifesto SHA-256 e ensaiar o restauro num destino isolado. Uma cópia SQL isolada não preserva os anexos. Aplicar as duas migrações Support por EF com a credencial operacional; a credencial normal da API mantém apenas as permissões de execução.
3. Confirmar runtime ASP.NET Core corrigido 10.0.13 ou superior na linha .NET 10, ou compatibilidade do pacote autocontido com o IIS/ANCM do alojamento. O pacote local não prova essa compatibilidade.
4. Definir `Support__FilesRoot` como diretório absoluto privado fora de `wwwroot` e de qualquer raiz estática. Verificar permissões efetivas de leitura/escrita/criação/remoção para a identidade do processo IIS, espaço livre e quota institucional (`Support__InstitutionStorageLimitBytes`, inicial 100 MiB). Verificar que WebDeploy não remove esse diretório e que upload, download, reinício e nova publicação conservam ficheiros e correspondência com a base. Nunca apontar para Downloads, pasta pública ou link/reparse point.
5. Confirmar no IIS, proxy e API o corpo multipart máximo de 16 MiB (16 777 216 bytes); ensaiar o limite real, a recusa acima do limite e os limites de três anexos, 5 MiB cada/15 MiB combinados. O atributo da API não prova a configuração IIS.
6. Verificar no domínio real SignalR LongPolling com `Authorization`, negociação, CORS com origem autorizada, GET/POST/DELETE, interrupção/reconexão e recuperação por cursor/estado. Não ativar query bearer nem stateful reconnect. Confirmar que os registos do processo/host não registam credenciais; a configuração de logs local não demonstra a configuração W3C do IIS.
7. Publicar inicialmente com `Support__Enabled=false`. Só após migração/upload/configuração revistos, validar com contas fictícias em duas sessões: mensagem pública, nota interna e anexo invisíveis à escola, isolamento entre instituições/docentes, anexos privados, fecho terminal/histórico e recuperação de fecho durante interrupção. Ativar só quando todas estas verificações tiverem passado; em caso de falha conservar/restaurar a versão estável e o conjunto consistente SQL+ficheiros.

Os requisitos de alojamento dos pontos 3–7 ainda não foram verificados nesta entrega local, pois exigem acesso/configuração/publicação no destino. A revisão global do ramo e o CI pertencem à etapa seguinte. Não foram criados novos ecrãs MAUI, e-mail/push, sanções, faturação real ou acesso assistido.
