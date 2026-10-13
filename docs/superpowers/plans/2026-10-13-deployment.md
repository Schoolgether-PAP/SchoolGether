# Publicação de demonstração — plano de execução

**Goal:** Disponibilizar a versão estável à equipa através de HTTPS.

**Architecture:** Base online independente, API Production no MonsterASP.NET e cliente estático no GitHub Pages. Reutilizar migrações e serviços de administração existentes; operações privadas ficam em .superpowers/deployment, ignorada pelo Git.

**Tech Stack:** .NET 10, EF Core 10/Oracle MySQL, Aiven, IIS/WebDeploy, Blazor WASM e GitHub Pages.

**Spec:** ../specs/2026-10-13-deployment-design.md

## Global Constraints

- Não alterar a stack nem a base local.
- Segredos apenas no servidor e ficheiros privados ignorados pelo Git.
- Migrações exclusivamente por EF Core.
- Sem integração automática de PRs ou contratação de serviços pagos.
- Suporte fica para a etapa seguinte.

## Review Focus

1. Perfil de publicação de outro website: rejeitar antes do upload.
2. Base com dados desconhecidos ou master existente: não sobrescrever contas.
3. TLS inválido: interromper sem ignorar validação.
4. Recursos WASM sob /SchoolGether/: verificar caminhos e configuração pública.
5. Origem não autorizada: CORS não deve conceder acesso.

## Tarefa 1 — base online e API

- [x] Validar perfil WebDeploy, destino, plano gratuito e estado Running.
- [x] Preparar operador privado em .superpowers/deployment/cloud-setup, com destino fixo e ligação por configuração privada.
- [x] Aplicar migrações existentes; criar e promover apenas a conta autorizada, com auditoria. Verificar migrações pendentes zero e master esperado.
- [x] Preparar chave JWT exclusiva, ligação TLS e CORS em variáveis do processo IIS; publicar o pacote Release revisto.
- [x] Confirmar health/ready no destino e solicitar ao utilizador ativação HTTPS caso ainda indisponível.

## Tarefa 2 — cliente estático

- [x] Preparar bundle Release com base /SchoolGether/, API HTTPS e .nojekyll; validar ausência de segredos.
- [x] Documentar/configurar publicação Pages em alteração separada e revisável, com CI aprovado.
- [x] Confirmar recursos, login e painel no site publicado; não declarar pronto antes de HTTPS funcionar.

## Tarefa 3 — entrega

- [x] Rever alteração operacional/configuração e verificar health/ready, login master, CORS permitido/negado e logout.
- [x] Atualizar documentação com resultados reais e limitações dos planos gratuitos.
- [ ] Entregar links e próximos passos concretos, incluindo revisão/merge pelo colega quando pendente.

Execução nativa nesta sessão, conforme autorização de continuidade/publicação já recebida; sem introduzir uma nova aprovação para operações que o utilizador já solicitou.
