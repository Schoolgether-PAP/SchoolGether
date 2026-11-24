# Interface compacta e painéis orientados às tarefas

Pedido: nove correções visuais identificadas nas capturas, incluindo reformulação dos painéis. Execução direta, mantendo a stack, funcionalidades, permissões, dados e preços já aprovados.

1. Marketing: cabeçalho de 72 px, tipografia de leitura 16–18 px, hero menor, remover faixa decorativa SaguiLearn, destacar a ligação ao produto, logótipo SchoolGether maior. Planos em três cartões verticais comparáveis (uma coluna em móvel), com limites docentes reais 20/50/100; perguntas frequentes compactas.
2. Login: fundo neutro claro e formulário branco, sem grandes blocos verde/bege. Preservar campos, validação e estados.
3. Aplicação: retirar cabeçalho institucional duplicado após autenticação; navegação lateral branca com ícones consistentes, indicação da área atual e conta no rodapé. Pesquisa e favoritos existentes continuam disponíveis, sem duas faixas permanentemente abertas.
4. Visão geral da plataforma: indicadores reais agrupados, distribuição e histórico legíveis; gráficos proporcionais, sem botões sem ação. Preservar personalização e filtros.
5. Instituição e restantes perfis: aplicar a mesma estrutura; acessos rápidos reais e estado do plano; professores/alunos mantêm perfil e suporte disponível. Sem criar módulos fictícios.
6. Validar compilação Web e fluxos afetados no navegador (desktop/390 px), pesquisa, navegação, acordeão, plano selecionado, login e permissões visíveis. Não repetir testes do backend, que não muda. Revisão independente antes de publicar; atualizar o PR existente.

Fontes primárias consultadas: Moodle Dashboard (https://docs.moodle.org/en/Dashboard), Canvas Global Navigation (https://community.instructure.com/en/kb/articles/662861-how-do-i-use-the-global-navigation-menu), Carbon Data Table (https://carbondesignsystem.com/components/data-table/usage/), Dashboard Design Patterns (https://arxiv.org/abs/2205.00757), Material responsive navigation (https://developer.android.com/develop/ui/views/layout/build-responsive-navigation).

Aplicação das referências: separar orientação global do conteúdo; ações junto das listas; visões gerais com métricas reais e pormenor progressivo; navegação recolhível em ecrãs pequenos. São decisões de design adaptadas ao projeto, não resultados de testes com utilizadores.
