#!/usr/bin/env python3
"""Structural validation for the Grafana + SQL Server tournament dashboard (issue #79).

This is an infrastructure-only change, so there is no C# code to unit-test.
Instead we assert the issue-specific invariants of the infra artifacts that are
most likely to silently break the live dashboard:

  1. tournament.json is valid JSON with refresh "5s", a fixed top-level uid, and
     exactly the 4 expected panels.
  2. Every panel/target datasource uid in tournament.json matches the datasource
     uid declared in mssql.yaml (uid mismatch -> blank dashboard).
  3. docker-compose.yml uses ${MSSQL_SA_PASSWORD} (no hard-coded plaintext SA
     password) and declares a grafana service + grafana-data volume.
  4. .env.example exists and defines MSSQL_SA_PASSWORD and GRAFANA_ADMIN_PASSWORD.

Dependency-light: uses stdlib json; uses PyYAML if available, otherwise a tiny
regex fallback to read the datasource uid. Exits non-zero on any failure.

Run from the repo root (or anywhere — paths are resolved relative to this file):
    python3 infra/grafana/validate_provisioning.py
"""

import json
import os
import re
import sys

# Repo root is two levels up from infra/grafana/
ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), os.pardir, os.pardir))

DASHBOARD = os.path.join(ROOT, "infra", "grafana", "provisioning", "dashboards", "tournament.json")
DASHBOARD_PROVIDER = os.path.join(ROOT, "infra", "grafana", "provisioning", "dashboards", "dashboards.yaml")
DATASOURCE = os.path.join(ROOT, "infra", "grafana", "provisioning", "datasources", "mssql.yaml")
COMPOSE = os.path.join(ROOT, "docker-compose.yml")
ENV_EXAMPLE = os.path.join(ROOT, ".env.example")

# ---- Issue #80 (Prometheus API observability) artifacts ----------------------
PROM_DATASOURCE = os.path.join(ROOT, "infra", "grafana", "provisioning", "datasources", "prometheus.yaml")
API_DASHBOARD = os.path.join(ROOT, "infra", "grafana", "provisioning", "dashboards", "api-health.json")
PROMETHEUS_CONFIG = os.path.join(ROOT, "infra", "prometheus.yml")

PROM_DATASOURCE_UID = "scramblecoin-prometheus"
API_DASHBOARD_UID = "scramblecoin-api-health"
API_EXPECTED_PANEL_TITLES = [
    "HTTP Requests",
    "Latency",
    "Error Rate",
    "Moves per Second",
]

EXPECTED_PANEL_TITLES = [
    "Active Games",
    "Games by Status",
    "Bot Leaderboard",
    "Games Completed per Minute",
]
PLAINTEXT_SA_PASSWORD = "ScrambleCoin_Dev!2024"

# ---- Issue #81 (Loki + Promtail log aggregation) artifacts -------------------
LOKI_DATASOURCE = os.path.join(ROOT, "infra", "grafana", "provisioning", "datasources", "loki.yaml")
LOGS_DASHBOARD = os.path.join(ROOT, "infra", "grafana", "provisioning", "dashboards", "logs.json")
PROMTAIL_CONFIG = os.path.join(ROOT, "infra", "promtail", "config.yml")
INFRA_README = os.path.join(ROOT, "infra", "README.md")
CI_WORKFLOW = os.path.join(ROOT, ".github", "workflows", "infra-validate.yml")

LOKI_DATASOURCE_UID = "scramblecoin-loki"
LOKI_DATASOURCE_URL = "http://loki:3100"
PROMTAIL_PUSH_URL = "http://loki:3100/loki/api/v1/push"
README_GAMEID_QUERY = '{job="scramblecoin-api"} |= "<GameId>"'

# ---- Issue #132 (Azure Container Apps full-stack deploy) artifacts -----------
ACA_BICEP = os.path.join(ROOT, "infra", "aca.bicep")
ACA_PARAMS = os.path.join(ROOT, "infra", "aca.parameters.json")
PROM_CLOUD_CONFIG = os.path.join(ROOT, "infra", "prometheus", "prometheus.cloud.yml")
CLOUD_DATASOURCES_DIR = os.path.join(ROOT, "infra", "grafana", "provisioning-cloud", "datasources")
CLOUD_LOKI_DS = os.path.join(CLOUD_DATASOURCES_DIR, "loki.yaml")
CLOUD_PROM_DS = os.path.join(CLOUD_DATASOURCES_DIR, "prometheus.yaml")
CLOUD_MSSQL_DS = os.path.join(CLOUD_DATASOURCES_DIR, "mssql.yaml")
LOKI_DOCKERFILE = os.path.join(ROOT, "infra", "loki", "Dockerfile")
PROM_DOCKERFILE = os.path.join(ROOT, "infra", "prometheus", "Dockerfile")
GRAFANA_DOCKERFILE = os.path.join(ROOT, "infra", "grafana", "Dockerfile")
WEB_DOCKERFILE = os.path.join(ROOT, "src", "ScrambleCoin.Web", "Dockerfile")
DEPLOY_ACA_WORKFLOW = os.path.join(ROOT, ".github", "workflows", "deploy-aca.yml")
ACA_VALIDATE_WORKFLOW = os.path.join(ROOT, ".github", "workflows", "aca-validate.yml")

ACA_CONTAINER_APP_NAMES = ["loki", "prometheus", "grafana", "scramblecoin-api", "scramblecoin-web"]
CLOUD_MSSQL_UID = "scramblecoin-mssql"

_failures = []
_checks = 0


def check(condition, message):
    global _checks
    _checks += 1
    if condition:
        print(f"  PASS: {message}")
    else:
        print(f"  FAIL: {message}")
        _failures.append(message)
    return bool(condition)


def load_yaml(path):
    """Parse a YAML file. Prefer PyYAML; fall back to None (caller handles)."""
    try:
        import yaml  # type: ignore
    except ImportError:
        return None
    with open(path, "r", encoding="utf-8") as fh:
        return yaml.safe_load(fh)


def datasource_uid_via_regex(path):
    with open(path, "r", encoding="utf-8") as fh:
        text = fh.read()
    m = re.search(r"^\s*uid:\s*([A-Za-z0-9_\-]+)\s*$", text, re.MULTILINE)
    return m.group(1) if m else None


def _all_datasource_uids(obj):
    """Recursively yield every datasource uid found under panels/targets."""
    uids = []

    def _uid_of(ds):
        if isinstance(ds, dict):
            return ds.get("uid")
        return ds

    def walk(panels):
        for p in panels or []:
            ds = p.get("datasource")
            if ds is not None:
                uids.append(_uid_of(ds))
            for t in (p.get("targets") or []):
                tds = t.get("datasource")
                if tds is not None:
                    uids.append(_uid_of(tds))
            # nested panels (rows)
            if p.get("panels"):
                walk(p.get("panels"))

    walk(obj.get("panels", []))
    return uids


def validate_issue_80():
    """Assert the Prometheus API-observability artifacts (issue #80)."""
    print("\n== prometheus API observability validation (issue #80) ==")

    # ---- Prometheus datasource ----------------------------------------------
    print("\ndatasources/prometheus.yaml:")
    prom_ds_uid = None
    if check(os.path.isfile(PROM_DATASOURCE), "prometheus.yaml exists"):
        ds_doc = load_yaml(PROM_DATASOURCE)
        if ds_doc is not None:
            check(isinstance(ds_doc, dict) and "datasources" in ds_doc,
                  "prometheus.yaml parses as YAML with a 'datasources' list")
            try:
                ds0 = ds_doc["datasources"][0]
                prom_ds_uid = ds0.get("uid")
                check(ds0.get("type") == "prometheus",
                      f"datasource type is 'prometheus' (got {ds0.get('type')!r})")
            except (KeyError, IndexError, TypeError):
                prom_ds_uid = None
        else:
            prom_ds_uid = datasource_uid_via_regex(PROM_DATASOURCE)
            check(prom_ds_uid is not None, "prometheus.yaml uid readable (regex fallback)")
    check(prom_ds_uid == PROM_DATASOURCE_UID,
          f"datasource uid is {PROM_DATASOURCE_UID!r} (got {prom_ds_uid!r})")

    # ---- api-health.json dashboard ------------------------------------------
    print("\ndashboards/api-health.json:")
    dash = None
    if check(os.path.isfile(API_DASHBOARD), "api-health.json exists"):
        try:
            dash = json.load(open(API_DASHBOARD, "r", encoding="utf-8"))
            check(True, "api-health.json is valid JSON")
        except json.JSONDecodeError as exc:
            check(False, f"api-health.json is valid JSON ({exc})")

    if dash is not None:
        check(dash.get("refresh") == "5s",
              f"dashboard refresh is '5s' (got {dash.get('refresh')!r})")
        check(dash.get("uid") == API_DASHBOARD_UID,
              f"dashboard uid is {API_DASHBOARD_UID!r} (got {dash.get('uid')!r})")

        panels = dash.get("panels", []) or []
        titles = [p.get("title", "") for p in panels]
        check(len(panels) == 4,
              f"dashboard has exactly 4 panels (got {len(panels)}: {titles})")
        for expected in API_EXPECTED_PANEL_TITLES:
            check(any(expected.lower() in (t or "").lower() for t in titles),
                  f"panel present with title containing {expected!r}")

        # Every panel/target datasource uid must equal the prometheus datasource uid.
        uids = _all_datasource_uids(dash)
        check(len(uids) > 0, "api-health.json declares datasource references")
        mismatches = [u for u in uids if u != PROM_DATASOURCE_UID]
        check(not mismatches,
              f"all datasource uids match {PROM_DATASOURCE_UID!r} "
              f"({'OK' if not mismatches else 'mismatches: ' + repr(mismatches)})")

    # ---- prometheus.yml scrape config ---------------------------------------
    print("\ninfra/prometheus.yml:")
    prom_doc = None
    prom_text = ""
    if check(os.path.isfile(PROMETHEUS_CONFIG), "prometheus.yml exists"):
        prom_text = open(PROMETHEUS_CONFIG, "r", encoding="utf-8").read()
        prom_doc = load_yaml(PROMETHEUS_CONFIG)
    if prom_doc is not None:
        scrape_interval = (prom_doc.get("global") or {}).get("scrape_interval")
        check(scrape_interval == "5s",
              f"global.scrape_interval is '5s' (got {scrape_interval!r})")
        # Collect all target strings across scrape_configs.
        all_targets = []
        for sc in (prom_doc.get("scrape_configs") or []):
            for static in (sc.get("static_configs") or []):
                all_targets.extend(static.get("targets") or [])
        check(any("scramblecoin-api:5001" in t for t in all_targets),
              f"has a scrape target containing 'scramblecoin-api:5001' (got {all_targets})")
        check(any("host.docker.internal:5001" in t for t in all_targets),
              f"has a scrape target containing 'host.docker.internal:5001' (got {all_targets})")
    else:
        # PyYAML unavailable — fall back to textual checks.
        check(re.search(r"scrape_interval:\s*5s", prom_text) is not None,
              "global.scrape_interval is '5s' (text check)")
        check("scramblecoin-api:5001" in prom_text,
              "has a scrape target containing 'scramblecoin-api:5001' (text check)")
        check("host.docker.internal:5001" in prom_text,
              "has a scrape target containing 'host.docker.internal:5001' (text check)")

    # ---- docker-compose.yml: prometheus + scramblecoin-api services ----------
    print("\ndocker-compose.yml (issue #80 services):")
    if os.path.isfile(COMPOSE):
        compose_text = open(COMPOSE, "r", encoding="utf-8").read()
        compose_doc = load_yaml(COMPOSE)
    else:
        compose_text, compose_doc = "", None
    if compose_doc is not None:
        services = (compose_doc.get("services") or {})
        check("prometheus" in services, "declares a 'prometheus' service")
        prom_svc = services.get("prometheus") or {}
        check("prom/prometheus" in str(prom_svc.get("image", "")),
              f"prometheus service uses 'prom/prometheus' image (got {prom_svc.get('image')!r})")
        check("scramblecoin-api" in services, "declares a 'scramblecoin-api' service")
    else:
        check(re.search(r"^\s{2}prometheus:", compose_text, re.MULTILINE) is not None,
              "declares a 'prometheus' service (text check)")
        check("prom/prometheus" in compose_text,
              "prometheus service uses 'prom/prometheus' image (text check)")
        check(re.search(r"^\s{2}scramblecoin-api:", compose_text, re.MULTILINE) is not None,
              "declares a 'scramblecoin-api' service (text check)")


def validate_issue_81():
    """Assert the Loki + Promtail log-aggregation artifacts (issue #81)."""
    print("\n== Loki + Promtail log aggregation validation (issue #81) ==")

    # ---- Loki datasource ----------------------------------------------------
    print("\ndatasources/loki.yaml:")
    loki_ds_uid = None
    loki_ds0 = None
    if check(os.path.isfile(LOKI_DATASOURCE), "loki.yaml exists"):
        ds_doc = load_yaml(LOKI_DATASOURCE)
        if ds_doc is not None:
            check(isinstance(ds_doc, dict) and "datasources" in ds_doc,
                  "loki.yaml parses as YAML with a 'datasources' list")
            try:
                loki_ds0 = ds_doc["datasources"][0]
                loki_ds_uid = loki_ds0.get("uid")
                check(loki_ds0.get("type") == "loki",
                      f"datasource type is 'loki' (got {loki_ds0.get('type')!r})")
                check(loki_ds0.get("url") == LOKI_DATASOURCE_URL,
                      f"datasource url is {LOKI_DATASOURCE_URL!r} (got {loki_ds0.get('url')!r})")
            except (KeyError, IndexError, TypeError):
                loki_ds_uid = None
        else:
            loki_ds_uid = datasource_uid_via_regex(LOKI_DATASOURCE)
            check(loki_ds_uid is not None, "loki.yaml uid readable (regex fallback)")
            loki_text = open(LOKI_DATASOURCE, "r", encoding="utf-8").read()
            check("type: loki" in loki_text, "datasource type is 'loki' (text check)")
            check(LOKI_DATASOURCE_URL in loki_text,
                  f"datasource url is {LOKI_DATASOURCE_URL!r} (text check)")
    check(loki_ds_uid == LOKI_DATASOURCE_UID,
          f"datasource uid is {LOKI_DATASOURCE_UID!r} (got {loki_ds_uid!r})")

    # ---- logs.json dashboard ------------------------------------------------
    print("\ndashboards/logs.json:")
    dash = None
    if check(os.path.isfile(LOGS_DASHBOARD), "logs.json exists"):
        try:
            dash = json.load(open(LOGS_DASHBOARD, "r", encoding="utf-8"))
            check(True, "logs.json is valid JSON")
        except json.JSONDecodeError as exc:
            check(False, f"logs.json is valid JSON ({exc})")

    if dash is not None:
        check(bool(dash.get("uid")),
              f"dashboard has a fixed top-level uid (got {dash.get('uid')!r})")

        panels = dash.get("panels", []) or []
        check(len(panels) >= 2,
              f"dashboard has at least 2 panels (got {len(panels)})")

        panel_types = [p.get("type") for p in panels]
        check("logs" in panel_types,
              f"dashboard has a 'logs'-type panel (got types {panel_types})")
        check(any(t in ("barchart", "timeseries") for t in panel_types),
              f"dashboard has a bar/timeseries volume panel (got types {panel_types})")

        # Every panel/target datasource uid must equal the Loki datasource uid
        # (a uid mismatch yields a blank panel).
        uids = _all_datasource_uids(dash)
        check(len(uids) > 0, "logs.json declares datasource references")
        mismatches = [u for u in uids if u != LOKI_DATASOURCE_UID]
        check(not mismatches,
              f"all datasource uids match {LOKI_DATASOURCE_UID!r} "
              f"({'OK' if not mismatches else 'mismatches: ' + repr(mismatches)})")

        # Templating: a 'job' variable and a free-text 'search' variable.
        tmpl = ((dash.get("templating") or {}).get("list")) or []
        tmpl_names = [v.get("name") for v in tmpl]
        check("job" in tmpl_names,
              f"templating contains a 'job' variable (got {tmpl_names})")
        search_var = next((v for v in tmpl if v.get("name") == "search"), None)
        check(search_var is not None,
              f"templating contains a 'search' variable (got {tmpl_names})")
        if search_var is not None:
            check(search_var.get("type") == "textbox",
                  f"'search' variable is free-text (type 'textbox', got {search_var.get('type')!r})")

    # ---- promtail config ----------------------------------------------------
    print("\ninfra/promtail/config.yml:")
    pt_doc = None
    pt_text = ""
    if check(os.path.isfile(PROMTAIL_CONFIG), "promtail config.yml exists"):
        pt_text = open(PROMTAIL_CONFIG, "r", encoding="utf-8").read()
        pt_doc = load_yaml(PROMTAIL_CONFIG)
    if pt_doc is not None:
        clients = pt_doc.get("clients") or []
        client_url = clients[0].get("url") if clients else None
        check(client_url == PROMTAIL_PUSH_URL,
              f"clients[0].url is {PROMTAIL_PUSH_URL!r} (got {client_url!r})")

        scrape_configs = pt_doc.get("scrape_configs") or []
        check(len(scrape_configs) == 2,
              f"exactly two scrape_configs (got {len(scrape_configs)})")

        # Index scrape configs by job label.
        jobs = {}
        for sc in scrape_configs:
            for static in (sc.get("static_configs") or []):
                labels = static.get("labels") or {}
                job = labels.get("job")
                if job:
                    jobs[job] = (sc, labels)
        check("scramblecoin-api" in jobs,
              f"has a scrape job labelled 'scramblecoin-api' (got {sorted(jobs)})")
        check("scramblecoin-web" in jobs,
              f"has a scrape job labelled 'scramblecoin-web' (got {sorted(jobs)})")

        expected_paths = {
            "scramblecoin-api": "scramblecoin-api-*.log",
            "scramblecoin-web": "scramblecoin-web-*.log",
        }
        for job, suffix in expected_paths.items():
            if job in jobs:
                sc, labels = jobs[job]
                path = labels.get("__path__", "")
                check(path.endswith(suffix),
                      f"{job} __path__ ends with {suffix!r} (got {path!r})")

                stages = sc.get("pipeline_stages") or []
                stage_keys = [k for st in stages if isinstance(st, dict) for k in st.keys()]
                check("json" in stage_keys,
                      f"{job} has a 'json' pipeline stage (got {stage_keys})")

                labels_stage = next((st.get("labels") for st in stages
                                     if isinstance(st, dict) and "labels" in st), None) or {}
                check("level" in labels_stage,
                      f"{job} labels stage promotes 'level' (got {list(labels_stage)})")
                # High-cardinality fields must NOT be promoted to labels.
                check("game_id" not in labels_stage and "GameId" not in labels_stage,
                      f"{job} labels stage does NOT promote high-cardinality game_id "
                      f"(got {list(labels_stage)})")
    else:
        # PyYAML unavailable — fall back to textual checks.
        check(PROMTAIL_PUSH_URL in pt_text,
              f"clients url contains {PROMTAIL_PUSH_URL!r} (text check)")
        check("job: scramblecoin-api" in pt_text,
              "has a scrape job labelled 'scramblecoin-api' (text check)")
        check("job: scramblecoin-web" in pt_text,
              "has a scrape job labelled 'scramblecoin-web' (text check)")
        check("scramblecoin-api-*.log" in pt_text,
              "api __path__ ends with 'scramblecoin-api-*.log' (text check)")
        check("scramblecoin-web-*.log" in pt_text,
              "web __path__ ends with 'scramblecoin-web-*.log' (text check)")

    # ---- docker-compose.yml: loki + promtail services -----------------------
    print("\ndocker-compose.yml (issue #81 services):")
    if os.path.isfile(COMPOSE):
        compose_text = open(COMPOSE, "r", encoding="utf-8").read()
        compose_doc = load_yaml(COMPOSE)
    else:
        compose_text, compose_doc = "", None
    if compose_doc is not None:
        services = (compose_doc.get("services") or {})
        volumes = (compose_doc.get("volumes") or {})

        check("loki" in services, "declares a 'loki' service")
        loki_svc = services.get("loki") or {}
        check("grafana/loki" in str(loki_svc.get("image", "")),
              f"loki service uses 'grafana/loki' image (got {loki_svc.get('image')!r})")

        check("promtail" in services, "declares a 'promtail' service")
        promtail_svc = services.get("promtail") or {}
        check("grafana/promtail" in str(promtail_svc.get("image", "")),
              f"promtail service uses 'grafana/promtail' image (got {promtail_svc.get('image')!r})")

        check("api-logs" in volumes, "declares the top-level 'api-logs' volume")

        # scramblecoin-api mounts the api-logs volume at /app/logs
        api_svc = services.get("scramblecoin-api") or {}
        api_mounts = [str(m) for m in (api_svc.get("volumes") or [])]
        check(any("api-logs:/app/logs" in m for m in api_mounts),
              f"scramblecoin-api mounts 'api-logs:/app/logs' (got {api_mounts})")

        # promtail mounts: same api-logs volume (read-only), web bind, and config
        pt_mounts = [str(m) for m in (promtail_svc.get("volumes") or [])]
        check(any(m.startswith("api-logs:") and m.endswith(":ro") for m in pt_mounts),
              f"promtail mounts the 'api-logs' volume read-only (got {pt_mounts})")
        check(any("./src/ScrambleCoin.Web/logs" in m for m in pt_mounts),
              f"promtail binds './src/ScrambleCoin.Web/logs' (got {pt_mounts})")
        check(any("infra/promtail/config.yml" in m for m in pt_mounts),
              f"promtail mounts its config file (got {pt_mounts})")

        # depends_on relationships
        def _depends(svc):
            dep = svc.get("depends_on")
            if isinstance(dep, dict):
                return list(dep.keys())
            if isinstance(dep, list):
                return dep
            return []
        check("loki" in _depends(promtail_svc),
              f"promtail depends_on 'loki' (got {_depends(promtail_svc)})")
        grafana_svc = services.get("grafana") or {}
        check("loki" in _depends(grafana_svc),
              f"grafana depends_on includes 'loki' (got {_depends(grafana_svc)})")
    else:
        check(re.search(r"^\s{2}loki:", compose_text, re.MULTILINE) is not None,
              "declares a 'loki' service (text check)")
        check("grafana/loki" in compose_text,
              "loki service uses 'grafana/loki' image (text check)")
        check(re.search(r"^\s{2}promtail:", compose_text, re.MULTILINE) is not None,
              "declares a 'promtail' service (text check)")
        check("grafana/promtail" in compose_text,
              "promtail service uses 'grafana/promtail' image (text check)")
        check("api-logs" in compose_text, "declares the 'api-logs' volume (text check)")

    # ---- infra/README.md documents the GameId LogQL query -------------------
    print("\ninfra/README.md:")
    if check(os.path.isfile(INFRA_README), "infra/README.md exists"):
        readme_text = open(INFRA_README, "r", encoding="utf-8").read()
        check(README_GAMEID_QUERY in readme_text,
              f"documents the GameId LogQL query {README_GAMEID_QUERY!r}")

    # ---- CI workflow includes infra/promtail in its paths filter ------------
    print("\n.github/workflows/infra-validate.yml:")
    if check(os.path.isfile(CI_WORKFLOW), "infra-validate.yml exists"):
        ci_text = open(CI_WORKFLOW, "r", encoding="utf-8").read()
        check("infra/promtail/**" in ci_text,
              "paths filter includes 'infra/promtail/**'")


def validate_issue_132():
    """Assert the Azure Container Apps full-stack deploy artifacts (issue #132)."""
    print("\n== Azure Container Apps deploy validation (issue #132) ==")

    # ---- aca.bicep: resources + container apps --------------------------------
    print("\ninfra/aca.bicep:")
    bicep_text = ""
    if check(os.path.isfile(ACA_BICEP), "aca.bicep exists"):
        bicep_text = open(ACA_BICEP, "r", encoding="utf-8").read()
        check("Microsoft.ContainerRegistry/registries" in bicep_text,
              "aca.bicep declares an ACR (Microsoft.ContainerRegistry/registries)")
        check("Microsoft.App/managedEnvironments" in bicep_text,
              "aca.bicep declares a managed environment (Microsoft.App/managedEnvironments)")
        check("Microsoft.App/containerApps" in bicep_text,
              "aca.bicep declares container apps (Microsoft.App/containerApps)")
        for app in ACA_CONTAINER_APP_NAMES:
            check(f"name: '{app}'" in bicep_text,
                  f"aca.bicep defines a container app named {app!r}")

        # ---- Critical regression guard: port-less internal service URLs ------
        # The review-fixed bug was internal URLs carrying the container port
        # (ACA internal DNS resolves by app name, ingress maps the port).
        check("'http://loki'" in bicep_text,
              "aca.bicep uses port-less Loki URL ('http://loki')")
        check("'http://prometheus'" in bicep_text,
              "aca.bicep uses port-less Prometheus URL ('http://prometheus')")
        check("http://loki:3100" not in bicep_text,
              "aca.bicep does NOT use ported Loki URL ('http://loki:3100') [regression guard]")
        check("http://prometheus:9090" not in bicep_text,
              "aca.bicep does NOT use ported Prometheus URL ('http://prometheus:9090') [regression guard]")

    # ---- aca.parameters.json: valid JSON --------------------------------------
    print("\ninfra/aca.parameters.json:")
    if check(os.path.isfile(ACA_PARAMS), "aca.parameters.json exists"):
        try:
            json.load(open(ACA_PARAMS, "r", encoding="utf-8"))
            check(True, "aca.parameters.json is valid JSON")
        except json.JSONDecodeError as exc:
            check(False, f"aca.parameters.json is valid JSON ({exc})")

    # ---- prometheus.cloud.yml: port-less api scrape target --------------------
    print("\ninfra/prometheus/prometheus.cloud.yml:")
    if check(os.path.isfile(PROM_CLOUD_CONFIG), "prometheus.cloud.yml exists"):
        prom_doc = load_yaml(PROM_CLOUD_CONFIG)
        prom_text = open(PROM_CLOUD_CONFIG, "r", encoding="utf-8").read()
        if prom_doc is not None:
            check(isinstance(prom_doc, dict) and "scrape_configs" in prom_doc,
                  "prometheus.cloud.yml parses as YAML with 'scrape_configs'")
            jobs = prom_doc.get("scrape_configs") or []
            api_job = next((j for j in jobs if j.get("job_name") == "scramblecoin-api"), None)
            check(api_job is not None,
                  "prometheus.cloud.yml has a 'scramblecoin-api' scrape job")
            if api_job is not None:
                check(api_job.get("metrics_path") == "/metrics",
                      f"api scrape job metrics_path is '/metrics' (got {api_job.get('metrics_path')!r})")
                targets = []
                for sc in (api_job.get("static_configs") or []):
                    targets.extend(sc.get("targets") or [])
                check("scramblecoin-api" in targets,
                      f"api scrape target is port-less 'scramblecoin-api' (got {targets})")
                check(not any(":" in t for t in targets),
                      f"api scrape targets carry no port (got {targets})")
        else:
            check("job_name: scramblecoin-api" in prom_text,
                  "prometheus.cloud.yml targets 'scramblecoin-api' (text check)")
            check("metrics_path: /metrics" in prom_text,
                  "prometheus.cloud.yml uses metrics_path /metrics (text check)")
            check("'scramblecoin-api'" in prom_text or "scramblecoin-api']" in prom_text,
                  "prometheus.cloud.yml scrape target present (text check)")

    # ---- Cloud Grafana datasources -------------------------------------------
    print("\ninfra/grafana/provisioning-cloud/datasources/:")

    # loki.yaml
    if check(os.path.isfile(CLOUD_LOKI_DS), "cloud loki.yaml exists"):
        loki_text = open(CLOUD_LOKI_DS, "r", encoding="utf-8").read()
        loki_doc = load_yaml(CLOUD_LOKI_DS)
        if loki_doc is not None:
            ds0 = (loki_doc.get("datasources") or [{}])[0]
            check(ds0.get("type") == "loki",
                  f"cloud loki datasource type is 'loki' (got {ds0.get('type')!r})")
            check(ds0.get("url") == "${LOKI_URL}",
                  f"cloud loki datasource url is '${{LOKI_URL}}' (got {ds0.get('url')!r})")
        else:
            check("type: loki" in loki_text, "cloud loki type is 'loki' (text check)")
            check("${LOKI_URL}" in loki_text, "cloud loki url uses ${LOKI_URL} (text check)")

    # prometheus.yaml
    if check(os.path.isfile(CLOUD_PROM_DS), "cloud prometheus.yaml exists"):
        prom_ds_text = open(CLOUD_PROM_DS, "r", encoding="utf-8").read()
        prom_ds_doc = load_yaml(CLOUD_PROM_DS)
        if prom_ds_doc is not None:
            ds0 = (prom_ds_doc.get("datasources") or [{}])[0]
            check(ds0.get("type") == "prometheus",
                  f"cloud prometheus datasource type is 'prometheus' (got {ds0.get('type')!r})")
            check(ds0.get("url") == "${PROMETHEUS_URL}",
                  f"cloud prometheus datasource url is '${{PROMETHEUS_URL}}' (got {ds0.get('url')!r})")
        else:
            check("type: prometheus" in prom_ds_text, "cloud prometheus type is 'prometheus' (text check)")
            check("${PROMETHEUS_URL}" in prom_ds_text, "cloud prometheus url uses ${PROMETHEUS_URL} (text check)")

    # mssql.yaml
    if check(os.path.isfile(CLOUD_MSSQL_DS), "cloud mssql.yaml exists"):
        mssql_text = open(CLOUD_MSSQL_DS, "r", encoding="utf-8").read()
        mssql_doc = load_yaml(CLOUD_MSSQL_DS)
        if mssql_doc is not None:
            ds0 = (mssql_doc.get("datasources") or [{}])[0]
            check(ds0.get("type") == "mssql",
                  f"cloud mssql datasource type is 'mssql' (got {ds0.get('type')!r})")
            check(ds0.get("uid") == CLOUD_MSSQL_UID,
                  f"cloud mssql datasource uid is {CLOUD_MSSQL_UID!r} (got {ds0.get('uid')!r})")
            check(str(ds0.get("url", "")).startswith("${"),
                  f"cloud mssql url is env-var-driven (got {ds0.get('url')!r})")
            check(str(ds0.get("user", "")).startswith("${"),
                  f"cloud mssql user is env-var-driven (got {ds0.get('user')!r})")
            pw = ((ds0.get("secureJsonData") or {}).get("password"))
            check(str(pw or "").startswith("${"),
                  f"cloud mssql password is env-var-driven (got {pw!r})")
            encrypt = ((ds0.get("jsonData") or {}).get("encrypt"))
            check(str(encrypt) == "true",
                  f"cloud mssql jsonData.encrypt is 'true' (got {encrypt!r})")
        else:
            check("type: mssql" in mssql_text, "cloud mssql type is 'mssql' (text check)")
            check(f"uid: {CLOUD_MSSQL_UID}" in mssql_text, "cloud mssql uid (text check)")
            check("${AZURE_SQL_HOST}" in mssql_text, "cloud mssql url uses ${AZURE_SQL_HOST} (text check)")
            check("${AZURE_SQL_USER}" in mssql_text, "cloud mssql user uses ${AZURE_SQL_USER} (text check)")
            check("${AZURE_SQL_PASSWORD}" in mssql_text, "cloud mssql password uses ${AZURE_SQL_PASSWORD} (text check)")
            check("encrypt: 'true'" in mssql_text, "cloud mssql encrypt is 'true' (text check)")

    # No hardcoded local hostnames in any cloud datasource.
    cloud_ds_combined = ""
    for p in (CLOUD_LOKI_DS, CLOUD_PROM_DS, CLOUD_MSSQL_DS):
        if os.path.isfile(p):
            cloud_ds_combined += open(p, "r", encoding="utf-8").read()
    check("localhost" not in cloud_ds_combined,
          "no hardcoded 'localhost' in cloud datasources")
    check("scramblecoin-sqlserver" not in cloud_ds_combined,
          "no hardcoded 'scramblecoin-sqlserver' hostname in cloud datasources")

    # ---- Config-baking Dockerfiles -------------------------------------------
    print("\nobservability + web Dockerfiles:")
    check(os.path.isfile(LOKI_DOCKERFILE), "infra/loki/Dockerfile exists")
    check(os.path.isfile(PROM_DOCKERFILE), "infra/prometheus/Dockerfile exists")
    check(os.path.isfile(GRAFANA_DOCKERFILE), "infra/grafana/Dockerfile exists")
    check(os.path.isfile(WEB_DOCKERFILE), "src/ScrambleCoin.Web/Dockerfile exists")

    # ---- deploy-aca.yml workflow ---------------------------------------------
    print("\n.github/workflows/deploy-aca.yml:")
    if check(os.path.isfile(DEPLOY_ACA_WORKFLOW), "deploy-aca.yml exists"):
        deploy_text = open(DEPLOY_ACA_WORKFLOW, "r", encoding="utf-8").read()
        deploy_doc = load_yaml(DEPLOY_ACA_WORKFLOW)
        if deploy_doc is not None:
            # PyYAML parses the bare `on:` key as boolean True.
            on_block = deploy_doc.get("on", deploy_doc.get(True))
            check(isinstance(on_block, dict) and "workflow_dispatch" in on_block,
                  "deploy-aca.yml is triggered by workflow_dispatch")
        else:
            check("workflow_dispatch:" in deploy_text,
                  "deploy-aca.yml is triggered by workflow_dispatch (text check)")
        check("az acr build" in deploy_text,
              "deploy-aca.yml builds images via 'az acr build'")
        check("dotnet ef database update" in deploy_text,
              "deploy-aca.yml runs an EF Core migration step")

    # ---- aca-validate.yml CI workflow ----------------------------------------
    print("\n.github/workflows/aca-validate.yml:")
    if check(os.path.isfile(ACA_VALIDATE_WORKFLOW), "aca-validate.yml exists"):
        acaval_text = open(ACA_VALIDATE_WORKFLOW, "r", encoding="utf-8").read()
        acaval_doc = load_yaml(ACA_VALIDATE_WORKFLOW)
        check(acaval_doc is not None, "aca-validate.yml is valid YAML")
        if acaval_doc is not None:
            on_block = acaval_doc.get("on", acaval_doc.get(True))
            check(isinstance(on_block, dict) and "pull_request" in on_block,
                  "aca-validate.yml is triggered on pull_request")
        check("az bicep build" in acaval_text,
              "aca-validate.yml runs 'az bicep build'")


def main():
    print("== tournament dashboard provisioning validation (issue #79) ==\n")

    # ---- Invariant 4: .env.example -------------------------------------------
    print(".env.example:")
    if check(os.path.isfile(ENV_EXAMPLE), ".env.example exists"):
        env_text = open(ENV_EXAMPLE, "r", encoding="utf-8").read()
    else:
        env_text = ""
    check(re.search(r"^\s*MSSQL_SA_PASSWORD\s*=", env_text, re.MULTILINE) is not None,
          ".env.example defines MSSQL_SA_PASSWORD")
    check(re.search(r"^\s*GRAFANA_ADMIN_PASSWORD\s*=", env_text, re.MULTILINE) is not None,
          ".env.example defines GRAFANA_ADMIN_PASSWORD")

    # ---- Invariant 3: docker-compose.yml -------------------------------------
    print("\ndocker-compose.yml:")
    if check(os.path.isfile(COMPOSE), "docker-compose.yml exists"):
        compose_text = open(COMPOSE, "r", encoding="utf-8").read()
        compose_doc = load_yaml(COMPOSE)
    else:
        compose_text, compose_doc = "", None
    check(PLAINTEXT_SA_PASSWORD not in compose_text,
          f"no hard-coded plaintext SA password ({PLAINTEXT_SA_PASSWORD!r})")
    check("${MSSQL_SA_PASSWORD}" in compose_text,
          "SA password is injected via ${MSSQL_SA_PASSWORD}")
    if compose_doc is not None:
        services = (compose_doc.get("services") or {})
        volumes = (compose_doc.get("volumes") or {})
        check("grafana" in services, "declares a 'grafana' service")
        check("sqlserver" in services, "declares a 'sqlserver' service")
        check("grafana-data" in volumes, "declares the 'grafana-data' volume")
    else:
        # PyYAML unavailable — fall back to textual checks.
        check(re.search(r"^\s{2}grafana:", compose_text, re.MULTILINE) is not None,
              "declares a 'grafana' service (text check)")
        check("grafana-data" in compose_text, "declares the 'grafana-data' volume (text check)")

    # ---- Datasource uid (needed for invariant 2) -----------------------------
    print("\ndatasources/mssql.yaml:")
    ds_uid = None
    if check(os.path.isfile(DATASOURCE), "mssql.yaml exists"):
        ds_doc = load_yaml(DATASOURCE)
        if ds_doc is not None:
            check(isinstance(ds_doc, dict) and "datasources" in ds_doc,
                  "mssql.yaml parses as YAML with a 'datasources' list")
            try:
                ds_uid = ds_doc["datasources"][0]["uid"]
            except (KeyError, IndexError, TypeError):
                ds_uid = None
        else:
            ds_uid = datasource_uid_via_regex(DATASOURCE)
            check(ds_uid is not None, "mssql.yaml uid readable (regex fallback)")
    check(bool(ds_uid), f"datasource declares a uid (got {ds_uid!r})")

    # ---- dashboards.yaml provider parses -------------------------------------
    print("\ndashboards/dashboards.yaml:")
    if check(os.path.isfile(DASHBOARD_PROVIDER), "dashboards.yaml exists"):
        prov_doc = load_yaml(DASHBOARD_PROVIDER)
        if prov_doc is not None:
            check(isinstance(prov_doc, dict) and "providers" in prov_doc,
                  "dashboards.yaml parses as YAML with a 'providers' list")
        else:
            print("  SKIP: PyYAML unavailable — provider YAML parse not strictly checked")

    # ---- Invariants 1 & 2: tournament.json -----------------------------------
    print("\ndashboards/tournament.json:")
    dash = None
    if check(os.path.isfile(DASHBOARD), "tournament.json exists"):
        try:
            dash = json.load(open(DASHBOARD, "r", encoding="utf-8"))
            check(True, "tournament.json is valid JSON")
        except json.JSONDecodeError as exc:
            check(False, f"tournament.json is valid JSON ({exc})")

    if dash is not None:
        check(dash.get("refresh") == "5s",
              f"dashboard refresh is '5s' (got {dash.get('refresh')!r})")
        check(bool(dash.get("uid")),
              f"dashboard has a fixed top-level uid (got {dash.get('uid')!r})")

        panels = dash.get("panels", []) or []
        titles = [p.get("title", "") for p in panels]
        check(len(panels) == 4,
              f"dashboard has exactly 4 panels (got {len(panels)}: {titles})")
        for expected in EXPECTED_PANEL_TITLES:
            check(any(expected.lower() in (t or "").lower() for t in titles),
                  f"panel present with title containing {expected!r}")

        # Invariant 2: every panel/target datasource uid matches the datasource uid.
        mismatches = []
        checked_refs = 0

        def _uid_of(ds):
            if isinstance(ds, dict):
                return ds.get("uid")
            return ds  # may be a string or template var

        for p in panels:
            for ref_label, ds in [("panel", p.get("datasource"))] + \
                    [("target", t.get("datasource")) for t in (p.get("targets") or [])]:
                if ds is None:
                    continue
                uid = _uid_of(ds)
                checked_refs += 1
                if uid != ds_uid:
                    mismatches.append(f"{p.get('title')!r}/{ref_label}: {uid!r}")
        check(checked_refs > 0, "tournament.json declares datasource references")
        check(not mismatches,
              f"all datasource uids match {ds_uid!r} "
              f"({'OK' if not mismatches else 'mismatches: ' + '; '.join(mismatches)})")

    # ---- Issue #80: Prometheus API observability -----------------------------
    validate_issue_80()

    # ---- Issue #81: Loki + Promtail log aggregation --------------------------
    validate_issue_81()

    # ---- Issue #132: Azure Container Apps full-stack deploy ------------------
    validate_issue_132()

    # ---- Summary -------------------------------------------------------------
    print("\n" + "=" * 60)
    if _failures:
        print(f"RESULT: FAILED — {len(_failures)} of {_checks} checks failed:")
        for f in _failures:
            print(f"  - {f}")
        return 1
    print(f"RESULT: OK — all {_checks} checks passed.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
