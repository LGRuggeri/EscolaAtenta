# AGENTS.md — Escola Atenta

> Arquivo de referência para agentes de codificação. Leia este documento antes de modificar qualquer parte do projeto.

## 1. Visão Geral

O **Escola Atenta** é um sistema de monitoramento de frequência escolar e prevenção de evasão, projetado para funcionar em escolas com conectividade limitada ou inexistente (arquitetura **edge-first**).

**Funcionalidades principais:**

- Chamada digital via app mobile (Android) com feedback háptico.
- Detecção automática de padrões de faltas e alertas de evasão.
- Níveis de alerta: Aviso, Intermediário, Vermelho e Preto (crítico).
- Auditoria de alertas com responsável e justificativa.
- Gestão de turmas, alunos e usuários com papéis (Monitor, Supervisão, Administrador).
- Relatórios de presença por aluno, turma e intervalo de datas.
- Quadro de Honra para turmas com 100% de frequência.
- Sincronização offline-first: WatermelonDB no mobile + sync bidirecional com servidor.
- Histórico de turmas do aluno: registra mudanças de série, turno ou período preservando vínculos anteriores.
- Migração individual e em lote de alunos entre turmas.
- Relatório de frequência por turma e período de datas informado pelo usuário.

**Idioma do projeto:** português. Código, comentários, documentação e mensagens de commit usam português como idioma principal, com termos técnicos em inglês quando apropriado (nomes de classes, métodos, pacotes NuGet/NPM, etc.).

## 2. Stack Tecnológico

### Backend (.NET)

| Tecnologia | Versão | Uso |
|---|---|---|
| .NET / ASP.NET Core | 9.0 | API REST e Windows Service |
| Entity Framework Core | 9.x | ORM + Migrations |
| SQLite | WAL mode | Banco embarcado local na escola |
| MediatR | 12.x | CQRS (Commands/Queries/Handlers) e Domain Events |
| Serilog | 8.x | Logging estruturado (console + arquivo rotativo) |
| JWT Bearer + Refresh Tokens | — | Autenticação stateless |
| BCrypt.Net-Next | 4.0.3 | Hash de senhas |
| xUnit + FluentAssertions | — | Testes unitários e de integração leve |
| Inno Setup | — | Instalador Windows para deploy nas escolas |

### Frontend Mobile (React Native / Expo)

| Tecnologia | Versão | Uso |
|---|---|---|
| React Native | 0.81.5 | Framework mobile |
| Expo SDK | 54.0.0 | Build, OTA e desenvolvimento |
| React Native Paper | 5.15 | UI Material Design 3 |
| WatermelonDB | 0.28 | Banco local offline-first |
| React Navigation | 7.x | Navegação entre telas |
| Axios | 1.x | Cliente HTTP |
| Expo Haptics | 15.x | Feedback tátil |
| Expo Linear Gradient | 15.x | Gradientes visuais |
| Expo Secure Store | 15.x | Armazenamento seguro de tokens |

### Outros componentes

- **EscolaAtenta.TrayMonitor**: aplicativo Windows Forms na bandeja do sistema que monitora saúde da API, reinicia o serviço e aplica atualizações OTA.
- **HeartbeatWorker**: envia sinais de vida para uma API central na nuvem (desativado por padrão se `EndpointNuvem` estiver vazio).
- **CloudEgressWorker**: worker em standby para egress de dados alterados para a nuvem (desativado por padrão).

## 3. Arquitetura e Organização do Código

### Clean Architecture + DDD (Backend)

A dependência de projetos segue o fluxo inward:

```
EscolaAtenta.Domain          (zero deps externas)
  └─ EscolaAtenta.Application (depende de Domain + Infrastructure)
       └─ EscolaAtenta.Infrastructure (depende apenas de Domain)
            └─ EscolaAtenta.API (depende de Application + Infrastructure)
```

> **Nota importante:** A camada `Application` referencia `Infrastructure` de forma consciente, para que os handlers possam usar o `AppDbContext` diretamente. Isso evita boilerplate de repositórios abstratos nesta fase. A referência circular `Infrastructure -> Application` foi eliminada.

### Estrutura de pastas

```
EscolaAtenta/
├── src/
│   ├── EscolaAtenta.Domain/              # Entidades, enums, value objects, eventos, interfaces, exceções
│   ├── EscolaAtenta.Application/           # Commands, Queries, Handlers, DTOs, EventHandlers
│   ├── EscolaAtenta.Infrastructure/      # EF Core, Migrations, DbContext, Auth, Workers, Serviços de infra
│   ├── EscolaAtenta.API/                 # Controllers, Middleware, DI, Program.cs, Workers
│   ├── EscolaAtenta.App/                 # App mobile React Native (Expo)
│   └── EscolaAtenta.TrayMonitor/         # Monitor de bandeja do Windows
├── Tests/
│   ├── EscolaAtenta.Domain.Tests/        # Testes de entidades de domínio
│   └── EscolaAtenta.Application.Tests/   # Testes de handlers CQRS (com SQLite in-memory)
├── EscolaAtenta.sln
├── build-release.ps1                     # Build e empacotamento de release (API + TrayMonitor)
├── escolaatenta-installer.iss            # Script Inno Setup para criar o instalador
├── version.json                          # Metadados de release OTA
└── INSTALACAO.txt                        # Guia de instalação para escolas
```

### Backend — padrões por camada

#### `Domain`

- Entidades herdam de `EntityBase` (`src/EscolaAtenta.Domain/Common/EntityBase.cs`).
- `EntityBase` fornece: `Id` (Guid), auditoria (`DataCriacao`, `DataAtualizacao`, `UsuarioCriacao`, `UsuarioAtualizacao`), multi-tenant (`EscolaId`), cloud sync (`CloudSyncedAt`) e domain events.
- Regras de negócio ficam dentro das entidades (ex: `Aluno.RegistrarPresenca`, `Aluno.RegistrarFalta`).
- Domain Events são disparados pelas entidades e despachados pelo `AppDbContext` após `SaveChangesAsync`.
- Exceções customizadas herdam de `DomainException`.
- Interfaces de serviços de infra (ex: `IAuthService`, `ICurrentUserService`, `ISqliteWriteLockProvider`, `IEscolaTenantProvider`) ficam no `Domain` para inverter dependências.

#### `Application`

- Organizado por feature: `Chamadas/`, `Turmas/`, `Alunos/`, `Usuarios/`, `Alertas/`, `Dashboard/`, `Auth/`, `EventHandlers/`, `Common/`.
- Cada feature contém `Commands`, `Queries`, `Handlers` e `Dtos` quando aplicável.
- Handlers recebem `AppDbContext` e serviços de infra via construtor.
- Domain event handlers também estão nesta camada (`EscolaAtenta.Application/EventHandlers/`).

#### `Infrastructure`

- `Data/AppDbContext.cs`: DbContext principal, aplica global query filters, auditoria, soft delete e despacho de domain events.
- `Data/Migrations/`: migrations EF Core geradas (não editar manualmente).
- `Data/Configurations/`: `IEntityTypeConfiguration<T>` para cada entidade.
- `Services/`: implementações de `IAuthService`, `ICurrentUserService`, `SqliteWriteLockProvider`, `EscolaTenantProvider`, etc.
- `DatabaseSeeder.cs`: seed do administrador inicial durante o startup.

#### `API`

- `Program.cs`: configuração de serviços, middleware, autenticação, rate limiting, CORS, health checks, Serilog, migrations automáticas e seed.
- `Controllers/`: controllers ASP.NET Core enxutos que delegam para MediatR.
- `Middleware/`: `SecurityHeadersMiddleware` e `GlobalExceptionHandler`.
- `Workers/`: `HeartbeatWorker`, `CloudEgressWorker`, `SyncLogCleanupWorker`.
- `Properties/PublishProfiles/win-x64-selfcontained.pubxml`: publicação self-contained do Windows.

### Mobile (`src/EscolaAtenta.App/`)

```
src/EscolaAtenta.App/
├── src/
│   ├── screens/            # Telas organizadas por contexto (auth, dashboard, gestao, operacao, relatorios, settings)
│   ├── components/         # Componentes reutilizáveis (ui/ e domain/)
│   ├── services/           # Chamadas HTTP, sync, API e configuração de servidor
│   ├── database/           # Schema, models e migrations do WatermelonDB
│   ├── navigation/         # AppNavigator e tipos de rotas
│   ├── hooks/              # useAuth, useSyncEngine, useInactivityLogout
│   ├── contexts/           # AuthContext
│   ├── theme/              # Tema, cores e estilos de formulário
│   └── types/              # DTOs e enums compartilhados
├── app.json                # Configuração Expo
├── eas.json                # Perfis de build EAS
├── package.json            # Dependências NPM
└── tsconfig.json           # TypeScript strict + decorators
```

- O app usa **WatermelonDB** para persistência offline-first.
- A URL do servidor é configurável via tela `ConfiguracaoServidorScreen` e salva localmente.
- O `api.ts` centraliza o Axios, incluindo interceptadores para JWT e refresh token.
- Decorators do Babel são necessários para o WatermelonDB (`@babel/plugin-proposal-decorators`).

## 4. Build e Testes

### Pré-requisitos

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- [Node.js 20+](https://nodejs.org/) (para o app mobile)
- Expo CLI / EAS CLI (para builds do app)
- Inno Setup Compiler (`iscc.exe`) para gerar o instalador Windows

### Backend

```bash
# Restaurar dependências
dotnet restore

# Build completo
dotnet build

# Aplicar migrations (cria/gerencia o banco SQLite automaticamente)
dotnet ef database update --project src/EscolaAtenta.Infrastructure --startup-project src/EscolaAtenta.API

# Executar a API em desenvolvimento
dotnet run --project src/EscolaAtenta.API
# Acesse: http://localhost:5114/health
```

### Testes

```bash
# Todos os testes
dotnet test

# Apenas domínio
dotnet test Tests/EscolaAtenta.Domain.Tests

# Apenas aplicação
dotnet test Tests/EscolaAtenta.Application.Tests
```

- Testes de domínio usam xUnit + FluentAssertions, sem dependência de infraestrutura.
- Testes de aplicação usam SQLite in-memory (`:memory:`) com `EnsureCreated()` e fakes para serviços (`Tests/EscolaAtenta.Application.Tests/Fakes/`).

### Publicação self-contained (Release)

```powershell
# Publicar API
dotnet publish src/EscolaAtenta.API/EscolaAtenta.API.csproj -p:PublishProfile=win-x64-selfcontained

# Publicar TrayMonitor
dotnet publish src/EscolaAtenta.TrayMonitor/EscolaAtenta.TrayMonitor.csproj -p:PublishProfile=win-x64-selfcontained

# Ou build completo + update.zip + version.json (executar como Administrador não é necessário para build)
.\build-release.ps1 -Version "1.2.3" -DownloadUrl "https://seu-cdn/releases/update.zip"

# Gerar instalador Windows (requer Inno Setup)
iscc.exe escolaatenta-installer.iss
```

### Mobile

```bash
cd src/EscolaAtenta.App

# Instalar dependências
npm install --legacy-peer-deps

# Iniciar Expo (modo desenvolvimento)
npm start

# Build Android local (APK)
cd android && ./gradlew assembleRelease

# Build via EAS
npx eas build --profile production
```

> **Atenção:** O build do Android pode gerar arquivos grandes em `android/app/build/` e `node_modules/`. Essas pastas estão no `.gitignore`.

## 5. Configuração

### Backend (`src/EscolaAtenta.API/appsettings.json`)

Seções relevantes:

- `Urls`: endpoint de escuta (`http://0.0.0.0:5114`).
- `ConnectionStrings.DefaultConnection`: caminho do SQLite (`escolaatenta_local.db`, resolvido para `AppContext.BaseDirectory` em runtime).
- `Serilog`: configuração de logs estruturados.
- `RegrasNegocio`: limiares de alertas.
- `Jwt`: `SecretKey`, `Issuer`, `Audience`. Em produção local, se `SecretKey` estiver vazio, a aplicação gera uma chave aleatória e salva no `appsettings.json`.
- `Heartbeat`: envio de heartbeat para nuvem (desativado se `EndpointNuvem` vazio).
- `CloudSync`: egress para nuvem (desativado por padrão).
- `EscolaContext.Id`: identificador do tenant local.

### Mobile

- O endereço do servidor é configurado pelo usuário na tela `ConfiguracaoServidorScreen`.
- O padrão de porta é `5114` e o protocolo é `http://` (a rede local da escola geralmente não usa HTTPS).
- `app.json` habilita `usesCleartextTraffic` para Android.

## 6. Convenções de Código

### C# (.NET)

- `ImplicitUsings` e `Nullable` habilitados.
- Nomes de classes, métodos e propriedades em português quando representam conceitos de negócio (ex: `RealizarChamadaHandler`, `RegistrarPresenca`, `EscolaId`).
- Nomes de interfaces, serviços de infra e termos técnicos podem ser em inglês (ex: `IAuthService`, `ICurrentUserService`, `AppDbContext`).
- Comentários e documentação XML em **português**.
- Guids gerados no domínio (client-side), não identity do banco.
- Auditoria, multi-tenant e soft delete são gerenciados centralmente pelo `AppDbContext` — não replicar manualmente em handlers.
- Domain Events devem ser disparados apenas por métodos de negócio dentro das entidades.

### TypeScript / React Native

- TypeScript em modo `strict`.
- `experimentalDecorators` habilitado para WatermelonDB.
- Componentes e hooks nomeados em português ou inglês conforme o contexto existente (manter consistência com os arquivos ao redor).
- Importações relativas com `../` são comuns; manter o padrão do projeto.
- Comentários e strings visíveis ao usuário em português.

## 7. Testes e Qualidade

- Execute `dotnet test` antes de considerar uma alteração finalizada.
- Ao adicionar um novo handler CQRS, adicionar testes correspondentes em `Tests/EscolaAtenta.Application.Tests/Handlers/`.
- Ao adicionar regras de negócio em entidades, adicionar testes em `Tests/EscolaAtenta.Domain.Tests/Entities/`.
- Use fakes (não mocks excessivos) para serviços de infra: `FakeAuthService`, `FakeCurrentUserService`, `FakeMediator`, `FakeTenantProvider`, `FakeSqliteWriteLockProvider`.
- Testes de aplicação usam `SqliteConnection` com `:memory:` e `EnsureCreated()`; lembre-se de chamar `ctx.ChangeTracker.Clear()` entre passos para evitar tracking indesejado.

## 8. Segurança

- **Autenticação JWT** com refresh token rotation. Tokens antigos são revogados ao renovar.
- **Hash de senhas** com BCrypt (`BCrypt.Net-Next`).
- **Rate limiting**:
  - `GlobalPolicy`: Token Bucket, 100 tokens / 50 por minuto por IP.
  - `AuthPolicy`: Fixed Window, 5 req/min por **email** (mitiga NAT em redes escolares).
- **Proteção contra IDOR**: controllers e handlers validam permissões de turma/usuário (ex: apenas Administrador pode operar fora de suas turmas vinculadas).
- **Multi-tenant por `EscolaId`**: global query filters aplicam o tenant automaticamente.
- **Soft delete**: entidades `ISoftDeletable` não são removidas fisicamente; hard delete é proibido para entidades sincronizáveis com a nuvem.
- **Troca de senha obrigatória** no primeiro acesso do administrador seedado.
- **Headers de segurança** (`SecurityHeadersMiddleware`): `X-Content-Type-Options`, `X-Frame-Options`, `X-XSS-Protection`, `Referrer-Policy`, `Permissions-Policy`, remoção de `X-Powered-By` e `Server`.
- **Senha do administrador inicial** é gerada automaticamente e exibida **apenas nos logs** no primeiro startup. Ver `INSTALACAO.txt` para instruções.
- **Nunca commitar secrets**: `appsettings.Production.json`, `Jwt:SecretKey` real ou credenciais de nuvem devem ser mantidas fora do repositório (user-secrets, `.env` ou configuração no ambiente).

## 9. Deploy e Release

### Fluxo de release (Windows)

1. Ajustar `version.json` e executar `build-release.ps1`:
   ```powershell
   .\build-release.ps1 -Version "1.2.3" -DownloadUrl "https://seu-cdn/releases/update.zip"
   ```
2. O script gera `update.zip` (API + TrayMonitor) e `version.json`.
3. Fazer upload de `update.zip` para o CDN configurado.
4. Gerar o instalador:
   ```powershell
   iscc.exe escolaatenta-installer.iss
   ```
5. O instalador cria `C:\EscolaAtenta\`, registra o serviço Windows "EscolaAtenta" e adiciona o TrayMonitor na inicialização.

### Atualizações OTA

- O `TrayMonitor` faz polling de `UpdateCheckUrl` (configurado em `appsettings.json` do TrayMonitor) a cada 4 horas.
- Quando detecta uma versão superior em `version.json`, exibe notificação e permite instalar o update automaticamente.
- O processo de update copia o próprio executável para `%TEMP%` e se eleva via UAC para substituir os binários em `C:\EscolaAtenta\`.

### Instalação em escolas

Consulte `INSTALACAO.txt` para o passo a passo completo, incluindo:

- Requisitos de hardware e rede.
- Liberação da porta TCP 5114 no firewall.
- Descoberta do IP do servidor.
- Instalação do APK no celular.
- Configuração inicial de usuários, turmas e alunos.
- Backup e manutenção.

## 10. Pontos de Atenção para Agentes

- **Migrations:** nunca editar arquivos em `src/EscolaAtenta.Infrastructure/Data/Migrations/` manualmente. Use `dotnet ef migrations add` e `dotnet ef database update`.
- **SQLite em produção:** o banco é criado automaticamente ao lado do executável. Em modo Windows Service, o diretório de trabalho padrão é `C:\Windows\System32`; o código resolve o caminho absoluto via `AppContext.BaseDirectory` em `Program.cs`.
- **Soft delete:** se precisar excluir um registro, verifique se a entidade implementa `ISoftDeletable`. Não implemente hard delete para entidades sincronizáveis.
- **Auditoria:** campos `DataCriacao`, `UsuarioCriacao`, etc., são preenchidos automaticamente. Não defini-los manualmente em handlers.
- **Multi-tenant:** `EscolaId` é preenchido automaticamente no `SaveChangesAsync` pelo `EscolaTenantProvider`. Proteja queries adicionando filtros quando necessário.
- **Logs:** logs são escritos em `C:\EscolaAtenta\Logs` (produção) ou ao lado do executável (fallback). Retenção padrão: 30 dias, limite de 10 MB por arquivo.
- **Mobile:** lembre-se de que o app pode operar offline. Sincronização com o servidor deve ser resiliente a falhas de rede e conflitos de dados.
- **Serviço Windows:** para reiniciar/parar/iniciar o serviço, o TrayMonitor executa `sc.exe` com elevação UAC (`Verb = "runas"`).

## 11. Arquivos-Chave para Consulta Rápida

| Arquivo | Propósito |
|---|---|
| `EscolaAtenta.sln` | Solução Visual Studio com todos os projetos |
| `src/EscolaAtenta.API/Program.cs` | Configuração e pipeline de startup da API |
| `src/EscolaAtenta.Infrastructure/Data/AppDbContext.cs` | DbContext, auditoria, soft delete e domain events |
| `src/EscolaAtenta.Domain/Common/EntityBase.cs` | Base de entidades: Id, auditoria, multi-tenant, events |
| `src/EscolaAtenta.API/appsettings.json` | Configuração principal do backend |
| `src/EscolaAtenta.App/app.json` | Configuração Expo do app mobile |
| `src/EscolaAtenta.App/package.json` | Dependências NPM do mobile |
| `build-release.ps1` | Build e empacotamento de release |
| `escolaatenta-installer.iss` | Script do instalador Windows |
| `INSTALACAO.txt` | Guia de instalação e operação para escolas |
| `README.md` | Documentação geral do projeto |
| `src/EscolaAtenta.Domain/Entities/AlunoTurmaHistorico.cs` | Histórico de vínculos aluno-turma |
| `src/EscolaAtenta.Application/Turmas/Handlers/RelatorioTurmaHandler.cs` | Relatório de frequência por turma e intervalo de datas |
| `src/EscolaAtenta.Application/Alunos/Handlers/TransferirAlunoHandler.cs` | Transferência individual de aluno entre turmas |
| `src/EscolaAtenta.Application/Turmas/Handlers/MigrarTurmaHandler.cs` | Migração em lote entre turmas |
