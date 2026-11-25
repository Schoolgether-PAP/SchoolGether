# Navegação, calendário e ligações académicas — 25/11/2026

Ramo `feature/school-workspaces`; continuação da adesão completa em `014bd01` e investigação da avaliação em `c54b165`.

## Alterações

- Preferência de aspeto automático/claro/escuro, partilhada entre a apresentação pública e a aplicação. Apenas preferências visuais no armazenamento do navegador.
- Menu lateral recolhível no computador, com nomes acessíveis; navegação móvel mantém os rótulos.
- Calendário escolar por instituição: docentes propõem, administradores publicam/aprovam e alunos consultam eventos publicados. Horas de Portugal continental, controlo de versões, auditoria e pedidos de criação repetíveis.
- Inscrição de alunos por turma/curso/ano letivo e atribuição de professores a disciplinas selecionadas, com cancelamento/restauro. A lista e o histórico identificam a disciplina pelo código e nome, incluindo seleções retiradas.
- Projeção interna de contextos pedagógicos autorizados, sem alargar os filtros dos catálogos administrativos.
- Opção explícita de armazenamento privado do suporte em `App_Data`, para alojamentos com caminho físico atribuído. Desativada por omissão, como o suporte.
- Contactos públicos preparam um e-mail no programa do utilizador; não afirmam que a mensagem foi enviada.

## Evidência e limites

Testes focados registados nesta sessão: 17 integrações de inscrições/atribuições/calendário/opções de suporte aprovadas, com dois cenários MySQL ignorados nessa execução. Ambos os cenários MySQL foram executados depois, separadamente, com sucesso num servidor MySQL descartável. Teste adicional de atribuições aprovado após corrigir a descrição das disciplinas e acrescentar verificações de histórico, versão desatualizada e seleção retirada. Seis testes de cliente do calendário aprovados, incluindo recuperação após resposta perdida.

Compilação Web aprovada sem avisos/erros antes da integração dos componentes pedagógicos seguintes. A revisão independente aceitou a correção dos nomes das disciplinas. O calendário recebeu correções adicionais após revisão; a validação visual da criação e a revisão final ainda estão em curso.

No navegador local foram verificados acesso institucional, configuração já concluída na adesão, menu móvel, menu recolhido e aspeto escuro. A tentativa de criar um evento devolveu validação inválida; a mesma operação direta na API local devolveu 201. Não declarar a verificação visual concluída enquanto essa diferença não for resolvida.

Estes registos não demonstram publicação. O suporte online continua por ativar e verificar. As áreas pedagógicas estão em desenvolvimento; consultar os registos próprios. Não existem cobranças reais nem foi alterada a avaliação de alunos reais.
