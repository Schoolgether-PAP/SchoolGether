# SchoolGether — direção aprovada em novembro de 2026

Empresa SaguiLearn; produto SchoolGether. O utilizador autorizou continuar a implementação da nova orientação em 2026-11-09. Primeiro o site; depois a app. Pagamentos exclusivamente simulados na PAP.

## Direção

- Apoio ao professor: planificação, preparação de aulas, materiais próprios, atividades, avaliação, feedback e acompanhamento dos alunos.
- Remover o catálogo de livros fornecidos pela plataforma. Materiais apenas enviados pelos professores; a escolha de proteção continua dependente do autor e dos direitos existentes.
- Calendário partilhado: direção e funcionários autorizados publicam; professores propõem eventos para aprovação. Apenas aprovados são publicados para o público autorizado.
- Manutenção é módulo adicional contratado pela escola: pedidos de avaria, equipamento/local, anexos, responsável, estados e histórico. Não confundir com suporte SaguiLearn.
- Anuidades Essencial 1 000 EUR, Profissional 2 500 EUR, Institucional 4 000 EUR. Serviços de formação/configuração/suporte personalizado através de pacotes de horas. Correção de defeitos do produto não consome essas horas.
- Os preços de manutenção e suporte usados em projeções são hipóteses, não catálogo aprovado nem compra implementada.

## Primeira entrega: reposicionamento e catálogo

Preservar as subscrições existentes (preço, período e capacidade). Novas adesões usam versão de catálogo nova, periodicidade anual e capacidade por docentes. O utilizador delegou a definição de limites adequados: decisão inicial do implementador 20/50/100 professores; estudantes e responsáveis não consomem esses lugares. Não converter nem recalcular subscrições anteriores automaticamente.

Usar metadados explícitos de preço/período/unidade de capacidade, com armazenamento e DTOs consistentes. A migração mantém valores antigos e atribui Monthly/Accounts às linhas existentes. A reserva continua transacional e atómica, respeitando autorização institucional e rollback quando a criação de conta falha. Professores desativados conservam lugar, tal como contas antigas; não existe eliminação/restauro de contas nesta entrega.

A página pública descreve a nova direção e assinala funcionalidades ainda em desenvolvimento; não apresenta calendário, materiais, planificação ou manutenção como disponíveis. Adesão e painel mostram a unidade do plano recebido, incluindo antigos planos mensais por contas. Não há cobrança, renovação automática, limite de armazenamento definido ou ativação de módulos pagos nesta entrega.

## Continuidade

Conservar inscrições locais no ramo feature/academic-enrolments para revisão separada. Depois desta entrega: materiais docentes e painel docente; calendário com aprovação; autorização de funcionários; módulo de manutenção e gestão simulada de módulos/horas. Especificar permissões e limites antes de implementar cada módulo. Gamificação, stand e livros deixam de ser prioridades do MVP; não apagar dados ou migrações históricas.

## Verificação

Testes focados de catálogo/adesão/limites por papel/legado/isolamento, migração em base temporária quando disponível, compilação API/site e revisão visual desktop/móvel. Não aplicar migrações nem publicar em produção automaticamente.
