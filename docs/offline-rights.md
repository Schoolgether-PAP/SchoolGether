# Leitura offline e direitos sobre materiais

Data: 2026-10-14. Requisito recebido; funcionalidade por desenvolver na etapa de offline. Este registo não é uma especificação completa nem aprova uma implementação de DRM.

## Intenção confirmada pelo utilizador

Alguns materiais têm restrições de direitos e de comercialização. O aluno deve poder consultar uma cópia offline autorizada dentro do SchoolGether, sem receber um ficheiro normal na pasta Downloads que possa partilhar. A experiência de conteúdos offline da Netflix é uma referência de funcionamento.

### Origem e escolha de proteção — decisão confirmada

- Todos os materiais disponibilizados pela SaguiLearn/SchoolGether são protegidos. Professores e alunos não podem desativar essa proteção.
- Nos materiais disponibilizados pelo professor, ele escolhe se o material é protegido ou não protegido.
- Protegido: consulta dentro da plataforma e, quando houver autorização offline, cópia protegida dentro da app, sem exportação livre.
- Não protegido: permite disponibilizar download normal ao aluno autorizado. Isto não torna o material público, não dispensa os acessos da instituição nem concede autorização automática para comercialização.

A implementação deverá identificar a origem do material e aplicar estas regras na API, além da interface. A escolha de proteção não permite remover restrições impostas pelo titular dos direitos. Proposta do agente ainda por confirmar: selecionar «Protegido» inicialmente no formulário do professor, permitindo-lhe alterar essa escolha nos seus materiais quando autorizado.

## Proposta técnica para definir posteriormente

- Distinguir, por material, consulta online, leitura offline protegida e exportação autorizada. A ausência de autorização offline deve impedir a preparação da cópia local.
- Guardar a cópia protegida cifrada em armazenamento privado da app; não basta esconder o botão de download ou mudar a extensão.
- Associar a autorização de leitura à conta, instituição e dispositivo. Usar proteção de chaves adequada a Android e Windows; não distribuir uma chave universal dentro do código.
- Usar um leitor integrado que não exporte ficheiros em claro, não use aplicações externas e respeite acessibilidade. Formatos suportados por definir.
- Definir validade da autorização offline, renovação online e comportamento ao sair ou trocar de conta. Sem ligação, uma revogação no servidor não pode ser conhecida imediatamente; a validade limita essa janela.
- Conservar os originais em armazenamento privado no servidor e validar autorização nos endpoints. Não usar URLs públicas permanentes para materiais protegidos.

Estas medidas são propostas do agente, ainda não regras de produto aprovadas. A solução deve ser avaliada separadamente em Android, Windows e navegador; proteção equivalente não é presumida.

## Limites e direitos

Armazenamento privado e cifra dificultam cópia casual, mas não garantem impedir extração num dispositivo comprometido, captura de ecrã ou fotografia. Não apresentar a implementação como DRM certificado ou proteção absoluta.

A permissão de comercialização, a de consulta, a de disponibilização offline e a de redistribuição são questões distintas. Permitir uma ação na plataforma não concede direitos sobre o conteúdo; é necessário conhecer as autorizações do titular. Para a PAP, usar materiais próprios ou licenciados adequadamente e demonstrar as restrições com exemplos autorizados.

## Decisões pendentes

Tipos de material (documentos, livros, vídeos ou outros), autorizações offline por categoria, escolha inicial no formulário do professor, validade offline, número de dispositivos e requisitos do leitor. A regra de proteção por origem acima já está confirmada. Os restantes pontos serão definidos antes da implementação, preservando a prioridade atual de página pública e adesão institucional.
