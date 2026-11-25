# Proposed plan amendment: prohibit backend test attachments

**Status**: Amendment approved by the Owner on 2026-11-25. The linked plan, model, contracts and tasks incorporate this decision; implementation and cross-platform proof remain required.
**Baseline**: Group A approved; group B partial implementation committed as 0cf8605. T007 remains incomplete.

## Observed problem

The real failure canary ran 36 methods: 35 passed and one intentionally failed, platform exit 2. The four constructed evidence records excluded the synthetic sentinel. However, the pinned MSTest adapter copied a registered attachment into the results directory and printed its path before the data consumer could filter anything. No supported configuration switch disables that staging path.

The independently checked pinned source confirms [file registration](https://github.com/microsoft/testfx/blob/0b1d8b5f7b2186835e2cf26292f3478f7cca087e/src/Adapter/MSTestAdapter.PlatformServices/Services/TestContextImplementation.Properties.cs#L144-L163), [unconditional staging](https://github.com/microsoft/testfx/blob/0b1d8b5f7b2186835e2cf26292f3478f7cca087e/src/Adapter/MSTest.TestAdapter/TestingPlatformAdapter/MSTestTestFramework.cs#L267-L301) and [staging before publication](https://github.com/microsoft/testfx/blob/0b1d8b5f7b2186835e2cf26292f3478f7cca087e/src/Adapter/MSTest.TestAdapter/TestingPlatformAdapter/MtpTestResultRecorder.cs#L168-L187).

A public test-method wrapper can clear returned ResultFiles before staging, but it does not prevent retention of original per-test temporary files. It is not a complete basis for the currently promised no-raw-attachment behavior. Cleanup after execution also cannot guarantee privacy if interrupted.

## Recommended amendment

Prohibit result attachments in this backend full-check suite and reject unsupported attachment-producing test code before test execution.

- Keep the safe event consumer, actual platform exits, safe XML/JSON and group C's in-memory stdout/stderr discard.
- Add a pre-execution attachment-policy guard for the current closed set of owned test classes/executors. Reject compiled references to TestContext.AddResultFile, including owned helper calls; reject indirect/custom execution paths the guard cannot cover instead of assuming they are safe.
- Do not run a forbidden attachment case to completion. Its expected outcome is a safe policy failure before any test body or attachment staging.
- Continue testing real failing assertions/stdout separately so ordinary application failures still execute, preserve platform failure and produce safe joined verdicts.
- Keep assertion diagnostic file generation disabled. Imported framework behavior and any future test extension require explicit review before expanding the supported test shape.
- This is a project test-policy guard, not a sandbox for arbitrary or malicious file-writing code. Test authors still must not write guest identity or credentials into logs/files. No synthetic or real private fields are newly allowed in diagnostics.

## Concrete guard design for review

Use the existing runtime's public System.Reflection.Metadata.MetadataReader/PortableExecutable.PEReader APIs to inspect managed test/project outputs without loading or executing the inspected assembly. These APIs are available locally without adding a package; a read-only metadata check found zero methods on the current test assembly's module type, so no existing module initializer needs an exception. Implementation still requires cross-platform proof.

The parent full-check runs the inspection after its verified rebuild and before starting the test executable. The child validates the associated guard/build attestation before starting MTP. Inspect all owned method bodies, including helpers, constructors and generated async bodies; decode member references/function-pointer targets and reject TestContext.AddResultFile. Reject unapproved indirect invocation/dynamic-code routes, custom executors/lifecycle/data/inherited test shapes or unresolved metadata instead of silently allowing them. Current ordinary owned test methods form the initial supported set. Reject user module initializers before loading the inspected executable. Known framework dependencies remain pinned/reviewed; assertion-file capture stays disabled.

Preserve normal test assertions and async outcomes for accepted methods. A forbidden-API canary must fail policy inspection without executing its test body, producing only a fixed safe reason and phase record. Test direct and owned-helper attachment calls, unsupported indirect routes, and malformed metadata separately. Retain real returned-test-failure and console-sentinel cases without attachments to prove the failure pipeline independently.

Build forbidden-call canaries only as disposable inputs outside the normal suite; otherwise the normal suite's own scan would reject the canary code. Assert no test-body side effect, no platform exit and no staged file after policy rejection.

Add an explicit post-build policy phase between build and test in the full-check manifest. Its failure leaves test execution notRun, platform exit unset and only safe phase metadata exportable. Only the parent full-check performs policy inspection before loading the executable. A direct legacy invocation must remain policyVerified=false and verificationEligible=false; it cannot satisfy full verification. Bind successful policy inspection to the exact built artifact digest, alongside the existing source/build attestation, before starting MTP.

This adds a small internal metadata guard, not a dependency, service, framework fork or filesystem sandbox. Future test shapes require a reviewed guard extension. The guard must not claim to prevent arbitrary file I/O in otherwise trusted test code.

## Proposed acceptance changes

T007 gains the attachment-policy guard and negative checks. T005/V05 gains a preflight-rejection attachment canary, replacing the infeasible expectation that a genuine registered attachment runs yet never reaches MSTest staging. Existing assertion/stdout/metadata sentinel checks remain. The source/build binding and full-suite required identities still apply.

The guide documents attachments as unsupported in this backend full-check mode. Browser artifacts and future runtime test frameworks remain separately scoped; this proposal does not authorize changing them.

## Before / after

Before: an owned test registers a file; MSTest copies it; only afterward does the safe consumer see the event.

After: the full-check guard rejects forbidden attachment paths before tests start. Supported tests still run through the existing MSTest executor and safe consumer with their actual pass/fail outcomes. See [proposal diagram](../../diagrams/006-layered-testing-verification/attachment-policy-proposal.drawio).

## Owner decision

The Owner approved the attachment-ban/metadata-guard amendment. Continue with pre-execution rejection of forbidden attachments and separate real assertion/stdout failure checks. No runner/framework replacement, dependency or service is approved.

Neither choice permits logging guest identity or credentials. Group B is not complete; later source attestation and hosted privacy evidence remain pending.

## Review and current limits

Independent read-only review recommends Owner approval of this amendment. Its two implementation clarifications are included above: external disposable canaries and explicit post-build phase/entry-point semantics. The two-page diagram parses with unique per-page IDs and valid edge endpoints; rendered layout remains unverified.

The guard is not implemented and has no cross-platform or hosted execution proof yet. Group B's clean unit results do not override its actual failed attachment canary. Automatic command review rejected both recursive probe cleanup and a narrower three-file cleanup with the generic reason blocked by policy. The synthetic fault probe remains in ignored test-results/group-b-failure-d13ee031-b110-4559-9957-f7c83a6da65f; no sentinel contents or raw diagnostics were committed or uploaded.

