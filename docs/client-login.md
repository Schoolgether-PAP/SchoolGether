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
