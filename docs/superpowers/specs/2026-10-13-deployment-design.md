# Publicação de demonstração

O utilizador autorizou publicar a versão estável antes do suporte, escolheu Aiven gratuito e MonsterASP.NET e confirmou o e-mail da equipa para uma conta master nova na base online. Mantém-se a arquitetura definida no AGENTS.md.

- API .NET 10 em schoolgether-api.runasp.net, ambiente Production e segredos apenas no servidor.
- MySQL Aiven mysql-30f7706e, plano free-1-1gb, base defaultdb inicialmente vazia; esquema criado exclusivamente pelas migrações EF existentes. Não copiar a base local.
- Conta master schoolgether2627@gmail.com, credenciais novas e independentes das contas dos alojamentos. Criação e promoção auditadas pelos serviços existentes.
- Blazor WASM no GitHub Pages, origem https://schoolgether-pap.github.io e caminho /SchoolGether/. Nenhum segredo no bundle estático.
- TLS com CA do projeto. O provider Oracle foi validado com VerifyCA; hostname verificado separadamente com OpenSSL. Não alegar VerifyFull nem contornar erros de certificado.
- Publicação exige HTTPS válido na API, health/ready, login, CORS e carregamento dos recursos do site. Antes disso a publicação permanece incompleta.
- Código revisto por PR e colega; não integrar PRs automaticamente. Suporte permanece fora desta entrega.

Dependência externa: o plano gratuito MonsterASP.NET exige pedido de ativação HTTPS depois do upload. Preparar instruções para o utilizador; não enviar mensagens ao suporte sem autorização explícita.
