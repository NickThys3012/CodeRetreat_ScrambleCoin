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

EXPECTED_PANEL_TITLES = [
    "Active Games",
    "Games by Status",
    "Bot Leaderboard",
    "Games Completed per Minute",
]
PLAINTEXT_SA_PASSWORD = "ScrambleCoin_Dev!2024"

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
