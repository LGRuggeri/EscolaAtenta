--- ISSUE 1 ---
# [Segurança] Consultas e pull expõem dados de turmas sem vínculo

Labels sugeridas: security, alta

## Descrição
EA-01: Um usuário autenticado pode pedir o primeiro pull, consultar alunos com faltas ou listar turmas. Esses handlers não recebem a identidade para restringir UsuarioTurmas. AppDbContext aplica somente Ativo, não propriedade. A consulta opcional por TurmaId no dashboard é filtro do cliente, não autorização.
Pré-condições: Monitor ou Supervisão autenticado, com vínculo apenas à turma A, e dados existentes na turma B. Acesso à API local da escola.

## Evidência
- Arquivo: src/EscolaAtenta.Application/Chamadas/Handlers/SyncPullHandler.cs:54-58
- Arquivo: src/EscolaAtenta.Application/Chamadas/Handlers/SyncPullHandler.cs:114-141
- Arquivo: src/EscolaAtenta.Application/Alunos/Queries/GetAlunosComFaltasQuery.cs:43-80
- Arquivo: src/EscolaAtenta.Application/Turmas/Handlers/GetTurmasQueryHandler.cs:18-24
- Arquivo: src/EscolaAtenta.API/Controllers/SyncController.cs:17-38
- Arquivo: src/EscolaAtenta.API/Controllers/DashboardController.cs:16-38
- Revisão: f7550204891aa6f2914a9280fe26ddb2d55ef793

```text
var todosAlunos = await _context.Alunos
    .AsNoTracking().ToListAsync(ct);
```

Verificação: Reprodução HTTP com SQLite em memória: pull, dashboard e listagem retornaram 200 e incluíram os dados da turma B para Monitor vinculado apenas à A.

## Impacto
Exposição de nomes, IDs, turma e contadores de frequência; dashboard também retorna matrícula. Permite descobrir IDs utilizados nas outras falhas. Não depende de vários tenants no mesmo banco.

## Sugestão de correção
Aplicar escopo comum por UsuarioTurmas às consultas e aos deltas created/updated/deleted; permitir abrangência global apenas a Administrador. Definir remoção local quando um vínculo for revogado.

## Critérios de aceite
- [ ] Reproduzir o cenário descrito e confirmar a rejeição ou segregação esperada.
- [ ] Preservar o fluxo legítimo para o papel e os vínculos autorizados.
- [ ] Adicionar teste de regressão que use os controles reais e SQLite quando aplicável.

--- FIM ISSUE 1 ---

--- ISSUE 2 ---
# [Segurança] Cache offline permanece compartilhado entre contas e servidores

Labels sugeridas: security, alta

## Descrição
EA-02: O app mantém uma única instância de WatermelonDB. Sair remove tokens e estado React, mas não associa nem segrega o banco por usuário/servidor. Após outro login, telas consultam o mesmo cache por turma; a configuração de outro servidor apenas troca a URL.
Pré-condições: Mesma instalação do app já contém dados; outra conta autenticada entra, ou o servidor configurado é alterado. Não exige root no Android. A revisão é estática, sem execução em dispositivo.

## Evidência
- Arquivo: src/EscolaAtenta.App/src/contexts/AuthContext.tsx:98-102
- Arquivo: src/EscolaAtenta.App/src/database/index.ts:10-24
- Arquivo: src/EscolaAtenta.App/src/screens/gestao/AlunosScreen.tsx:36-57
- Arquivo: src/EscolaAtenta.App/src/services/serverConfig.ts:10-13
- Arquivo: src/EscolaAtenta.App/src/services/sync/watermelondbSync.ts:165-178
- Revisão: f7550204891aa6f2914a9280fe26ddb2d55ef793

```text
async function signOut() {
    await authStorage.removeToken();
    setUser(null);
    setDeveAlterarSenha(false);
}
```

Verificação: Fluxo estático completo: singleton database -> signOut sem segregação -> nova sessão -> consulta local apenas por turma_id. Não houve reprodução Android.

## Impacto
A segunda sessão pode visualizar dados baixados pela anterior. Deltas locais pendentes também não carregam proprietário de sessão, criando risco de envio sob a identidade seguinte. O defeito permanece mesmo após corrigir o filtro do servidor.

## Sugestão de correção
Segregar cache e fila de sync por servidor e identidade, suspendendo sync durante a troca. Preservar pendências da conta anterior em armazenamento inacessível à nova sessão; evitar apagá-las indiscriminadamente.

## Critérios de aceite
- [ ] Reproduzir o cenário descrito e confirmar a rejeição ou segregação esperada.
- [ ] Preservar o fluxo legítimo para o papel e os vínculos autorizados.
- [ ] Adicionar teste de regressão que use os controles reais e SQLite quando aplicável.

--- FIM ISSUE 2 ---

--- ISSUE 3 ---
# [Segurança] Rotas REST contornam a restrição administrativa aplicada no sync

Labels sugeridas: security, alta

## Descrição
EA-03: O sync permite criar e editar cadastros apenas a Administrador, mas as rotas REST exigem só autenticação. As criações não verificam papel; criar aluno verifica apenas se TurmaId existe. As edições aceitam qualquer papel com vínculo, contrariando o bloqueio administrativo equivalente do sync.
Pré-condições: Monitor ou Supervisão com token válido. Para criar aluno em turma alheia basta conhecer seu GUID; ele é obtenível em EA-01. Edições exigem vínculo, ao contrário das criações.

## Evidência
- Arquivo: src/EscolaAtenta.Application/Turmas/Handlers/CriarTurmaHandler.cs:18-28
- Arquivo: src/EscolaAtenta.Application/Alunos/Handlers/CriarAlunoHandler.cs:19-37
- Arquivo: src/EscolaAtenta.Application/Turmas/Handlers/AtualizarTurmaHandler.cs:35-51
- Arquivo: src/EscolaAtenta.Application/Alunos/Handlers/AtualizarAlunoHandler.cs:35-51
- Arquivo: src/EscolaAtenta.API/Controllers/TurmasController.cs:14-35
- Arquivo: src/EscolaAtenta.API/Controllers/AlunosController.cs:13-32
- Arquivo: src/EscolaAtenta.Application/Chamadas/Handlers/SyncPushHandler.cs:172-183
- Arquivo: src/EscolaAtenta.Application/Chamadas/Handlers/SyncPushHandler.cs:685-695
- Revisão: f7550204891aa6f2914a9280fe26ddb2d55ef793

```text
var turma = await _context.Turmas
    .FirstOrDefaultAsync(t => t.Id == request.TurmaId, cancellationToken);
// Apenas existência é verificada antes de criar o aluno.
```

Verificação: HTTP 201 para Monitor criar turma e inserir aluno na turma B sem vínculo. Edições verificadas estaticamente. Agrupamento por causa raiz: políticas diferentes para a mesma operação.

## Impacto
Criação de turmas e inserção de alunos em turmas não autorizadas, além de edição de cadastros vinculados por papéis que o sync proíbe. Compromete integridade cadastral.

## Sugestão de correção
Centralizar a política administrativa e aplicá-la igualmente no REST e no sync. Se a política permitir criação delegada, verificar explicitamente o vínculo da turma e documentar a exceção.

## Critérios de aceite
- [ ] Reproduzir o cenário descrito e confirmar a rejeição ou segregação esperada.
- [ ] Preservar o fluxo legítimo para o papel e os vínculos autorizados.
- [ ] Adicionar teste de regressão que use os controles reais e SQLite quando aplicável.

--- FIM ISSUE 3 ---

--- ISSUE 4 ---
# [Segurança] Supervisão resolve alertas de turmas sem vínculo

Labels sugeridas: security, alta

## Descrição
EA-04: A rota restringe o papel, mas o handler busca qualquer AlertaId e verifica somente se o identificador do chamador é um GUID. Não compara o TurmaId do alerta com UsuarioTurmas.
Pré-condições: Usuário de Supervisão, GUID de um alerta de outra turma e justificativa válida. O GUID deve ser conhecido previamente; não se presume adivinhação de GUIDs.

## Evidência
- Arquivo: src/EscolaAtenta.Application/Alertas/Handlers/ResolverAlertaHandler.cs:20-37
- Arquivo: src/EscolaAtenta.API/Controllers/AlertasController.cs:74-81
- Revisão: f7550204891aa6f2914a9280fe26ddb2d55ef793

```text
var alerta = await _context.AlertasEvasao
    .FirstOrDefaultAsync(a => a.Id == request.AlertaId, cancellationToken);
alerta.MarcarComoResolvido(usuarioId, request.Justificativa);
```

Verificação: Com Supervisor vinculado apenas à A, PATCH de alerta da B retornou 204 no host isolado com SQLite.

## Impacto
Encerramento indevido de alertas de evasão e gravação de justificativa/responsável fora da área de atuação. O Monitor é bloqueado pela rota, mas Supervisão não tem acesso global nas consultas protegidas.

## Sugestão de correção
Autorizar pela turma persistida no alerta antes de MarcarComoResolvido. Administrador pode ter acesso global; definir tratamento restrito para alertas sem turma.

## Critérios de aceite
- [ ] Reproduzir o cenário descrito e confirmar a rejeição ou segregação esperada.
- [ ] Preservar o fluxo legítimo para o papel e os vínculos autorizados.
- [ ] Adicionar teste de regressão que use os controles reais e SQLite quando aplicável.

--- FIM ISSUE 4 ---

--- ISSUE 5 ---
# [Segurança] Troca obrigatória de senha é imposta apenas pela navegação

Labels sugeridas: security, média

## Descrição
EA-05: O login emite JWT com papel completo quando DeveAlterarSenha=true. Nenhuma política do servidor restringe esse token. O app decide mostrar a tela de troca por estado React; a restauração de sessão não restaura essa obrigação e a resposta de refresh usa o valor padrão false.
Pré-condições: Credencial inicial válida de uma conta marcada para troca obrigatória. O atacante não obtém a senha por esta falha; ele contorna a restrição depois de autenticar.

## Evidência
- Arquivo: src/EscolaAtenta.App/src/navigation/AppNavigator.tsx:45-53
- Arquivo: src/EscolaAtenta.App/src/contexts/AuthContext.tsx:42-65
- Arquivo: src/EscolaAtenta.Application/Auth/LoginHandler.cs:58-82
- Arquivo: src/EscolaAtenta.API/Controllers/AuthController.cs:142-150
- Arquivo: src/EscolaAtenta.Infrastructure/Services/AuthService.cs:48-73
- Revisão: f7550204891aa6f2914a9280fe26ddb2d55ef793

```text
var loginResult = _authService.GerarToken(usuario);
// DeveAlterarSenha é apenas um campo da resposta.
```

Verificação: Login retornou deveAlterarSenha=true e o JWT foi aceito em GET /usuarios com 200 antes de qualquer troca.

## Impacto
Uso completo da conta com senha inicial, inclusive funções administrativas para uma conta Administrador, sem concluir a troca exigida.

## Sugestão de correção
Aplicar bloqueio no servidor para contas com troca pendente, permitindo somente troca e encerramento da sessão. Revalidar a obrigação na restauração/refresh e emitir sessão adequada após a troca.

## Critérios de aceite
- [ ] Reproduzir o cenário descrito e confirmar a rejeição ou segregação esperada.
- [ ] Preservar o fluxo legítimo para o papel e os vínculos autorizados.
- [ ] Adicionar teste de regressão que use os controles reais e SQLite quando aplicável.

--- FIM ISSUE 5 ---

--- ISSUE 6 ---
# [Segurança] Mudanças de segurança não invalidam sessões existentes

Labels sugeridas: security, alta

## Descrição
EA-06: A validação JWT verifica assinatura, emissor, público e prazo, mas não o estado/papel atual no banco nem uma versão de sessão. Desativação e rebaixamento alteram apenas Usuario. Troca de senha não revoga refresh tokens.
Pré-condições: Posse de token emitido antes da alteração. O JWT persiste até expirar, por padrão 60 minutos e tolerância de 30 segundos. Refresh anterior à troca de senha permanece utilizável por até seu prazo de 30 dias; login posterior revoga os anteriores.

## Evidência
- Arquivo: src/EscolaAtenta.API/Program.cs:112-135
- Arquivo: src/EscolaAtenta.Application/Usuarios/Commands/AlternarStatusUsuarioCommand.cs:23-33
- Arquivo: src/EscolaAtenta.Application/Usuarios/Commands/AtualizarUsuarioCommand.cs:21-27
- Arquivo: src/EscolaAtenta.API/Controllers/AuthController.cs:102-107
- Arquivo: src/EscolaAtenta.API/Controllers/AuthController.cs:123-142
- Arquivo: src/EscolaAtenta.Infrastructure/Services/AuthService.cs:46-57
- Revisão: f7550204891aa6f2914a9280fe26ddb2d55ef793

```text
usuario.AlterarSenha(novoHash);
await _dbContext.SaveChangesAsync(ct);
// Não revoga RefreshTokens nem invalida JWTs existentes.
```

Verificação: Três reproduções: JWT de usuário inativo aceito; papel Administrador antigo aceito após rebaixamento; refresh anterior aceito após trocar senha. Todos retornaram 200.

## Impacto
Conta desativada continua lendo dados; ex-Administrador continua acessando endpoints administrativos até o JWT expirar. Um refresh comprometido pode restabelecer sessão após a vítima trocar a senha.

## Sugestão de correção
Introduzir versão de sessão/security stamp validada no servidor; revogar sessões e refresh tokens em desativação, rebaixamento e troca de senha. Manter os fluxos legítimos de refresh e offline conforme política explícita.

## Critérios de aceite
- [ ] Reproduzir o cenário descrito e confirmar a rejeição ou segregação esperada.
- [ ] Preservar o fluxo legítimo para o papel e os vínculos autorizados.
- [ ] Adicionar teste de regressão que use os controles reais e SQLite quando aplicável.

--- FIM ISSUE 6 ---

--- ISSUE 7 ---
# [Segurança] Fallback JWT conhecido permite forjar Administrador em Development

Labels sugeridas: security, alta

## Descrição
EA-07: Quando Jwt:SecretKey está vazia em Development, emissor e validador usam o mesmo literal versionado. A configuração atual de desenvolvimento deixa a chave vazia, e o perfil de execução escuta em 0.0.0.0. Uma pessoa com acesso ao código consegue assinar JWTs com papel Administrador.
Pré-condições: API em Development com chave vazia e alcançável pelo atacante. Este achado é condicional ao modo; produção com chave vazia gera 64 bytes aleatórios, portanto não usa esse fallback.

## Evidência
- Arquivo: src/EscolaAtenta.API/Program.cs:79-84
- Arquivo: src/EscolaAtenta.Infrastructure/Services/AuthService.cs:32-40
- Arquivo: src/EscolaAtenta.API/Properties/launchSettings.json:8-10
- Arquivo: src/EscolaAtenta.API/appsettings.Development.json:33-37
- Revisão: f7550204891aa6f2914a9280fe26ddb2d55ef793

```text
if (builder.Environment.IsDevelopment()) {
    secretKey = "[SEGREDO REDIGIDO]";
}
```

Verificação: Validação criptográfica local confirmou papel Administrador. Histórico: commit 1fadfb0da5698028b8ac21ca9194e3164b42f3e9, src/EscolaAtenta.API/appsettings.Development.json:34 e build-temp/appsettings.Development.json:34 continham chave literal redigida.

## Impacto
Autenticação forjada e acesso administrativo sem senha. A assinatura local foi demonstrada com sub sintético e papel Administrador, sem publicar chave ou token.

## Sugestão de correção
Remover o literal de ambos os caminhos. Usar segredo local aleatório ou exigir configuração segura; restringir a interface de desenvolvimento a loopback. Rotacionar chaves de instâncias que tenham usado o literal.

## Critérios de aceite
- [ ] Reproduzir o cenário descrito e confirmar a rejeição ou segregação esperada.
- [ ] Preservar o fluxo legítimo para o papel e os vínculos autorizados.
- [ ] Adicionar teste de regressão que use os controles reais e SQLite quando aplicável.

--- FIM ISSUE 7 ---

--- ISSUE 8 ---
# [Segurança] Instalador concede leitura de segredos locais a usuários comuns

Labels sugeridas: security, alta

## Descrição
EA-08: O instalador concede users-readexec à pasta base; no Inno Setup essa permissão inclui herança para descendentes. O startup de produção grava a chave JWT em API/appsettings.json e o seed registra a senha inicial em logs sob a pasta base. Não há ACL restritiva específica nesses destinos no instalador.
Pré-condições: Instalação pelo script fornecido, herança padrão mantida e conta Windows comum com acesso à máquina. Chave gerada em arquivo e/ou logs do seed presentes. ACL de uma instalação real não foi inspecionada; é achado condicional do artefato de deploy.

## Evidência
- Arquivo: escolaatenta-installer.iss:66-70
- Arquivo: src/EscolaAtenta.API/Program.cs:87-101
- Arquivo: src/EscolaAtenta.Infrastructure/Data/DatabaseSeeder.cs:100-105
- Arquivo: src/EscolaAtenta.API/appsettings.json:12-12
- Revisão: f7550204891aa6f2914a9280fe26ddb2d55ef793

```text
Name: "{app}"; Permissions: admins-full system-full users-readexec
json["Jwt"]!["SecretKey"] = secretKey;
```

Verificação: Evidência estática do script e dos destinos de gravação; sem instalação, UAC ou alteração de ACL na máquina. Sem comprovação dinâmica de uma instalação específica.

## Impacto
Leitura da chave de assinatura por usuário local permite forjar identidade administrativa; logs podem expor credencial inicial. A aleatoriedade da chave não protege contra sua leitura.

## Sugestão de correção
Separar dados, segredos e logs dos binários. Restringir leitura ao serviço/SYSTEM e administradores. Dar ao TrayMonitor somente a informação operacional necessária, sem acesso à chave JWT. Provisionar a senha inicial por canal restrito e temporário.

## Critérios de aceite
- [ ] Reproduzir o cenário descrito e confirmar a rejeição ou segregação esperada.
- [ ] Preservar o fluxo legítimo para o papel e os vínculos autorizados.
- [ ] Adicionar teste de regressão que use os controles reais e SQLite quando aplicável.

--- FIM ISSUE 8 ---