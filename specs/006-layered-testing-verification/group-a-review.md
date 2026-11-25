# Group A review: T001–T004

**Outcome**: Approved after independent read-only re-review at 038750a. No findings remain.

The initial implementation at 6f22d1b added a reviewed required-identity inventory, validation and scoped intake assertions. Review found that scenario metadata/fixtures were read before the inventory guard. The fix compares raw reflected identities first and retains all metadata checks afterward.

A persistent copied-output regression reproduced the previous ordering failure. It now verifies the safe inventory diagnostic, exit 1, no test stdout and no map/TRX/result when invalid inventory and missing fixtures occur together.

## Recorded checks

- Original suite: 24 passed.
- Four initial inventory regressions: four intended failures before implementation; focused implementation check: seven passed.
- Initial expanded suite: 28 passed; copied-output invalid inventory smoke checks rejected all four shapes before execution.
- Ordering regression: one intended failure before the fix, then passed.
- Current focused evidence suite: 12 passed. Full suite: 29 passed, zero failed/skipped; build: zero warnings/errors.
- Source graph refreshed; 1,119 nodes/2,280 edges at the final refresh. The initial six no-symbol files remain a navigation limitation, not application test failures.

The reviewer inspected source and existing evidence without editing files or rerunning suites. Hosted CI, source-bound safe reporter/runner changes, behavioral evals and runtime booking proof remain pending. Group B T005–T008 is next; the full feature is not complete.
