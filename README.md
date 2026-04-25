# 🪙 ScrambleCoin

A Blazor Server game server for **Scramblecoin** — a turn-based board game from *Disney Dreamlight Valley* (A Rift in Time expansion).

---

## 🏗️ Solution Structure

```
ScrambleCoin.sln
├── src/
│   ├── ScrambleCoin.Domain/          # Entities, Value Objects, Domain Events, Enums, Domain Interfaces
│   ├── ScrambleCoin.Application/     # MediatR Commands, Queries, Handlers, DTOs, Application Interfaces
│   ├── ScrambleCoin.Infrastructure/  # EF Core DbContext, Repositories, Migrations, SQL config
│   └── ScrambleCoin.Web/             # Blazor Server, REST API endpoints, SignalR hubs, DI wiring
└── tests/
    ├── ScrambleCoin.Domain.Tests/     # Pure C# entity/rules tests (no mocks)
    ├── ScrambleCoin.Application.Tests/# Handler tests with NSubstitute mocks
    ├── ScrambleCoin.Infrastructure.Tests/ # EF Core repository tests (Docker SQL Server)
    ├── ScrambleCoin.Web.Tests/        # bUnit component tests + WebApplicationFactory API tests
    └── ScrambleCoin.E2E.Tests/        # Playwright full-browser tests against running app
```

---

## ⚙️ Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9) (`9.0.x`)
- [SQL Server](https://www.microsoft.com/en-us/sql-server) or Docker (for integration tests)
- [PowerShell](https://learn.microsoft.com/en-us/powershell/scripting/install/installing-powershell) (for Playwright browser installation — **macOS:** `brew install powershell`)

---

## 🗄️ Local Development (Docker SQL Server)

### 1. Start SQL Server

```bash
docker compose up -d
```

SQL Server 2022 will start on `localhost,1433`. Wait ~15 s for the health check to pass.

### 2. Configure the connection string

```bash
cp src/ScrambleCoin.Web/appsettings.Development.example.json \
   src/ScrambleCoin.Web/appsettings.Development.json
```

> `appsettings.Development.json` is gitignored — it stays on your machine only.

### 3. Apply EF Core migrations

```bash
dotnet ef database update \
  --project src/ScrambleCoin.Infrastructure \
  --startup-project src/ScrambleCoin.Web
```

---

## 🚀 Getting Started

### 1. Restore & Build

```bash
dotnet restore
dotnet build
```

### 2. Run the App

```bash
dotnet run --project src/ScrambleCoin.Web
```

The Blazor Server app will start on `https://localhost:7102` / `http://localhost:5026`.

### 3. Run All Tests

```bash
dotnet test
```

---

## 🎭 E2E Tests (Playwright)

Playwright requires browser binaries to be downloaded before running E2E tests.

### First-time setup — install Playwright browsers

**Option A: Using the `playwright` CLI (if installed globally)**

```bash
playwright install chromium
```

**Option B: Using the PowerShell script bundled with the package**

> **macOS prerequisite:** `brew install powershell` (one-time)

```bash
# Build first so the script is present
dotnet build tests/ScrambleCoin.E2E.Tests

# Then install browsers
pwsh tests/ScrambleCoin.E2E.Tests/bin/Debug/net9.0/playwright.ps1 install
```

> 💡 Only `chromium` is required for the current test suite. Add `firefox` or `webkit` as needed.

### Run E2E tests

```bash
dotnet test tests/ScrambleCoin.E2E.Tests
```

> ⚠️ E2E tests that navigate to the running app require the app to be started first (`dotnet run --project src/ScrambleCoin.Web`).

---

## 🔧 Configuration

### Connection String

Set a SQL Server connection string in `appsettings.json` (or via environment variable / user secrets):

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=ScrambleCoin;Trusted_Connection=True;"
  }
}
```

### Logging

Logging is configured automatically:

| Sink | Condition |
|------|-----------|
| Console | Always |
| Rolling File (`logs/scramblecoin-.log`) | Always (rolls daily, keeps 7 days) |
| Application Insights | When `APPLICATIONINSIGHTS_CONNECTION_STRING` env var is set |

To enable Application Insights locally:

```bash
export APPLICATIONINSIGHTS_CONNECTION_STRING="InstrumentationKey=..."
dotnet run --project src/ScrambleCoin.Web
```

---

## 🧱 Tech Stack

| Layer | Technology |
|-------|-----------|
| UI | Blazor Server (.NET 9) |
| Architecture | Clean Architecture |
| Messaging | MediatR (commands & queries) |
| Bot API | REST (minimal API / controllers) |
| Live updates | SignalR |
| Database | SQL Server via EF Core |
| Logging | Serilog + Azure Application Insights |
| Testing | xUnit + bUnit + Playwright + NSubstitute |

---

## 📋 Clean Architecture Rules

- **Domain** — zero dependencies on other projects; pure C# only
- **Application** — depends only on Domain; defines interfaces implemented by Infrastructure
- **Infrastructure** — depends on Application and Domain; never directly referenced by Web for business logic
- **Web** — depends on Application; dispatches MediatR requests via `IMediator.Send()`

---

## ☁️ Azure Deployment

### Prerequisites

- [Azure CLI](https://learn.microsoft.com/en-us/cli/azure/install-azure-cli) installed and logged in (`az login`)
- An existing Azure **subscription** and **resource group**

```bash
# Create a resource group if you don't have one
az group create --name <resource-group-name> --location northeurope
```

---

### Deploy the infrastructure

All Azure resources are defined in `infra/main.bicep`. Deploy with a single command:

```bash
az deployment group create \
  --resource-group <resource-group-name> \
  --template-file infra/main.bicep \
  --parameters infra/main.parameters.json
```

> ⚠️ The `sqlAdminPassword` value in `infra/main.parameters.json` is a placeholder.
> **Never commit a real password.** Pass the password securely on the CLI instead:

```bash
az deployment group create \
  --resource-group <resource-group-name> \
  --template-file infra/main.bicep \
  --parameters infra/main.parameters.json \
  --parameters sqlAdminPassword="<your-secure-password>"
```

---

### Resources provisioned

| Resource | Name (derived from `appName`) | SKU / Tier |
|---|---|---|
| Log Analytics Workspace | `log-scramblecoin` | PerGB2018, 30-day retention |
| Application Insights | `appi-scramblecoin` | Workspace-based, web |
| App Service Plan | `asp-scramblecoin` | F1 Free (Linux) |
| App Service | `app-scramblecoin` | .NET 9 on Linux |
| Azure SQL Server | `sql-scramblecoin` | SQL auth, v12 |
| Azure SQL Database | `sqldb-scramblecoin` | Basic (5 DTUs) |

---

### Cost estimate (North Europe, pay-as-you-go)

| Resource | Estimated monthly cost |
|---|---|
| App Service Plan F1 Free | **€0** |
| Azure SQL Database Basic | ~€4 |
| Application Insights | ~free (pay-per-use, low volume) |
| Log Analytics Workspace | ~free (low ingestion volume) |
| **Total** | **~€4–5 / month** |

> ⚠️ **F1 Free tier limit:** 60 compute-minutes/day. Sufficient for dev and testing — upgrade to **B1 (~€12/month)** for the actual CodeRetreat event day to avoid service interruptions.

> Prices are approximate. Check the [Azure Pricing Calculator](https://azure.microsoft.com/en-us/pricing/calculator/) for up-to-date figures.

---

### Validate the Bicep template (without deploying)

```bash
# If Azure CLI with Bicep extension is available:
az bicep build --file infra/main.bicep

# Or using the standalone Bicep CLI:
bicep build infra/main.bicep

# Validate against your resource group (dry-run):
az deployment group validate \
  --resource-group <resource-group-name> \
  --template-file infra/main.bicep \
  --parameters infra/main.parameters.json \
  --parameters sqlAdminPassword="<your-secure-password>"
```

---

## 🛠️ Useful Commands

```bash
# Build solution
dotnet build

# Run all tests
dotnet test

# Run specific test project
dotnet test tests/ScrambleCoin.Domain.Tests

# Add EF Core migration
dotnet ef migrations add <MigrationName> --project src/ScrambleCoin.Infrastructure --startup-project src/ScrambleCoin.Web

# Apply migrations
dotnet ef database update --project src/ScrambleCoin.Infrastructure --startup-project src/ScrambleCoin.Web

# Run app
dotnet run --project src/ScrambleCoin.Web
```
