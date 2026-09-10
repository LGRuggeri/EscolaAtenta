import json,pathlib,re,subprocess
O=pathlib.Path(__file__).resolve().parent
R=O.parents[1]
def loc(p,a,b=None): return {'arquivo':p,'inicio':a,'fim':b or a}
def f(i,c,s,t,ls,desc,cond,impact,fix,proof,code):
 return dict(id=i,categoria=c,severidade=s,titulo=t,locais=ls,descricao=desc,condicoes=cond,impacto=impact,correcao=fix,verificacao=proof,trecho=code,aceite=['Reproduzir o cenário descrito e confirmar a rejeição ou segregação esperada.','Preservar o fluxo legítimo para o papel e os vínculos autorizados.','Adicionar teste de regressão que use os controles reais e SQLite quando aplicável.'])
A='src/EscolaAtenta.Application/'; P='src/EscolaAtenta.API/'; M='src/EscolaAtenta.App/src/'; I='src/EscolaAtenta.Infrastructure/'
fs=[
f('EA-01',1,'alta','Consultas e pull expõem dados de turmas sem vínculo',[
loc(A+'Chamadas/Handlers/SyncPullHandler.cs',54,58),loc(A+'Chamadas/Handlers/SyncPullHandler.cs',114,141),loc(A+'Alunos/Queries/GetAlunosComFaltasQuery.cs',43,80),loc(A+'Turmas/Handlers/GetTurmasQueryHandler.cs',18,24),loc(P+'Controllers/SyncController.cs',17,38),loc(P+'Controllers/DashboardController.cs',16,38)],
'Um usuário autenticado pode pedir o primeiro pull, consultar alunos com faltas ou listar turmas. Esses handlers não recebem a identidade para restringir UsuarioTurmas. AppDbContext aplica somente Ativo, não propriedade. A consulta opcional por TurmaId no dashboard é filtro do cliente, não autorização.',
'Monitor ou Supervisão autenticado, com vínculo apenas à turma A, e dados existentes na turma B. Acesso à API local da escola.',
'Exposição de nomes, IDs, turma e contadores de frequência; dashboard também retorna matrícula. Permite descobrir IDs utilizados nas outras falhas. Não depende de vários tenants no mesmo banco.',
'Aplicar escopo comum por UsuarioTurmas às consultas e aos deltas created/updated/deleted; permitir abrangência global apenas a Administrador. Definir remoção local quando um vínculo for revogado.',
'Reprodução HTTP com SQLite em memória: pull, dashboard e listagem retornaram 200 e incluíram os dados da turma B para Monitor vinculado apenas à A.',
'var todosAlunos = await _context.Alunos\n    .AsNoTracking().ToListAsync(ct);'),
f('EA-02',1,'alta','Cache offline permanece compartilhado entre contas e servidores',[
loc(M+'contexts/AuthContext.tsx',98,102),loc(M+'database/index.ts',10,24),loc(M+'screens/gestao/AlunosScreen.tsx',36,57),loc(M+'services/serverConfig.ts',10,13),loc(M+'services/sync/watermelondbSync.ts',165,178)],
'O app mantém uma única instância de WatermelonDB. Sair remove tokens e estado React, mas não associa nem segrega o banco por usuário/servidor. Após outro login, telas consultam o mesmo cache por turma; a configuração de outro servidor apenas troca a URL.',
'Mesma instalação do app já contém dados; outra conta autenticada entra, ou o servidor configurado é alterado. Não exige root no Android. A revisão é estática, sem execução em dispositivo.',
'A segunda sessão pode visualizar dados baixados pela anterior. Deltas locais pendentes também não carregam proprietário de sessão, criando risco de envio sob a identidade seguinte. O defeito permanece mesmo após corrigir o filtro do servidor.',
'Segregar cache e fila de sync por servidor e identidade, suspendendo sync durante a troca. Preservar pendências da conta anterior em armazenamento inacessível à nova sessão; evitar apagá-las indiscriminadamente.',
'Fluxo estático completo: singleton database -> signOut sem segregação -> nova sessão -> consulta local apenas por turma_id. Não houve reprodução Android.',
'async function signOut() {\n    await authStorage.removeToken();\n    setUser(null);\n    setDeveAlterarSenha(false);\n}'),
f('EA-03',2,'alta','Rotas REST contornam a restrição administrativa aplicada no sync',[
loc(A+'Turmas/Handlers/CriarTurmaHandler.cs',18,28),loc(A+'Alunos/Handlers/CriarAlunoHandler.cs',19,37),loc(A+'Turmas/Handlers/AtualizarTurmaHandler.cs',35,51),loc(A+'Alunos/Handlers/AtualizarAlunoHandler.cs',35,51),loc(P+'Controllers/TurmasController.cs',14,35),loc(P+'Controllers/AlunosController.cs',13,32),loc(A+'Chamadas/Handlers/SyncPushHandler.cs',172,183),loc(A+'Chamadas/Handlers/SyncPushHandler.cs',685,695)],
'O sync permite criar e editar cadastros apenas a Administrador, mas as rotas REST exigem só autenticação. As criações não verificam papel; criar aluno verifica apenas se TurmaId existe. As edições aceitam qualquer papel com vínculo, contrariando o bloqueio administrativo equivalente do sync.',
'Monitor ou Supervisão com token válido. Para criar aluno em turma alheia basta conhecer seu GUID; ele é obtenível em EA-01. Edições exigem vínculo, ao contrário das criações.',
'Criação de turmas e inserção de alunos em turmas não autorizadas, além de edição de cadastros vinculados por papéis que o sync proíbe. Compromete integridade cadastral.',
'Centralizar a política administrativa e aplicá-la igualmente no REST e no sync. Se a política permitir criação delegada, verificar explicitamente o vínculo da turma e documentar a exceção.',
'HTTP 201 para Monitor criar turma e inserir aluno na turma B sem vínculo. Edições verificadas estaticamente. Agrupamento por causa raiz: políticas diferentes para a mesma operação.',
'var turma = await _context.Turmas\n    .FirstOrDefaultAsync(t => t.Id == request.TurmaId, cancellationToken);\n// Apenas existência é verificada antes de criar o aluno.'),
f('EA-04',3,'alta','Supervisão resolve alertas de turmas sem vínculo',[
loc(A+'Alertas/Handlers/ResolverAlertaHandler.cs',20,37),loc(P+'Controllers/AlertasController.cs',74,81)],
'A rota restringe o papel, mas o handler busca qualquer AlertaId e verifica somente se o identificador do chamador é um GUID. Não compara o TurmaId do alerta com UsuarioTurmas.',
'Usuário de Supervisão, GUID de um alerta de outra turma e justificativa válida. O GUID deve ser conhecido previamente; não se presume adivinhação de GUIDs.',
'Encerramento indevido de alertas de evasão e gravação de justificativa/responsável fora da área de atuação. O Monitor é bloqueado pela rota, mas Supervisão não tem acesso global nas consultas protegidas.',
'Autorizar pela turma persistida no alerta antes de MarcarComoResolvido. Administrador pode ter acesso global; definir tratamento restrito para alertas sem turma.',
'Com Supervisor vinculado apenas à A, PATCH de alerta da B retornou 204 no host isolado com SQLite.',
'var alerta = await _context.AlertasEvasao\n    .FirstOrDefaultAsync(a => a.Id == request.AlertaId, cancellationToken);\nalerta.MarcarComoResolvido(usuarioId, request.Justificativa);'),
f('EA-05',2,'média','Troca obrigatória de senha é imposta apenas pela navegação',[
loc(M+'navigation/AppNavigator.tsx',45,53),loc(M+'contexts/AuthContext.tsx',42,65),loc(A+'Auth/LoginHandler.cs',58,82),loc(P+'Controllers/AuthController.cs',142,150),loc(I+'Services/AuthService.cs',48,73)],
'O login emite JWT com papel completo quando DeveAlterarSenha=true. Nenhuma política do servidor restringe esse token. O app decide mostrar a tela de troca por estado React; a restauração de sessão não restaura essa obrigação e a resposta de refresh usa o valor padrão false.',
'Credencial inicial válida de uma conta marcada para troca obrigatória. O atacante não obtém a senha por esta falha; ele contorna a restrição depois de autenticar.',
'Uso completo da conta com senha inicial, inclusive funções administrativas para uma conta Administrador, sem concluir a troca exigida.',
'Aplicar bloqueio no servidor para contas com troca pendente, permitindo somente troca e encerramento da sessão. Revalidar a obrigação na restauração/refresh e emitir sessão adequada após a troca.',
'Login retornou deveAlterarSenha=true e o JWT foi aceito em GET /usuarios com 200 antes de qualquer troca.',
'var loginResult = _authService.GerarToken(usuario);\n// DeveAlterarSenha é apenas um campo da resposta.'),
f('EA-06',2,'alta','Mudanças de segurança não invalidam sessões existentes',[
loc(P+'Program.cs',112,135),loc(A+'Usuarios/Commands/AlternarStatusUsuarioCommand.cs',23,33),loc(A+'Usuarios/Commands/AtualizarUsuarioCommand.cs',21,27),loc(P+'Controllers/AuthController.cs',102,107),loc(P+'Controllers/AuthController.cs',123,142),loc(I+'Services/AuthService.cs',46,57)],
'A validação JWT verifica assinatura, emissor, público e prazo, mas não o estado/papel atual no banco nem uma versão de sessão. Desativação e rebaixamento alteram apenas Usuario. Troca de senha não revoga refresh tokens.',
'Posse de token emitido antes da alteração. O JWT persiste até expirar, por padrão 60 minutos e tolerância de 30 segundos. Refresh anterior à troca de senha permanece utilizável por até seu prazo de 30 dias; login posterior revoga os anteriores.',
'Conta desativada continua lendo dados; ex-Administrador continua acessando endpoints administrativos até o JWT expirar. Um refresh comprometido pode restabelecer sessão após a vítima trocar a senha.',
'Introduzir versão de sessão/security stamp validada no servidor; revogar sessões e refresh tokens em desativação, rebaixamento e troca de senha. Manter os fluxos legítimos de refresh e offline conforme política explícita.',
'Três reproduções: JWT de usuário inativo aceito; papel Administrador antigo aceito após rebaixamento; refresh anterior aceito após trocar senha. Todos retornaram 200.',
'usuario.AlterarSenha(novoHash);\nawait _dbContext.SaveChangesAsync(ct);\n// Não revoga RefreshTokens nem invalida JWTs existentes.'),
f('EA-07',4,'alta','Fallback JWT conhecido permite forjar Administrador em Development',[
loc(P+'Program.cs',79,84),loc(I+'Services/AuthService.cs',32,40),loc(P+'Properties/launchSettings.json',8,10),loc(P+'appsettings.Development.json',33,37)],
'Quando Jwt:SecretKey está vazia em Development, emissor e validador usam o mesmo literal versionado. A configuração atual de desenvolvimento deixa a chave vazia, e o perfil de execução escuta em 0.0.0.0. Uma pessoa com acesso ao código consegue assinar JWTs com papel Administrador.',
'API em Development com chave vazia e alcançável pelo atacante. Este achado é condicional ao modo; produção com chave vazia gera 64 bytes aleatórios, portanto não usa esse fallback.',
'Autenticação forjada e acesso administrativo sem senha. A assinatura local foi demonstrada com sub sintético e papel Administrador, sem publicar chave ou token.',
'Remover o literal de ambos os caminhos. Usar segredo local aleatório ou exigir configuração segura; restringir a interface de desenvolvimento a loopback. Rotacionar chaves de instâncias que tenham usado o literal.',
'Validação criptográfica local confirmou papel Administrador. Histórico: commit 1fadfb0da5698028b8ac21ca9194e3164b42f3e9, src/EscolaAtenta.API/appsettings.Development.json:34 e build-temp/appsettings.Development.json:34 continham chave literal redigida.',
'if (builder.Environment.IsDevelopment()) {\n    secretKey = "[SEGREDO REDIGIDO]";\n}'),
f('EA-08',4,'alta','Instalador concede leitura de segredos locais a usuários comuns',[
loc('escolaatenta-installer.iss',66,70),loc(P+'Program.cs',87,101),loc(I+'Data/DatabaseSeeder.cs',100,105),loc(P+'appsettings.json',12,12)],
'O instalador concede users-readexec à pasta base; no Inno Setup essa permissão inclui herança para descendentes. O startup de produção grava a chave JWT em API/appsettings.json e o seed registra a senha inicial em logs sob a pasta base. Não há ACL restritiva específica nesses destinos no instalador.',
'Instalação pelo script fornecido, herança padrão mantida e conta Windows comum com acesso à máquina. Chave gerada em arquivo e/ou logs do seed presentes. ACL de uma instalação real não foi inspecionada; é achado condicional do artefato de deploy.',
'Leitura da chave de assinatura por usuário local permite forjar identidade administrativa; logs podem expor credencial inicial. A aleatoriedade da chave não protege contra sua leitura.',
'Separar dados, segredos e logs dos binários. Restringir leitura ao serviço/SYSTEM e administradores. Dar ao TrayMonitor somente a informação operacional necessária, sem acesso à chave JWT. Provisionar a senha inicial por canal restrito e temporário.',
'Evidência estática do script e dos destinos de gravação; sem instalação, UAC ou alteração de ACL na máquina. Sem comprovação dinâmica de uma instalação específica.',
'Name: "{app}"; Permissions: admins-full system-full users-readexec\njson["Jwt"]!["SecretKey"] = secretKey;')]
fortes=[
(P+'Controllers/UsuariosController.cs:18','Gestão de usuários exige Administrador no servidor; Monitor recebeu 403.'),
(A+'Chamadas/Handlers/SyncPushHandler.cs:622-639','Atualização de presença autoriza pela turma real do registro persistido, não pelo TurmaId do cliente.'),
(A+'Alunos/Handlers/TransferirAlunoHandler.cs:42-57','Transferência valida vínculo com origem e destino para usuário não administrador.'),
(A+'Turmas/Handlers/MigrarTurmaHandler.cs:29-31','Migração em lote exige Administrador também no handler.'),
(A+'Alunos/Handlers/GetHistoricoPresencasAlunoQueryHandler.cs:54-89','Histórico de presenças restringe registros às turmas vinculadas.'),
(I+'Services/AuthService.cs:89-108','Senhas verificadas e armazenadas com BCrypt; não são retornadas nos DTOs de consulta.'),
(P+'Program.cs:87-101','Produção sem chave gera segredo aleatório de 64 bytes; ressalva de armazenamento em EA-08.'),
(M+'services/api.ts:105-119','Tokens são armazenados no SecureStore; o cache escolar exige segregação adicional.')]
lim=[
'Auditoria das cinco categorias solicitadas, baseada em código e reproduções locais. Não é pentest de produção, auditoria de dependências/CVEs, rede escolar, Android nativo ou OTA completo.',
'Estado inicial limpo no Git; commit f7550204891aa6f2914a9280fe26ddb2d55ef793, branch fix/revisao-sync-seguranca-dados. Nenhum arquivo de produto foi corrigido.',
'Host de reprodução registra os controllers/handlers reais e autenticação equivalente, com SQLite em memória. Omite rate limiter, workers, startup e middleware global de erros; não é teste do Program.cs completo. Erro 500 de controle negado nesse host não é o status esperado de produção.',
'Histórico: 85 commits alcançáveis por refs locais e 1.821 blobs candidatos únicos examinados por heurística; sem fetch, reflog, objetos inalcançáveis ou teste remoto de credenciais. Binários, arquivos grandes e dependências têm cobertura limitada. .env contém exemplo legado; scanner não cobre todas as atribuições sem aspas.',
'Sourcemap local: 1.473 fontes e 52 entradas fora de node_modules; sem assinaturas de credenciais no teste limitado. Artefato pode estar desatualizado em relação ao HEAD. APKs e bytecode Hermes localizados não foram decompilados, e nenhum novo bundle foi gerado.',
'GetAuditoriaAlertasQueryHandler não filtra vínculos, mas a consulta de detalhes falhou com SQLite devido à ordenação DateTimeOffset. Não foi contado como vazamento comprovado; corrigir autorização junto da compatibilidade do provider. Os testes existentes usam InMemory nesse handler.',
'RegistrarPresencaHandler não tem rota nem chamador de produto encontrado. A associação AlunoId/ChamadaId não é validada nesse caminho interno; risco latente fora da contagem de IDOR explorável.',
'ObterHistoricoTurmasAlunoHandler permite histórico completo após vínculo com qualquer turma histórica, conforme comentário explícito. A política de negócio dessa abrangência deve ser confirmada; não foi considerada falha comprovada.',
'Não há filtro global EscolaId, mas a implementação atual é single-school por banco e CloudEgressWorker é stub. Não foi inventado cenário ativo de isolamento multiescola. AGENTS.md descreve filtros multi-tenant que o código atual não implementa.',
'Alguns guards dependem de Guid.TryParse e não negam identidade malformada; o emissor legítimo só emite GUID. Sem caminho independente demonstrado para obter token inválido assinado, não há achado adicional.',
'XSS: não foi identificado fluxo de entrada controlada para execução HTML/JavaScript no código atual. A UI usa componentes React Native Text e a API retorna JSON. Menções a WebView em lockfile não provam sink. Nenhum teste em navegador/Android foi executado.',
'EA-02 e EA-08 têm comprovação estática; cache não foi executado no Android e ACL não foi testada em instalação real. Transporte HTTP local é configuração declarada; não foi reclassificado como chave hardcoded ou como XSS.'
]
# Inventário sistemático: classes IRequestHandler/INotificationHandler, incluindo as embutidas em Commands/Queries.
controles={
'LoginHandler':('POST /auth/login','email/senha; sem ID de objeto','BCrypt + usuário ativo; EA-05/EA-06'),
'GetAlunosComFaltasHandler':('GET /dashboard/alunos-com-faltas','TurmaId opcional query','Somente autenticação; EA-01'),
'CriarAlunoHandler':('POST /alunos','TurmaId body','Somente existência da turma; EA-03'),
'AtualizarAlunoHandler':('PUT /alunos/{id}','Id path substitui body','Administrador ou vínculo atual; papel inconsistente EA-03'),
'GetAlunosPorTurmaQueryHandler':('GET /alunos/turma/{turmaId}','TurmaId path','Administrador ou vínculo; bloqueio confirmado'),
'GetHistoricoPresencasAlunoQueryHandler':('GET /alunos/{id}/historico-presencas','GUID/ID externo path','Resolve SyncLog; vínculo atual e filtro por turma histórica'),
'ObterHistoricoTurmasAlunoHandler':('GET /alunos/{id}/historico-turmas','GUID/ID externo path','Qualquer vínculo histórico; política ampla documentada, limitação'),
'TransferirAlunoHandler':('POST /alunos/{id}/transferir','AlunoId path, NovaTurmaId body','Administrador ou vínculo origem E destino'),
'CriarTurmaHandler':('POST /turmas','Sem ID alvo; dados body','Sem papel administrativo; EA-03'),
'AtualizarTurmaHandler':('PUT /turmas/{id}','Id path substitui body','Administrador ou vínculo; papel inconsistente EA-03'),
'GetTurmasQueryHandler':('GET /turmas','Sem ID','Sem filtro de vínculo; EA-01'),
'MigrarTurmaHandler':('POST /turmas/{id}/migrar','Origem path, destino/AlunosIds body','Administrador em rota e handler; alunos filtrados por origem'),
'RelatorioTurmaHandler':('GET /turmas/{id}/relatorio','GUID/externo path; período query','Supervisão/Admin na rota; vínculo no handler; dados por turma'),
'GetAlertasHandler':('GET /alertas','Sem ID; filtros query','Administrador ou turmas vinculadas no banco'),
'GetAuditoriaAlertasQueryHandler':('GET /alertas/auditoria','Sem ID; nome/datas query','Papéis na rota; sem filtro de vínculo; SQLite bloqueia detalhes, limitação'),
'ResolverAlertaHandler':('PATCH /alertas/{id}/resolver','AlertaId path','Supervisão/Admin; sem vínculo por objeto EA-04'),
'GetTurmasFrequenciaPerfeitaQueryHandler':('GET /dashboard/turmas-frequencia-perfeita','Datas query','Filtra chamadas por turmas vinculadas'),
'RealizarChamadaHandler':('POST /chamadas/realizar','TurmaId, Alunos[].AlunoId, ResponsavelId body','Vínculo por turma; aluno da turma/registro salvo; responsável JWT'),
'ObterChamadaPorTurmaEDiaHandler':('GET /chamadas/turma/{turmaId}/dia/{data}','GUID/externo e data path','Resolve ID e valida vínculo antes da leitura'),
'RegistrarPresencaHandler':('Interno; sem rota encontrada','ChamadaId e AlunoId command','Vínculo da chamada; associação aluno/chamada não verificada, limitação'),
'SyncPullHandler':('GET /sync/pull','Timestamp query','Somente autenticação; EA-01'),
'SyncPushHandler':('POST /sync/push','IDs de turma, aluno e presença no body, GUID/externo','Inspeção integral das 1.176 linhas; admin para cadastros, vínculo real para presenças, prazo de edição; deleted ignorados'),
'CriarUsuarioHandler':('POST /usuarios','Papel e dados body','Administrador na rota; senha aleatória; EA-05'),
'AtualizarUsuarioHandler':('PUT /usuarios/{id}','Id path; papel body','Administrador na rota; sessão antiga não invalidada EA-06'),
'AlternarStatusUsuarioHandler':('PATCH /usuarios/{id}/status','Id path','Administrador na rota; soft delete; sessão antiga EA-06'),
'GetUsuarioByIdQueryHandler':('GET /usuarios/{id}','Id path','Administrador na rota; DTO sem senha'),
'GetUsuariosQueryHandler':('GET /usuarios','Filtros query','Administrador na rota; DTO sem senha'),
'LimiteFaltasAtingidoHandler':('Evento interno','AlunoId/TurmaId gerados no domínio','Sem rota; publicado por AppDbContext após gravação'),
'FaltasConsecutivasNormalizadasHandler':('Evento interno','AlunoId gerado no domínio','Sem rota; publicado por AppDbContext após gravação')}
rows=[]
for p in (R/'src/EscolaAtenta.Application').rglob('*.cs'):
 if 'obj' in p.parts or 'bin' in p.parts: continue
 text=p.read_text(encoding='utf-8-sig')
 if not re.search(r'I(?:Request|Notification)Handler',text): continue
 for m in re.finditer(r'public class (\w+)[\s\S]*?:\s*I(?:Request|Notification)Handler',text):
  nome=m.group(1); op,ids,ctrl=controles[nome]; rows.append({'handler':nome,'arquivo':p.relative_to(R).as_posix(),'linha':text[:m.start()].count('\n')+1,'operacao':op,'origem_ids':ids,'controle_resultado':ctrl,'cobertura':'fluxo integral revisado'})
rows.extend([
 {'handler':'AuthController.TrocarSenha','arquivo':P+'Controllers/AuthController.cs','linha':94,'operacao':'PUT /auth/trocar-senha','origem_ids':'Identidade JWT; senha body','controle_resultado':'Consulta usuário ativo pelo sub; EA-06','cobertura':'integral'},
 {'handler':'AuthController.Refresh','arquivo':P+'Controllers/AuthController.cs','linha':121,'operacao':'POST /auth/refresh','origem_ids':'RefreshToken body','controle_resultado':'Token válido e usuário ativo; rotação sequencial; EA-05/EA-06','cobertura':'integral'},
 {'handler':'Health/OpenAPI','arquivo':P+'Program.cs','linha':342,'operacao':'GET /health; /health/ready; /openapi/{documentName}.json','origem_ids':'Sem IDs de objetos escolares','controle_resultado':'Health público; OpenAPI só Development; sem escrita','cobertura':'integral'},
 {'handler':'Workers','arquivo':P+'Workers/CloudEgressWorker.cs','linha':31,'operacao':'Background: egress, heartbeat, cleanup','origem_ids':'Configuração local; nenhum ID HTTP','controle_resultado':'Egress stub, cleanup desativado; heartbeat usa endpoint configurado','cobertura':'integral nas cinco categorias'}])
rotas=[]
for p in (R/'src/EscolaAtenta.API/Controllers').glob('*.cs'):
 for n,line in enumerate(p.read_text(encoding='utf-8-sig').splitlines(),1):
  if '[Http' in line: rotas.append({'arquivo':p.relative_to(R).as_posix(),'linha':n,'atributo':line.strip()})
d={'projeto':'Escola Atenta','data':'08/09/2026','commit':'f7550204891aa6f2914a9280fe26ddb2d55ef793','branch':'fix/revisao-sync-seguranca-dados','achados':fs,'pontos_fortes':fortes,'limitacoes':lim,'handlers':rows,'rotas':rotas,'testes':'dotnet test: 63 domínio + 204 aplicação = 267 aprovados; 0 falhas. 15 cenários/controles do projeto isolado confirmados. Avisos existentes CS8604 e CS0618.','categorias':[{'id':1,'nome':'Isolamento de dados','status':'Revisada com achados; cache validado estaticamente'},{'id':2,'nome':'Permissões no servidor','status':'Revisada com achados'},{'id':3,'nome':'IDOR','status':'Revisada com achados; todos os handlers inventariados'},{'id':4,'nome':'Segredos expostos','status':'Parcial: histórico e fonte; APK/bytecode/ACL real não validados'},{'id':5,'nome':'XSS','status':'Revisada sem achados no código atual; sem teste dinâmico'}]}
(O/'dados.json').write_text(json.dumps(d,ensure_ascii=False,indent=2),encoding='utf-8')
lines=['# Cobertura da auditoria','',d['testes'],'',f'Revisão: {d["commit"]}. Estado inicial limpo.','',f'{len(rotas)} ações HTTP; {len(controles)} classes de handler; operações diretas e workers adicionais abaixo.','', '| Handler/operação | Arquivo:linha | IDs | Controle efetivo / resultado |','|---|---|---|---|']
for x in rows: lines.append(f'| {x["handler"]}: {x["operacao"]} | {x["arquivo"]}:{x["linha"]} | {x["origem_ids"]} | {x["controle_resultado"]} |')
lines += ['','## Pontos fortes']+[f'- {p}: {t}' for p,t in fortes]+['','## Limitações']+[f'- {s}' for s in lim]
lines += ['','## Cobertura por arquivo','', 'Inventário completo (hash e número de linhas): inventario.json. Fonte própria: buscas de segredos/XSS em 705 arquivos textuais; revisão de fluxo em todos os handlers, controllers, controles centrais, sessão/sync mobile e scripts de deploy. Arquivos apenas inventariados não equivalem a revisão manual integral.','', '## Rotas HTTP']+[f'- {x["arquivo"]}:{x["linha"]} {x["atributo"]}' for x in rotas]
(O/'cobertura.md').write_text('\n'.join(lines),encoding='utf-8')
issues=[]
for n,x in enumerate(fs,1):
 evid='\n'.join(f'- Arquivo: {z["arquivo"]}:{z["inicio"]}-{z["fim"]}' for z in x['locais'])
 issue=f'--- ISSUE {n} ---\n# [Segurança] {x["titulo"]}\n\nLabels sugeridas: security, {x["severidade"]}\n\n## Descrição\n{x["id"]}: {x["descricao"]}\nPré-condições: {x["condicoes"]}\n\n## Evidência\n{evid}\n- Revisão: {d["commit"]}\n\n```text\n{x["trecho"]}\n```\n\nVerificação: {x["verificacao"]}\n\n## Impacto\n{x["impacto"]}\n\n## Sugestão de correção\n{x["correcao"]}\n\n## Critérios de aceite\n'+ '\n'.join('- [ ] '+a for a in x['aceite'])+f'\n\n--- FIM ISSUE {n} ---'
 issues.append(issue)
(O/'issues.md').write_text('\n\n'.join(issues),encoding='utf-8')
print(f'{len(fs)} achados; {len(rows)} entradas de cobertura; {len(rotas)} ações HTTP; {len(controles)} handlers.')
