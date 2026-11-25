# Estado de trabalho em 25/11/2026

Continuação em `feature/school-workspaces`, worktree `SchoolGether-workspaces`, a partir de `feature/site-ligacao` em `855a2ac`. A implementação segue `docs/superpowers/plans/2026-11-25-school-workspaces.md`. O pedido inclui navegação recolhível, modo escuro, adesão/configuração completas, suporte, calendário e painéis pedagógicos. Não assumir que alterações deste ramo foram publicadas; confirmar os registos e os commits.

O utilizador pediu avaliação adequada ao ano, tipo de ensino e turma, em vez de uma escala global de 0–20. A investigação e implementação ficam registadas no ramo.

Este documento é uma fotografia. Consulta `git fetch`, os PR e os registos seguintes antes de continuar. Publicação e integração no ramo principal são operações diferentes.

## Ramos e publicação

- Repositório: https://github.com/Schoolgether-PAP/SchoolGether
- O ramo predefinido `main`, no commit `e5f3e07`, contém a apresentação README e uma versão antiga do código. Não o tratar como a aplicação publicada atual.
- O PR [#14](https://github.com/Schoolgether-PAP/SchoolGether/pull/14) continua aberto para `develop`. O ramo `feature/teacher-focus` está em `66bca31` na consulta realizada para esta pasta.
- O PR [#17](https://github.com/Schoolgether-PAP/SchoolGether/pull/17) foi integrado em `feature/teacher-focus`, incluindo o estado `bfc750b`. As correções visuais posteriores em `855a2ac` estão no ramo `feature/site-ligacao`; não assumir que ficaram incluídas naquele PR já fechado.
- A versão mais recente do site verificada nesta sessão corresponde a `feature/site-ligacao`, commit `855a2aca8de0512ad608b97201641364243a3689`. Confirmar a integração destas correções numa revisão própria antes de iniciar trabalho concorrente no mesmo código.
- Artefactos públicos em `gh-pages`, commit `2a71d1d`; deployment `45419253613` aprovado. Não desenvolver no ramo de artefactos.
- Site: https://schoolgether-pap.github.io/SchoolGether/
- API: https://schoolgether-api.runasp.net/
- A aplicação está em `/SchoolGether/app/`; a raiz contém o site institucional. URLs antigos com `?view=area`, `entrar` ou `aderir` são encaminhados para a aplicação.
- A base online passou do Aiven para TiDB em 24/11/2026, com TLS VerifyFull e conta técnica limitada. Não reutilizar configurações Aiven antigas. Valores de acesso não estão nesta pasta.

## Implementado e limites

No ramo atual: autenticação/perfis/papéis, gestão institucional e de contas, administração global, permissões e auditoria; cursos, disciplinas e turmas; adesão demonstrativa e condições de subscrição guardadas; site institucional e interface dos painéis. Existem componentes de suporte, mas o suporte online foi observado como desativado: não afirmar que o atendimento está operacional.

Professores e alunos têm os serviços existentes de perfil; isso não equivale a painéis pedagógicos completos. A área docente pode mostrar o suporte disponível, respeitando a configuração do servidor. Materiais, calendário e aulas no site público são exemplos fictícios. O formulário público de contactos não envia mensagens; o endereço mailto abre o cliente de correio.

Por desenvolver ou validar: ferramentas pedagógicas de planificação e materiais, calendário escolar completo, manutenção, permissões de novos perfis, execução nativa/offline e outras fases descritas nas especificações. Há trabalho de matrículas num ramo separado; não considerar integrado sem inspecionar o PR/diff.

## Evidência disponível

CI `45418769190` aprovado para `855a2ac`; empacotamento Web e revisão independente concluídos. Verificados no navegador: login/logout, instituição/admin em desktop, menus e perfil a 390 px, filtros, favoritos e pesquisa com estado vazio, atualização do perfil e escolha de plano. Estes resultados não substituem verificações das alterações seguintes.

No ramo atual, consultar `AGENTS.md`, `docs/site-ligacao.md`, `docs/deployment.md`, `docs/identity.md`, `docs/client-login.md`, `docs/platform-admin.md`, `docs/public-onboarding.md`, `docs/academic-courses.md`, `docs/academic-course-units.md`, `docs/academic-classes.md` e `docs/superpowers/`. Os caminhos podem não existir ainda no `main` antigo.
