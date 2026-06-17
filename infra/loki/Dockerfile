# Custom Loki image for Azure Container Apps.
#
# ACA cannot bind-mount host files, so observability services ship as custom
# images. Loki's stock image already bundles a sensible single-instance config
# at /etc/loki/local-config.yaml that writes to ephemeral local storage — which
# is exactly what we want for a one-day event (data is lost on restart, per the
# agreed design). We therefore keep the default ENTRYPOINT/config and only pin
# the base image so the build/push pipeline is uniform with the other services.
#
# Build context: infra/loki
#   docker build -f infra/loki/Dockerfile -t loki infra/loki
FROM grafana/loki:latest

EXPOSE 3100
