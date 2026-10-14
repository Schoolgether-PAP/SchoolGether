# SchoolGether — página pública e adesão institucional

Data inicial: 2026-10-13. Revisão: 2026-10-14. Estado: proposta e plano aprovados pelo utilizador com «Está aprovado», após revisão independente e escolha de preços e limites. Implementação local autorizada. Nenhum registo público implementado ou publicado no momento da aprovação.

## Decisões recebidas

- Correção expressa do utilizador: SaguiLearn é a empresa; SchoolGether é uma aplicação desenvolvida por ela. A página do produto apresenta SchoolGether; Sobre nós identifica SaguiLearn. Não tratar estes nomes como nomes anteriores/alternativos do mesmo produto, nem inventar registo jurídico da empresa.

- A raiz do site deve apresentar o SchoolGether, a empresa/equipa, as funcionalidades e os planos. O login passa a ser uma opção Entrar, não a página inicial.
- Visitante escolhe um plano, cria as credenciais do responsável, cria a instituição e configura a plataforma, com percurso semelhante ao PHC GO.
- Pagamentos são fictícios e apenas para demonstração, conforme decisão anterior. Não recolher cartão ou criar cobranças reais. Em 2026-10-14, o utilizador escolheu os preços de 59 €, 149 € e 299 € por mês propostos no documento de negócio, em vez dos exemplos anteriores de 19 €, 49 € e 99 €.
- Desenvolver também os painéis da instituição, dos professores e dos alunos; os respetivos módulos educativos terão entregas próprias.
- Manter nome, logo, cores e .NET 10/API única/Blazor WASM/GitHub Pages. Uma instituição por conta.
- A adesão autónoma passa a ser uma exceção explícita à criação de contas por administradores: cria exclusivamente o primeiro InstitutionAdmin e a sua nova instituição. Professores, alunos e restantes responsáveis continuam a ser criados por administradores. Nunca permitir escolher PlatformAdmin/master no registo público.

## Referência observada

https://www.cegid.com/ib/pt/produtos/cegid-phc-software-gestao/cegid-phc-go/ consultado em 2026-10-13: apresentação do produto/benefícios, comparação de três planos, opções de subscrição e chamada Experimente grátis. O formulário público de trial pede nome, e-mail, telefone, empresa e NIF, com consentimento. Isto inspira a organização da apresentação e adesão; não demonstra o provisionamento interno da Cegid, nem implica copiar preços, texto, marcas ou regras comerciais.

## Proposta visual

Página pública: navegação Plataforma, Planos, Sobre nós e Entrar; introdução SchoolGether, apresentação das três perspetivas (instituição/professor/aluno), comparação de planos, explicação da adesão e secção sobre SaguiLearn, a empresa responsável pelo produto. Indicar claramente a demonstração e identificar funcionalidades ainda em desenvolvimento, sem inventar clientes, testemunhos, parceiros, estatísticas ou garantias comerciais.

Paleta: marfim #fbf8f3, castanho #512f1f, laranja #c86521, texto #2e231e, linhas #ded1c3 e areia #f1e5d5. Cabeçalhos com contraste editorial; texto/controlos seguem a tipografia legível da plataforma. Logo existente; sem alterar o painel global já aprovado. Prévia local descartável antes de consolidar os ecrãs de produto.

Adesão em quatro passos: plano; responsável (nome/e-mail/senha/confirmação); instituição (nome/tipo/localidade); confirmação da contratação simulada. No produto, a confirmação cria instituição, primeiro responsável e subscrição simulada numa transação; falha não deixa uma instituição incompleta nem converte uma conta existente.

Após a adesão, área privada institucional com assistente inicial: rever perfil da instituição, escolher ano letivo e preparar os primeiros utilizadores. Não criar dados fictícios de alunos/professores nem apresentar módulos incompletos como funcionais. A implementação de cursos, unidades curriculares, materiais e avaliações é uma fase própria, conforme AGENTS.md.

Preços fictícios escolhidos pelo utilizador: Essencial (59 €/mês), Evolução (149 €/mês) e Campus (299 €/mês). Limites escolhidos pelo utilizador em 2026-10-14: 100, 350 e 1150 contas por instituição, respetivamente. Todos têm as funcionalidades educativas disponíveis; os limites de contas são a diferença nesta entrega. Não prometer suporte já implementado, DRM, pagamentos reais ou capacidade comercial de produção.

## Entrega funcional proposta

### Resultado para o utilizador

Uma pessoa visita a página pública, escolhe um plano, cria a conta do primeiro responsável e a sua nova instituição, confirma a adesão simulada e entra na configuração inicial. Ao concluir, encontra uma área institucional mínima com dados da instituição, plano e gestão inicial de professores/alunos. Voltar a entrar não repete a criação da instituição. Os painéis educativos completos continuam em entregas seguintes.

Escolhas do agente incluídas na proposta aprovada: formulário mínimo sem NIF, telefone ou cartão; entrada automática após o registo através do login existente; ano letivo configurável; primeira gestão de contas usando as operações já existentes; navegação por parâmetro na raiz para funcionar com o alojamento estático atual.

### Página pública, entrada e navegação

- A raiz sem parâmetro apresenta sempre a página pública, mesmo com sessão ativa. Uma sessão ativa mostra «A minha área» e permite voltar à apresentação.
- Navegação: Plataforma, Planos, Sobre nós e Entrar. A apresentação distingue SaguiLearn (empresa) de SchoolGether (produto) e mantém a logo e a paleta escolhidas.
- Usar inicialmente URLs na raiz com `?view=entrar`, `?view=aderir&plan=essential` e `?view=area`, com âncoras para as secções públicas. Esta escolha evita depender de uma rota física nova no GitHub Pages e deve permitir recarregar, voltar e avançar no navegador sob `/SchoolGether/`. Parâmetros desconhecidos conduzem à página pública ou à seleção de plano, sem criar dados.
- O login continua a usar IdentitySession e a sessão web em memória. Sem sessão, a área pede entrada. Após login: PlatformAdmin conserva o painel aprovado; InstitutionAdmin abre a configuração/área institucional; Teacher e Student conservam o perfil existente até aos seus painéis educativos.
- A página pública não depende de login. O catálogo carregado da API tem estados de carregamento, indisponível e tentar novamente; uma falha no catálogo não elimina a apresentação ou o acesso ao login. Não usar preços antigos como alternativa silenciosa a uma API indisponível.
- Identificar as funcionalidades ainda em desenvolvimento. Não inventar parceiros, clientes, resultados, testemunhos, descontos ou condições de disponibilidade.

### Catálogo de planos e capacidades

- Catálogo único no servidor, com IDs estáveis `essential`, `evolution` e `campus`, nome, preço mensal em EUR, limite de contas e versão. UI lê o catálogo; o servidor decide valores e capacidades.
- A seleção da página é a mesma do resumo e da subscrição guardada. O pedido envia ID e versão do catálogo; não aceita preço, limite, papel administrativo ou instituição existente fornecidos pelo cliente. Se o catálogo mudou, 409 pede revisão do novo resumo antes de confirmar novamente.
- Conta-se cada conta institucional não eliminada, incluindo responsáveis, docentes e alunos e mesmo quando inativa; o administrador global não ocupa uma vaga. A inativação temporária não liberta uma vaga. O primeiro responsável ocupa uma vaga.
- Aplicar o limite em todas as operações que acrescentam uma conta, incluindo API Identity, provisionamento por administrador global e primeira adesão. Duas criações concorrentes para a última vaga não podem ultrapassar o limite; serializar reserva/contagem por instituição dentro da transação.
- Instituições existentes sem subscrição não recebem um plano arbitrário nem uma restrição nova silenciosa: mostram «Sem plano de demonstração associado» e mantêm o comportamento anterior nesta entrega. A atribuição/mudança de plano destas instituições será uma tarefa própria.
- Não há diferenciação artificial de proteção de dados ou acessibilidade por plano. Não implementar nesta entrega promoções, mudança de plano, faturação, gateways de pagamento ou indicadores de receita real.

### Adesão em quatro passos

1. Plano: escolher um dos planos do catálogo atual; mostrar preço e limite com indicação de demonstração.
2. Responsável: nome obrigatório (até 150 caracteres), e-mail válido (até 256), palavra-passe e confirmação. Manter política Identity: 12 a 128 caracteres, maiúscula, minúscula, número e símbolo. Não guardar a confirmação no servidor.
3. Instituição: nome obrigatório até 200 caracteres; tipo obrigatório entre Escola, Faculdade, Politécnico, Centro de formação e Outro; localidade opcional até 100 caracteres. Não assumir que um nome identifica uma entidade jurídica única; e-mail da conta é único globalmente. Esta ação cria um espaço na plataforma, não constitui uma empresa/escola legalmente.
4. Resumo: plano/valor/limite, responsável e instituição; voltar e corrigir antes do envio. Explicar «Serão criadas uma conta e uma instituição neste ambiente de demonstração. Não existe cobrança». Exigir confirmação desta condição antes da operação.

- Validações de cada passo e no servidor, com foco no primeiro erro; não perder campos ao voltar. Desativar confirmação repetida enquanto o pedido corre. Senha/confirmacão nunca entram na URL, no armazenamento persistente, em mensagens ou logs.
- O registo público aceita apenas pessoas sem sessão no cliente; uma sessão ativa conduz à própria área ou permite sair antes de aderir com outra conta. O endpoint rejeita tentativas autenticadas de converter uma conta existente. Não oferece registo público de professores, alunos ou administradores globais.
- O servidor cria numa única transação: conta global de autenticação sem privilégios de plataforma; instituição; pertença InstitutionAdmin; perfil/configuração institucional inicial; subscrição fictícia; evento de auditoria. Uma falha em qualquer ponto desfaz tudo. O responsável é o próprio criador registado, sem inventar um operador com ID 0.
- A criação é coordenada em Application por interfaces dos módulos Identity, Institutions, Billing e Audit. Nenhum módulo lê tabelas de outro para produzir o fluxo. A transação deve abranger também os SaveChanges internos de UserManager.
- A guarda de escrita institucional não pode ser desligada para pedidos anónimos em geral. A infraestrutura usa um âmbito interno de provisionamento limitado à conta e à instituição novas da mesma transação: permite apenas as inserções iniciais e a associação do criador, e termina no fim do pedido. Não aceita IDs de instituição/conta ou permissões desse âmbito vindos do DTO, não altera uma instituição existente e não fabrica um principal PlatformAdmin. Filtros de leitura continuam ativos nas operações normais.
- A origem auto-serviço fica auditada sem senha, tokens, e-mail ou formulário completo em snapshots. Apenas IDs, nome institucional, plano, estado de demonstração e referência do pedido. O evento de sucesso só existe com o commit dos dados.
- E-mail já usado, inclusive por conta eliminada/inativa, não é convertido nem reutilizado. Resposta 409 indica que não foi possível concluir com esse e-mail e oferece Entrar; não revela ID, papel, instituição ou estado da conta anterior. O índice único e a transação resolvem concorrência.
- 201 confirma a criação, sem token ou senha. O site usa então uma única tentativa de login com as credenciais ainda em memória. Limpa sempre a senha/confirmacão após essa tentativa. Se o login falhar, indica «Conta criada; entra para continuar», sem voltar a enviar o registo.
- Se a resposta do registo se perder, não repetir automaticamente o POST. Explicar que o resultado não pôde ser confirmado e oferecer entrada com as credenciais escolhidas. Uma repetição manual com o mesmo e-mail nunca cria uma segunda instituição. Não devolver recibos de registo anónimos consultáveis nem guardar senhas para repetição.
- Não há verificação por e-mail nesta demonstração; não alegar titularidade validada, nem criar convites por correio nesta entrega. Mensagens e textos públicos deixam claro o ambiente e aconselham dados de exemplo. Um uso real exige definir verificação de e-mail e termos adequados em entrega posterior.

### Primeira configuração e área institucional mínima

- InstitutionAdmin autenticado consulta exclusivamente a sua instituição, sem escolher um ID na query. Dados: nome, tipo, localidade, ano letivo, estado de configuração e versão de concorrência; plano e capacidade lidos através da interface Billing.
- Na adesão, o perfil nasce com tipo/localidade escolhidos e configuração pendente. Instituições antigas sem perfil recebem estado inicial pendente para configuração voluntária ao entrar no novo site; não criar perfis para todas por migração.
- Assistente: rever nome/tipo/localidade; escolher ano letivo; confirmar. Ano no formato `AAAA/AAAA`, segundo ano igual ao primeiro + 1, com início entre 1900 e 2199. Sugestão de ano baseada no relógio do servidor e início em setembro, editável. Nesta fase é um rótulo de configuração, não cria cursos ou inscrições.
- PUT exige versão atual. Concluir grava perfil, eventual nome da instituição, ano e instante de conclusão na mesma transação auditada. Conflito 409 preserva o rascunho e permite recarregar; não sobrepor alterações feitas por outro administrador. Estado concluído é consultado no servidor em entradas seguintes.
- Depois do assistente: resumo da instituição/plano, «Utilizadores», «Configuração», «O meu perfil» e «Sair». Lista de membros paginada e criação individual de Teacher/Student aproveitam contratos e regras existentes; nenhum seletor de outra instituição e nenhuma opção para criar InstitutionAdmin/PlatformAdmin.
- Formulário de utilizador: nome, e-mail, senha inicial e papel Teacher/Student; avisar que é uma credencial inicial a comunicar fora da plataforma, sem alegar convite enviado ou alteração obrigatória de senha que ainda não existe. Falha 400/409 mantém os campos não sensíveis; sucesso limpa a senha e atualiza lista/ocupação. Não listar senhas.
- Nesta entrega, alteração de senha, remoção/inativação de contas institucionais, importação em lote, novos responsáveis, cursos, turmas e disciplinas não entram no painel mínimo. Não apresentar botões de módulos incompletos como ações funcionais.
- O perfil, a entrada, refresh, saída e painel global mantêm comportamento e permissões anteriores. Respostas tardias de conta anterior não repõem dados após logout/troca de sessão.

### API e dados

Rotas propostas:

| Método/rota | Regra |
|---|---|
| GET `/api/v1/billing/plans` | Público; catálogo e estado da adesão, sem dados de cliente |
| POST `/api/v1/onboarding/institutions` | Público quando explicitamente ativado; cria apenas nova instituição/primeiro responsável |
| GET `/api/v1/institutions/current/setup` | InstitutionAdmin ativo da própria instituição |
| PUT `/api/v1/institutions/current/setup` | Mesmo acesso; versão obrigatória; configuração/renomeação auditadas |
| GET `/api/v1/institutions/current/subscription` | InstitutionAdmin ativo; plano/capacidade ou ausência de plano |

- Contracts contêm DTOs e constantes partilhadas, sem entidades EF. ApiClient partilhado disponibiliza operações públicas e autenticadas. Controllers apenas validam/transpõem e chamam Application; textos em recursos PT.
- Proposta de tabelas: perfil institucional (único por institution_id), subscrição de demonstração (única por institution_id), e campos de catálogo congelados na subscrição. Datas, created_by, deleted_at e row_version conforme as normas. Manter institution_id e guardas/filtros nas tabelas de dados de cliente. Catálogo fixo em código nesta entrega, sem tabela/editor administrativo desnecessários.
- A subscrição fica explicitamente `Demonstration`, guarda código/versão/nome/preço mensal/moeda/capacidade acordados e não produz uma fatura, pagamento ou contrato real. Sem renovação automática ou expiração financeira fictícia escondida.
- Adicionar dados apenas por migração EF, sem comandos manuais na base. Testes SQLite e MySQL verificam as mesmas regras; não usar a base online para experiências destrutivas.
- 400 entrada inválida; 401 sessão inválida; 403 acesso indevido; 409 e-mail, quota, catálogo ou versão em conflito; 429 limite de pedidos; 503 adesão desativada. Erros são ProblemDetails localizados e sem detalhes internos. Catálogo e operações de adesão respondem com Cache-Control adequado; registo/subscrição/configuração privada não são cache público.
- Limitar tamanho do corpo e pedidos de registo (proposta: 3 por minuto/IP, sem fila, mais teto global por processo de 20 por minuto). Usar RemoteIpAddress efetivo do alojamento e não confiar em cabeçalhos forwarded arbitrários. Preservar HTTPS/CORS/limites de Identity existentes. Nenhuma nova dependência externa necessária.
- Configuração `Onboarding:Enabled` desativada por omissão. Testes e ambiente local isolado podem ativar; publicação ativa o endpoint só após migração e verificação da entrega. O catálogo informa o estado; com registo desativado, apresenta os planos e informa indisponibilidade da adesão, mantendo o login. Não confundir este interruptor com autorização para criar contas.

### Qualidade e critérios de aceitação

- Registo válido cria exatamente uma instituição, responsável InstitutionAdmin, perfil, subscrição e evento; login seguinte e configuração funcionam.
- Tentativas inválidas, plano adulterado, catálogo antigo, conta existente ou erro de persistência não deixam dados parciais nem criam privilégios globais.
- Concorrência para e-mail e última vaga conserva unicidade/capacidade. A API recusa escrita/leitura em outra instituição e os professores/alunos não acedem à configuração.
- Perfil concluído e plano persistem em novo login. Migração conserva instituições, contas, suspensão, histórico e layout do painel anteriores.
- UI verificada com teclado, foco, erros associados a campos, mensagens anunciadas, leitura em 390 px e ausência de overflow. Contraste e alvos de toque conforme normas. Verificar loading, API indisponível, double-click, 409, 429, saída durante pedidos e navegação/recarregamento sob base `/SchoolGether/`.
- Testes de domínio/aplicação, API relacional, cliente HTTP/sessão e navegador com fixtures; cenário MySQL temporário para migração/atomicidade/quotas. Não depender de testes manuais repetidos no Swagger nem de contas pessoais.
- Resultado só se publica após testes/CI, PR e revisão de outro elemento. A implementação da app conserva o login existente e recebe este percurso numa entrega posterior, conforme decisão explícita de primeiro o site.

### Fora desta entrega

Suporte/chat/anexos aprovado, painéis educativos completos, cursos/atividades, biblioteca, proteção offline/DRM, gamificação, pagamentos reais, promoções, mudança de plano, app institucional, recuperação de senha e validação de e-mail. Os requisitos de proteção por origem de material permanecem em `docs/offline-rights.md`; os da conversa de suporte no ramo feature/support.

## Organização das entregas

1. Página pública, planos, adesão autónoma e primeira configuração institucional.
2. Painel institucional e gestão de professores/alunos, definindo os módulos académicos com o utilizador.
3. Integrar o suporte aprovado nestes percursos e no painel global; a proposta revista fica preservada em feature/support, commit 5b2d806.
4. Painéis e atividades de professores/alunos, com requisitos específicos a definir.
5. Levar funcionalidades à app após a versão correspondente do site, conforme escolha explícita do utilizador.

Esta ordem integra a proposta aprovada em resposta à nova entrada pública; não elimina o suporte aprovado. Plano de implementação em `docs/superpowers/plans/2026-10-14-public-onboarding.md`.

## Prévia descartável

Ficheiro local ignorado pelo Git da revisão de 2026-10-14: .superpowers/brainstorm/380-1791967415/content/public-v3.html. Inclui apresentação, preços escolhidos (59/149/299 €), limites escolhidos (100/350/1150), entrada separada e adesão guiada até à configuração inicial. Não chama a API nem cria contas, instituições ou pagamentos.

Verificação Chromium: desktop e 390 px, logo, seleção de plano, requisitos de senha, resumo, confirmação simulada, configuração e diálogo de entrada/Escape. Zero erros JavaScript e pedidos externos; capturas inspecionadas em .superpowers/verification/public-preview. Isto não demonstra registo público real nem implementação dos painéis escolares.

Revisão independente de 2026-10-14: não foram identificadas outras dúvidas importantes para o construtor além da decisão de capacidade e da aprovação da descrição completa. Depois da revisão, o utilizador substituiu os limites propostos de 50/250/1000 por 100/350/1150 contas. Após atualizar a descrição e apresentar o plano, respondeu «Está aprovado». Esta aprovação não decorre da passagem do tempo nem apenas da escolha dos limites.
