# Painel institucional simples e profissional

## Intenção e decisões do utilizador
O utilizador delegou a organização: painel profissional, simples e fácil de navegar. Preservar SchoolGether, logo/paleta aprovadas e stack Blazor. Executar apenas testes necessários às alterações e riscos concretos, sem repetir suites inalteradas.

## Entrega e escolhas de implementação
Reorganizar a área InstitutionAdmin atual. Menu lateral persistente no desktop e menu compacto acessível no telemóvel; navegação clara Visão geral, Utilizadores, Suporte, Definições, O meu perfil e Sair. Reutilizar as operações existentes, sem introduzir ações sem implementação.
Visão geral com nome institucional, ano letivo, configuração, plano de demonstração e capacidade reais. Atalhos Gerir utilizadores, Abrir suporte e Editar dados. Não inferir contagens de professores/alunos, atividade ou relatórios que a API não fornece. Sem gráficos inventados nem seletor de perspetivas.
Cor: fundo#f5f6f7, superfícies#ffffff, texto#36281f, secundário#6e625a, ação#663a20, acento#c87532. Segoe UI/system, alinhamento à esquerda, hierarquia por títulos/espaçamento, logo existente. Consistência com painel global e suporte, sem tipografia Georgia herdada.
Preservar configuração inicial obrigatória, contas legadas sem plano, limites/capacidade, criação/listagem/paginação de utilizadores, suporte autorizado, próprio perfil e saída. Mensagens e ficheiros de suporte conservam o ciclo de vida já existente.

## Estados e acessibilidade
Carregamento e falhas com repetição, conta sem plano sem métricas fictícias, vagas disponíveis nunca negativas, contagem real indicada mesmo acima do limite. Não divulgar dados da sessão anterior. Menu móvel fecha ao navegar; Escape/foco e estado selecionado semanticamente corretos. Largura390px sem overflow horizontal, alvos44px, foco visível.

## Âmbito e verificação
Esta entrega melhora a estrutura visual das capacidades existentes; não equivale à gestão académica completa. Cursos/unidades curriculares/turmas/inscrições são próximos incrementos. Não alterar API, base de dados, app nativa ou demonstração online.
Verificação necessária: WebRelease e um cenário Chromium controlado cobrindo carregamento/resumo real, navegação/atalhos, criação de utilizador, dados legados/falha recuperável, suporte, perfil/saída com resposta tardia, teclado/menu/Escape e390px. Capturas finais desktop/móvel inspecionadas. Sem nova suite unitária que copie HTML/CSS, sem backend/MAUI/MySQL inalterados. Rever uma vez o delta e corrigir só problemas acionáveis.
