# Correções da auditoria - plano de implementação

Objetivo: corrigir EA-01 a EA-08, preservando uso offline e dados existentes.
Especificação: docs/security-audit/dados.json; revisão original f7550204891aa6f2914a9280fe26ddb2d55ef793.
Arquitetura: escopo por turma no servidor, validação de estado de sessão a cada JWT, banco mobile por identidade/servidor e segredo local protegido.
Stack: .NET 9, SQLite, MediatR, React Native/WatermelonDB e Inno Setup.

- [x] Autorizações: testes negativos e positivos de leitura/pull, criação/edição admin e resolução por turma; implementar e rodar testes focados.
- [x] Sessões: criar testes HTTP com middleware/eventos reais e testes diretos do AuthController para 403 de troca obrigatória, 401 após desativação/rebaixamento e rejeição de refresh anterior à troca; implementar validação centralizada sem migration manual.
- [x] Mobile: teste de namespace/concorrência; segregar banco sem destruir pendências, impedir troca durante operações e restaurar obrigação de senha.
- [x] Segredos: teste de geração/persistência; substituir chave conhecida, proteger dados/config/logs em instalação nova e existente sem executar instalador na máquina.
- [x] Integração: executar dotnet test, TypeScript e testes mobile; registrar limitações e arquivos corrigidos.

Decisões: trabalhar na branch de correção atual, sem commit/push; manter relatório original como fotografia do estado auditado. Reproduções originais não são reescritas para aparentar sucesso; testes novos expressam comportamento seguro.
Responsabilidades sem sobreposição: agente autorização em handlers cadastros/consultas; agente mobile apenas app; agente segredos provider novo/instalador; raiz AuthService, Program (integração), autenticação e testes HTTP.
