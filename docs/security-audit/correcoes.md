# Correções da auditoria de segurança

Data: 09/09/2026. Base auditada: f7550204891aa6f2914a9280fe26ddb2d55ef793.
Alterações implementadas na árvore de trabalho; sem publicação ou alteração do serviço instalado.
O relatório PDF e as reproduções originais continuam como evidência do estado anterior.

| Achado | Correção implementada | Evidência |
|---|---|---|
| EA-01 | Consultas de turmas, faltas, auditoria e sync filtradas por vínculo; administrador mantém visão global; identidade inválida negada. Pull inclui snapshot de IDs autorizados, usado pelo app para excluir cache revogado e recuperar dados de vínculos novos. | EscopoAcessoTests e testes de handlers; TypeScript |
| EA-02 | Banco separado por servidor e usuário; tokens vinculados à origem; logout aguarda sync/requisições/refresh; navegação desmonta ao trocar conta. | Quatro testes Node, incluindo preservação de pendências e troca de instância; TypeScript |
| EA-03 | Criação/edição de cadastros exige Administrador nos controllers e handlers; formulários e ações correspondentes restritos no app. | Testes de criação/atualização e EscopoAcessoTests |
| EA-04 | Resolução de alertas exige Supervisão vinculada à turma persistida ou Administrador. | Testes de resolução dentro/fora do vínculo |
| EA-05 | Claim de troca obrigatória preservada no refresh e restauração mobile; middleware bloqueia operações protegidas enquanto pendente, permitindo a troca. | SessaoHttpTests e SegurancaSessaoTests |
| EA-06 | JWT validado contra estado atual do usuário; mudanças de senha, papel e desativação invalidam sessão; troca de senha revoga refresh; rotação transacional consome token uma vez. | Testes HTTP 401 após alterações e testes do AuthController |
| EA-07 | Removida chave fixa de desenvolvimento; chave aleatória persistente protegida em Secrets/jwt.key; configuração ausente no emissor falha explicitamente. | JwtKeyProviderTests: geração, persistência, ACL do arquivo e configuração |
| EA-08 | Instalador aplica ACLs restritas em API, banco, configurações e logs, incluindo instalações existentes; Users mantém acesso somente ao TrayMonitor. Script verifica elevação administrativa antes de alterar permissões. | Sintaxe PowerShell validada; validação integral do instalador pendente em Windows elevado |

## Validação executada

- `dotnet test --no-restore --verbosity quiet`: 285 testes aprovados (63 domínio e 222 aplicação), zero falhas.
- `node node_modules/typescript/bin/tsc --noEmit` no app: aprovado.
- `node --test tests/sessionScope.test.cjs`: quatro testes aprovados.
- `git diff --check`: aprovado.
- Parser PowerShell do script de ACL: aprovado.
- Teste de ACL em diretório temporário sintético: interrompido por falta de elevação administrativa Windows. Nenhuma instalação real foi alterada. Foi acrescentada verificação de elevação antes das mutações para impedir aplicação parcial nesta condição.

## Limites e implantação

Não foi executado APK em aparelho/emulador nem compilado/executado o instalador Inno Setup. Os testes Node usam adaptadores simulados e não substituem validação nativa de WatermelonDB/SecureStore. A proteção completa das ACLs deve ser validada no instalador com elevação administrativa antes da distribuição.

Atualize API e aplicativo em conjunto: o novo app exige snapshot de autorização no pull. JWTs antigos exigem renovação ou novo login. Mudanças de perfil invalidam também refresh emitido anteriormente. A revogação depende de contato com o servidor; um dispositivo inteiramente offline não recebe imediatamente mudanças de permissão.

O banco legado mobile sem identificação de dono fica preservado, mas não é aberto pela nova versão. Não há atribuição automática dessas pendências a uma conta: antes de atualizar dispositivos antigos, sincronize as pendências com a conta correta na versão anterior, ou faça recuperação administrativa identificando a origem. Os novos bancos por conta preservam suas próprias pendências entre logins.

Para instalações antigas: parar a API, aplicar o instalador atualizado e rotacionar chaves anteriormente expostas conforme INSTALACAO.txt. O código não revoga cópias já obtidas de chaves, logs ou dados. Não incluir Secrets, SQLite ou logs nos pacotes. Nenhuma migration foi necessária.
