# Custom Grafana image for Azure Container Apps.
#
# ACA cannot bind-mount host files, so the datasource + dashboard provisioning
# is baked into the image. The cloud datasources (provisioning-cloud/) use
# env-var-driven URLs/secrets (LOKI_URL, PROMETHEUS_URL, AZURE_SQL_*) instead of
# the docker-compose hostnames used by the local provisioning/ tree (left
# untouched for local dev). The dashboard JSONs are reused as-is from the local
# provisioning tree so there is a single source of truth.
#
# Build context: infra/grafana
#   docker build -f infra/grafana/Dockerfile -t grafana infra/grafana
FROM grafana/grafana:latest

# Cloud datasources + dashboard provider (env-substituted at startup).
COPY provisioning-cloud/datasources/ /etc/grafana/provisioning/datasources/
COPY provisioning-cloud/dashboards/dashboards.yaml /etc/grafana/provisioning/dashboards/dashboards.yaml

# Reuse the same dashboard JSONs as local dev (single source of truth).
COPY provisioning/dashboards/tournament.json /etc/grafana/provisioning/dashboards/tournament.json
COPY provisioning/dashboards/api-health.json /etc/grafana/provisioning/dashboards/api-health.json
COPY provisioning/dashboards/logs.json /etc/grafana/provisioning/dashboards/logs.json

EXPOSE 3000
