"""Write or verify a synthetic evaluation on the local Checkin Langfuse instance."""
import argparse
import base64
from datetime import datetime, timezone
import json
from pathlib import Path
import subprocess
import time
from urllib.error import HTTPError, URLError
from urllib.request import Request, urlopen
from uuid import UUID, uuid5

BASE = "http://127.0.0.1:3300/api/public/"


def request(path, token, body=None):
    data = json.dumps(body).encode() if body is not None else None
    headers = {"Authorization": f"Basic {token}", "Content-Type": "application/json"}
    with urlopen(Request(BASE + path, data=data, headers=headers), timeout=10) as response:
        return json.load(response)


def check(run_id, write):
    common = subprocess.check_output(
        ["git", "-C", str(Path(__file__).parent), "rev-parse", "--path-format=absolute", "--git-common-dir"],
        text=True,
    ).strip()
    secret_file = Path(common).parent / "secrets" / "langfuse.env"
    config = dict(line.split("=", 1) for line in secret_file.read_text().splitlines() if line)
    token = base64.b64encode(f"{config['PUBLIC_KEY']}:{config['SECRET_KEY']}".encode()).decode()
    score_id = str(uuid5(run_id, "task-success"))
    if write:
        timestamp = datetime.now(timezone.utc).isoformat()
        start = time.time_ns()
        span = {"traceId": run_id.hex, "spanId": score_id.replace("-", "")[:16],
                "name": "Synthetic persistence check", "startTimeUnixNano": str(start),
                "endTimeUnixNano": str(start + 1000), "attributes": [
                    {"key": "langfuse.trace.tags", "value": {"arrayValue": {"values": [
                        {"stringValue": "integration-smoke"}]}}},
                    {"key": "langfuse.trace.metadata.task", "value": {"stringValue": "storage-check"}},
                    {"key": "langfuse.trace.metadata.checkpoint", "value": {"stringValue": "verified"}}]}
        request("otel/v1/traces", token, {"resourceSpans": [{"scopeSpans": [
            {"scope": {"name": "checkin-storage-check"}, "spans": [span]}]}]})
        score = {"id": score_id, "traceId": run_id.hex, "name": "smoke_task_success",
                 "value": 1, "dataType": "NUMERIC"}
        batch = [{"id": str(uuid5(run_id, "score-event")), "timestamp": timestamp,
                  "type": "score-create", "body": score}]
        result = request("ingestion", token, {"batch": batch})
        if result.get("errors") or len(result.get("successes", [])) != len(batch):
            raise RuntimeError("Langfuse did not accept the synthetic score")
    for _ in range(60):
        try:
            rows = request(f"v3/scores?id={score_id}&traceId={run_id.hex}", token)["data"]
            if rows:
                if len(rows) != 1 or rows[0]["id"] != score_id or rows[0]["value"] != 1:
                    raise RuntimeError("Unexpected synthetic score")
                print(f"Verified smoke score for {run_id}")
                return
        except HTTPError as exc:
            if exc.code not in (404, 502, 503):
                raise
        except URLError:
            pass
        time.sleep(2)
    raise RuntimeError("Score did not become readable in time")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("action", choices=("write", "read"))
    parser.add_argument("run_id", type=UUID)
    args = parser.parse_args()
    check(args.run_id, args.action == "write")
