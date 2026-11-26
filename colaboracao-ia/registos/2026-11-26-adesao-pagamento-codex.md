# Adesão institucional e pagamento simulado — 26/11/2026

## Pedido e base
Melhorar a adesão funcional, pedir dados necessários, apresentar cobrança simulada e retirar avisos genéricos de demonstração. Ramo feature/institution-signup-checkout sobre feature/learning-materials 4e1cd45; PR #20 já integrado.

## Alterações
- Cinco passos com navegação, validação por campo, foco nos erros, resumo editável e envio único apenas na confirmação final.
- Nome/tipo/ano letivo, e-mail institucional, previsão de docentes, responsável/cargo/e-mail de acesso e palavra-passe. Telefone, morada, código postal, localidade e NIPC opcionais. NIPC validado apenas por formato, sem certificação fiscal.
- Estimativa acima da capacidade gera aviso e permite rever o plano; nunca altera quotas ou cria contas automaticamente.
- Contactos normalizados e persistidos na transação existente; consulta autenticada limitada à própria instituição. Configuração posterior preserva os contactos e apresenta-os em leitura.
- Contrato opcional Details omitido quando nulo para preservar clientes antigos. Instituições antigas mantêm campos nulos.
- Pagamento por referência/transferência apenas visual: não recolhe cartão/IBAN, não emite referências pagáveis, não cria pagamento ou fatura e não guarda preferência. Confirmações não aceitam contrato comercial nem renovação. Preços base acrescidos de IVA, sem inventar taxa.
- Retiradas mensagens globais de demonstração. Avisos localizados preservados: pagamento simulado, e-mail sem verificação, pré-visualização sem alterar dados e funcionalidades em desenvolvimento.
- Interface adaptável, temas existentes e contraste/foco preservados; progresso móvel mostra números e o nome do passo ativo.

## Validação
- Testes novos falharam antes da implementação: sete cenários API e dois Web. Passaram após implementação.
- API: 351 aprovados, 13 MySQL excluídos na suite normal. Domínio: 119; aplicação: 7; ApiClient: 93; Web: 10.
- Oracle MySQL 8 descartável: Onboarding 1/1, com migração e concorrência/criação única e contactos persistidos. Dados e servidor temporários limpos pelo script.
- Pacote Web Release preparado com Publish-Web.ps1 numa pasta de artefactos privada, sem publicação; API configurada para domínio de exemplo.
- EF confirmou ausência de alterações pendentes do modelo; diff sem erros de whitespace.
- Navegador com API fictícia local: percurso até criar e entrar na instituição, consentimento em falta com foco, contactos no perfil. Vista móvel 390x844 sem sobreposição de progresso. Não corresponde a um teste contra a API publicada.
- Revisão independente encontrou anúncio prematuro de manutenção disponível; corrigido no bloco e FAQ. Segunda revisão sem novos pontos acionáveis.

## Limites e continuidade
- Migração nova 20261126094610_InstitutionSignupContacts não aplicada online. Nada publicado nesta entrega.
- Aplicar migrações com cópia/restauro e publicar API antes da Web. Registo público continua desativado por omissão.
- Contactos novos apenas em leitura na configuração; edição posterior pode ser acrescentada com validação e concorrência.
- Ficheiros privados, atividades/entregas/feedback, comunicação e manutenção não foram implementados nesta tarefa. Não anunciar como disponíveis.
- Não enviar credenciais, backups ou artefactos privados para GitHub.
