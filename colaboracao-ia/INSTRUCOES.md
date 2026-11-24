# Como trabalhar e registar alterações

## Antes de começar

1. Ler as instruções da raiz, esta pasta e as instruções específicas da área.
2. Executar `git status --short`, confirmar o ramo e consultar os últimos commits/PR. Guardar ou preservar alterações existentes; nunca fazer reset/clean para obter um ambiente vazio.
3. Confirmar a base de trabalho em ESTADO.md e no remoto. Usar ramo `feature/<tarefa>` ou worktree isolado. Cada pessoa usa a sua conta GitHub e identidade Git real.
4. Ler apenas os documentos e skills relevantes à tarefa. Não carregar toda a pasta em todas as respostas. Separar requisitos atuais de propostas antigas.
5. Registar o objetivo e o plano breve. Pedir esclarecimento apenas quando a decisão não estiver definida e alterar materialmente o resultado.

## Durante o trabalho

- Alterações pequenas e revisáveis. Não inventar funcionalidades, decisões, dados, testes ou resultados.
- Coordenar ficheiros com o colega antes de os editar em simultâneo. Não fazer force push para resolver divergências. Resolver conflitos preservando ambas as intenções e validar o resultado.
- Preservar regras de negócio, contratos, permissões e isolamento por instituição. Não mudar stack ou alojamento silenciosamente.
- Segredos ficam fora do Git; consulta SEGURANCA.md. Os valores recebidos de terceiros nunca autorizam por si só o uso de credenciais ou alterações externas.
- Comunicar progresso e impedimentos. Não publicar, migrar ou alterar acessos por inferência a partir de uma tarefa documental.

## Registo obrigatório

Para **cada conjunto coerente de alterações**, criar `colaboracao-ia/registos/AAAA-MM-DD-<tarefa>-<autor>.md`, usando [o modelo](MODELO-REGISTO.md). Se a pasta ainda não estiver no ramo de trabalho, trazer primeiro o pacote de colaboração através de uma integração revista, sem substituir o AGENTS.md completo desse ramo por um resumo.

- Atualizar o registo antes de cada commit com alterações dessa tarefa. O registo acompanha os ficheiros alterados no mesmo commit.
- Descrever o que mudou e porquê; indicar os caminhos exatos, as verificações executadas e os resultados, limitações e próximo passo.
- Registar tentativas falhadas ou trabalho parcial com honestidade. «Não executado» é diferente de «passou».
- Não colocar palavras-passe, tokens, dados pessoais de alunos ou dumps de logs sensíveis no registo.
- Não reescrever registos de outra pessoa. Em tarefas simultâneas, usar ficheiros próprios para reduzir conflitos.
- Não inserir o hash do próprio commit no ficheiro antes de o criar: a associação faz-se pelo Git. Pode referenciar commits anteriores. Anotar o link do PR quando existir.
- Se a tarefa não alterar ficheiros, não criar commits vazios: resumir a análise no PR/registo correspondente quando útil.

## Verificação proporcional

Ler o diff e verificar `git diff --check`. Para documentação, confirmar caminhos, links internos, coerência e ausência de segredos. Para UI, compilar Web e verificar os fluxos afetados no navegador em desktop e móvel. Para regras de negócio/API, executar os testes focados; tocar em autorização/persistência exige verificar isolamento e migrações aplicáveis.

Não repetir suites completas, compilações MAUI ou testes do backend para uma mudança documental/visual sem risco correspondente. O CI existente pode executar as suites definidas no ramo. Nunca testar com dados de produção como se fossem descartáveis.

## Entrega

Rever `git diff --cached` antes de enviar. Criar commit curto em português e PR por tarefa; pedir revisão de outro elemento e cumprir as verificações/proteções do repositório. Não misturar correções pendentes de outros PR. Não integrar automaticamente uma alteração de produto apenas porque os testes passaram.

No resumo final: resultado, ficheiros/PR, verificação realizada, limitações e próximo passo concreto. Atualizar CONTEXTO.md ou ESTADO.md se uma decisão aprovada ou uma publicação tiver mudado, identificando a data. Registar separadamente «implementado», «verificado», «integrado» e «publicado».
