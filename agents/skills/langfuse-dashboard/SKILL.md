---
name: langfuse-dashboard
description: Start or locate the local Checkin Langfuse dashboard and return browser links for task score analytics.
---

# Checkin Langfuse dashboard

From the repository root run `pwsh -NoProfile -File eval/langfuse/dashboard.ps1`. On success, return the printed Dashboard and Score analytics URLs as clickable links. Use `-Open` only if the user asks to launch the browser. If startup fails, report the actual error.

These links open on the machine hosting the Checkin stack. Sign-in instructions are in [the deployment guide](../../../eval/langfuse/README.md). Keep credential values out of chat. Opening the dashboard displays existing evaluations. Report actual metric values only when you have retrieved them.
