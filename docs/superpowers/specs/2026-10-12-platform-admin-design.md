# Painel da plataforma SchoolGether — proposta detalhada

Data: 2026-10-12. Estado: objetivos e regras gerais aprovados na conversa;
esta descrição detalhada e a prévia visual aguardam validação antes de código de produto.

## Intenção e decisões do utilizador

O proprietário quer administrar o SchoolGether, prestar suporte às instituições
clientes e acompanhar utilização e ações da equipa. Os painéis da escola,
professores e alunos serão definidos posteriormente.

Decisões expressas:

- O proprietário é o administrador master e define os privilégios dos restantes.
- Todos os administradores podem atuar em qualquer instituição. Não existe lista
  de instituições atribuídas por administrador; o que varia são as ações permitidas.
- Suporte pode consultar e intervir no contexto de um cliente, de forma identificada.
- São desejadas gestão de conteúdos e contas, moderação, planos, ofertas,
  avaliações, relatórios, histórico administrativo e logs técnicos.
- Pagamentos, cobranças e receitas são fictícios, exclusivamente para demonstração.
- Nome SchoolGether, logo original e paleta castanha/laranja já escolhidos.
- Sem prazos ou estimativas de duração como critério de organização.
- PHC GO é uma referência de organização e interação. Adaptar os padrões úteis,
  conservando a identidade SchoolGether e o contexto educativo.

## Estado existente

Há API Identity com JWT/refresh, administrador global `PlatformAdmin`, instituições,
administrador institucional, docentes/alunos e isolamento por instituição. Site e
app permitem entrada, próprio perfil e saída. Ainda não há painel administrativo,
distinção de master, permissões granulares, auditoria de produto ou faturação.

`PlatformAdmin` atualmente não pode criar outro `PlatformAdmin` pela API. O
administrador inicial é criado num assistente local. Não confundir o novo desenho
com funcionalidades já disponíveis, nem o protótipo com dados reais.

## Experiência do painel

Após entrar como administrador da plataforma, abrir Visão geral. Menu lateral
em ecrã largo; gaveta com botão Menu em ecrã estreito. Cabeçalho com título da
área, contexto e acesso a Meu perfil/Sair. O perfil deixa de ser o destino único.
As rotas autenticadas mantêm a sessão existente; carregar novamente o site exige
nova entrada, enquanto a política de sessão web continuar em memória.

Navegação prevista: Visão geral, Instituições, Administradores, Suporte,
Contas e moderação, Conteúdos, Planos e ofertas, Relatórios, Avaliações,
Histórico de ações, Estado técnico. Mostrar apenas áreas autorizadas e já
implementadas; a prévia pode mostrar o mapa completo com marcação de demonstração.
Não publicar botões sem funcionamento como se as áreas já existissem.

Visão geral privilegia trabalho pendente e atalhos úteis. Indicadores e gráficos
abrem a respetiva lista filtrada. Ausência de dados apresenta estado vazio e
ação concreta, sem inventar indicadores. Nesta primeira proposta, a logo
identifica o menu; o conteúdo principal usa o espaço antes reservado à logo grande.

### Adaptação da referência PHC GO

Análise em 2026-10-12: dashboard, lista e detalhe de clientes observados numa
sessão autorizada, sem alterar registos. A documentação oficial complementa a
análise de suporte; esse módulo não foi observado na conta. Ver
[análise e referências](2026-10-12-phc-go-reference.md).

- Cabeçalho com pesquisa, avisos e perfil; menu organizado por trabalho diário
  e administração. Primeira entrega: pesquisa de áreas autorizadas. A pesquisa
  transversal de registos depende de contratos próprios e não pesquisa apenas
  a página carregada como se abrangesse toda a base.
- Favoritos de áreas e acessos recentes aceleram tarefas repetidas. Na entrega A
  são preferências em memória, limpas ao sair; não guardar tokens ou dados de
  instituições no armazenamento do navegador. Persistência por conta é posterior.
- Instituições: filtro de nome/estado, escolha de colunas não essenciais, densidade
  compacta opcional e detalhe lateral com resumo, responsáveis e histórico.
  Fechar o detalhe preserva filtros/página; criação de responsável usa operação
  autorizada separada. No móvel, o detalhe ocupa o ecrã; tabelas têm scroll local.
- Visão geral personalizável por mostrar/ocultar blocos disponíveis, sem editor
  livre de layouts nesta entrega. Métricas abrem listas filtradas quando existirem.
  Não mostrar suporte, avaliações ou finanças fictícias no produto da entrega A.
- Entrega B: avisos internos e suporte em lista/quadro, com filtros de prioridade,
  responsável e estado; filas por atribuir e sem resposta. Mudanças de estado e
  atribuição são ações explícitas auditadas; arrastar cartões não é necessário.
- Entrega C: relatórios por período e instituição, com exportação autorizada e
  valores financeiros identificados como simulação.

A prévia v2 mostra a direção completa com dados inventados. Pesquisa global de
exemplos, suporte e avisos são interações demonstrativas, não novas funcionalidades
da API nem compromissos adicionais para a entrega A.

Padrões reutilizáveis posteriormente: listas/detalhes para cursos e materiais,
quadro de atividades para docentes, favoritos e pendências para alunos. São
possibilidades a discutir ao definir cada painel, não especificações já aprovadas.

## Master e permissões

Escolha proposta do agente: manter os quatro papéis existentes. O master é um
atributo protegido de uma conta `PlatformAdmin`; restantes administradores têm
permissões explícitas guardadas no servidor. Não criar papel escolar novo.

- Há um master nesta primeira versão. Só ele cria/desativa administradores e
  altera permissões. Outros administradores não se promovem nem alteram colegas.
- Master não pode desativar, remover ou retirar a própria condição no painel.
  Transferência de propriedade não pertence à primeira entrega.
- Criar administrador sem privilégios concedidos por omissão; master assinala
  as capacidades autorizadas. Modelos de permissões poderão facilitar o formulário,
  sem serem novos papéis nem dar acesso implícito.
- Consulta, criação, edição, suspensão/moderação, gestão de planos, intervenção
  de suporte, histórico, exportação e logs são capacidades distintas.
- Administrar clientes não concede acesso a segredos, palavras-passe, tokens ou
  configurações privadas do servidor.
- API verifica permissões atuais. Esconder menus não substitui autorização.
  Retirar uma capacidade ou desativar um administrador deve impedir a ação no
  pedido seguinte, incluindo sessões que ainda tenham JWT válido.
- Uma migração não escolhe arbitrariamente o master. Um assistente local
  controlado em Development promove a conta global existente escolhida pelo
  operador, com confirmação do identificador, e recusa promoção se já há master.
  Não recria a conta nem altera a senha. A recuperação fora do painel exige
  procedimento de operador documentado; não um endpoint público de recuperação
  de propriedade. Preparar este mecanismo para produção pertence ao alojamento.

Proteger contas administrativas com MFA, convites e recuperação controlada é
objetivo de uma entrega própria antes de acesso de suporte a dados reais alojados.
Não apresentar esses mecanismos como implementados na base inicial.

## Instituições e contas

Lista global paginada com pesquisa por nome/identificador e filtro por estado.
Detalhe com nome, responsáveis, estado, datas e histórico. Criar instituição e
criar a primeira conta institucional são operações distintas, com estados de
sucesso/falha próprios: falhar a conta não deve fingir que a instituição não existe.

Suspender uma instituição preserva os dados e impede entrada e utilização pelos
seus membros; reativar restaura acesso elegível, não revoga sanções individuais.
Não reutilizar `deleted_at` para suspensão: suspensão e eliminação são conceitos
distintos. Exclusão definitiva, migração de alunos e edição de resultados
académicos não pertencem à primeira entrega.

Administradores globais autorizados consultam dados de diferentes instituições
por operações administrativas explícitas. Endpoints escolares existentes
continuam isolados pelo contexto validado da conta. Não desligar globalmente
os filtros EF para dar acesso ao painel.

## Suporte e acesso assistido

Pedidos incluem instituição, autor, assunto, categoria, prioridade, responsável,
estado e mensagens. Estados: aberto, em atendimento, à espera do cliente,
resolvido e fechado. Notas internas nunca aparecem ao cliente. Anexos exigem
limites de tamanho/tipo e armazenamento com autorização.

Acesso assistido exige permissão, instituição escolhida, motivo e referência do
pedido quando existir. A sessão de suporte tem prazo limitado, faixa persistente
com instituição e operador, ação Terminar suporte e consulta por omissão.
Intervir exige capacidade adicional e motivo. Não entrar com a senha do cliente,
não obter tokens dele e não atribuir ao cliente as alterações do operador.
Sem painel escolar nesta entrega inicial, este fluxo fica descrito e só é
implementado quando houver um contexto de cliente útil para consultar.

## Conteúdos, contas e moderação

Separar advertência, suspensão temporária, banimento e eliminação lógica. Registar
motivo, âmbito, evidência/referência e prazo quando aplicável. Suspensão e banimento
impedem novas sessões e o uso da conta em pedidos protegidos; não apagar material
institucional por suspender o seu autor. Prever contestação e decisão posterior.

Ocultar/restaurar conteúdos é uma ação de moderação identificada. Editar conteúdo
do cliente exige capacidade separada, histórico de versões e contexto claro.
Não dar acesso automático a alterar notas, avaliações académicas ou respostas
de alunos. Os tipos de materiais serão definidos no módulo Library/Academic.

## Planos, ofertas e pagamentos de demonstração

Planos têm nome, funcionalidades, limites e valor demonstrativo. Uma instituição
pode mudar de plano, mantendo histórico e data de efeito. Não apagar dados quando
um limite diminui; impedir novas operações que excedam o limite e explicar a situação.

Ofertas têm benefício, destinatários (todos ou instituições escolhidas), início,
fim e regras explícitas de acumulação. Primeira regra proposta: uma oferta ativa
por subscrição, sem acumulação automática; alterações posteriores são decisões
do produto. Validar intervalos e valores para não criar totais negativos.

Todos os registos financeiros são simulados. Identificar a área, os recibos de
demonstração e os relatórios com «Demonstração — sem pagamentos reais». Não pedir
dados de cartão, não integrar gateways, não emitir faturas fiscais nem efetuar
transferências. Preferir eventos «pagamento simulado», «cancelado» e «reembolso
simulado». Gráficos financeiros somam apenas os eventos simulados coerentes;
dados de exemplo só existem com indicação explícita, nunca como receitas reais.

Moeda proposta para a demonstração: EUR, valores decimais e arredondamento
consistente. Preços, impostos fictícios e catálogo serão definidos antes de
implementar o módulo Billing, sem pressupor cobrança comercial.

## Relatórios, avaliações, auditoria e logs

Relatórios operacionais: instituições/contas por estado, utilização, suporte e
moderação. Financeiros: totais simulados, planos/ofertas e evolução no intervalo
selecionado. Filtros de período/instituição, definição visível da métrica e
exportação autorizada. Não inferir atividade de alunos apenas por contas criadas.

Avaliações da plataforma são distintas das avaliações de materiais e das notas.
Permitir classificação, comentário, resposta e acompanhamento. Moderação por
regras identificadas, não remoção de crítica por ser desfavorável.

Auditoria regista operações administrativas relevantes: ator real, ação,
alvo/instituição, instante UTC, resultado, motivo, referência do pedido e
alterações anteriores/novas permitidas. Consultas sensíveis, exportações,
mudanças de permissões, acesso assistido e ações recusadas também são registadas.
Não guardar senhas, tokens, segredos nem cópias integrais de documentos pessoais.
Não há editar/apagar histórico no painel, incluindo para master.

Alterações de negócio e seu evento de auditoria devem ser consistentes: um
sucesso administrativo não pode ser confirmado sem o evento persistido.
Tentativas recusadas têm registo próprio sem criar falsas alterações concluídas.
Histórico global de administração tem acesso explícito; não fica exposto pela
listagem escolar nem permite contornar isolamento por fornecer um ID arbitrário.

Logs técnicos têm severidade, módulo, referência e instituição quando aplicável.
São eventos produzidos pela própria plataforma, não acesso indiscriminado ao
dispositivo de um cliente. Consulta filtrada e protegida, sem terminal remoto,
SQL livre, ficheiros do servidor ou dados secretos no navegador.
Backups/restauro são procedimentos operacionais, inicialmente documentados e
testados fora do painel; não adicionar um botão de restauro sem fluxo verificado.

## Entregas por dependências

### A — Base administrativa (primeira implementação)

- Navegação autenticada para administradores e próprio perfil como área separada.
- Master, administradores globais e permissões efetivas no servidor.
- Lista/criação/renomeação/suspensão/reativação de instituições e criação da
  primeira conta institucional, com motivo e confirmação nas ações sensíveis.
- Histórico das operações anteriores, com pesquisa e paginação.
- Visão geral alimentada pelos dados disponíveis. Sem métricas fictícias.
- Estados vazios, carregamento, erro/repetição, sessão expirada, acesso recusado
  e conflito de edição concorrente; não perder dados de formulário numa falha recuperável.
- Migration EF, testes e documentação local; não publicar ou modificar contas
  existentes automaticamente durante o desenho.

### B — Atendimento e moderação

Suporte, notificações internas, contas/sanções e contexto assistido; conteúdos
quando Library/Academic fornecer tipos e histórico. MFA/convites/recuperação
administrativa antes de uso com dados reais alojados. Receção de pedidos do lado
do cliente depende do respetivo painel; demonstração isolada claramente marcada.

### C — Planos e acompanhamento

Planos, ofertas e pagamentos exclusivamente simulados, relatórios e avaliações.
Regras de preços/limites e quem submete avaliações são detalhadas nesta etapa.

### D — Operação técnica

Consulta protegida de logs, alertas, métricas operacionais, retenção e procedimentos
de backup/restauro. Não abrir execução de código ou acesso ao servidor pelo painel.

Cada entrega passa por revisão e validação; esta ordem é de dependências, não
uma previsão de duração. Funcionalidades que ainda não existem ficam fora do
menu de produto ou explicitamente identificadas num roteiro separado.

## Restrições e escolhas deixadas ao construtor

Manter .NET 10, Clean Architecture modular, API única, Oracle EF/MySQL,
Contracts/ApiClient e Blazor WASM; textos em recursos portugueses. O painel web
da plataforma é o foco desta proposta. App conserva login/perfil e a paridade
administrativa móvel é uma entrega posterior, sem WebView improvisada.

O construtor define nomes concretos de capabilities, endpoints `/api/v1/platform/...`,
DTOs, índices, transações e divisão das tarefas, respeitando as regras acima.
Contas e privilégios globais pertencem a Identity; instituições a Institutions;
Audit recebe eventos por contrato de módulo. Nenhum controller concentra regras
de negócio. Guardas de suspensão e de permissões devem usar estado atual, sem
esperar expiração de JWT para fazer efeito.

Interface: branco `#FFFFFF`, fundo claro `#F5F6F7`, texto `#36281F`, ação `#663A20`,
laranja `#C87532`, divisórias `#E5E0DC`. Tipografia de sistema já presente no projeto,
hierarquia simples, logo original e foco visível. Não adicionar dependência de
gráficos nem fontes remotas nesta fase de desenho.

## Critérios de aceitação da entrega A

1. Master entra e vê navegação, instituições e histórico; perfil e saída continuam funcionais.
2. Master cria outro administrador e concede capacidades específicas. Este pode
   atuar em qualquer instituição, mas só nas operações autorizadas.
3. Administrador comum não altera privilégios; pedidos diretos não autorizados
   são recusados, mesmo que o menu seja manipulado.
4. Retirada de permissão/desativação tem efeito no pedido seguinte; master não
   pode bloquear o próprio acesso pelo painel.
5. Criar/alterar/suspender/reativar instituição grava resultado e auditoria coerentes;
   falha de auditoria não produz sucesso sem histórico. Conflito de versão é explicado.
6. Suspensão institucional não permite pedidos de membros com sessão anterior;
   reativação não remove sanção individual. Isolamento escolar mantém-se.
7. Dados de duas instituições diferentes são testados automaticamente; operações
   globais autorizadas e operações escolares continuam com âmbitos distintos.
8. Navegação/formulários funcionam com teclado e a 390 px; mensagens são localizadas.
9. Nenhum indicador financeiro real, segredo ou conteúdo fictício aparece como
   dado real. Não há integração de pagamentos.
10. Compilação, testes HTTP/domínio/clientes e cenários de navegador cobrem as
    operações implementadas; documentar separadamente o que não foi executado.

## Prévia visual

Protótipo local separado do site funcional, marcado «Prévia — dados de exemplo».
Mostra o mapa completo do painel e exemplos de visão geral, instituições,
administradores e histórico, sem contactar a API ou alterar dados. Serve para
validar navegação/hierarquia; não comprova funcionalidades implementadas.

## Revisão da proposta

Builder check independente de 2026-10-12: nenhuma questão importante em falta
para planear a entrega A. Credenciais iniciais podem seguir o fluxo local já
existente; convites/MFA/recuperação continuam explícitos na entrega B.

Prévia verificada em Chromium: navegação das áreas, filtros de instituições,
criação apenas em memória do protótipo, diálogo de permissões, pesquisa de
histórico e menu a 390 px. Sem pedidos à API real, sem erros JavaScript e sem
overflow horizontal global nas vistas verificadas. Capturas desktop/mobile
inspecionadas. Isto verifica o protótipo, não o painel de produto nem auditoria
completa de acessibilidade.

Adaptação PHC GO: builder check independente sem questões importantes em falta
para planear A. Prévia v2 verificada em Chromium a 1440 e 390 px: pesquisa de
exemplos, favoritos/recentes, ocultação de blocos, colunas/densidade, detalhe com
secções e preservação do filtro, suporte em lista/quadro, avisos, permissões e
pesquisa do histórico. Sem pedidos à API real nem erros JavaScript; sem overflow
horizontal global nas vistas verificadas. Capturas desktop e detalhe móvel
inspecionados. Continua a ser um protótipo, separado do produto.
