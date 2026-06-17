# Custom Prometheus image for Azure Container Apps.
#
# ACA cannot bind-mount host files, so the cloud scrape config is baked into the
# image. This config (prometheus.cloud.yml) targets the API at its ACA internal
# hostname (scramblecoin-api:5001) instead of the docker-compose hostnames used
# by the local infra/prometheus.yml (which is left untouched for local dev).
#
# Build context: infra/prometheus
#   docker build -f infra/prometheus/Dockerfile -t prometheus infra/prometheus
FROM prom/prometheus:latest

COPY prometheus.cloud.yml /etc/prometheus/prometheus.yml

EXPOSE 9090
