# Pausa e passagem à outra IA — 25/11/2026

O utilizador pediu explicitamente para pausar, enviar todo o código realizado e fornecer um prompt de continuação. Os implementadores foram parados; não há desenvolvimento concorrente ativo nesta sessão. O checkpoint inclui trabalho incompleto identificado como tal, sem integração no ramo principal nem publicação.

## Correções à análise recebida

- O ramo já tinha quatro commits de trabalho antes deste checkpoint: 0989d90, 014bd01, c54b165 e d70779a. Não apenas dois.
- Existe PR aberto em rascunho **#19**, de feature/school-workspaces para feature/site-ligacao.
- Modos de aspeto, menus, calendário, inscrições/atribuições e planificações já foram implementados; não reiniciar essas tarefas.
- A investigação da avaliação foi realizada e tem fontes oficiais e testes de domínio. Falta aplicar a configuração de avaliação às futuras atividades/turmas.
- Inscrições e atribuições foram recuperadas e integradas neste ramo; não dependem de um ramo remoto desconhecido.

## Código completo em incrementos

d70779a contém calendário, inscrições, atribuições, contextos pedagógicos, planos privados, navegação e temas. Os registos anteriores detalham a evidência. Os 19 testes HTTP focados passaram; dois casos MySQL ignorados nesse comando foram executados separadamente. Ensaios MySQL de inscrições e planificações foram repetidos com sucesso após corrigir compatibilidade/transações; calendário também passou em MySQL. Web compilada sem avisos/erros. Não há comprovação de execução nativa MAUI destas novas áreas.

Revisões por tarefa corrigiram nomes de disciplinas no histórico, recuperação de criações incertas, sessão e erros de atualização do calendário, contexto/ano/paginação da planificação e bloqueio da autorização nas escritas. A última correção no predicado de autenticação/lockout foi incluída na repetição MySQL aprovada. Falta revisão global do ramo e validação de todos os percursos no navegador.

## Código interrompido que também foi guardado

Materiais de texto e links HTTPS: entidade, serviço, persistência, guarda transacional, API, contratos, cliente, interface e testes. Modificações partilhadas em Program, DbContext e MemorySessionRegistration. Falta migração/snapshot e ligação ao menu; não há uploads privados neste módulo.

O relatório do implementador foi copiado para `2026-11-25-materiais-em-curso.md`. **Adenda posterior àquele relatório:** o responsável executou `dotnet build src/Clients/SchoolGether.Web/SchoolGether.Web.csproj --no-restore --verbosity quiet` com zero avisos/erros; teste `LearningMaterialsApiClientTests` aprovado (1/1); teste `LearningMaterialsHttpTests` aprovado (1/1, SQLite EnsureCreated). Não foram feitos ensaio MySQL, inspeção visual nem revisão de segurança dos materiais. O teste HTTP ter passado não resolve a falta de migração.

As tentativas do implementador de gerar a migração falharam na compilação inicial e depois no acesso local a UserSecrets. Isso não é limitação do EF nem prova de modelo correto: usar uma configuração de design local explícita, sem credenciais de produção, e gerar/rever a migração antes de continuar. A tabela só existe no SQLite descartável criado para os testes.

## Continuação recomendada

Ler `../ESTADO.md` e `../CONTINUAR-PROMPT.md`. Concluir/rever os materiais, ficheiros privados, atividades, entregas e feedback; aplicar avaliação por percurso/turma; rever o conjunto, testar interfaces e ativar/verificar suporte. Só depois preparar migrações/publicação com recuperação, mantendo o PR de trabalho e os registos atualizados.

Os testes de concorrência de inscrições não dependem de SERIALIZABLE: TiDB não o suporta; preservar versões otimistas e o bloqueio explícito do contexto pedagógico. O provider Oracle necessita da conversão DateOnly já adotada nos planos. Não usar a limpeza de órfãos de suporte para ficheiros pedagógicos sem adaptar as referências e o armazenamento.

## Segurança e estado local

Credenciais, configurações de produção, backups, DPAPI e binários ficaram fora do GitHub. O utilizador autorizou a cópia privada da API, já realizada fora do repositório. Nenhuma migração nova foi aplicada à base online, nem o novo site/API foi publicado.

Os servidores de pré-visualização foram encerrados. Foi restaurado o appsettings.Development.json original e removida a pasta temporária SchoolGether.Api que continha apenas uma cópia da configuração pública para o host de testes. O código e os documentos necessários à continuação ficam no checkpoint, sem depender dessa pasta temporária.
