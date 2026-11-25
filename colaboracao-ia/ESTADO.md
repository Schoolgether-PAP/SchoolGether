# Passagem de trabalho — 25/11/2026

**Desenvolvimento em pausa a pedido do utilizador. A outra IA pode assumir a continuação.** Esta fotografia substitui a análise que conhecia apenas 0989d90/014bd01 e dizia que não existia PR.

## Base correta

- Repositório: https://github.com/Schoolgether-PAP/SchoolGether
- Partir do último `origin/feature/school-workspaces`, num clone/worktree próprio e limpo. Não usar o main antigo como base da aplicação.
- Existe o **PR de trabalho #19**, aberto e em rascunho: https://github.com/Schoolgether-PAP/SchoolGether/pull/19. Destino `feature/site-ligacao`, origem `feature/school-workspaces`.
- Commits anteriores ao checkpoint de pausa: `0989d90` (plano/contexto), `014bd01` (adesão), `c54b165` (avaliação portuguesa), `d70779a` (calendário, turmas, planificações e navegação).
- O checkpoint de pausa guarda também **materiais em desenvolvimento**. Consultar git log para o SHA final; HEAD não está pronto para publicar.
- Consulta remota nesta passagem: main `21226fe`, develop `0def4d4`, feature/site-ligacao `855a2ac`, gh-pages `2a71d1d`. Confirmar novamente antes de integrar; não reescrever outros ramos.

## Implementado em incrementos

1. Adesão: perfil completo e ano letivo criados atomicamente com instituição, conta e subscrição; confirmação sem cartão nem cobrança real. Registo público desativado por omissão.
2. Aspeto: automático/claro/escuro, preferência visual persistida, menu recolhível no computador e menu próprio no telemóvel.
3. Calendário: docentes propõem; administrador publica/aprova/rejeita/cancela; alunos consultam publicados da própria escola. Lisboa/UTC, versões, auditoria e recuperação de criação incerta.
4. Inscrições e atribuições: aluno por turma/curso/ano; professor por disciplina selecionada; cancelamento/restauro com histórico. **Já estão neste ramo**; não procurar um ramo remoto de matrículas. A identificação da disciplina permanece no histórico após ser retirada da seleção.
5. Contextos pedagógicos: projeção autenticada por atribuição/inscrição ativa; filtros dos catálogos administrativos preservados.
6. Planificações: planos privados do professor associados a contexto ativo; criar/listar/editar/arquivar, versões, auditoria e repetição segura. Ligados ao menu docente. A entrada de docentes/alunos mostra as respetivas turmas.
7. Avaliação: fontes oficiais em `docs/assessment-portugal.md`; regras e testes de domínio em `Domain/Learning/AssessmentScale.cs`. **Configuração por escola/turma e avaliação das atividades ainda não implementadas.** Não impor 0–20 a todos.
8. Suporte: módulo existente preservado; opção explícita `Support:UseApplicationDataStorage` para diretório privado fora de wwwroot. Ativação online pendente.

## Evidência e limites

19 testes HTTP focados aprovados, com dois cenários MySQL ignorados nessa execução. Calendário, inscrições e planificações passaram separadamente num MySQL descartável; os dois últimos foram repetidos após correções. Planificações incluíram migração integral, privacidade, repetições e edições simultâneas. Seis testes de cliente do calendário e um de planificações passaram. Web compilada sem avisos/erros.

Navegador local: acesso institucional, configuração concluída, menu móvel/recolhido, modo escuro e criação de evento confirmados. Ainda falta validar visualmente a adesão completa e os novos percursos de turmas/planificações e a vista semanal após o último ajuste. A revisão global final e CI verde do checkpoint não foram confirmados. Consultar os registos de 25/11 para detalhes; compilar não equivale a publicar.

## Materiais: ponto da interrupção

Código provisório nas quatro camadas, contratos, cliente, componente `TeachingMaterials.razor`, recursos e testes para texto/links HTTPS, com rascunhos/publicação e consulta por alunos inscritos. Program, DbContext e registo do cliente foram alterados. **Não existe migração LearningMaterials nem snapshot atualizado; o componente não está ligado à navegação.**

A API compilou. Na passagem, o Web compilou sem avisos/erros, o teste de cliente de materiais passou (1/1) e o teste HTTP de materiais passou (1/1, SQLite com EnsureCreated). Isto não valida migrações. Revisão de segurança/concorrência, ensaio MySQL/TiDB e interface pendentes. Não usar o modelo contra bases existentes sem gerar e rever a migração.

Relatório: `registos/2026-11-25-materiais-em-curso.md`, com adenda em `registos/2026-11-25-pausa-passagem.md`.

## Próxima sequência

1. Rever/concluir materiais já iniciados, gerar migração após LearningLessonPlans, testar autorização/publicação/revogação/repetição/conflitos e ligar ao menu.
2. Ficheiros privados: autorização em cada download, validação, quotas, limpeza após falha e persistência. O módulo iniciado só cobre texto/links. Não reutilizar cegamente a limpeza de órfãos do suporte, que conhece apenas os seus anexos.
3. Atividades, entregas e feedback privado; configurar percurso/escala por turma e guardar a escala usada na atividade. Registos pedagógicos não equivalem a certificação oficial nem devem converter percentagens automaticamente.
4. Revisão global, desktop/móvel/temas, CI, suporte online, migrações e publicação com recuperação. Registar cada entrega coerente e atualizar o PR.

## Cuidados técnicos

- TiDB não suporta SERIALIZABLE. Não ignorar essa verificação. Inscrições/atribuições usam versões otimistas dos pais; escritas pedagógicas usam `ILearningContextDirectory.LockForWriteAsync` com SELECT FOR UPDATE e verificação de contexto, conta, bloqueio de autenticação e instituição.
- Oracle MySQL apresentou erro ao ler DateOnly; planos usam conversão explícita DateOnly/DateTime e coluna date. A migração não publicada foi regenerada: `20261125100745_LearningLessonPlans`.
- Outras migrações novas: `20261125092333_SchoolCalendar`, `20261125093431_AcademicMemberships`. Ainda não aplicadas ao TiDB publicado.
- Conservar chave/conteúdo de criações incertas; usar IDs estáveis na seleção de contexto; cancelar pedidos ao trocar de sessão e impedir resultados antigos de alterarem a sessão nova.
- Distinguir implementado, testado, revisto, enviado e publicado. Cada alteração coerente deve ter registo e commit/push.

## Publicação e segredos

Site: https://schoolgether-pap.github.io/SchoolGether/; API: https://schoolgether-api.runasp.net/. Esta entrega **não foi publicada**. A base online é TiDB, não Aiven. Suporte online ainda por ativar/verificar.

O utilizador autorizou uma cópia privada da API publicada em `C:\PAP\artifacts\workspaces-production-2026-11-25\api-before`, fora do GitHub; foi realizada. O script privado `C:\PAP\artifacts\Deploy-Workspaces-Api.ps1` foi preparado, não aplicado nem validado integralmente. Consultar docs/deployment.md.

Segredos, configurações de produção, DPAPI, backups e binários não são enviados. Obter acessos apenas pelos canais privados autorizados. Os servidores temporários de verificação foram encerrados; a configuração local temporária 57526 foi retirada do código antes do checkpoint.

## Atualização — ramo `feature/learning-materials` (25/11/2026)

Parte de `feature/school-workspaces` `1f48b11`. Acrescenta a migração `LearningMaterials` (escrita à mão, **por validar no CI**), a ligação de «Materiais» ao menu de professores e alunos e a revisão de segurança por leitura. Compilado; testes de materiais **não executados** localmente (bloqueio do Windows a DLL novas). Nada enviado para produção. Ver `registos/2026-11-25-materiais-migracao-claude.md`. Seguem-se ficheiros privados, atividades, entregas, feedback e configuração de avaliação por turma.
