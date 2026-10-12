# Referência PHC GO para o SchoolGether

Data: 2026-10-12. Proposta de adaptação; não descreve funcionalidades já entregues.

## O que foi observado

Numa sessão autorizada foram consultados o dashboard, a lista de clientes e o
detalhe de um cliente. A navegação mostrou menu agrupado, pesquisa, favoritos,
acessos recentes, avisos e ações contextuais. A lista permite configurar colunas,
selecionar registos e navegar por páginas. O detalhe abre num painel lateral,
com secções e ações, preservando a lista ao fundo.

Não foram alterados registos de negócio. O contexto isolado do navegador foi
fechado após a observação. Não foram guardadas credenciais, cookies, tokens,
nomes de clientes ou outros dados da conta neste documento ou no protótipo.
Suporte em quadro/lista e personalização detalhada foram analisados na
documentação oficial; não se afirma que estavam disponíveis na conta consultada.

## O que adaptar

| Padrão | Utilidade no SchoolGether | Entrega proposta |
|---|---|---|
| Pesquisa de áreas, favoritos e recentes | Encontrar trabalho frequente sem percorrer o menu | A, apenas áreas autorizadas e preferências em memória |
| Lista com filtros, colunas e densidade | Consultar instituições em ecrãs diferentes | A |
| Detalhe lateral com secções | Consultar responsáveis e histórico mantendo o contexto da lista | A |
| Mostrar/ocultar blocos | Privilegiar o trabalho relevante de cada operador | A, apenas blocos e dados existentes |
| Avisos e filas de atenção | Identificar pedidos sem responsável ou sem resposta | B |
| Suporte em lista/quadro | Acompanhar pedidos por estado, prioridade e responsável | B |
| Relatórios e exportação | Comparar utilização e resultados demonstrativos | C |

Escolhas do agente: personalização simples, sem construtor de dashboards; ações
explícitas em vez de exigir arrastar cartões; preferências temporárias na primeira
entrega; sem operações em lote até existirem regras claras por operação. Pesquisa
global de registos exige paginação e autorização no servidor e fica para evolução.

Nos futuros painéis educativos, os mesmos padrões podem servir cursos e materiais,
atividades de docentes, favoritos e pendências dos alunos. O funcionamento desses
painéis será definido com o utilizador numa etapa própria.

Não transportar nomenclatura comercial, dados, marca ou cores PHC. A identidade
é SchoolGether, com a logo original e castanho/laranja. Não acrescentar faturação
fiscal, cartões ou pagamentos reais: toda a componente financeira é simulação.

## Fontes oficiais

- [Homepage PHC GO](https://helpcenter.phcgo.net/PT/sug/ptxview.aspx?stamp=%21%21%21d6b8f56154e6135b2d55b6): pesquisa, favoritos, recentes e avisos.
- [Dashboards PHC GO](https://helpcenter.phcgo.net/pt/sug/ptxview.aspx?stamp=341b1ggce8983bdb8g757%3A): organização e personalização de indicadores e acessos.
- [Monitor de suporte](https://helpcenter.phcgo.net/PT/sug/ptxview.aspx?stamp=b%3A8145de6bb%3A39g9e75577): filas, filtros e apresentação em lista/quadro.
- [Projetos e tarefas](https://helpcenter.phcgo.net/PT/sug/ptxview.aspx?stamp=f2ff969b4c82dc4cgf75cc): organização de tarefas por estado, prioridade e responsável.

As adaptações educativas e a divisão em entregas são propostas nossas. A
documentação do fornecedor descreve capacidades que podem variar por plano.
