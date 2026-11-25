# Avaliação por percurso escolar

- Pedido: investigar os métodos de avaliação por ano e tipo de ensino em Portugal, em vez de impor a escala 0–20.
- Pesquisa em DGE, ANQEP e Diário da República: fontes, diferenças e limites em `docs/assessment-portugal.md`.
- Regras novas: `src/Backend/SchoolGether.Domain/Learning/AssessmentScale.cs`. Sugestões por percurso/ano, feedback sem nota, menções do 1.º ciclo, níveis 1–5, valores 0–20, aproveitamento e percentagem de instrumentos sem conversão automática para nota oficial.
- O percurso é explícito: o primeiro ano de uma licenciatura não é interpretado como primeiro ano básico. Pré-escolar admite apenas observações descritivas. Validação de limites e tipos de classificação no domínio.
- Testes: `AssessmentScaleTests` começou por falhar por ausência da implementação; após implementação, 18 casos passaram, zero excluídos. Verifica limites, percursos, ano incoerente, nota fracionária indevida e ausência de conversões qualitativas.
- Esta entrega prepara regras de domínio; a configuração por turma e o feedback nas atividades serão ligados no módulo pedagógico. Não há pautas, SIGO, certificados oficiais nem cálculo de classificação final de curso.
- Sem migração/publicação nesta entrega. Próximo passo: integrar estas regras nas atividades, guardando a escala usada para preservar avaliações anteriores.
