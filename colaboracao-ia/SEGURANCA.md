# Acessos privados e colaboração

**Esta pasta e o repositório são públicos. Não guardar aqui credenciais, mesmo em ficheiros chamados «privados».** Um ramo ou uma pasta com nome discreto não restringe o acesso.

## Configuração local

- Backend em desenvolvimento: usar o mecanismo .NET User Secrets ou variáveis de ambiente, seguindo a documentação de instalação do ramo. Confirmar os nomes de configuração em Program.cs e nos exemplos versionados; não adivinhar valores.
- Configuração de produção: permanece no alojamento e no mecanismo privado de publicação. Nunca copiar web.config de produção para um commit, screenshot, PR, chat ou pacote público.
- Blazor é executado no navegador: qualquer configuração enviada ao frontend é pública. A URL pública da API pode estar no frontend; palavras-passe da base, chaves JWT e tokens de administração não podem.
- Ficheiros .env, certificados privados, PublishSettings, chaves e backups ficam fora do controlo de versões. Confirmar a exclusão com `git check-ignore` quando aplicável e rever o diff antes de cada envio. Acrescentar ao .gitignore não remove ficheiros já versionados ou o histórico.

## Partilha com o colega

O colega deve usar a sua própria conta GitHub e acessos individuais com os privilégios mínimos necessários. Partilhar os acessos necessários através de um gestor de palavras-passe ou outro canal privado acordado, nunca nesta pasta. Uma IA pode usar a configuração local dentro da autorização recebida; não deve copiar segredos para o contexto partilhado.

Não criar utilizadores, alterar permissões, redefinir palavras-passe, publicar dados ou disponibilizar serviços sem autorização correspondente. Ter uma cópia de uma credencial não constitui autorização geral.

## Incidente

Se um segredo entrar num commit, parar a divulgação, avisar o responsável, revogar/rodar o segredo afetado e planear a limpeza do histórico. Apagar o ficheiro no commit seguinte não torna o segredo novamente seguro. Não reescrever a história partilhada sem coordenação.
