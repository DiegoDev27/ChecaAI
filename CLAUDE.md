# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview
ChecaAI is a .NET 8 Web API platform for Brazilian political transparency. It tracks politicians (senators, deputies, councilors), their votes, proposals, and expenses by integrating with government APIs and web scraping municipal chambers.

## Architecture
- **Clean Architecture** with separation of concerns across layers
- **ChecaAI.Api**: Web API controllers and configuration
- **ChecaAI.Application**: Business logic and services  
- **ChecaAI.Domain**: Entity models and core domain logic
- **ChecaAI.Infrastructure**: Data access, Entity Framework, external integrations
- **ChecaAI.Worker**: Background service for data scraping and synchronization

## Database
- **PostgreSQL** with Entity Framework Core 8.0.11
- **Main Entities**: Politician, Proposal, VotingSession, Vote, PoliticianExpense
- **DbContext**: ChecaAIDbContext in Infrastructure layer

## Development Commands

### Database
```bash
# Start PostgreSQL with Docker Compose (run from repo root)
docker-compose up -d

# Create migration (run from repo root)
dotnet ef migrations add <MigrationName> --project backend/ChecaAI.Infrastructure --startup-project backend/ChecaAI.Api

# Update database (run from repo root)
dotnet ef database update --project backend/ChecaAI.Infrastructure --startup-project backend/ChecaAI.Api

# Stop services
docker-compose down
```

### Build and Run
```bash
# Build solution (run from backend/)
cd backend && dotnet build

# Run API (run from repo root)
dotnet run --project backend/ChecaAI.Api

# Run specific project
dotnet run --project backend/<ProjectName>

# Run Worker for data synchronization
dotnet run --project backend/ChecaAI.Worker
```

### Testing
```bash
# Run all tests (when test projects are added)
dotnet test

# Run specific test project
dotnet test <TestProjectPath>
```

## Configuration
- **Connection String**: appsettings.json DefaultConnection
- **Government APIs**: 
  - Câmara dos Deputados: https://dadosabertos.camara.leg.br/api/v2
  - Senado Federal: https://legis.senado.leg.br/dadosabertos
- **PgAdmin**: http://localhost:8080 (admin@checa-ai.com / admin123)

## Key Features Implementation
1. **Government API Integration**: Services to fetch data from Brazilian government transparency APIs
2. **Web Scraping**: Services for municipal chambers without public APIs  
3. **Vote Tracking**: Complete voting history and politician positions
4. **Expense Monitoring**: Political expenses and reimbursements tracking
5. **Data Synchronization**: Regular updates from multiple data sources

## Project Structure
```
├── backend/                   # .NET backend (Clean Architecture)
│   ├── ChecaAI.Api/           # Web API layer
│   ├── ChecaAI.Application/   # Business logic
│   ├── ChecaAI.Domain/        # Domain entities
│   │   └── Entities/          # Core entities (Politician, Vote, etc.)
│   ├── ChecaAI.Infrastructure/ # Data access
│   │   └── Data/              # DbContext and configurations
│   ├── ChecaAI.Worker/        # Background services
│   │   ├── Models/DTOs/       # XML deserialization models
│   │   ├── Services/          # Data scraping services
│   │   └── Configuration/     # Worker settings
│   └── checa-ai.sln           # Solution file
├── frontend/                  # Frontend (web + mobile — em construção)
├── docker-compose.yml         # PostgreSQL setup
└── .env.example               # Environment variables template
```

## Development Workflow
1. Always run `docker-compose up -d` before starting development
2. Use migrations for database changes: create migration, then update database  
3. Follow Clean Architecture principles: Domain → Application → Infrastructure → API
4. Test locally with Swagger UI at https://localhost:7001/swagger (when running)

## Data Worker (ChecaAI.Worker)
The Worker is a background service that automatically synchronizes data from government APIs.

### Features
- **Automatic Senate Data Sync**: Fetches senator data from https://legis.senado.leg.br/dadosabertos/senador/lista/atual
- **Configurable Intervals**: Sync frequency configurable via appsettings.json
- **Retry Logic**: Automatic retries with exponential backoff
- **Comprehensive Logging**: Detailed logging for monitoring and troubleshooting

### Configuration (appsettings.json)
```json
{
  "DataSync": {
    "SenateDataSyncInterval": "02:00:00",  // Every 2 hours
    "EnableScheduledSync": true,
    "SyncStartTime": "06:00:00",           // Start at 6 AM
    "RetryAttempts": 3,
    "RetryDelay": "00:01:00"
  },
  "SenateApi": {
    "BaseUrl": "https://legis.senado.leg.br/dadosabertos",
    "RequestTimeout": "00:05:00"
  }
}
```

### Usage
```bash
# Run worker once (one-time sync)
dotnet run --project backend/ChecaAI.Worker

# Run worker as service (continuous monitoring)
# Set EnableScheduledSync: true in appsettings.json
dotnet run --project backend/ChecaAI.Worker
```

## External Dependencies
- Microsoft.EntityFrameworkCore 8.0.11
- Npgsql.EntityFrameworkCore.PostgreSQL 8.0.11
- Microsoft.Extensions.Http 10.0.1
- PostgreSQL 15 (via Docker)
- PgAdmin 4 (via Docker)

---

## Frontend — Decisões de Stack (definido, não implementado ainda)

### Estrutura do Monorepo (Turborepo)
```
frontend/
├── apps/
│   ├── web/        ← Next.js 14 (App Router)
│   └── mobile/     ← Expo (React Native)
└── packages/
    ├── ui/         ← componentes compartilhados
    ├── api-client/ ← cliente tipado para a API .NET
    └── types/      ← interfaces TypeScript do domínio (Politician, Vote, etc.)
```

### Stack Web — Next.js 14
- **Framework**: Next.js 14 App Router (SSR/SSG para SEO)
- **Estilo**: Tailwind CSS + shadcn/ui
- **Estado**: TanStack Query (server state) + Zustand (client state)
- **Gráficos**: Recharts
- **Mapas**: React Leaflet (vereadores por cidade)
- **Tempo real**: `@microsoft/signalr` (alertas de plenário)
- **IA**: Vercel AI SDK (streaming de respostas do Claude)
- **Deploy**: Vercel

### Stack Mobile — Expo
- **Framework**: Expo (React Native) com Expo Router
- **Estilo**: NativeWind (Tailwind para RN)
- **Gráficos**: Victory Native
- **Push notifications**: Expo Push + FCM/APNs
- **Mapas**: react-native-maps

### Ordem de Implementação
1. Monorepo + package `types` compartilhado
2. Next.js web — páginas de parlamentares e votações
3. Componentes de gráficos (histórico de votações, despesas)
4. Endpoint de IA no backend .NET (RAG com Claude API)
5. Chat/busca inteligente no web
6. Expo mobile — consulta rápida + notificações push
7. Alertas de plenário em tempo real (SignalR + push)

---

## Alertas de Plenário em Tempo Real (planejado, não implementado)

### Objetivo
Detectar votações suspeitas (madrugada, urgência, quórum baixo) e notificar usuários em tempo real.

### Fontes de Dados
- **Câmara**: `GET https://dadosabertos.camara.leg.br/api/v2/votacoes?dataInicio=...`
- **Senado**: `GET https://legis.senado.leg.br/dadosabertos/plenario/votacao/lista/{data}`
- Polling a cada **90 segundos** via `BackgroundService` no `ChecaAI.Worker`

### Componentes a Criar
| Componente | Projeto | Descrição |
|---|---|---|
| `PlenaryWatcherService` | `ChecaAI.Worker` | BackgroundService — polling das APIs a cada 90s |
| `VotingAlertEngine` | `ChecaAI.Application` | Pontua suspeição (horário, duração, urgência, quórum) |
| `PlenaryHub` | `ChecaAI.Api` | SignalR Hub — notificação em tempo real para web |
| `PushNotificationService` | `ChecaAI.Infrastructure` | Envia push via Expo/FCM para mobile |

### Critérios de Pontuação (VotingAlertEngine)
| Critério | Pontos |
|---|---|
| Votação entre 23h–5h | +40 |
| Votação entre 21h–23h | +20 |
| Duração < 15 minutos | +35 |
| Regime de urgência | +25 |
| Quórum < 30% | +20 |
| Palavras-chave polêmicas na ementa | +10 cada |
- Score ≥ 60 → **CRÍTICO** · Score ≥ 35 → **ATENÇÃO** · demais → NORMAL

### Fluxo
```
PlenaryWatcherService (90s loop)
  → detecta votação nova
  → VotingAlertEngine.Evaluate() → score
  → se score > 30:
      → PlenaryHub → SignalR → browser (Next.js useVotingAlerts hook)
      → PushNotificationService → Expo Push → mobile
      → Claude API → gera resumo em linguagem simples → inclui na notificação
```

---

## Backend — Backlog de Cobertura (priorizado)

### Cobertura Atual por Cargo
| Cargo | Status |
|---|---|
| Senador Federal | ✅ Completo |
| Deputado Federal | ⚠️ Parcial (interface definida, coleta incompleta) |
| Vereador | ⚠️ Mínimo (3 cidades apenas) |
| Deputado Estadual | ❌ Não implementado |
| Prefeito | ❌ Não implementado |
| Governador | ❌ Não implementado |
| Presidente | ❌ Não implementado |

### APIs a Integrar (por prioridade)

**ALTA — maior impacto, fonte já disponível**
- **Câmara dos Deputados** (completar): `/deputados/{id}/despesas`, `/deputados/{id}/frequencias`, `/votacoes/{id}/votos`
- **TSE** (`dadosabertos.tse.jus.br`): declaração de bens, gastos de campanha, resultados eleitorais — cobre TODOS os cargos eleitos
- **Portal da Transparência CGU** (`portaldatransparencia.gov.br/api-de-dados`): salários, diárias, viagens de todos os federais

**MÉDIA**
- Deputados Estaduais (SP, RJ, MG pelo menos — scraping)
- Governadores (portais estaduais de transparência)
- Comissões e frentes parlamentares (Câmara + Senado)
- Presença/absenteísmo em sessões

**BAIXA — complexo, sem padronização**
- Prefeitos (5.570 municípios, sem API unificada)
- Assembleias dos 27 estados
- Portais municipais de transparência

### Entidades de Domínio a Criar
| Entidade | Fonte | Dados |
|---|---|---|
| `PoliticianSalary` | CGU | salário bruto, líquido, subsídios, mês/ano |
| `CampaignExpense` | TSE | ano eleitoral, categoria, valor, fornecedor, CNPJ |
| `AssetDeclaration` | TSE | ano eleitoral, tipo de bem, valor declarado |
| `ElectionResult` | TSE | ano, cargo, estado, votos recebidos, eleito |
| `SessionAttendance` | Câmara/Senado | data, presente/ausente, justificativa |
| `Committee` | Câmara/Senado | nome, sigla, tipo (Permanente/CPI/Especial) |
| `CommitteeMembership` | Câmara/Senado | cargo na comissão (Presidente/Titular/Suplente) |