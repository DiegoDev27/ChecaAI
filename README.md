# Checa.AI - Transparência Política Brasileira

API .NET para verificação de transparência política de senadores, deputados e vereadores brasileiros.

## Configuração do Ambiente

### 1. Pré-requisitos
- Docker Desktop
- .NET 8 SDK
- Git

### 2. Configuração do Banco PostgreSQL

1. Copie o arquivo de configuração:
```bash
cp .env.example .env
```

2. Suba o banco PostgreSQL com Docker Compose:
```bash
docker-compose up -d
```

3. Acesse o PgAdmin em: http://localhost:8080
   - Email: admin@checaai.com  
   - Senha: admin123

### 3. Conexão com o Banco
- Host: localhost
- Port: 5432
- Database: checaai
- User: checaai_user
- Password: dev_password123

## Como Executar

### 1. Subir o PostgreSQL
```bash
docker-compose up -d
```

### 2. Executar as migrações do banco
```bash
dotnet ef database update --project ChecaAI.Infrastructure --startup-project ChecaAI.Api
```

### 3. Executar a API
```bash
dotnet run --project ChecaAI.Api
```

### 4. Acessar a documentação
- API Swagger: https://localhost:7001/swagger
- PgAdmin: http://localhost:8080

## Endpoints Principais

### Políticos
- `GET /api/politicians` - Lista todos os políticos
- `GET /api/politicians/{id}` - Busca político por ID
- `GET /api/politicians/{id}/votes` - Votos de um político
- `GET /api/politicians/{id}/expenses` - Despesas de um político

### Propostas
- `GET /api/proposals` - Lista propostas
- `GET /api/proposals/{id}` - Busca proposta por ID
- `GET /api/proposals/{id}/voting-sessions` - Sessões de votação de uma proposta

### Sessões de Votação
- `GET /api/voting-sessions` - Lista sessões de votação
- `GET /api/voting-sessions/{id}` - Busca sessão por ID
- `GET /api/voting-sessions/{id}/votes` - Votos de uma sessão
- `GET /api/voting-sessions/{id}/results` - Resultados detalhados da votação

### Web Scraping Municipal
- `GET /api/municipal/cities/{cityName}/councilors?stateCode={state}` - Vereadores por cidade
- `GET /api/municipal/supported-cities` - Cidades suportadas para scraping

## Comandos Úteis

### Docker
```bash
# Subir serviços
docker-compose up -d

# Ver logs
docker-compose logs -f

# Parar serviços
docker-compose down

# Limpar volumes (ATENÇÃO: apaga dados)
docker-compose down -v
```

### .NET
```bash
# Build do projeto
dotnet build

# Executar API
dotnet run --project ChecaAI.Api

# Criar nova migração
dotnet ef migrations add <NomeDaMigracao> --project ChecaAI.Infrastructure --startup-project ChecaAI.Api

# Atualizar banco
dotnet ef database update --project ChecaAI.Infrastructure --startup-project ChecaAI.Api
```