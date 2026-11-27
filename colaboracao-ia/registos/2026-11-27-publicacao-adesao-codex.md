# Publicação da adesão — 27/11/2026

## Estado final — concluído em 27/11/2026

- API win-x86 publicada e fora de manutenção. Health e readiness HTTP 200.
- Login, leitura, recusa de operações globais e logout aprovados para InstitutionAdmin, Student e Teacher. Contrato de contactos presente e contacto inválido rejeitado com 400, sem criar contas novas.
- Catálogo online: 1 750/2 950/4 000 EUR; adesão ativa.
- Artefactos enviados a gh-pages em 19c2e69; job Pages 45973713729 concluído com sucesso. Manifesto público identifica sourceCommit 8309e21596979546c860407f6d86bd7564328468.
- Verificação HTTP pública confirmou três bordas pretas uniformes, ausência da mensagem antiga e CSS da adesão de cinco passos. WASM SchoolGether.Web servido publicamente coincide byte a byte (SHA-256) com o pacote preparado.
- Script público scripts/Test-PublicDeployment.ps1 primeiro falhou contra a versão antiga e passou após a publicação. Não contém credenciais.
- A inspeção visual online pelo navegador não foi repetida: a ligação da extensão exige atualização do Codex. Evidência desta publicação é HTTP/binários/API; a inspeção visual local anterior consta do registo de 26/11.
- PR de código e registo: https://github.com/Schoolgether-PAP/SchoolGether/pull/22. Não é necessário integrar código-fonte no ramo gh-pages.

## Causa da versão antiga
O PR #21 foi integrado em feature/learning-materials (8d0ad87), mas o GitHub Pages publica gh-pages, que continuava em 2a71d1d e com manifesto de origem 855a2ac, de 24/11. O ajuste de bordas 92536be foi posterior ao merge; está incluído neste ramo como 8309e21.

## Preparado e aplicado
- Ramo fix/publish-signup-checkout sobre o merge, incluindo borda preta nos três planos e remoção da nota de preços demonstrativos.
- Pacote Web Release de 8309e21 preparado; commit de artefactos 19c2e69 ainda local, não enviado. Cache de refinements.css/app.js versionada no pacote.
- Cópia privada da API e backup TiDB cifrado DPAPI verificado em C:/PAP/artifacts/signup-production-2026-11-26, fora do GitHub.
- TiDB passou de 12 para 17 migrações EF. Foram preservadas as 2 instituições e 7 contas. Migrações de calendário, inscrições, planificações, materiais e contactos aplicadas.
- O mysqldump com single-transaction falhou em SAVEPOINT no TiDB; a cópia foi feita com escritas da API paradas, sem locks, e cifrada/verificada por SHA-256. Não foi usada a exportação falhada.

## Incidente e estado à retoma
- Erro meu: o primeiro pacote da API foi preparado win-x64; o alojamento confirmado usa win-x86 inprocess. Devolveu 500.32. A arquitetura foi corrigida num pacote win-x86, com guarda que compara o runtimeTarget do pacote com a cópia anterior.
- Uma falha da revisão automática por limite de utilização impediu executar a correção em 26/11. A API ficou em manutenção (503).
- Retoma de 27/11: a revisão permitiu executar, mas WebDeploy não consegue ligar à porta 8172 na rede atual (ERROR_DESTINATION_NOT_REACHABLE). Nenhuma nova transferência foi feita nesta retoma. API ainda em manutenção.
- O painel do alojamento no navegador também ficou indisponível à automação: o conector exige atualização da aplicação Codex por política de rede. Não foi contornada essa proteção.
- Pedido ao utilizador para mudar para ligação móvel pendente. Não dar a publicação como concluída nem enviar a Web antes de a API passar health/ready e autenticação.

## Como continuar
1. Com acesso à porta 8172, executar o script privado Release.ps1 -Mode Deploy (usa api-stage-x86), seguido de -Mode Resume.
2. Verificar health/ready, login/perfis/permissões/logout. Se falhar, usar -Mode Restore para binários/configuração anteriores; migrações aditivas permanecem.
3. Enviar o commit local 19c2e69 do clone privado pages ao ramo gh-pages, preservando história e bytes dos recursos.
4. Verificar manifesto público com sourceCommit 8309e21, três bordas pretas, ausência da nota e os cinco passos da adesão. Guardar evidência e atualizar este registo/ESTADO.

Scripts, credenciais, backups e configurações de produção ficam privados. Não copiar para GitHub. Um merge de código-fonte não publica automaticamente API, base ou Pages.
