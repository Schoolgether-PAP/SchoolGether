# Login no site e na app

Data: 2026-10-12. Estado: implementação autorizada; proposta visual B escolhida.

O utilizador autorizou continuar o trabalho e pediu participação nas decisões
do site. Escolheu a proposta B para o login e delegou ao agente a primeira
versão do ecrã após entrar, reservando-se a possibilidade de corrigir ao seu
gosto. A primeira versão mostra o próprio perfil e a opção de sair, conforme
o âmbito desta entrega; novos módulos serão discutidos nas entregas seguintes.

Preferências confirmadas na conversa:

- A primeira página apresentada ao aluno será a página de login.
- Microsoft Teams e Google Classroom são as referências visuais indicadas.
- As cores devem estar relacionadas com a logo fornecida, com o sagui e as
  iniciais SG: castanho, laranja, branco e cinzento claro.
- O utilizador confirmou que o nome verdadeiro do projeto e o nome apresentado
  na interface são SchoolGether. Usar a logo fornecida; «SaguiLearn» aparece
  apenas no nome do ficheiro e não deve ser usado como marca na interface.

## Objetivo e ponto de partida

Permitir que a equipa e os utilizadores entrem no SchoolGether sem usar o
Swagger. A primeira entrega permite entrar com uma conta existente, consultar
o próprio perfil e sair, no site Blazor e na app MAUI Android/Windows.

O PR #4 foi aprovado por outro elemento da equipa e integrado em `develop`
(`93462d6`). A API já fornece login, perfil, renovação e logout. O ApiClient
partilhado já expõe esses pedidos; os clientes ainda têm os ecrãs de exemplo.

Decisões já tomadas pelo utilizador e pelo projeto: .NET 10, Blazor WASM,
MAUI nativo XAML/MVVM, API única, uma instituição por conta e contas criadas
pelos administradores. Não considerar prazos como variável de planeamento.

## Fluxo proposto

1. Ao abrir o site, apresentar o formulário de entrada com e-mail e
   palavra-passe, botão «Entrar» e indicação de que as contas são fornecidas
   pela instituição. Não haverá registo público.
2. Validar campos obrigatórios; durante o pedido, mostrar «A entrar…» e impedir
   envios repetidos. A palavra-passe fica oculta, com ação acessível para a
   mostrar. Não a guardar, nem preencher com credenciais de demonstração.
3. Após login, consultar `/api/v1/identity/me` e mostrar «O meu perfil» com nome,
   e-mail, papel traduzido e instituição. A API só devolve o ID da instituição;
   apresentar «Instituição 2», por exemplo, sem inventar o nome. Para o
   PlatformAdmin, apresentar «Administração da plataforma».
4. «Sair» revoga a sessão através da API e limpa os dados locais. Regressar ao
   formulário de entrada. Impedir que uma resposta atrasada de renovação ou
   perfil volte a abrir uma sessão que já terminou.
5. Quando o acesso expirar, renovar a sessão automaticamente. Se a renovação
   for rejeitada, limpar a sessão e apresentar «A sessão terminou. Entre
   novamente.» Uma falha de rede permite tentar novamente e não deve ser
   apresentada como palavra-passe incorreta.

As mesmas ações e estados existem na app. Um utilizador autenticado abre o
perfil; sem sessão válida, abre o formulário de entrada. A interface nunca
substitui a autorização e o isolamento que a API já aplica.

## Sessão e armazenamento — escolhas propostas pelo agente

- Site: sessão apenas em memória nesta entrega. Atualizar a página ou fechar
  o separador requer novo login. Não guardar tokens em localStorage ou
  sessionStorage. Explicar este comportamento na documentação de utilização.
- App: access token em memória e refresh token no SecureStorage do MAUI.
  Ao reabrir, tentar renovar antes de mostrar o perfil; se rejeitado, apagar
  o refresh token. Nunca persistir a palavra-passe. Uma indisponibilidade de
  rede ou do armazenamento deve produzir um estado recuperável, sem apresentar
  dados privados de uma sessão anterior como se estivessem confirmados.
- A gestão partilhada fica em ApiClient; cada cliente fornece o armazenamento
  adequado. Apenas uma renovação de cada vez, porque a API roda os refresh
  tokens e rejeita a sua reutilização. Uma resposta 401 pode originar no máximo
  uma renovação e uma repetição do pedido, sem ciclos de tentativas.
- Ao sair, limpar sempre a sessão local. Se a API estiver indisponível,
  informar que a saída foi local e que a sessão no servidor não foi revogada;
  não apresentar confirmação de revogação. Tokens de acesso já emitidos podem
  continuar válidos até expirar, conforme o comportamento atual da API.
- Não escrever palavras-passe, tokens ou respostas de autenticação em logs.

Estas escolhas são propostas, não preferências já expressas pelo utilizador.

## Apresentação e acessibilidade — direção proposta

O utilizador aceitou comparar duas propostas visuais no navegador. Prévia
local preparada em 2026-10-12, sem ligação à API nem recolha de credenciais:

- A: formulário centrado, com a logo acima dos campos.
- B: logo numa área própria à esquerda e formulário à direita; uma coluna
  em ecrãs estreitos.

O utilizador escolheu a proposta B. Foram verificadas no Chromium
as duas propostas em computador e telemóvel: logo carregada, alternância
entre propostas/tamanhos, ausência de deslocamento horizontal a 390 px e
ausência de erros JavaScript. Capturas inspecionadas visualmente. Isto valida
apenas o protótipo visual, não a autenticação ou a acessibilidade completa.
Os protótipos e o estado do servidor ficam em `.superpowers/`, ignorado no Git.

Interface educativa simples, em português, com formulário legível e perfil
organizado por campos, sem estatísticas ou módulos ainda inexistentes.

Substituir a proposta inicial azul por uma paleta baseada na logo, conforme
pedido do utilizador. Tons propostos pelo agente, ainda ajustáveis na revisão
visual: fundo/superfície `#FFFFFF`, texto castanho-escuro `#36281F`, ação
castanha `#663A20`, destaque laranja `#C87532`, apoio cinzento-claro `#E4E1DD`
e erro `#A12C30`. São aproximações visuais, não valores extraídos do ficheiro.
Validar os contrastes dos pares efetivamente usados antes de concluir; o
laranja não deve automaticamente receber texto branco. Usar Open Sans, já
incluída na app, com fallback do sistema no site;
caso se reutilizem os ficheiros no site, conservar a licença correspondente.

No computador, identificação SchoolGether junto ao formulário, com largura
de leitura limitada; em ecrãs estreitos, uma única coluna. Campos e textos
alinhados à esquerda. O destaque visual deve vir da identificação da escola
e da hierarquia dos campos, sem imagens decorativas ou painéis fictícios.

Suportar teclado, foco visível, etiquetas associadas, erros anunciados por
leitores de ecrã, redimensionamento de texto e alvos de toque de pelo menos
48 unidades na app. Usar SemanticProperties nos controlos MAUI interativos.
Guardar textos em recursos; remover as páginas Counter/Weather e o contador
de exemplo ao substituir a navegação. Não alegar conformidade WCAG completa
apenas com verificações automáticas.

## Arquitetura e configuração

- Clientes continuam a depender apenas de Contracts e ApiClient.
- MAUI usa CommunityToolkit.Mvvm conforme AGENTS.md; justificar a inclusão
  dessa dependência no PR. Limitar os targets suportados a Android e Windows.
- Configurar a URL da API separadamente da URL dos ficheiros do site.
  O desenvolvimento web usa `http://localhost:5227` e API em
  `http://localhost:5265`; as origens web já estão previstas no CORS local.
- Windows usa a API local. O emulador Android usa `10.0.2.2` para alcançar
  o computador; documentar a ligação e verificar o acesso real. HTTP é
  permitido apenas em desenvolvimento. Uma versão distribuída exige URL
  HTTPS configurada; não desativar validação de certificados.
- Testar num telemóvel físico fica para quando houver dispositivo e ligação
  disponíveis. Não abrir firewall nem expor a API local automaticamente.
- A configuração de URL não é secreta. Chaves JWT e ligação MySQL continuam
  exclusivamente na API, fora dos clientes e do Git.

## Verificação da entrega

O agente prepara e executa testes automatizados significativos da gestão
partilhada da sessão: entrada, perfil autenticado, uma única renovação com
pedidos simultâneos, rejeição de refresh, falha de rede e saída durante um
pedido pendente. Incluir estes testes no CI junto dos testes existentes.

Verificar login válido/inválido, perfil, saída, navegação após saída e mensagens
de erro nos clientes. Compilar o site em Release e a app para Windows e
Android. Distinguir compilação de execução real: confirmar visualmente o site
e registar explicitamente qualquer plataforma sem execução disponível.
Testar navegação por teclado e os estados de carregamento/erro.

Não repetir os testes manuais de Identity já confirmados quando a automação
cobre o mesmo comportamento. O utilizador revê os ecrãs e um colega revê o
PR após o CI, como nas entregas anteriores.

## Limites e continuação

Esta entrega não inclui gestão de contas na interface, recuperação de
palavra-passe, alteração de perfil, módulos académicos ou acesso offline.
Não requer novas tabelas ou migrações da base de dados.

Após integrar o login: preparar Aiven, alojamento HTTPS da API e GitHub Pages,
verificar o login contra a API alojada e só depois iniciar o módulo Academic.
Escolhas menores de nomes, organização de ficheiros e componentes ficam com
o implementador, respeitando estas regras e AGENTS.md.
