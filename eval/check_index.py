"""Check shared skill routes and evaluation inventory without invoking an agent."""

import json
from pathlib import Path
import re
import sys


def check(eval_dir):
    agents = eval_dir.parent / "agents"
    available = {p.name for p in (agents / "skills").iterdir() if p.is_dir()}
    routes = set()
    errors = []
    for line in (agents / "README.md").read_text(encoding="utf-8").splitlines():
        cells = line.strip().strip("|").split("|")
        if not line.startswith("|") or len(cells) != 2:
            continue
        names = cells[1].strip()
        if names in {"Skill name", "---"}:
            continue
        routes.update(re.sub(r"\s+\([^()]*\)$", "", names).split(" / "))
    for name in sorted(routes | available):
        if name not in routes or not (agents / "skills" / name / "SKILL.md").is_file():
            errors.append(f"Missing route or SKILL.md: {name}")

    ids = set()
    targets = json.loads((eval_dir / "targets.json").read_text(encoding="utf-8"))
    for target in targets:
        key = target["id"]
        if key in ids:
            errors.append(f"Duplicate target: {key}")
        ids.add(key)
        for field in ("skill", "runbook"):
            path = target[field]
            if field == "runbook" and path is None:
                continue
            if not path or not (eval_dir / path).is_file():
                errors.append(f"{key}: missing {field}: {path}")
    if errors:
        sys.exit("\n".join(errors))
    print(f"Verified {len(routes)} skill routes and {len(ids)} eval targets.")


if __name__ == "__main__":
    check(Path(__file__).resolve().parent)
