# Integração contínua

## O que é verificado

O GitHub Actions executa o workflow `.github/workflows/ci.yml` em Ubuntu.
Os jobs `Compilar API` e `Compilar Site` instalam o SDK .NET 10, restauram
dependências e compilam em Release. Uma falha num job não impede o outro de terminar.

Compilar a API também valida Domain, Application, Infrastructure e Contracts.
Compilar o site também valida ApiClient e Contracts. Assim, sete dos oito projetos
de aplicação são compilados sem exigir os componentes MAUI no servidor de CI.

A solução completa inclui a app MAUI e precisa de componentes específicos de cada
plataforma. Por isso, o CI usa os caminhos dos projetos da API e do site em vez de
`dotnet build` na solução completa. A app continua a ser validada em Windows.

O job `Testes do domínio` executa os testes xUnit da entidade Institution.
O job da API também executa `migrations has-pending-model-changes` para detetar
alterações no modelo que ainda não tenham uma migração. Esse check usa uma ligação
de design sem credenciais e não acede ao servidor MySQL.

O job `Testes da API` inicia a aplicação em memória com `WebApplicationFactory`.
Verifica os endpoints de diagnóstico, erros e validação em `ProblemDetails`, CORS
e disponibilidade do Swagger apenas em desenvolvimento. Substitui a verificação
da base de dados por um serviço de teste: não precisa de MySQL nem de segredos.
Os testes de Identity usam SQLite relacional em memória e as implementações reais
de Identity/JWT, com contas e chave temporárias. Verificam também que um administrador
não lista ou cria contas noutra instituição, a rotação/revogação de sessões e as
restrições dos papéis. A versão atual do bundle SQLite evita a biblioteca nativa
vulnerável que vinha na dependência original.
Estes testes não verificam a integração real com o provider ou o servidor MySQL;
essa ligação pode ser confirmada localmente seguindo [o guia da API](api.md).

Adicionar os testes de Application com os respetivos módulos e estender os testes
de isolamento às novas tabelas institucionais, conforme definido no `AGENTS.md`.

O job `Testes do cliente API` executa a suite .NET da sessão partilhada e do
ViewModel MAUI, com HTTP e armazenamento controlados. Os testes ligam apenas o
ViewModel puro e recursos; não referenciam os targets MAUI nem precisam de
emulador. Cobrem entrada, renovação concorrente, saída durante pedidos pendentes,
falhas de rede e de armazenamento. Não substituem a execução nativa da app.

## Fluxo da equipa

1. Trabalhar num ramo `feature/<modulo>-<descricao>` a partir de `develop`.
2. Executar os comandos de compilação do README antes de enviar alterações.
3. Enviar o ramo e abrir um pull request para `develop`.
4. Confirmar que os cinco jobs do CI estão verdes no separador **Checks** do pull request.
5. Pedir revisão a outro elemento e integrar depois da aprovação.

Na configuração inicial, criar `develop` a partir de `main` se ainda não existir.
O primeiro pull request acrescenta o workflow; os eventos de push nos ramos
`feature/**` permitem validar esse workflow antes da integração.

Depois da primeira execução, configurar a proteção de `main` e `develop` para
exigir os checks `Compilar API`, `Compilar Site`, `Testes do domínio`, `Testes da API`
e `Testes do cliente API`,
além da revisão de outro elemento.
A criação do workflow não ativa automaticamente a proteção dos ramos.
O quinto job desta entrega só será executado no GitHub depois de enviar o ramo;
as verificações locais não demonstram uma execução remota do novo workflow.

## Quando uma execução falha

- **Preparar .NET 10:** verificar disponibilidade do SDK e das actions utilizadas.
- **Restaurar dependências:** verificar a versão do pacote e o acesso ao NuGet.
- **Compilar em Release:** abrir o log e corrigir os erros apontados pelo compilador.
- **Funciona no Windows mas falha em Ubuntu:** verificar maiúsculas e minúsculas nos
  caminhos; o sistema de ficheiros Linux distingue-as.

Usar os mesmos comandos do README para reproduzir a compilação localmente.
Nenhuma cadeia de ligação, chave JWT ou outro segredo é necessário para este CI.

## Referências

- [Compilar e testar .NET no GitHub Actions](https://docs.github.com/en/actions/tutorials/build-and-test-code/net)
- [Action oficial para preparar o SDK](https://github.com/actions/setup-dotnet)
- [Seleção do SDK com global.json](https://learn.microsoft.com/en-us/dotnet/core/tools/global-json)
