"""Exercise the inventory CLI against disposable repository layouts."""

import json
from pathlib import Path
import shutil
import subprocess
import sys
from tempfile import TemporaryDirectory
import unittest


class CheckIndexTests(unittest.TestCase):
    def setUp(self):
        workspace = TemporaryDirectory()
        self.addCleanup(workspace.cleanup)
        root = Path(workspace.name)
        self.agents = root / "agents"
        self.evals = root / "eval"
        self.evals.mkdir()
        self.checker = self.evals / "check_index.py"
        shutil.copyfile(Path(__file__).with_name("check_index.py"), self.checker)
        self.write_route("sample")
        self.skill = self.agents / "skills" / "sample" / "SKILL.md"
        self.skill.parent.mkdir(parents=True)
        self.skill.write_text("# Sample skill\n", encoding="utf-8")
        self.runbook = self.evals / "case.md"
        self.runbook.write_text("# Sample case\n", encoding="utf-8")
        self.target = {
            "id": "sample-target",
            "skill": "../agents/skills/sample/SKILL.md",
            "runbook": "case.md",
        }
        self.write_targets(self.target)

    def write_route(self, skill):
        self.agents.mkdir(exist_ok=True)
        (self.agents / "README.md").write_text(f"| Sample task | {skill} |\n", encoding="utf-8")

    def write_targets(self, *targets):
        (self.evals / "targets.json").write_text(json.dumps(targets), encoding="utf-8")

    def invoke(self):
        return subprocess.run(
            [sys.executable, str(self.checker)],
            cwd=self.evals,
            capture_output=True,
            text=True,
            encoding="utf-8",
            timeout=15,
        )

    def assert_failure(self, *messages):
        result = self.invoke()
        self.assertEqual(result.returncode, 1)
        self.assertEqual(result.stdout, "")
        self.assertCountEqual(result.stderr.splitlines(), messages)

    def test_valid_inventory(self):
        result = self.invoke()
        self.assertEqual(result.returncode, 0)
        self.assertEqual(result.stderr, "")
        self.assertEqual(result.stdout.strip(), "Verified 1 skill routes and 1 eval targets.")

    def test_unrouted_skill(self):
        extra = self.agents / "skills" / "extra"
        extra.mkdir()
        (extra / "SKILL.md").write_text("# Extra skill\n", encoding="utf-8")
        self.assert_failure("Missing route or SKILL.md: extra")

    def test_route_without_skill_file(self):
        incomplete = self.agents / "skills" / "incomplete"
        incomplete.mkdir()
        self.write_route("sample / incomplete")
        self.assert_failure("Missing route or SKILL.md: incomplete")

    def test_duplicate_target_id(self):
        self.write_targets(self.target, self.target)
        self.assert_failure("Duplicate target: sample-target")

    def test_missing_target_skill_path(self):
        self.write_targets(dict(self.target, skill="../agents/skills/missing/SKILL.md"))
        self.assert_failure("sample-target: missing skill: ../agents/skills/missing/SKILL.md")

    def test_missing_runbook(self):
        self.runbook.unlink()
        self.assert_failure("sample-target: missing runbook: case.md")


if __name__ == "__main__":
    unittest.main()
