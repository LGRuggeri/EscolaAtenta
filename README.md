# Escola Atenta

Sistema de monitoramento de frequencia escolar e prevencao de evasao. Arquitetura **edge-first** — funciona offline em escolas sem internet confiavel.

## Visao Geral

O Escola Atenta permite que monitores realizem chamadas diarias pelo celular (Android), detecta automaticamente padroes de faltas e atrasos, gera alertas de evasao escolar e disponibiliza relatorios de frequencia para supervisao e diretoria.

### Funcionalidades Principais

- **Chamada digital** — registro de presenca via app mobile com feedback haptico
- **Alertas automaticos de evasao** — regras configuraveis (faltas consecutivas, percentual de ausencia)
- **Niveis de alerta** — Aviso, Intermediario, Vermelho, Preto (risco critico)
- **Auditoria de alertas** — historico completo de tratativas com responsavel e justificativa
- **Gestao de turmas, alunos e usuarios** — CRUD com controle de papeis (Monitor, Supervisao, Administrador)
- **Relatorios de presenca** — consulta por aluno, turma e periodo
- **Quadro de Honra** — destaque para turmas com 100% de frequencia
- **Sincronizacao offline-first** — WatermelonDB no mobile + sync bidirecional com servidor

## Arquitetura

```
┌─────────────────────────────────────────────────────────────┐
│                     Escola (Edge Node)                       │
│                                                              │
│  ┌──────────────┐    HTTP/LAN     ┌───────────────────────┐ │
│  │  App Mobile   │ ◄────────────► │   API Server          │ │
│  │  React Native │    :5114       │   ASP.NET Core 9      │ │
│  │  Expo 54      │                │   SQLite WAL          │ │
│  │  WatermelonDB │                │   Windows Service     │ │
│  └──────────────┘                └───────────────────────┘ │
│                                          │                  │
│                                   ┌──────┴──────┐          │
│                                   │ TrayMonitor  │          │
│                                   │ OTA Updates  │          │
│                                   └─────────────┘          │
└─────────────────────────────────────────────────────────────┘
```

### Clean Architecture + DDD

```
Domain          (zero deps)          Entidades, Value Objects, Domain Events
  └── Application  (MediatR CQRS)   Commands, Queries, Handlers
        └── Infrastructure           EF Core, SQLite, Auth, Workers
              └── API                Controllers, Middleware, DI
```

## Stack Tecnologica

### Backend

| Tecnologia | Versao | Uso |
|---|---|---|
| ASP.NET Core | 9.0 | API REST |
| Entity Framework Core | 9.x | ORM + Migrations |
| SQLite | WAL mode | Banco embarcado (resiliencia a quedas de energia) |
| MediatR | 12.x | CQRS (Commands/Queries) |
| Serilog | 8.x | Logging estruturado |
| BCrypt.Net | - | Hash de senhas |
| JWT + Refresh Tokens | - | Autenticacao |

### Frontend Mobile

| Tecnologia | Versao | Uso |
|---|---|---|
| React Native | Expo 54 | Framework mobile |
| React Native Paper | 5.15 | UI (Material Design 3) |
| WatermelonDB | 0.28 | Banco local offline-first |
| Expo Linear Gradient | 15.x | Gradientes visuais |
| Expo Haptics | 15.x | Feedback tatil |
| React Navigation | 7.x | Navegacao |
| Axios | 1.x | HTTP client |

### Infraestrutura

| Componente | Descricao |
|---|---|
| Windows Service | API roda como servico nativo |
| Inno Setup | Installer para escolas |
| TrayMonitor | Atualizacoes OTA automaticas |
| HeartbeatWorker | Ping a cada 15min para detectar escolas offline |

## Estrutura do Projeto

```
EscolaAtenta/
├── src/
│   ├── EscolaAtenta.Domain/           # Entidades, eventos, regras de negocio
│   ├── EscolaAtenta.Application/      # CQRS handlers, DTOs, services
│   ├── EscolaAtenta.Infrastructure/   # EF Core, migrations, auth, workers
│   ├── EscolaAtenta.API/              # Controllers, middleware, DI
│   ├── EscolaAtenta.App/              # React Native (Expo) mobile app
│   └── EscolaAtenta.TrayMonitor/      # Monitor de atualizacoes OTA
├── Tests/
│   ├── EscolaAtenta.Domain.Tests/     # Testes unitarios do dominio
│   └── EscolaAtenta.Application.Tests/# Testes dos handlers CQRS
└── EscolaAtenta.sln
```

## Como Executar

### Pre-requisitos

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- [Node.js 20+](https://nodejs.org/)
- [Expo CLI](https://docs.expo.dev/get-started/installation/)

### Backend (API)

```bash
# Restaurar dependencias
dotnet restore

# Aplicar migrations (cria o banco SQLite automaticamente)
dotnet ef database update --project src/EscolaAtenta.Infrastructure --startup-project src/EscolaAtenta.API

# Executar a API
dotnet run --project src/EscolaAtenta.API
```

A API estara disponivel em `http://localhost:5114`. Health check: `GET /health`.

### Frontend (App Mobile)

```bash
cd src/EscolaAtenta.App

# Instalar dependencias
npm install --legacy-peer-deps

# Iniciar o Expo
npm start
```

Escaneie o QR code com o Expo Go (Android) ou gere o APK com `eas build`.

### Testes

```bash
# Todos os testes
dotnet test

# Apenas dominio
dotnet test Tests/EscolaAtenta.Domain.Tests

# Apenas application
dotnet test Tests/EscolaAtenta.Application.Tests
```

## Configuracao

### Regras de Negocio (`appsettings.json`)

```json
{
  "RegrasNegocio": {
    "LimiteFaltasParaAlerta": 5,
    "PercentualFaltasParaEvasao": 25
  }
}
```

### Servidor no App Mobile

O app permite configurar o IP do servidor via tela de **Configuracao de Servidor** (menu de configuracoes). O padrao e a porta `5114`.

## Papeis de Usuario

| Papel | Permissoes |
|---|---|
| **Monitor** | Realizar chamadas, consultar turmas e alunos atribuidos |
| **Supervisao** | Tudo do Monitor + visualizar alertas, tratar alertas, relatorios |
| **Administrador** | Acesso total + gestao de usuarios, turmas, configuracoes |

## Seguranca

- Autenticacao JWT com refresh token rotation
- Hash de senhas com BCrypt
- Rate limiting: 100 req/global + 5 req/min por email no login
- Protecao contra IDOR (validacao de ownership em queries)
- Multi-tenant por EscolaId com global query filters
- Soft delete com `ISoftDeletable`
- Troca de senha obrigatoria no primeiro acesso
- Logs estruturados com Serilog (rotacao 10MB, retencao 30 dias)

## Licenca

Projeto privado. Todos os direitos reservados.
