# Checkin Booking

A local learning test stand comparing Kafka, RabbitMQ and Google Cloud Pub/Sub through the same booking-to-DPS registration flow.

Hotel/PMS -> normalize booking -> broker -> save booking -> fictional DPS registration -> status returned to the hotel and monitoring UI.

Planned stack: .NET, PostgreSQL, Redis, webhooks and Blazor. All hotels, guest data and DPS integrations are synthetic. Application implementation has not started.

## Workspace

- [AGENTS.md](AGENTS.md): scope, business rules and coding guidance.
- [agents/README.md](agents/README.md): shared Graphify, Spec Kit and Docker skill routing.
- [.specify/](.specify/): Spec Kit templates and workflows; constitution remains a template.
- [diagrams/](diagrams/): editable draw.io diagrams.
- [eval/README.md](eval/README.md): manual skill evaluation workflow; no evaluations have been run.

Start with contracts and a Kafka example, then compare the same cases with RabbitMQ and Pub/Sub. Project folders are created only as needed.

Graphify output is generated locally and excluded from Git. With Graphify installed, run `./agents/hooks/refresh-graph.ps1` from the repository root. RTK is optional host tooling for compact command output.

Docker skill provenance and license are retained in [agents/sources/docker-skills/](agents/sources/docker-skills/).
