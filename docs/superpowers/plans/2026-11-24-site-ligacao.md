# Opção 3 e identidade visual da plataforma — plano

**Objetivo:** publicar a opção 3 aprovada como site institucional SaguiLearn e alinhar o login e os painéis SchoolGether, preservando as funcionalidades.
**Arquitetura:** páginas institucionais estáticas na raiz do pacote Pages; aplicação Blazor existente em app/, com compatibilidade para os antigos URLs ?view=area/entrar/aderir. Sem nova dependência.
**Requisitos:** empresa e contactos em páginas próprias; Public Sans, floresta/ameixa/creme, logótipos atuais; e-mail saguilearn@gmail.com e morada confirmada; preços novos 1750/2950/4000, contratos anteriores preservados. Sem cobranças reais.

- [ ] Integrar os ficheiros da opção 3 como fonte versionada, retirar o seletor de templates e conservar os exemplos marcados como demonstração.
- [ ] Adaptar ligações de login/adesão e ligações antigas; manter formulários reais da aplicação e o contacto por e-mail (formulário público demonstrativo).
- [ ] Atualizar tipografia, cores, cabeçalhos, login, painéis e componentes sem alterar autorização/ações existentes.
- [ ] Atualizar catálogo e versão apenas para novas inscrições, verificando testes focados de preços e preservação.
- [ ] Compilar e empacotar .NET/Web; verificar navegação, login, perfis e menu em desktop/390 px.
- [ ] Revisão independente, corrigir problemas, publicar código em PR e artefactos/API autorizados com cópia anterior preservada; verificar online.

Riscos de revisão: base path /SchoolGether/app/, compatibilidade dos URLs antigos, ações com estados disabled/focus/erro, isolamento e preservação de condições anteriores; não publicar credenciais ou artefactos privados.
