# 2026-11-24 — Pacote de colaboração para outra IA

- Autor / ferramenta: Codex, a pedido do responsável do projeto.
- Ramo: feature/ai-collaboration, baseado em main e5f3e07.
- Objetivo: disponibilizar contexto de projeto, instruções, perfis de agentes e um processo de registo de alterações para colaboração com um colega.
- Estado: implementação documental e revisão independente concluídas.

## Alterações

Criada a pasta colaboracao-ia com entrada de leitura, contexto, estado dos ramos, regras de trabalho, perfis/capacidades, segurança e modelo de registo. Criado AGENTS.md na raiz para orientar IAs que o detetam automaticamente. Adicionada ligação no README público.

Os resumos distinguem o main antigo do código publicado mais recente e distinguem propostas de funcionalidades disponíveis. Não foram copiados segredos, transcrições privadas, instruções internas do modelo nem todo o catálogo de skills.

## Ficheiros

- `AGENTS.md` — entrada automática de instruções.
- `README.md` — ligação ao pacote.
- `colaboracao-ia/README.md`, `CONTEXTO.md`, `ESTADO.md`, `INSTRUCOES.md`, `AGENTES.md`, `SEGURANCA.md`, `MODELO-REGISTO.md` — documentação portátil.
- Este ficheiro — primeiro registo do processo.

## Verificação

Confirmados código/estado das entregas anteriores e estado remoto dos PR14/17. `git diff --check` passou; os links Markdown locais dos dez documentos verificados resolvem; a inspeção de padrões sensíveis não encontrou ocorrências. Revisão independente concluída sem problemas acionáveis. Não foram executadas suites do produto para esta alteração exclusivamente documental.

## Integração e publicação

Na preparação deste commit: envio e integração ainda não realizados. O PR associado ao commit regista o resultado posterior destas operações. Esta entrega é documental e não publica uma nova versão da aplicação ou da API.

## Limites e próximo passo

Os registos são uma regra de trabalho, não uma garantia automática de comportamento de qualquer IA. O colega deve ler a entrada, confirmar o ramo atual, configurar acessos locais por canal privado e começar a sua tarefa num ramo próprio, com registo próprio. O PR17 já fechado não deve ser confundido com a integração das correções posteriores em 855a2ac.
