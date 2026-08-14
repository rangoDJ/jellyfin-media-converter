#!/usr/bin/env python3
"""Add or replace a version entry in manifest.json, sourced from build.yaml.

Usage: update_manifest.py <manifest.json> <build.yaml> <zip_path> <source_url>
"""

import hashlib
import json
import sys
from datetime import datetime, timezone
from pathlib import Path

import yaml


def md5_of(path: Path) -> str:
    digest = hashlib.md5()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(65536), b""):
            digest.update(chunk)
    return digest.hexdigest()


def main() -> None:
    manifest_path, build_yaml_path, zip_path, source_url = (Path(sys.argv[1]), Path(sys.argv[2]), Path(sys.argv[3]), sys.argv[4])

    with build_yaml_path.open("r", encoding="utf-8") as handle:
        build = yaml.safe_load(handle)

    manifest = json.loads(manifest_path.read_text(encoding="utf-8")) if manifest_path.exists() else []

    plugin_entry = next((entry for entry in manifest if entry.get("guid") == build["guid"]), None)
    if plugin_entry is None:
        plugin_entry = {
            "guid": build["guid"],
            "name": build["name"],
            "description": build.get("description", "").strip(),
            "overview": build.get("overview", ""),
            "owner": build.get("owner", ""),
            "category": build.get("category", "General"),
            "versions": [],
        }
        manifest.append(plugin_entry)
    else:
        plugin_entry["name"] = build["name"]
        plugin_entry["description"] = build.get("description", "").strip()
        plugin_entry["overview"] = build.get("overview", "")
        plugin_entry["owner"] = build.get("owner", "")
        plugin_entry["category"] = build.get("category", "General")

    version_entry = {
        "version": build["version"],
        "changelog": build.get("changelog", "").strip(),
        "targetAbi": build["targetAbi"],
        "sourceUrl": source_url,
        "checksum": md5_of(zip_path),
        "timestamp": datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ"),
    }

    plugin_entry["versions"] = [v for v in plugin_entry["versions"] if v["version"] != version_entry["version"]]
    plugin_entry["versions"].insert(0, version_entry)

    manifest_path.write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")


if __name__ == "__main__":
    main()
