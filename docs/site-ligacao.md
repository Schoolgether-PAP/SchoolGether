# Publicação do site SaguiLearn e da SchoolGether

A fonte institucional aprovada (opção 3) está em `src/Clients/SchoolGether.Web/Marketing`: inicial, empresa e contactos. O seletor de protótipos foi retirado. O formulário de contactos apenas valida a pré-visualização; o e-mail abre o programa de correio do visitante. Os exemplos de calendário e materiais são fictícios.

A aplicação Blazor fica em `app/`, preservando os serviços de autenticação e todos os painéis existentes. Os links antigos na raiz com `?view=entrar`, `?view=area` e `?view=aderir&plan=...` são encaminhados com os parâmetros para a aplicação. O botão de adesão abre o assistente existente; não cria contas através do protótipo.

Para preparar o pacote (pasta de saída nova):

```powershell
./scripts/Publish-Web.ps1 -OutputDirectory C:/PAP/artifacts/nova-publicacao
```

O script publica a aplicação, aplica o base path `/SchoolGether/app/`, configura a URL pública da API e copia o site institucional para a raiz. Verifica ficheiros privados antes de terminar. O pacote `site/` é o conteúdo a publicar em `gh-pages`; nunca publicar a pasta inteira de artefactos operacionais.

O catálogo `2026-11-24-v1` usa anuidades 1750/2950/4000 EUR. Esta alteração exige publicar também a API; não requer migração da base. Subscrições anteriores continuam a devolver as condições guardadas. Valores exclusivamente demonstrativos, sem cobrança real.

O tema `wwwroot/css/ligacao.css` usa Public Sans local, verde floresta, ameixa e creme. Os formulários, mensagens de validação, menus responsivos e ações existentes são preservados. Professores e alunos mantêm as funcionalidades que já existiam; esta entrega não implementa novas ferramentas pedagógicas.

A base online é TiDB Starter compatível com MySQL, migrada em 24/11/2026, com TLS VerifyFull e conta técnica limitada. Preservar todas as variáveis do web.config atual durante a publicação. Não reutilizar configurações antigas que apontem para o Aiven.
