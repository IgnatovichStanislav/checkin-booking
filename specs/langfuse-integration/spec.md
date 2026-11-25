# Local task evaluations in Langfuse

**Scope.** Development tooling for the Checkin project. A reviewer can submit a synthetic task checkpoint to a local Langfuse instance and retrieve its score after recreating the containers.

## User scenarios and acceptance criteria

| Scenario | Expected result |
| --- | --- |
| A teammate starts the evaluation stack from a linked worktree. | The setup derives the sibling storage folder from the main checkout name. An absolute `LANGFUSE_DATA_DIR` override selects another host folder. |
| A reviewer records a synthetic checkpoint. | The local project accepts a trace and a numeric score. The reviewer can retrieve the score by its identity and trace reference. |
| The operator recreates the stack. | Reading the same score succeeds without submitting it again. The ClickHouse data volume persists across container recreation. |

## Requirements

- Keep credentials in the main checkout's ignored secrets directory and bind published ports to loopback.
- Store project metadata, ingestion objects, queue data and ClickHouse logs in the configurable host folder. Use Docker-managed Linux storage for live ClickHouse traces and scores.
- Distinguish synthetic persistence checks from reviewed feature outcomes. Automatic session capture and recurring reports require separate configuration.

## Verification

The Windows Docker Desktop pilot submitted a synthetic checkpoint, recreated the containers, and retrieved its score of 1. Follow the [deployment and verification guide](../../eval/langfuse/README.md) to repeat the test. This evidence covers local persistence. Team-wide reporting and booking application behavior are outside this tooling change.
