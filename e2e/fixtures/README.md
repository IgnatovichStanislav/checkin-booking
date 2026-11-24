# E2E fixtures

Shared synthetic input data and expected results for end-to-end tests of the PMS-to-PDS flow.

Use [scenarios.md](scenarios.md) for stable scenario IDs. The catalog is a coverage plan, not evidence of executed tests.

- Use fictional hotels and guests only; never copy production records or real identity documents.
- Keep stable booking/guest IDs and explicit hotel ownership so tests can verify hotel isolation.
- Use deterministic UTC timestamps and hotel-local stay dates. Reuse scenarios across messaging implementations.
- Keep credentials and encryption keys out of fixtures; development secrets belong in the ignored root secrets/ folder.
- Add payload files when their contracts are agreed. This folder does not define a test runner or finalize schemas.
