# Prompt para a IA que vai continuar

Assume a continuação da SchoolGether/SaguiLearn. O outro agente está em pausa e não está a implementar alterações em paralelo. A análise que conhecia apenas 0989d90/014bd01 está desatualizada.

Faz fetch e cria um clone/worktree próprio e limpo a partir do último origin/feature/school-workspaces, num ramo feature/<tarefa>. Não uses o main antigo nem sobrescrevas alterações existentes. Já existe o PR de trabalho #19 para feature/site-ligacao: https://github.com/Schoolgether-PAP/SchoolGether/pull/19.

Antes de alterar código, lê AGENTS.md, colaboracao-ia/ESTADO.md, INSTRUCOES.md, SEGURANCA.md e os registos de 25/11, incluindo 2026-11-25-pausa-passagem.md e 2026-11-25-materiais-em-curso.md. Lê a especificação e o plano de 25/11/2026 em docs/superpowers/ e docs/assessment-portugal.md.

Não refaças adesão, modo escuro, menus recolhíveis, calendário, inscrições/atribuições nem planificações privadas: já foram implementados em incrementos e enviados. As matrículas já estão neste ramo. Confirma os respetivos limites de verificação nos registos.

Começa por concluir o módulo de materiais que ficou a meio. Existem código, contratos, cliente, interface e testes, mas falta migração/snapshot, revisão de segurança/concorrência, ensaio MySQL/TiDB e ligação ao menu. A compilação não prova que esteja pronto. Depois implementa ficheiros privados com quotas/limpeza segura, atividades, entregas e feedback privado, configurando a avaliação conforme percurso/ano/turma. Não impor 0–20 universalmente; consultar a investigação oficial já feita. Mantém a identidade visual e Português de Portugal.

Preserva .NET 10/Blazor/API única/EF Core e compatibilidade TiDB. Nunca usar SERIALIZABLE nem ignorar verificações de isolamento. Usa o bloqueio de contexto pedagógico existente e versões otimistas. Testa isolamento institucional, acesso revogado, publicação, permissões de proprietário, repetições e alterações simultâneas. Nos campos DateOnly segue a conversão explícita adotada para MySQL. Não descartar criações incertas nem permitir que uma sessão antiga altere a nova.

Conclui a integração e verificação operacional do suporte, revisão global, testes adequados e inspeção visual desktop/móvel/claro/escuro. Só aplica migrações/publica depois de resolver WIP, verificar CI e preparar recuperação. A API/site atuais ainda não receberam esta entrega. Preserva subscrições existentes; não efetues cobranças reais.

Em cada entrega coerente, escreve um registo em colaboracao-ia/registos, atualiza ESTADO.md e faz commit/push para o teu ramo com PR apropriado. Informa commits, verificações reais e pendências. Nunca coloques credenciais/backups/configurações privadas no repositório nem as peças em canais públicos. Inicia pelo estado Git e por um plano curto do que falta, sem repetir perguntas respondidas nos documentos.
