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
