# Estado de colaboração — 26/11/2026

Desenvolvimento retomado por pedido do utilizador. Este ficheiro descreve o estado atual; os registos datados conservam o histórico e os limites de cada entrega.

## Base de trabalho

- Repositório: https://github.com/Schoolgether-PAP/SchoolGether
- Ramo atual: `feature/institution-signup-checkout`, baseado em `feature/learning-materials` `4e1cd45` (PR #20 integrado).
- Não partir do main antigo para continuar estes módulos. Usar um ramo próprio sobre a entrega atual, confirmar `git status` e consultar os PR antes de editar.
- PR destas correções: https://github.com/Schoolgether-PAP/SchoolGether/pull/20, destino feature/learning-materials.
- O PR #19 continua a representar o checkpoint anterior `feature/school-workspaces` → `feature/site-ligacao`. Estas correções não reescrevem nem integram automaticamente esse PR.
- Atualização concorrente do colega integrada: bdc49ae (reutilização dos tokens de aluno/administrador no teste; mantida esta solução) e 7678db1 (registo do respetivo CI verde). Preservado o registo original do colega.
- Histórico: `014bd01` adesão; `c54b165` regras de avaliação; `d70779a` calendário, inscrições e planificações; `1f48b11` checkpoint; `9ff7277` migração e menu dos materiais.

## Adesão e pagamento simulado — 26/11

- Novo percurso de cinco passos: plano, instituição, responsável, pagamento simulado e confirmação. Os contactos institucionais são validados e guardados na mesma transação da conta.
- Migração `20261126094610_InstitutionSignupContacts`: sete campos opcionais para compatibilidade com instituições antigas. Aplicar à base antes de publicar API/Web. Não aplicada ao TiDB online.
- API: 351 aprovados/13 excluídos; domínio: 119; aplicação: 7; ApiClient: 93; Web: 10. MySQL temporário: Onboarding 1/1 aprovado.
- Pagamento sem cartão, IBAN, referência pagável, fatura ou cobrança. Preferência de pagamento apenas visual, não persistida. Verificação de e-mail ainda indisponível, indicada junto do campo.
- Textos públicos revistos; ficheiros, comunicação e manutenção identificados como funcionalidades em desenvolvimento.
- Revisão independente sem pendências após corrigir o estado do módulo de manutenção. Navegador local: criação/entrada com API fictícia e disposição móvel a 390 px.
- Registo completo: `registos/2026-11-26-adesao-pagamento-codex.md`. Entrega não publicada.

## Implementado

1. Adesão completa: instituição, primeiro administrador, ano letivo, perfil e subscrição na mesma transação; sem cobrança real e registo público desativado por omissão.
2. Temas claro/escuro/sistema, menus recolhíveis e navegação móvel.
3. Calendário: propostas docentes, aprovação/publicação institucional e consulta de eventos publicados pelos alunos; Lisboa/UTC, auditoria e versões.
4. Inscrições de alunos e atribuições docentes com cancelamento/restauro. Já estão neste percurso, não dependem de um ramo remoto separado.
5. Contextos pedagógicos filtrados pela atribuição/inscrição ativa e pela instituição.
6. Planificações privadas do professor: criar, consultar, editar e arquivar.
7. Materiais de texto/HTTPS: rascunhos, publicação, consulta pelos alunos inscritos e revogação de acesso. Migração `20261125140000_LearningMaterials`, snapshot e menus presentes desde `9ff7277`.
8. Suporte existente preservado; ativação/validação online pendente. Não confundir com manutenção escolar.

## Correções de 26/11

- O teste dos materiais reutiliza os tokens do aluno e do administrador: deixa de atingir o limite de autenticações. O limite de produção não foi alterado.
- Materiais e planificações só confirmam página/ano/resultados depois de uma resposta bem-sucedida. Uma falha não faz saltar páginas.
- Pedidos de criação conservam chave e conteúdo imutáveis durante mudanças de área na mesma sessão, incluindo cancelamento de um POST em curso. Reabrir recupera o formulário e repete o mesmo pedido.
- Atualizar o perfil mantém o pedido; terminar/mudar sessão ou identidade limpa-o. Não há gravação de conteúdos em armazenamento persistente do navegador.
- Saídas da aplicação com criação pendente têm proteção de navegação. O aviso externo permite ao utilizador abandonar; fechar/recarregar a página ou terminar a sessão perde o estado em memória. Não prometer recuperação entre sessões.
- Nova suite `SchoolGether.Web.Tests`, sem dependências externas adicionais, integrada na solução e no CI.
- Script MySQL com cenário `Materials` para ensaios descartáveis.

## Verificado nesta entrega

- Domínio: 119 aprovados; aplicação: 7; clientes HTTP: 93.
- API: 344 aprovados, 13 cenários MySQL excluídos na execução normal.
- Componentes Web: 8 aprovados, incluindo paginação, perda de resposta, desmontagem/reabertura, atualização de perfil e limpeza ao sair.
- Oracle MySQL 8 temporário: cenário Materials aprovado (1/1), com aplicação das migrações e verificação de publicação, revogação e isolamento.
- API e Web Release compiladas sem avisos/erros. EF confirmou correspondência entre modelo e migrações.
- Navegador local em vista estreita/modo escuro, com API fictícia: falha de paginação sem avanço; repetição para a página correta; criação com resposta 503; mudança para Perfil/Atualizar; recuperação e confirmação do mesmo material sem duplicado.
- Revisão independente encontrou perda de pedido em refresh de perfil; corrigida com regressões. Consultar o registo desta tarefa para o resultado final.

Não foi realizado ensaio no TiDB online, publicação, execução nativa MAUI ou inspeção visual global de todos os painéis. Testar MySQL não comprova todos os comportamentos de TiDB. O CI remoto desta correção deve ser consultado antes de integrar.

## Próxima sequência

1. Rever e integrar este incremento com a entrega dos materiais, coordenando os PR existentes. Confirmar CI remoto verde.
2. Implementar ficheiros privados dos materiais: autorização em cada transferência, validação, quotas, persistência e limpeza. Não reutilizar a limpeza de órfãos do suporte sem a adaptar.
3. Atividades, entregas e feedback privado. Configuração da avaliação por escola/percurso/turma e escala guardada na atividade; fontes oficiais em `docs/assessment-portugal.md`. Não impor 0–20 a todos nem converter percentagens automaticamente.
4. Desenvolver áreas pedagógicas MAUI. Web adaptável a telemóvel não substitui a app nativa.
5. Rever visualmente todos os percursos em desktop/móvel/temas, validar suporte operacional e preparar publicação com backup/restauro.

## Cuidados técnicos e publicação

- TiDB não suporta SERIALIZABLE. Preservar os bloqueios e verificações do diretório pedagógico, versões otimistas, isolamento e auditoria.
- DateOnly dos planos tem conversão explícita DateTime/date para o provider Oracle.
- Migrações novas desde calendário/inscrições/planificações/materiais ainda não aplicadas à base publicada.
- Site: https://schoolgether-pap.github.io/SchoolGether/; API: https://schoolgether-api.runasp.net/. Base online: TiDB, não Aiven.
- Cópia privada da API anteriormente autorizada em `C:\PAP\artifacts\workspaces-production-2026-11-25\api-before`. Scripts privados e segredos ficam fora do GitHub.
- Distinguir implementado, testado, revisto, enviado, integrado e publicado. Nunca enviar palavras-passe, tokens, configurações de produção ou backups.

Registo deste incremento: `registos/2026-11-26-fiabilidade-pedagogica-codex.md`.
