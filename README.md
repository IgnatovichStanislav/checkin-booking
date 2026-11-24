# Checkin Booking

A system that receives bookings from property management systems (PMS), registers stays and guests with a Police Department System (PDS), and tracks registration status.

Hotel/PMS -> normalize booking -> broker -> save booking -> fictional PDS registration -> status returned to the hotel and monitoring UI.

Planned stack: .NET, PostgreSQL, Redis, webhooks and Blazor. All hotels, guest data and PDS integrations are synthetic. Application implementation has not started.

See [CONTRIBUTING.md](CONTRIBUTING.md) for contributor onboarding, the shared AI workflow and safe publishing rules.

## Workspace

- [AGENTS.md](AGENTS.md): scope, business rules and coding guidance.
- [agents/README.md](agents/README.md): shared Graphify, Spec Kit and Docker skill routing.
- [.specify/](.specify/): Spec Kit templates and workflows; constitution remains a template.
- [diagrams/](diagrams/): editable draw.io diagrams.
- [eval/README.md](eval/README.md): manual skill evaluation workflow; no evaluations have been run.

Start with booking intake and registration contracts, then implement the end-to-end flow. Project folders are created only as needed.

Graphify output is generated locally and excluded from Git. With Graphify installed, run `./agents/hooks/refresh-graph.ps1` from the repository root. RTK is optional host tooling for compact command output.

Docker skill provenance and license are retained in [agents/sources/docker-skills/](agents/sources/docker-skills/).
