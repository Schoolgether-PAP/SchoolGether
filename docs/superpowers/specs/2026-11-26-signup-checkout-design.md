# Adesão institucional e pagamento simulado

Pedido: melhorar a adesão, guardar os dados necessários à instituição e apresentar um percurso de pagamento simulado; retirar a linguagem de demonstração repetida do site.

## Experiência aprovada pelo pedido

Manter o estilo SaguiLearn, a API partilhada e a criação atómica já existente. Cinco passos: plano, instituição, responsável, pagamento e revisão. Voltar conserva os dados; os erros apontam ao campo; o resumo permite rever antes de criar. Desktop com resumo lateral e mobile numa coluna. Claro/escuro, teclado e estado de submissão incluídos.

Dados existentes: plano, nome/tipo da instituição, ano letivo, localidade, nome/e-mail/palavra-passe do administrador. Acrescentar contactos institucionais, cargo do responsável e previsão de docentes. Decisões de implementação: e-mail institucional e cargo obrigatórios; telefone, morada, código postal e NIPC opcionais; número previsto de docentes obrigatório no formulário, inteiro de 1 a 10000. Código postal português NNNN-NNN quando fornecido, telefone 9–15 algarismos, NIPC 9 algarismos (validação de formato, não certificação fiscal). Não pedir idade, documento de identificação nem dados de alunos.

Os dados novos são guardados no perfil da instituição e devolvidos apenas na configuração autenticada dessa instituição. Contratos antigos sem o bloco adicional continuam aceites; não alterar condições de subscrições existentes. Dados novos são opcionais na migração, preservando instituições existentes. Auditoria não copia contactos ou palavra-passe.

Pagamento: escolher entre referência e transferência, ambos simulados. Nenhum cartão, IBAN, entidade Multibanco ou referência pagável. Apresentar preço anual do catálogo e «Sem cobrança» com aviso local de simulação. Não inventar taxa de IVA ou total fiscal: preço «+ IVA», sem documento fiscal. A escolha serve apenas à apresentação e não produz pagamento, fatura ou renovação. Confirmar autorização para representar a instituição e ciência de que não há cobrança antes de criar. Não pedir aceitação de termos inexistentes.

Textos públicos: retirar avisos globais de PAP/demonstração/dados fictícios e substituir por conteúdo do produto. Conservar avisos de ausência de cobrança na adesão e de ausência de verificação de e-mail junto ao campo. Distinguir funcionalidades disponíveis de funcionalidades em desenvolvimento nos locais relevantes; não anunciar atividades, entregas ou manutenção como prontas. Não alterar o estado interno Demonstration das subscrições nem fabricar testemunhos, certificações ou clientes.

## Plano de execução

1. Testes de API para persistência, rejeição dos dados novos inválidos e leitura isolada; testes Web do estado/validação/pagamento sem criação antecipada.
2. Expandir domínio/contratos/casos de uso/persistência e gerar migração EF com campos opcionais. Preservar transação e isolamento.
3. Reformular o wizard e textos visíveis, guardar na API apenas os dados institucionais, nunca dados de pagamento.
4. Testar suites relevantes, migrações MySQL temporárias e navegação desktop/mobile com API local; rever o diff.
5. Atualizar colaboracao-ia, commit/push e PR sobre feature/learning-materials. Sem publicação ou migração de produção neste incremento.

## Limites

Não implementar faturação real nem verificação de e-mail fictícia. Em falha de resposta da adesão, conservar o tratamento atual que evita reenvio automático e encaminha para login. O formulário não pode criar conta antes da confirmação final. Uma função indisponível continua identificada, mesmo removendo a palavra «demonstração».
