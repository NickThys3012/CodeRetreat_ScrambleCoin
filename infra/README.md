# ScrambleCoin — Infrastructure & Observability

This directory contains the local observability stack used during the event:
Grafana dashboards, a Prometheus scrape config, and the Loki + Promtail log
aggregation pipeline. Everything is wired together via the repo-root
`docker-compose.yml`.

| Service     | Purpose                                   | URL                     |
|-------------|-------------------------------------------|-------------------------|
| Grafana     | Dashboards + Explore (metrics & logs)     | http://localhost:3000   |
| Prometheus  | API metrics store (issue #80)             | http://localhost:9090   |
| Loki        | Log storage (LogQL)                       | http://localhost:3100   |
| Promtail    | Scrapes Serilog log files → Loki          | (no UI)                 |
| SQL Server  | Game database                             | localhost:1433          |

Grafana provisioning lives under `infra/grafana/provisioning/` (datasources +
dashboards are auto-loaded on container start).

## Bring it up

```bash
cp .env.example .env          # one-time: provides MSSQL_SA_PASSWORD etc.
docker compose up -d
```

## Logs (Loki + Promtail)

`ScrambleCoin.Api` and `ScrambleCoin.Web` write structured logs via Serilog's
`JsonFormatter` to rolling daily files under `logs/`. Promtail tails those files
and ships them to Loki:

- The **API** runs in a container, so its `logs/` are exposed through the shared
  named volume `api-logs` (mounted into both the API and Promtail containers).
- The **Web** still runs on the host, so its
  `src/ScrambleCoin.Web/logs/` directory is bind-mounted (read-only) into the
  Promtail container.

Because Serilog emits JSON, Promtail's `json` pipeline stage extracts structured
fields. `level` (e.g. `Information`, `Warning`, `Error`) is promoted to a Loki
**label** (low cardinality) for fast filtering; high-cardinality fields such as
`GameId` are extracted for matching but **not** promoted to labels.

### Verify it's working

1. `docker compose up -d` — confirm `scramblecoin-loki` and `scramblecoin-promtail` start.
2. Open http://localhost:3100/ready — should return `ready`.
3. Open Grafana → **Explore** → select the **ScrambleCoin Loki** data source.
4. Or open the provisioned dashboard **ScrambleCoin — Logs**.

### Useful LogQL queries

Find all events for a specific GameId:

```logql
{job="scramblecoin-api"} |= "<GameId>"
```

All warnings and errors from the API (quick bot-error triage):

```logql
{job="scramblecoin-api", level=~"Warning|Error"}
```

All Web logs:

```logql
{job="scramblecoin-web"}
```

---

## Deploy to Azure Container Apps (ACA)

The **entire stack** — Blazor Web, bot API, Grafana, Loki, Prometheus — can be
deployed to **Azure Container Apps** for the event. This is separate from the
App Service deploy (`release-and-deploy.yml`) and is triggered **manually**.

> **Local dev is unchanged.** `docker compose` + Promtail (the "Logs (Loki +
> Promtail)" section above) still works exactly as before. In the cloud, ACA
> cannot bind-mount host files and cannot tail container log files across apps,
> so logging is re-architected: the API and Web push logs **directly** to Loki
> via the `Serilog.Sinks.Grafana.Loki` sink. That sink is **conditional** — it
> only activates when the `Loki__Url` environment variable is set (which ACA
> sets, and local compose does not). Prometheus/Grafana ship as **custom images**
> that bake their config/provisioning in (since ACA can't mount the host files).

### What gets provisioned

`infra/aca.bicep` creates:

- **Azure Container Registry** (`acr<appName>`, Basic, admin-enabled) — holds the
  web/api images plus custom loki/prometheus/grafana images.
- **Log Analytics workspace** (`log-<appName>-aca`) for the ACA environment.
- **Container Apps environment** (`cae-<appName>`).
- **Five Container Apps**:
  - `scramblecoin-web` — external HTTPS, port 8080.
  - `scramblecoin-api` — external HTTPS, port 5001 (keeps `/metrics` exposed —
    see note below).
  - `grafana` — external HTTPS, port 3000.
  - `loki` — internal, port 3100 (ephemeral storage).
  - `prometheus` — internal, port 9090 (ephemeral storage).

**Azure SQL is reused**, not recreated — provision it once via `main.bicep`.

> **Note — API `ASPNETCORE_ENVIRONMENT=Docker`:** `Program.cs` only maps the
> Prometheus `/metrics` endpoint when the environment is **not** `Production`.
> The `scramblecoin-api` Container App therefore runs with
> `ASPNETCORE_ENVIRONMENT=Docker` (matching `docker-compose.yml`) so Prometheus
> can scrape it. The Web app runs as `Production`.

### Prerequisites

1. An Azure subscription and the Azure CLI (`az login`).
2. A resource group, e.g. `az group create -n rg-scramblecoin -l swedencentral`.
3. **Azure SQL provisioned first** via `main.bicep`:
   ```bash
   az deployment group create \
     -g rg-scramblecoin \
     --template-file infra/main.bicep \
     --parameters infra/main.parameters.json \
     --parameters sqlAdminPassword='<your-strong-password>'
   ```
   Note the `sqlServerFqdn` output (e.g. `sql-scramblecoin.database.windows.net`).

### GitHub secrets to set

| Secret | Purpose |
| --- | --- |
| `AZURE_CREDENTIALS` | Service principal JSON for `azure/login` (`az ad sp create-for-rbac --sdk-auth`). |
| `AZURE_SQL_CONNECTION_STRING` | Full Azure SQL connection string — used by the EF migration step. |
| `SQL_ADMIN_PASSWORD` | Azure SQL admin password — injected into the API/Web connection strings and the Grafana SQL datasource. |
| `GRAFANA_ADMIN_PASSWORD` | Grafana admin password — set as an ACA secret (never baked into the image). |

### Run the deploy

GitHub → **Actions** → **Deploy to Azure Container Apps** → **Run workflow**, and
fill in the inputs (`resourceGroup`, `acrName`, `location`, `sqlServerFqdn`). The
workflow will:

1. `azure/login` with `AZURE_CREDENTIALS`.
2. Ensure the ACR exists (idempotent).
3. `az acr build` (server-side, no local Docker) all five images, tagged with the
   git SHA.
4. `az deployment group create` for `infra/aca.bicep` with `imageTag=<sha>`.
5. Run EF Core migrations against Azure SQL.
6. Echo the resulting **Web / API / Grafana** URLs.

### Find the URLs

The final **Show deployment URLs** step prints the public endpoints. You can also
read them any time with:

```bash
az containerapp show -g rg-scramblecoin -n scramblecoin-web    --query properties.configuration.ingress.fqdn -o tsv
az containerapp show -g rg-scramblecoin -n scramblecoin-api    --query properties.configuration.ingress.fqdn -o tsv
az containerapp show -g rg-scramblecoin -n grafana             --query properties.configuration.ingress.fqdn -o tsv
```

> **Internal DNS note:** within the ACA environment, apps reach each other by
> name (`http://loki:3100`, `http://prometheus:9090`, `scramblecoin-api:5001`).
> The Grafana datasources and the API's `Loki__Url` are wired to these names.

### Manual deploy without GitHub Actions

You can run the same steps locally:

```bash
az acr build --registry acrscramblecoin --image scramblecoin-api:latest --file src/ScrambleCoin.Api/Dockerfile .
az acr build --registry acrscramblecoin --image scramblecoin-web:latest --file src/ScrambleCoin.Web/Dockerfile .
az acr build --registry acrscramblecoin --image loki:latest       --file infra/loki/Dockerfile infra/loki
az acr build --registry acrscramblecoin --image prometheus:latest --file infra/prometheus/Dockerfile infra/prometheus
az acr build --registry acrscramblecoin --image grafana:latest    --file infra/grafana/Dockerfile infra/grafana

az deployment group create \
  -g rg-scramblecoin \
  --template-file infra/aca.bicep \
  --parameters infra/aca.parameters.json \
  --parameters sqlAdminPassword='<pwd>' grafanaAdminPassword='<pwd>' imageTag=latest
```
