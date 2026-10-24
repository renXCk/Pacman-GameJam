#!/usr/bin/env python3
"""Check that Packages/manifest.json only uses Unity Registry packages.

Game Jam rule: only Unity Registry packages are allowed. This fails on any
dependency that points at a git URL, a local file: path or a tarball, and on
any scoped registry. Anything that is not com.unity.* is listed so a person
can check it. Standard library only, so it runs anywhere Python 3 runs.

Usage: python tools/check-packages.py [path/to/manifest.json]
Exit code 0 = pass, 1 = fail.
"""

import json
import re
import sys
from pathlib import Path

DEFAULT_MANIFEST = Path(__file__).resolve().parent.parent / "Packages" / "manifest.json"

# A registry version looks like 1.2.3, 1.2.3-pre.4 or 2.0.0-exp.1.
REGISTRY_VERSION = re.compile(r"^\d+\.\d+\.\d+([-+][0-9A-Za-z.\-]+)?$")


def classify(version: str) -> str | None:
    """Return why a version string is not a registry version, or None if it is fine."""
    lowered = version.lower()
    if lowered.startswith("file:"):
        return "local file: path"
    if lowered.endswith((".tgz", ".tar.gz")):
        return "tarball"
    if lowered.startswith(("git+", "git:", "ssh:", "http:", "https:")) or ".git" in lowered or "github.com" in lowered:
        return "git or URL dependency"
    if not REGISTRY_VERSION.match(version):
        return f"not a registry version ({version})"
    return None


def main() -> int:
    manifest_path = Path(sys.argv[1]) if len(sys.argv) > 1 else DEFAULT_MANIFEST
    try:
        manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as error:
        print(f"FAIL: cannot read {manifest_path}: {error}")
        return 1

    problems = []
    not_unity = []

    for registry in manifest.get("scopedRegistries", []):
        name = registry.get("name", "?")
        url = registry.get("url", "?")
        problems.append(f"scoped registry '{name}' ({url})")

    dependencies = manifest.get("dependencies", {})
    for package, version in sorted(dependencies.items()):
        reason = classify(str(version))
        if reason:
            problems.append(f"{package}: {reason}")
        if not package.startswith("com.unity."):
            not_unity.append(f"{package} {version}")

    print(f"Checked {len(dependencies)} dependencies in {manifest_path}")

    if not_unity:
        print("Not com.unity.* (check each one is on the Unity Registry):")
        for line in not_unity:
            print(f"  - {line}")

    if problems:
        print("FAIL: non-registry packages found:")
        for line in problems:
            print(f"  - {line}")
        return 1

    print("PASS: every package comes from the Unity Registry.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
