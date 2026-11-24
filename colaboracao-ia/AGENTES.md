# Papéis de agentes e skills

Estes são perfis reutilizáveis de trabalho, não cópias de processos ativos, instruções internas do modelo ou acesso automático a ferramentas. Uma IA sem subagentes pode executar os papéis sequencialmente; para revisão independente, pedir a outra pessoa/IA que leia o diff.

| Papel | Responsabilidade | Resultado esperado |
|---|---|---|
| Coordenação | Ler contexto, confirmar objetivo/base, dividir a tarefa e evitar conflitos | Plano breve e registo atualizado |
| Produto/UX | Compreender tarefas reais e propor navegação dentro da identidade aprovada | Decisões justificadas, acessíveis e sem funcionalidades fictícias |
| Implementação | Alterar apenas o âmbito acordado, respeitando arquitetura e contratos | Código claro, diff focado e verificações adequadas |
| Revisão | Ler requisitos e diff com atenção a regressões, autorização e acessibilidade | Problemas concretos com gravidade, caminho/linha e sugestão |
| Verificação | Confirmar resultados com evidência e testar apenas os riscos afetados | Comandos/fluxos executados e resultados reais |
| Publicação | Preparar pacote, preservar configuração, verificar online e ter reposição | Apenas quando autorizado; sem credenciais nos artefactos públicos |

## Pedido reutilizável de revisão

> Revê o diff entre BASE e HEAD para a tarefa descrita no registo CAMINHO. Lê o AGENTS.md do ramo. Verifica requisitos, regressões, estados de erro, acessibilidade e autorização aplicáveis. Não alteres ficheiros nem exponhas segredos. Reporta apenas problemas acionáveis, com gravidade, ficheiro/linha e razão. Distingue uma falha comprovada de uma hipótese. Se não houver problemas, diz o que foi revisto e as limitações.

## Skills usadas no ambiente de origem

Nomes úteis para procurar numa instalação compatível: `brainstorming`, `writing-plans`, `impeccable`, `design-taste-frontend`/`taste-skill`, `frontend-design`, `systematic-debugging`, `requesting-code-review`, `verification-before-completion`, `simplify`, `test-driven-development`, `subagent-driven-development`, `superagents` e `rtk`.

Para documentos existem capacidades de documentos, PDF, apresentações e folhas de cálculo; não são dependências da aplicação. Skills de educação podem ajudar a desenhar atividades, mas não devem transformar uma tarefa de programação numa consulta a todo o catálogo.

Este ficheiro **não instala nem inclui essas skills**. O colega deve confirmar as que a sua IA suporta, ler a respetiva documentação e instalar apenas fontes confiáveis com autorização adequada. Não assumir que existem ferramentas Canva, browser, GitHub ou execução de terminal. Sem uma capacidade, explicar a limitação e usar um método disponível; nunca inventar que uma skill ou teste correu.

As regras portáveis essenciais estão em INSTRUCOES.md: compreender antes de alterar, investigar falhas, preservar o contexto, verificar o resultado e registar. Não é necessário copiar todo o catálogo de skills para colaborar.
