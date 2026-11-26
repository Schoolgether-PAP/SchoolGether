# Áreas de trabalho — plano de implementação

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development. Execute sequencialmente; não lançar implementadores simultâneos.

**Goal:** Entregar áreas escolares funcionais, com adesão completa e navegação acessível.

**Architecture:** Expandir os módulos existentes e acrescentar calendário e aprendizagem através da API partilhada. Manter regras no domínio/aplicação e contratos próprios para os clientes.

**Tech Stack:** .NET 10, ASP.NET Core, EF Core/MySQL, Blazor WebAssembly, CSS e JavaScript sem novas dependências.

**Spec:** docs/superpowers/specs/2026-11-25-school-workspaces.md

## Global Constraints

- Isolamento por instituição e validação do papel no servidor.
- Subscrições existentes conservam condições; adesão sem cobranças reais.
- Português de Portugal e texto de interface em recursos.
- Nunca publicar credenciais; registo de colaboração em cada entrega.

## Review Focus

- Pedidos de outra instituição nunca revelam dados nem permitem alterações.
- Repetição/erro de adesão não deixa configuração incompleta.
- Escalas de avaliação respeitam percurso, turma e critérios definidos pela escola.
- Navegação recolhida e tema escuro mantêm rótulos, foco e contraste.
- Falhas de rede e sessões expiradas não apresentam sucesso nem dados de outra conta.

## Estado confirmado em 26/11/2026

Tarefas 1 e 3 implementadas nos commits anteriores e cobertas pelas suites atuais. Tema e navegação implementados; inspeção global de todos os ecrãs continua pendente. A tarefa 4 está parcial: inscrições, atribuições, planificações e materiais texto/HTTPS existem, mas ainda faltam ficheiros, atividades, entregas, feedback e configuração de avaliação. Nada deste ramo foi publicado. Consultar colaboracao-ia/ESTADO.md.

## Task 1: Adesão e configuração

- [x] Acrescentar ano letivo aos contratos/comando de adesão e completar perfil na mesma transação de criação.
- [x] Atualizar wizard com resumo do plano e confirmação sem cobrança real; preservar subscrições antigas.
- [x] Testar validação, atomicidade, isolamento e compatibilidade; compilar Web.
- [x] Rever e registar alterações; commit e push.

## Task 2: Tema e navegação

- [x] Implementar preferência claro/escuro/sistema persistida, em site público, login e painéis.
- [x] Implementar recolha dos três menus laterais com títulos e acesso por teclado.
- [ ] Rever textos de demonstração públicos e de interface, preservando transparência na adesão.
- [ ] Compilar e inspecionar desktop/móvel; registo, commit e push.

## Task 3: Calendário

- [x] Criar módulo com publicação/aprovação institucional, propostas docentes e consulta de alunos.
- [x] Criar calendário navegável, formulários, estados e cliente HTTP.
- [x] Testar papéis, datas, concorrência e isolamento; gerar migração EF e verificar modelo.
- [x] Integrar navegação, rever, registar, commit e push.

## Task 4: Áreas pedagógicas

- [x] Integrar inscrições e atribuições docentes (já presentes desde d70779a; não procurar um ramo separado).
- [ ] Implementar planificações, materiais privados da turma, atividades, entregas e feedback com validação no servidor.
- [ ] Implementar escalas adequadas ao percurso e ano, com configuração da escola e documentação de fontes oficiais.
- [ ] Integrar painéis e testes focados de acesso por professor/aluno, arquivos e isolamento; migrações EF.
- [ ] Rever, registar, commit e push.

## Task 5: Suporte, integração e entrega

- [ ] Verificar suporte existente e corrigir falhas/configuração sem alterar manutenção escolar.
- [ ] Executar verificações adequadas à integração, revisão independente, criar PR e verificar CI.
- [ ] Preparar publicação com backup e migrações verificadas, preservando configuração privada TiDB.
- [ ] Atualizar estado distinguindo implementado, verificado, enviado e publicado.
