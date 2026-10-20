# Institution Panel Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development to implement this single cohesive UI task. User requires focused testing only.

**Goal:** tornar a área institucional profissional e fácil de navegar com dados reais existentes.
**Architecture:** reorganizar InstitutionPanel e CSS, reutilizar InstitutionMembers/SetupForm/SupportWorkspace/IdentitySession; textos em recursos. Sem alterações de backend ou dependências.
**Tech Stack:** .NET10, Blazor WebAssembly, CSS isolado, Chromium/Playwright local.
**Spec:** docs/superpowers/specs/2026-10-20-institution-panel-design.md

## Global Constraints
- Preservar autorização, configuração inicial, sessões e dados reais; não inventar métricas/ações.
- Palette atual SchoolGether, Segoe UI/system, português em recursos, desktop/390px/teclado/foco44px.
- Testes focados: WebRelease + cenário browser das áreas alteradas, sem repetir backend/MAUI/MySQL inalterados.
- Não publicar nem mexer em dados/contas online; não criar módulos académicos neste incremento visual.

## Review Focus
Plano ausente/contas legadas; ocupação no/acima do limite; navegação durante leitura; logout e resposta tardia; menu móvel Escape e foco.

### Task 1: painel e navegação institucional
- [ ] Ler InstitutionPanel.razor/.css, InstitutionView e contratos existentes; componentes filhos apenas quando necessário.
- [ ] Reorganizar shell e visão geral; preservar pontos de entrada/funcionalidades; alterar recursos e CSS focados. Alterar componentes filhos apenas para coerência visual real, sem refatoração genérica.
- [ ] Preparar fixture Chromium com respostas HTTP controladas, apenas dados fictícios; reutilizar padrões em C:/PAP/artifacts/support-task7 quando útil.
- [ ] Compilar WebRelease; verificar num cenário coeso resumo/atalhos/navegação/criação/perfil/suporte/erros/legado/logout com resposta tardia e menu390px/Escape/foco. Para alterações visuais reversíveis, não acrescentar testes unitários que espelhem marcação.
- [ ] Capturar e inspecionar desktop/390px, corrigir defeitos observados; guardar comandos/resultados e caminhos em relatório curto.
- [ ] Fazer commit português, revisão única do delta e corrigir apenas achados concretos. Próxima fase: estrutura académica funcional.
