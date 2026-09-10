# Cobertura da auditoria

dotnet test: 63 domínio + 204 aplicação = 267 aprovados; 0 falhas. 15 cenários/controles do projeto isolado confirmados. Avisos existentes CS8604 e CS0618.

Revisão: f7550204891aa6f2914a9280fe26ddb2d55ef793. Estado inicial limpo.

28 ações HTTP; 29 classes de handler; operações diretas e workers adicionais abaixo.

| Handler/operação | Arquivo:linha | IDs | Controle efetivo / resultado |
|---|---|---|---|
| LoginHandler: POST /auth/login | src/EscolaAtenta.Application/Auth/LoginHandler.cs:24 | email/senha; sem ID de objeto | BCrypt + usuário ativo; EA-05/EA-06 |
| FaltasConsecutivasNormalizadasHandler: Evento interno | src/EscolaAtenta.Application/EventHandlers/FaltasConsecutivasNormalizadasHandler.cs:18 | AlunoId gerado no domínio | Sem rota; publicado por AppDbContext após gravação |
| LimiteFaltasAtingidoHandler: Evento interno | src/EscolaAtenta.Application/EventHandlers/LimiteFaltasAtingidoHandler.cs:30 | AlunoId/TurmaId gerados no domínio | Sem rota; publicado por AppDbContext após gravação |
| GetAlertasHandler: GET /alertas | src/EscolaAtenta.Application/Alertas/Handlers/GetAlertasHandler.cs:32 | Sem ID; filtros query | Administrador ou turmas vinculadas no banco |
| GetAuditoriaAlertasQueryHandler: GET /alertas/auditoria | src/EscolaAtenta.Application/Alertas/Handlers/GetAuditoriaAlertasQueryHandler.cs:26 | Sem ID; nome/datas query | Papéis na rota; sem filtro de vínculo; SQLite bloqueia detalhes, limitação |
| ResolverAlertaHandler: PATCH /alertas/{id}/resolver | src/EscolaAtenta.Application/Alertas/Handlers/ResolverAlertaHandler.cs:9 | AlertaId path | Supervisão/Admin; sem vínculo por objeto EA-04 |
| AtualizarAlunoHandler: PUT /alunos/{id} | src/EscolaAtenta.Application/Alunos/Handlers/AtualizarAlunoHandler.cs:11 | Id path substitui body | Administrador ou vínculo atual; papel inconsistente EA-03 |
| CriarAlunoHandler: POST /alunos | src/EscolaAtenta.Application/Alunos/Handlers/CriarAlunoHandler.cs:10 | TurmaId body | Somente existência da turma; EA-03 |
| GetAlunosPorTurmaQueryHandler: GET /alunos/turma/{turmaId} | src/EscolaAtenta.Application/Alunos/Handlers/GetAlunosPorTurmaQueryHandler.cs:12 | TurmaId path | Administrador ou vínculo; bloqueio confirmado |
| GetHistoricoPresencasAlunoQueryHandler: GET /alunos/{id}/historico-presencas | src/EscolaAtenta.Application/Alunos/Handlers/GetHistoricoPresencasAlunoQueryHandler.cs:13 | GUID/ID externo path | Resolve SyncLog; vínculo atual e filtro por turma histórica |
| ObterHistoricoTurmasAlunoHandler: GET /alunos/{id}/historico-turmas | src/EscolaAtenta.Application/Alunos/Handlers/ObterHistoricoTurmasAlunoHandler.cs:12 | GUID/ID externo path | Qualquer vínculo histórico; política ampla documentada, limitação |
| TransferirAlunoHandler: POST /alunos/{id}/transferir | src/EscolaAtenta.Application/Alunos/Handlers/TransferirAlunoHandler.cs:13 | AlunoId path, NovaTurmaId body | Administrador ou vínculo origem E destino |
| GetAlunosComFaltasHandler: GET /dashboard/alunos-com-faltas | src/EscolaAtenta.Application/Alunos/Queries/GetAlunosComFaltasQuery.cs:34 | TurmaId opcional query | Somente autenticação; EA-01 |
| ObterChamadaPorTurmaEDiaHandler: GET /chamadas/turma/{turmaId}/dia/{data} | src/EscolaAtenta.Application/Chamadas/Handlers/ObterChamadaPorTurmaEDiaHandler.cs:12 | GUID/externo e data path | Resolve ID e valida vínculo antes da leitura |
| RealizarChamadaHandler: POST /chamadas/realizar | src/EscolaAtenta.Application/Chamadas/Handlers/RealizarChamadaHandler.cs:13 | TurmaId, Alunos[].AlunoId, ResponsavelId body | Vínculo por turma; aluno da turma/registro salvo; responsável JWT |
| RegistrarPresencaHandler: Interno; sem rota encontrada | src/EscolaAtenta.Application/Chamadas/Handlers/RegistrarPresencaHandler.cs:29 | ChamadaId e AlunoId command | Vínculo da chamada; associação aluno/chamada não verificada, limitação |
| SyncPullHandler: GET /sync/pull | src/EscolaAtenta.Application/Chamadas/Handlers/SyncPullHandler.cs:20 | Timestamp query | Somente autenticação; EA-01 |
| SyncPushHandler: POST /sync/push | src/EscolaAtenta.Application/Chamadas/Handlers/SyncPushHandler.cs:32 | IDs de turma, aluno e presença no body, GUID/externo | Inspeção integral das 1.176 linhas; admin para cadastros, vínculo real para presenças, prazo de edição; deleted ignorados |
| GetTurmasFrequenciaPerfeitaQueryHandler: GET /dashboard/turmas-frequencia-perfeita | src/EscolaAtenta.Application/Dashboard/Handlers/GetTurmasFrequenciaPerfeitaQueryHandler.cs:17 | Datas query | Filtra chamadas por turmas vinculadas |
| AtualizarTurmaHandler: PUT /turmas/{id} | src/EscolaAtenta.Application/Turmas/Handlers/AtualizarTurmaHandler.cs:11 | Id path substitui body | Administrador ou vínculo; papel inconsistente EA-03 |
| CriarTurmaHandler: POST /turmas | src/EscolaAtenta.Application/Turmas/Handlers/CriarTurmaHandler.cs:9 | Sem ID alvo; dados body | Sem papel administrativo; EA-03 |
| GetTurmasQueryHandler: GET /turmas | src/EscolaAtenta.Application/Turmas/Handlers/GetTurmasQueryHandler.cs:9 | Sem ID | Sem filtro de vínculo; EA-01 |
| MigrarTurmaHandler: POST /turmas/{id}/migrar | src/EscolaAtenta.Application/Turmas/Handlers/MigrarTurmaHandler.cs:14 | Origem path, destino/AlunosIds body | Administrador em rota e handler; alunos filtrados por origem |
| RelatorioTurmaHandler: GET /turmas/{id}/relatorio | src/EscolaAtenta.Application/Turmas/Handlers/RelatorioTurmaHandler.cs:13 | GUID/externo path; período query | Supervisão/Admin na rota; vínculo no handler; dados por turma |
| AlternarStatusUsuarioHandler: PATCH /usuarios/{id}/status | src/EscolaAtenta.Application/Usuarios/Commands/AlternarStatusUsuarioCommand.cs:10 | Id path | Administrador na rota; soft delete; sessão antiga EA-06 |
| AtualizarUsuarioHandler: PUT /usuarios/{id} | src/EscolaAtenta.Application/Usuarios/Commands/AtualizarUsuarioCommand.cs:10 | Id path; papel body | Administrador na rota; sessão antiga não invalidada EA-06 |
| CriarUsuarioHandler: POST /usuarios | src/EscolaAtenta.Application/Usuarios/Commands/CriarUsuarioCommand.cs:15 | Papel e dados body | Administrador na rota; senha aleatória; EA-05 |
| GetUsuarioByIdQueryHandler: GET /usuarios/{id} | src/EscolaAtenta.Application/Usuarios/Queries/GetUsuarioByIdQuery.cs:10 | Id path | Administrador na rota; DTO sem senha |
| GetUsuariosQueryHandler: GET /usuarios | src/EscolaAtenta.Application/Usuarios/Queries/GetUsuariosQueryHandler.cs:22 | Filtros query | Administrador na rota; DTO sem senha |
| AuthController.TrocarSenha: PUT /auth/trocar-senha | src/EscolaAtenta.API/Controllers/AuthController.cs:94 | Identidade JWT; senha body | Consulta usuário ativo pelo sub; EA-06 |
| AuthController.Refresh: POST /auth/refresh | src/EscolaAtenta.API/Controllers/AuthController.cs:121 | RefreshToken body | Token válido e usuário ativo; rotação sequencial; EA-05/EA-06 |
| Health/OpenAPI: GET /health; /health/ready; /openapi/{documentName}.json | src/EscolaAtenta.API/Program.cs:342 | Sem IDs de objetos escolares | Health público; OpenAPI só Development; sem escrita |
| Workers: Background: egress, heartbeat, cleanup | src/EscolaAtenta.API/Workers/CloudEgressWorker.cs:31 | Configuração local; nenhum ID HTTP | Egress stub, cleanup desativado; heartbeat usa endpoint configurado |

## Pontos fortes
- src/EscolaAtenta.API/Controllers/UsuariosController.cs:18: Gestão de usuários exige Administrador no servidor; Monitor recebeu 403.
- src/EscolaAtenta.Application/Chamadas/Handlers/SyncPushHandler.cs:622-639: Atualização de presença autoriza pela turma real do registro persistido, não pelo TurmaId do cliente.
- src/EscolaAtenta.Application/Alunos/Handlers/TransferirAlunoHandler.cs:42-57: Transferência valida vínculo com origem e destino para usuário não administrador.
- src/EscolaAtenta.Application/Turmas/Handlers/MigrarTurmaHandler.cs:29-31: Migração em lote exige Administrador também no handler.
- src/EscolaAtenta.Application/Alunos/Handlers/GetHistoricoPresencasAlunoQueryHandler.cs:54-89: Histórico de presenças restringe registros às turmas vinculadas.
- src/EscolaAtenta.Infrastructure/Services/AuthService.cs:89-108: Senhas verificadas e armazenadas com BCrypt; não são retornadas nos DTOs de consulta.
- src/EscolaAtenta.API/Program.cs:87-101: Produção sem chave gera segredo aleatório de 64 bytes; ressalva de armazenamento em EA-08.
- src/EscolaAtenta.App/src/services/api.ts:105-119: Tokens são armazenados no SecureStore; o cache escolar exige segregação adicional.

## Limitações
- Auditoria das cinco categorias solicitadas, baseada em código e reproduções locais. Não é pentest de produção, auditoria de dependências/CVEs, rede escolar, Android nativo ou OTA completo.
- Estado inicial limpo no Git; commit f7550204891aa6f2914a9280fe26ddb2d55ef793, branch fix/revisao-sync-seguranca-dados. Nenhum arquivo de produto foi corrigido.
- Host de reprodução registra os controllers/handlers reais e autenticação equivalente, com SQLite em memória. Omite rate limiter, workers, startup e middleware global de erros; não é teste do Program.cs completo. Erro 500 de controle negado nesse host não é o status esperado de produção.
- Histórico: 85 commits alcançáveis por refs locais e 1.821 blobs candidatos únicos examinados por heurística; sem fetch, reflog, objetos inalcançáveis ou teste remoto de credenciais. Binários, arquivos grandes e dependências têm cobertura limitada. .env contém exemplo legado; scanner não cobre todas as atribuições sem aspas.
- Sourcemap local: 1.473 fontes e 52 entradas fora de node_modules; sem assinaturas de credenciais no teste limitado. Artefato pode estar desatualizado em relação ao HEAD. APKs e bytecode Hermes localizados não foram decompilados, e nenhum novo bundle foi gerado.
- GetAuditoriaAlertasQueryHandler não filtra vínculos, mas a consulta de detalhes falhou com SQLite devido à ordenação DateTimeOffset. Não foi contado como vazamento comprovado; corrigir autorização junto da compatibilidade do provider. Os testes existentes usam InMemory nesse handler.
- RegistrarPresencaHandler não tem rota nem chamador de produto encontrado. A associação AlunoId/ChamadaId não é validada nesse caminho interno; risco latente fora da contagem de IDOR explorável.
- ObterHistoricoTurmasAlunoHandler permite histórico completo após vínculo com qualquer turma histórica, conforme comentário explícito. A política de negócio dessa abrangência deve ser confirmada; não foi considerada falha comprovada.
- Não há filtro global EscolaId, mas a implementação atual é single-school por banco e CloudEgressWorker é stub. Não foi inventado cenário ativo de isolamento multiescola. AGENTS.md descreve filtros multi-tenant que o código atual não implementa.
- Alguns guards dependem de Guid.TryParse e não negam identidade malformada; o emissor legítimo só emite GUID. Sem caminho independente demonstrado para obter token inválido assinado, não há achado adicional.
- XSS: não foi identificado fluxo de entrada controlada para execução HTML/JavaScript no código atual. A UI usa componentes React Native Text e a API retorna JSON. Menções a WebView em lockfile não provam sink. Nenhum teste em navegador/Android foi executado.
- EA-02 e EA-08 têm comprovação estática; cache não foi executado no Android e ACL não foi testada em instalação real. Transporte HTTP local é configuração declarada; não foi reclassificado como chave hardcoded ou como XSS.

## Cobertura por arquivo

Inventário completo (hash e número de linhas): inventario.json. Fonte própria: buscas de segredos/XSS em 705 arquivos textuais; revisão de fluxo em todos os handlers, controllers, controles centrais, sessão/sync mobile e scripts de deploy. Arquivos apenas inventariados não equivalem a revisão manual integral.

## Rotas HTTP
- src/EscolaAtenta.API/Controllers/AlertasController.cs:43 [HttpGet]
- src/EscolaAtenta.API/Controllers/AlertasController.cs:75 [HttpPatch("{id}/resolver")]
- src/EscolaAtenta.API/Controllers/AlertasController.cs:98 [HttpGet("auditoria")]
- src/EscolaAtenta.API/Controllers/AlunosController.cs:27 [HttpPost]
- src/EscolaAtenta.API/Controllers/AlunosController.cs:39 [HttpGet("turma/{turmaId:guid}")]
- src/EscolaAtenta.API/Controllers/AlunosController.cs:50 [HttpPut("{id:guid}")]
- src/EscolaAtenta.API/Controllers/AlunosController.cs:65 [HttpGet("{id}/historico-presencas")]
- src/EscolaAtenta.API/Controllers/AlunosController.cs:79 [HttpPost("{id:guid}/transferir")]
- src/EscolaAtenta.API/Controllers/AlunosController.cs:99 [HttpGet("{id}/historico-turmas")]
- src/EscolaAtenta.API/Controllers/AuthController.cs:47 [HttpPost("login")]
- src/EscolaAtenta.API/Controllers/AuthController.cs:89 [HttpPut("trocar-senha")]
- src/EscolaAtenta.API/Controllers/AuthController.cs:117 [HttpPost("refresh")]
- src/EscolaAtenta.API/Controllers/ChamadasController.cs:28 [HttpPost("realizar")]
- src/EscolaAtenta.API/Controllers/ChamadasController.cs:43 [HttpGet("turma/{turmaId}/dia/{data}")]
- src/EscolaAtenta.API/Controllers/DashboardController.cs:31 [HttpGet("alunos-com-faltas")]
- src/EscolaAtenta.API/Controllers/DashboardController.cs:46 [HttpGet("turmas-frequencia-perfeita")]
- src/EscolaAtenta.API/Controllers/SyncController.cs:33 [HttpGet("pull")]
- src/EscolaAtenta.API/Controllers/SyncController.cs:46 [HttpPost("push")]
- src/EscolaAtenta.API/Controllers/TurmasController.cs:30 [HttpPost]
- src/EscolaAtenta.API/Controllers/TurmasController.cs:42 [HttpGet]
- src/EscolaAtenta.API/Controllers/TurmasController.cs:53 [HttpPut("{id}")]
- src/EscolaAtenta.API/Controllers/TurmasController.cs:68 [HttpPost("{id:guid}/migrar")]
- src/EscolaAtenta.API/Controllers/TurmasController.cs:93 [HttpGet("{id}/relatorio")]
- src/EscolaAtenta.API/Controllers/UsuariosController.cs:39 [HttpGet]
- src/EscolaAtenta.API/Controllers/UsuariosController.cs:62 [HttpGet("{id:guid}")]
- src/EscolaAtenta.API/Controllers/UsuariosController.cs:79 [HttpPost]
- src/EscolaAtenta.API/Controllers/UsuariosController.cs:108 [HttpPut("{id:guid}")]
- src/EscolaAtenta.API/Controllers/UsuariosController.cs:137 [HttpPatch("{id:guid}/status")]