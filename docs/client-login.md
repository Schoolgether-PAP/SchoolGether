# Entrada no SchoolGether

O site permite entrar com uma conta existente, consultar o próprio perfil e
sair. As contas são criadas pelos administradores; não existe registo público
nem recuperação de palavra-passe nesta entrega.

## Executar o site

Requer .NET 10:

```powershell
dotnet run --project src/Clients/SchoolGether.Web --launch-profile http
```

O site abre em http://localhost:5227 e usa a API em http://localhost:5265/.
O perfil de execução HTTPS usa https://localhost:7256. Estas duas origens já
estão autorizadas no CORS local da API; esta entrega não amplia o CORS.

A configuração da API é independente da origem dos ficheiros estáticos.
`src/Clients/SchoolGether.Web/wwwroot/appsettings.Development.json` define
`Api:BaseUrl` para desenvolvimento. Em produção, definir uma URL absoluta
HTTPS em `wwwroot/appsettings.json` antes de publicar os ficheiros estáticos.
A configuração pública não deve conter chaves, palavras-passe ou cadeias de
ligação. Sem URL válida, o site apresenta uma mensagem de configuração e
impede a entrada; nunca envia credenciais à origem dos ficheiros por omissão.
HTTP é aceite apenas no ambiente Development.

## Entrar, consultar e sair

Indicar e-mail e palavra-passe. A ação Mostrar/Ocultar controla a visibilidade
da palavra-passe, que é limpa após cada resposta. O perfil é consultado na API
após a entrada e mostra nome, e-mail, papel traduzido e ID de instituição.
O administrador global vê Administração da plataforma.

Atualizar perfil volta a consultar a API através da gestão partilhada de
sessão. Se o acesso tiver expirado, a sessão tenta renovar uma vez; uma
renovação rejeitada termina a sessão e pede nova entrada. Uma falha de rede
permite tentar novamente. Não existe temporizador global de renovação.

A sessão do site fica apenas em memória. Atualizar a página, fechar ou abrir
outro separador exige nova entrada. Não são guardados tokens em localStorage
ou sessionStorage e a palavra-passe nunca é persistida.

Sair limpa sempre a sessão local. Se a revogação remota não for confirmada,
o site informa que a saída ocorreu neste dispositivo. Tokens de acesso já
emitidos podem continuar válidos até expirar, conforme a API.

## Verificação

O formulário tem etiquetas, foco visível, mensagens anunciadas e ordem de
teclado. O layout B coloca a logo original à esquerda e o formulário ou perfil
à direita, passando a uma coluna em ecrãs estreitos. Os contrastes e a ausência
de overflow foram verificados em Chromium a 390 px; isto não constitui uma
auditoria completa de acessibilidade.

Os cenários de navegador desta entrega usam respostas HTTP Identity
controladas e contas isoladas em `example.test`. Não usam credenciais pessoais,
a base MySQL existente ou uma API de produção. Os testes partilhados cobrem
a gestão e concorrência da sessão; uma integração real com o alojamento será
verificada na entrega de publicação.

## Executar a app Android e Windows

A app usa .NET 10 MAUI nativo e CommunityToolkit.Mvvm 8.4.2, exigido pelo
AGENTS.md para MVVM. Apenas Android e Windows são targets suportados.

Com a API local em execução na porta 5265, a versão Debug Windows usa
`http://localhost:5265/`; o emulador Android usa `http://10.0.2.2:5265/` para
chegar ao computador anfitrião. `localhost` no emulador aponta para o próprio
emulador. Não é necessário abrir a firewall nem usar um dispositivo pessoal.

```powershell
dotnet build src/Clients/SchoolGether.Mobile/SchoolGether.Mobile.csproj -c Debug -f net10.0-windows10.0.19041.0
dotnet build src/Clients/SchoolGether.Mobile/SchoolGether.Mobile.csproj -c Debug -f net10.0-android
```

A app mantém o access token em memória e apenas o refresh token no
`SecureStorage.Default`, com a chave `schoolgether.refresh-token`. A
palavra-passe é limpa depois de cada tentativa. O arranque tenta recuperar a
sessão uma vez; uma falha de rede ou de armazenamento mostra uma ação para
repetir. O backup Android está desativado para evitar restaurar credenciais
protegidas com chaves de outro dispositivo.

Ao sair, o perfil desaparece mesmo que o armazenamento falhe. Nesse caso,
a app informa que não confirmou a remoção guardada nem a revogação remota,
bloqueia nova entrada e oferece repetir a limpeza. Se apenas a API falhar,
a sessão local é removida e a mensagem identifica a saída local.

Em Release, configurar explicitamente a propriedade de build pública
`SchoolGetherApiBaseUrl` com a URL HTTPS da API, por exemplo:

```powershell
dotnet build src/Clients/SchoolGether.Mobile/SchoolGether.Mobile.csproj -c Release -f net10.0-android '-p:SchoolGetherApiBaseUrl=https://api.example.test/'
```

O endereço acima é ilustrativo, não um serviço de demonstração. Sem URL
absoluta HTTPS válida (ou com credenciais, query ou fragmento), a app mostra
uma mensagem de configuração e bloqueia a entrada e a recuperação. Não há
fallback para a API local. HTTP Android é permitido apenas sob `DEBUG`; em
Release, `UsesCleartextTraffic` é `false`. Não há bypass de certificados TLS.

O layout da app coloca logo e conteúdo lado a lado quando há largura
suficiente e numa coluna em ecrãs estreitos. Tem recursos em português,
controlo da visibilidade da palavra-passe, anúncios de mensagens, semântica
e alvos mínimos de 48 unidades. A app mostra apenas o próprio perfil e saída.

A verificação desta entrega cobre testes .NET do ViewModel com sessão real
e HTTP controlado, e compilação Windows/Android. A interface nativa, o
SecureStorage real e a ligação do emulador à API não foram executados neste
ambiente; requerem verificação num anfitrião MAUI com execução disponível.
A compilação não demonstra esse funcionamento nem conformidade completa de
acessibilidade.
