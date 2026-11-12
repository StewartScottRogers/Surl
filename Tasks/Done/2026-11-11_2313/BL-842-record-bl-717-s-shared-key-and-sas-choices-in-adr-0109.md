---
id: BL-842
title: Record BL-717's Shared Key and SAS choices in ADR-0109
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-717, BL-1035]
touches: [Documentation/Planning/Decisions/ADR-0109-how-the-azure-storage-server-answers-az-abfs-and-abfss.md, Documentation/Conformance/Extended/AzureStorage.md]
requirement: none
created: 2026-11-05
completed: 2026-11-11
---
# BL-842 — Record BL-717's Shared Key and SAS choices in ADR-0109

## Goal

ADR-0109 carries an amendment, marked "Decided by Claude under Stewart's delegation", recording the choices
BL-717 made where decision 3 left them open, and the AzureStorage conformance matrix names the tests that now
cover Shared Key and shared access signatures.

## Context

- BL-717 built the checks (`Surl.Authentication`'s `AzureStorageVerifier`, the server's `AzureLoginJudge`) and
  lists each choice under its Notes. BL-717's `touches` did not include Documentation, so the ADR waits here.
- Choices to record: the `AzureStorageScope` contract on `HttpAuthenticationRequest` and the eight new
  `HttpAuthenticationRefusal` members; the SAS permission letters per operation (`c` alone never grants a write);
  `sr=d` served with `sdd`, `bs`/`bv` refused `UnservedSignatureKind`; each `AuthenticationErrorDetail` sentence
  (an unknown account reads `Signature did not match.`); an account whose password is not base64 cannot sign
  Shared Key or SAS; `+` in the query read as a space; credentials checked under `--allow-anonymous` for every
  Azure method (which BL-725 builds on).

## Acceptance criteria

- [x] ADR-0109 has an amendment naming BL-717 that states each choice in Context, marked "Decided by Claude
      under Stewart's delegation".
- [x] `Documentation/Conformance/Extended/AzureStorage.md` names `AzureStorageVerifierTests` and
      `AzureLoginJudgeTests` against the Shared Key and SAS rows.

## Notes

- Written in the session rather than through `align-and-document`: two Markdown files and no code, so the
  `verify` skill was not needed.
- ADR-0109 now has "Amendment, 2026-11-11 (BL-717)". It records each choice from BL-717's Notes and two that
  BL-717 left unstated: which refusals wait the failed-login delay, and that an `sv` before 2020-12-06 or an
  `sdd` out of range is `UnservedSignatureKind`. `AzureStorageVerifierTests` pins both.
- Every matrix row that named BL-717 (Surl-Errors-3, AuthorizeSharedKey-1 to 4, ServiceSas-1 to 3,
  AccountSas-1, Surl-Auth-5) now names its tests. AuthorizeSharedKey-4 also cites BL-725's
  `AzurePathStyleTests`, because the session builds the canonicalized resource from the target as the client
  sent it, so the account appears twice. UserDelegationSas-1 now names the test that pins its refusal.
- Lane 3 rerun: lane 1's docs commit (254b2d66) was cherry-picked unchanged; the build break at lane 1's
  integration was not caused by this task, which changes Markdown only. All 34 test methods the
  matrix cites were confirmed to exist in the source.
- 2026-11-11, lane 3: `dotnet build` fails before compiling anything - "A compatible .NET SDK was not found"
  for `global.json`'s 10.0.401. That is the build failure lane 1 hit too. BL-1035 (Stewart) reinstalls the
  SDK, so this task now depends on it. The docs are finished: rerun with
  `git cherry-pick --no-commit 254b2d66`, check the build, tick both boxes and move to Done.

## Log

- 2026-11-05: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Backlog. Lane 1 could not integrate: build failed after rebasing onto the other lanes' work. The work is on branch factory/BL-842-lane-1-20261111-060243; start with git cherry-pick --no-commit factory/BL-842-lane-1-20261111-060243 and fix it.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Backlog. Waits on BL-1035: dotnet build cannot find the .NET SDK 10.0.401 that global.json pins; the docs are done in commit 254b2d66
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Done. ADR-0109 records BL-717's Shared Key and SAS choices; AzureStorage matrix names AzureStorageVerifierTests and AzureLoginJudgeTests
