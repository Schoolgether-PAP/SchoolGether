# Suporte SchoolGether — descrição da primeira entrega

Data: 2026-10-13; integração revista em 2026-10-15 após publicar o PR #6. Estado: prévia visual e âmbito aprovados; revisão de construção concluída, sem questões materiais em falta. O utilizador autorizou retomar o desenvolvimento e escolheu explicitamente concluir primeiro o site e depois a app. As decisões adicionais abaixo são escolhas técnicas do agente, não respostas atribuídas ao utilizador. Este documento não altera o comportamento do produto.

## Objetivo

Permitir que instituições e professores comuniquem problemas à equipa SchoolGether e acompanhem as respostas dentro da plataforma. A equipa global organiza, atribui e resolve os pedidos, com identificação do operador e histórico das alterações.

## Decisões confirmadas pelo utilizador

- Apenas instituições e professores podem abrir pedidos. No modelo atual, instituição corresponde à conta InstitutionAdmin; professor corresponde a Teacher.
- O responsável pela instituição pode consultar e responder aos pedidos abertos pelos professores da sua instituição. O utilizador permite criar um papel adicional se necessário; escolha do agente: reutilizar InstitutionAdmin, que já representa esse responsável.
- Administradores globais autorizados podem atuar em qualquer instituição; as permissões controlam as operações.
- Manter SchoolGether, logo e paleta atuais, com organização inspirada no PHC GO.
- Não usar o tempo de desenvolvimento como variável de planeamento.
- Permitir anexos de imagens e documentos nos pedidos/conversas.
- Ter chat em tempo real entre professor ou instituição e a equipa de suporte.
- Após a resolução, encerrar definitivamente a conversa e conservá-la no histórico. Não permitir reabertura nem novas mensagens nesse chat; novo atendimento exige um novo chat.
- O utilizador delegou no agente a escolha de quem pode encerrar. Escolha do agente: tanto cliente como suporte, com confirmação; professor encerra o próprio chat, InstitutionAdmin encerra chats da sua instituição e suporte encerra conforme as suas permissões. Registar ator, instante e resumo de resolução quando encerrado pelo suporte.
- Quando não houver agente disponível, o cliente pode deixar mensagens persistidas para resposta posterior. Não perder a conversa nem exigir presença simultânea para abrir ou usar o pedido.

## Requisitos herdados da proposta geral

Cada pedido tem instituição, autor, assunto, categoria, prioridade, responsável, estado e mensagens. A proposta geral previa resolvido e fechado separados; a proposta consolidada abaixo une resolução e fecho definitivo, cumprindo a decisão posterior do utilizador, sem transição de reabertura. Notas internas ficam separadas das mensagens visíveis ao cliente. Operações administrativas relevantes ficam no histórico, sem palavras-passe ou tokens.

O acesso assistido ao painel do cliente depende de um contexto escolar útil e de especificação própria. O suporte não deve autenticar-se com a senha ou os tokens do cliente.

## Âmbito da primeira entrega

Começar pelo atendimento completo de pedidos: criação, consulta, conversa, atribuição e mudança de estado. Separar esta entrega das sanções, dos pagamentos simulados e do acesso assistido.

No painel global, uma área Suporte reúne a lista paginada, pesquisa, filtros por estado/prioridade/instituição e responsável. Um detalhe mostra a conversa e os dados do pedido; responder ao cliente e adicionar nota interna são ações identificadas separadamente. Estados vazios, erros recuperáveis e conflitos preservam os dados escritos.

Após o PR #6, integrar Suporte na navegação institucional existente (Resumo/Utilizadores/Configuração/O meu perfil), preservando essa área. Para Teacher, disponibilizar Suporte no percurso de perfil atual, com regresso ao perfil e saída, sem apresentar esse percurso como o painel académico completo. Student continua sem acesso. Decisão expressa do utilizador: implementar primeiro no site; a app terá a mesma funcionalidade numa entrega posterior, reutilizando Contracts, ApiClient e API. Não apresentar o suporte como implementado na app nesta entrega.

Manter a stack existente: .NET 10, API única, módulos nas camadas atuais, MySQL/EF Core, Contracts/ApiClient, Blazor e MAUI. Preservar isolamento por instituição e validar a autorização no servidor em cada pedido. Não adicionar bibliotecas por conveniência.

## Visibilidade proposta a partir da decisão do utilizador

O professor conserva acesso e participação nos seus próprios pedidos. InstitutionAdmin acompanha os pedidos da sua instituição e pode responder em nome dela. Outros professores não recebem acesso aos pedidos dos colegas. A equipa global consulta e responde mediante permissões de suporte. Estas fronteiras serão verificadas na API, incluindo pedidos diretos com identificadores de outra instituição. O acesso do autor e a exclusão de colegas são escolhas do agente para tornar a decisão operável, sujeitas à revisão da descrição final.

## Chat e histórico

As mensagens devem aparecer em tempo real aos participantes autorizados. Cada conversa pertence a uma instituição e mantém autor, equipa responsável, mensagens e anexos. Após interrupção de ligação, a interface deve recuperar as mensagens persistidas, sem as confundir com conteúdo já enviado. A autorização do chat e dos downloads deve respeitar as mesmas fronteiras da consulta HTTP.

O encerramento é definitivo. O servidor recusa mensagens e novos anexos no chat encerrado, incluindo pedidos diretos ou sessões antigas. O histórico permanece consultável pelos participantes autorizados. Iniciar novo chat cria uma conversa distinta e conserva a anterior intacta.

Tecnologia e limites concretos ainda não aprovados; o desenho final deverá justificar o mecanismo de tempo real compatível com Blazor WASM/MAUI e a política de anexos.

## Propostas concretas do agente

### Ecrãs e fluxo

- Painel global: área Suporte, fila paginada de 20 pedidos (máximo 100 por página), pesquisa por assunto ou ID e filtros por instituição, estado, prioridade e responsável. Separadores Em aberto e Histórico. Sem quadro Kanban nesta primeira entrega.
- Detalhe: assunto, instituição, autor, categoria, prioridade, responsável e conversa. Mensagens em ordem cronológica, com autor, data/hora local e anexos. Buscar as mais recentes e permitir carregar anteriores em lotes de 50; não descarregar todo o histórico por omissão.
- Caixa de resposta claramente identificada como Mensagem para o cliente ou Nota interna. Notas internas nunca chegam às respostas da API, às notificações ou aos downloads destinados aos clientes escolares. Mensagens enviadas são imutáveis; correções são novas mensagens.
- Professor e responsável: lista autorizada, Novo pedido, conversa, anexos e Encerrar. A alternância de perspetivas do protótipo desaparece; a conta autenticada determina o acesso.
- Novo pedido: assunto (até 150 caracteres), categoria Acesso/Académico/Problema técnico/Outro, prioridade Baixa/Normal/Alta (Normal por omissão), descrição inicial e anexos opcionais. Máximo de 4000 caracteres por mensagem/nota; rejeitar texto em branco sem anexo. Um pedido exige descrição inicial não vazia.
- Atribuição é organizativa: não impede outros operadores autorizados de responder. Mostrar responsável inativo como indisponível e permitir nova atribuição; não mudar o responsável silenciosamente.
- Conservar texto escrito e anexos ainda locais em falhas recuperáveis; mostrar progresso e impedir envios simultâneos. Ao sair da conta, eliminar rascunhos e dados em memória. Não guardar palavras-passe, tokens, notas ou ficheiros no armazenamento persistente do navegador.

### Estados e fecho definitivo

Estados: Aberto, Em atendimento, À espera do cliente e Fechado. Novo pedido começa Aberto. A equipa pode mudar entre os três estados ativos; responder não altera o estado silenciosamente. Uma mensagem do cliente em À espera do cliente volta o pedido a Aberto, para sinalizar à equipa que há resposta.

Resolver e encerrar é uma única operação, com confirmação e resumo obrigatório de até 1000 caracteres. O cliente vê o resumo público; notas internas não o substituem. Professor pode encerrar o próprio pedido; InstitutionAdmin qualquer pedido da sua instituição; operador global precisa de permissão de gestão. Quem encerra e quando ficam registados.

Fechado é terminal: não editar assunto, prioridade, atribuição, mensagens, notas ou anexos; não reabrir nem eliminar o histórico pela interface. Iniciar novo atendimento abre um pedido distinto, sem copiar mensagens ou ficheiros automaticamente. O fecho continua protegido no servidor contra pedidos diretos e envios concorrentes.

### Permissões

- Teacher ativo: criar, listar, consultar, responder, descarregar anexos públicos e encerrar apenas pedidos próprios na instituição atual.
- InstitutionAdmin ativo: as mesmas ações sobre todos os pedidos da própria instituição, incluindo os dos professores.
- Student: sem acesso ao suporte nesta entrega.
- PlatformAdmin: acrescentar support.read e support.manage ao catálogo existente. support.read permite consultar pedidos de qualquer instituição, mensagens públicas, notas internas e respetivos anexos. support.manage acrescenta responder, notas, prioridade, atribuição, estados e fecho. Exigir support.read para atribuir support.manage; validar no servidor e no formulário de permissões.
- Master recebe acesso pelo mecanismo atual; outros administradores não recebem privilégios novos automaticamente. Apenas o master delega estas permissões.
- Instituições/contas suspensas ou eliminadas não podem usar pedidos, chat ou downloads. A equipa global autorizada conserva acesso ao histórico para investigar. Revalidar estado/permissões no servidor, sem confiar apenas nos claims antigos ou na visibilidade dos botões.
- ID desconhecido ou fora do âmbito escolar devolve 404. Conta autenticada sem papel/capacidade devolve 403; sessão inválida devolve 401. Nunca devolver nomes, contagens, anexos ou notas de outra instituição a uma conta escolar.

### Anexos

Proposta inicial: JPEG, PNG, WebP, PDF, TXT, DOCX, XLSX e PPTX, até 5 MiB por ficheiro e três ficheiros por mensagem; total máximo 15 MiB. Sem ZIP genérico, executáveis, formatos com macros ou SVG. Validar tamanho durante a leitura, extensão e conteúdo; não confiar no MIME/filename enviados pelo navegador. Para Office, confirmar a estrutura Open XML e rejeitar macros/entradas perigosas, sem extrair arquivos para disco. Para TXT, exigir texto UTF-8 sem bytes NUL. Não prometer análise antivírus: não existe motor integrado nesta entrega.

Anexos pertencem a uma mensagem; herdam visibilidade pública/interna e autorização do pedido. Nomes originais servem apenas para apresentação, limitados a 150 caracteres, sem caminhos/controlos. O armazenamento usa identificadores aleatórios e fica em diretório privado de dados, nunca no bundle do site nem numa rota estática pública. Interface IFileStorage com implementação local conforme AGENTS.md; credenciais/entidades de infraestrutura não chegam aos clientes.

Download autenticado com Content-Disposition attachment, MIME escolhido pelo servidor e nosniff. Sem URLs públicas, pré-visualização de Office/HTML ou links externos de anexos. Ficheiros temporários são removidos em validação/rollback; falha de escrita não cria mensagem parcial. Limitar quota por instituição a 100 MiB inicialmente, configurável no servidor; ao atingir, continuar a permitir mensagens sem anexos. Não apagar histórico automaticamente para libertar espaço.

O processo de publicação deve preservar o diretório de anexos; migrações guardam metadados, não ficheiros binários. Backups da base não incluem o disco: documentar cópia dos anexos separadamente. Não atualizar produção antes de verificar upload/download e persistência após reinício na demonstração.

### Tempo real, avisos e falhas de ligação

Usar ASP.NET Core SignalR, já incluído no framework do servidor, e o cliente oficial Microsoft.AspNetCore.SignalR.Client 10.0.13, compatível com .NET 10 e mínimo corrigido para GHSA-334f-vvp4-7g6p. É a única dependência nova proposta: permite reaproveitar o chat na app e evita um protocolo WebSocket próprio. Referência: https://learn.microsoft.com/en-us/aspnet/core/signalr/dotnet-client?view=aspnetcore-10.0.

Enviar mensagens e anexos por HTTP autenticado; SignalR avisa que existem alterações, sem transportar corpos de mensagens, notas ou anexos. O cliente volta a consultar a API autorizada. A inscrição num pedido exige autorização; separar os grupos público/interno. Nunca enviar notificações de notas internas aos grupos escolares. Fechar ligações quando expira a autenticação, revalidar inscrições ao reconectar e obter o token pela sessão existente, sem expor tokens aos componentes.

Nesta entrega, usar LongPolling com Authorization no navegador e rejeitar autenticação por access_token na query em todos os caminhos, incluindo o hub. A supressão de queries nos registos IIS W3C não está demonstrada; WebSockets nativos podem usar cabeçalhos. Não registar query strings sensíveis. Rejeitar origens de navegador fora do CORS autorizado. Referência: https://learn.microsoft.com/en-us/aspnet/core/signalr/authn-and-authz?view=aspnetcore-10.0.

Ao reconectar, consultar mensagens posteriores ao último ID conhecido e atualizar estado/permissões; não confiar na entrega de eventos durante a interrupção. Usar um client_id GUID por envio para reconhecer repetições. Se a mesma chave já tiver sido aceite, devolver o envio existente; a mesma chave com conteúdo diferente devolve 409. Nunca duplicar mensagens/anexos numa repetição após timeout.

Se o canal ao vivo falhar mas HTTP funcionar, continuar a permitir enviar e disponibilizar Atualizar conversa. Mostrar que as atualizações ao vivo estão interrompidas. Falha total de ligação conserva o rascunho em memória e não mostra Enviado. Não existe fila offline persistente, envio automático de mensagens antigas ou indicador fictício de operadores disponíveis.

Avisos dentro da plataforma e atualização da fila/conversa nesta entrega. E-mail, notificações push, digitação, recibos de leitura e presença de operadores ficam para depois. Não introduzir um serviço externo de comunicação.

### Modelo, consistência e auditoria

Módulo Support em Domain/Application/Infrastructure/Api, DTOs em Contracts, acesso HTTP/tempo real em ApiClient e componentes Blazor próprios. Reutilizar interfaces de Identity/Institutions para elegibilidade; não ler tabelas de outro módulo diretamente nos serviços de suporte. Persistência por EF/MySQL, migração nova sem alterar migrações anteriores.

Entidades support_ticket, support_message e support_attachment com institution_id e campos de controlo do projeto; ticket tem author_id, assunto, categoria, prioridade, responsável, estado, resumo/ator/data de fecho e row_version. Mensagens e anexos têm autor/data, client_id para idempotência e visibilidade herdada. Autor/nome de apresentação guardados no envio para preservar o histórico.

Filtros globais EF e guardas de escrita para dados escolares. Operações globais só atravessam instituições após autorização explícita de suporte. HTTP, hub e download usam a mesma política de âmbito, com DTOs escolares que excluem notas e anexos internos na consulta, não apenas na apresentação.

Criação, resposta, nota, atribuição, prioridade, transições e fecho auditados transacionalmente, com ator, instituição, alvo e resultado. Download/recusas relevantes registados sem conteúdo de mensagens, bytes, tokens ou segredos. Reutilizar o escritor append-only existente; usar unidade de trabalho própria de Support para não mapear conflitos/uniqueness como AccountExists do módulo administrativo.

Todas as mutações do pedido atualizam row_version. Conflito de versão devolve 409 e preserva rascunho; a interface atualiza o pedido e pede decisão ao utilizador, sem repetir uma mutação silenciosamente. A escrita da mensagem concorre com o fecho sobre a mesma versão, impedindo acrescentar conteúdo após o fecho. Metadados e auditoria são atómicos; ficheiros são preparados em temporário e compensados se a transação falhar. Publicar eventos só depois do commit.

### Verificação e exclusões

Testar regras de fecho/permissões, limites/assinaturas de anexos, idempotência, corrida entre envio e fecho, rollback de ficheiros, notas internas, quota e isolamento em consulta/download/hub. Integração HTTP com SQLite de teste, mais cenário do provider Oracle em MySQL temporário; não usar a base online para suites destrutivas.

No site, verificar fila/criação/conversa/atribuição/nota/fecho/histórico, reconexão, rascunhos/409, teclado, foco e 390 px. Testar chat com duas sessões autorizadas e prova de que a sessão escolar não recebe notas internas. Só publicar depois de CI, revisão e aprovação do PR pela equipa.

Excluídos: app nativa nesta etapa, e-mail/push, pagamentos, punições, acesso assistido ao painel do cliente, exclusão de históricos, edição/reabertura de conversas fechadas, chatbot/respostas automáticas e importação dos exemplos do protótipo.

## Revisão de construção e decisões técnicas de 2026-10-15

Revisão independente da descrição: nada importante em falta para planear. A integração após o PR #6 está atualizada acima. Validar antes da publicação o diretório privado gravável, limites de upload, espaço, persistência em reinícios/publicações e cópia/restauro dos anexos no MonsterASP; verificar também os transportes SignalR. São verificações técnicas do agente, não requisitos a devolver ao utilizador como perguntas.

Escolhas do agente para tornar o plano executável:

- Acrescentar `support_storage_usage` ao módulo Support para reservar quota por instituição de forma atómica, inclusive entre pedidos distintos. Não escrever em tabelas de Institutions para bloquear essa quota.
- Corpos multipart limitados a 16 MiB, incluindo metadados; três ficheiros de até 5 MiB continuam o máximo. Diretórios temporários e definitivos privados. Validar Office sem extração, com limites de entradas e de tamanho descomprimido; não executar conteúdo recebido.
- Fingerprint de idempotência inclui texto, visibilidade e os anexos (nome normalizado, ordem, tamanho e hash). Repetir a mesma chave/conteúdo devolve o resultado já aceite, sem nova escrita, mesmo que entretanto tenha sido fechado; conteúdo diferente devolve 409. Isto não reabre o pedido nem permite acrescentar mensagens.
- Revalidar os destinatários antes de emitir avisos; uma inscrição anterior num grupo não conserva autorização após suspensão ou perda de permissão. Os avisos não contêm texto ou anexos. Uma falha do canal ao vivo não desfaz uma mensagem HTTP já confirmada.
- Concluir os ficheiros antes do commit SQL, compensar falhas e limpar órfãos antigos sem apagar ficheiros referenciados. Publicação preserva o diretório privado; backup SQL e de ficheiros são verificados em conjunto.
- Primeira implantação do suporte permanece desativada até verificação do alojamento e revisão da equipa. Não alterar as opções de adesão já publicadas.

## Próximo passo

Rever o plano `docs/superpowers/plans/2026-10-15-support.md` antes da execução por tarefas. Nenhum código de suporte foi criado neste desenho.




## Prévia visual de 2026-10-13

Protótipo descartável autorizado, com marcação visível de dados de exemplo e sem chamadas à API/base. Ficheiro local `.superpowers/brainstorm/435-1791916458/content/support-v1.html`; estado/endereço em `state/server-info` na mesma sessão. A troca Suporte/Professor/Responsável é uma simulação de perspetiva, não uma forma de autenticação.

Inclui fila pesquisável, abertos/histórico, conversa, identificação do autor, anexo de exemplo, resposta pública/nota interna, contexto/responsável e encerramento confirmado definitivo. Cliente envia mesmo sem suporte disponível; novo atendimento recebe outro ID. Professor vê apenas os próprios pedidos; responsável vê os da sua instituição. Anexos selecionados ficam apenas como nome nesta página; nenhum upload/download real ou comunicação em tempo real foi implementado. A resposta automática serve somente para demonstrar a interação.

Verificação Chromium aprovada em computador e 390 px: logo, pesquisa, troca de perspetiva, ausência de notas internas para clientes, mensagem sem agente, nome de anexo, encerramento/histórico, criação de chat distinto, diálogo/Escape e ausência de overflow horizontal. Zero erros de página e nenhuma chamada à API ou a serviços externos. Capturas inspecionadas em `.superpowers/verification/support-preview/`. Isto não comprova autorização no servidor nem implementação do módulo.
