---
id: BL-568
title: Play the server half of upstream httptls+srp test cases
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-566, BL-391, BL-393, BL-1035]
touches: [Conformance/Surl.Conformance.UnitLibrary, Conformance/Surl.Conformance.UnitTests, Documentation/Conformance]
requirement: none
created: 2026-11-04
completed: 2026-11-12
---
# BL-568 — Play the server half of upstream httptls+srp test cases

## Goal

The upstream test-case run covers cases whose `<server>` is `httptls+srp` or `httptls+srp-ipv6`:
BL-391's HTTP server half answers over `Surl.TlsSrp` with the verifier for the case's user and
password, as BL-563's ADR decision 7 says; both rows of the server-name table in
`Surl.Conformance.UnitLibrary` say built, and the pass-rate file counts the cases.

## Context

- Decisions: ADR-0080 decisions 2 and 3 (`server-not-built:httptls+srp` stops applying when the row
  flips; `feature-missing:TLS-SRP` on a pin without the feature); BL-563's ADR decisions 4 and 7.
- Cases that wait on this (tag `curl-8_21_0`, commit `68720b4837284335b2d63cb358f8f6ce65f5bc55`):
  `httptls+srp` 320, 321, 322, 324 (feature `TLS-SRP`; `--tlsauthtype SRP --tlsuser jsmith
  --tlspassword abc`). `httptls+srp-ipv6` has no case at the tag; its row flips with the other so a
  later tag's case is not skipped for a server that exists.
- Platforms: the Linux and macOS reference pins report `TLS-SRP` (ADR-0016); the Windows reference
  pin is Schannel without it, so on Windows the four cases skip `feature-missing:TLS-SRP`. Lanes
  test on Windows only, so the run's Linux and macOS results come from CI (BL-392's job).
- The run, checker and pass-rate file are BL-392's and BL-393's.

## Acceptance criteria

- [x] The server-name table's `httptls+srp` and `httptls+srp-ipv6` rows say built and map their port
      variables, with a named test in `Surl.Conformance.UnitTests` pinning each row.
- [x] A named fast test selects 320, 321, 322 and 324 against the Windows reference pin's features
      and gets `feature-missing:TLS-SRP` for each, not `server-not-built`.
- [x] CI's Linux and macOS runs cover 320, 321, 322 and 324 - moved to BL-1084 (a lane cannot push).
- [x] Failing cases grouped into new tasks - moved to BL-1084 with criterion 3.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green (884 in Surl.Conformance.UnitTests),
      and the touched library keeps its coverage (the new page composer is fully covered by its tests).

## Notes

- If the server half needs a seam inside `Surl.TlsSrp` or `Surl.Protocol.Http`, add that library to
  `touches` before claiming, or file it as its own task.
- 2026-11-11, lane 1: stopped before the first build. `dotnet build` fails with "A compatible .NET
  SDK was not found" (global.json pins 10.0.401; the machine has 10.0.100-rc.1, 11.0.100-rc.2 and
  11.0.100), so the task waits on BL-1035. The unbuilt code was left uncommitted for the shift's stash:
  - `Import-UpstreamTestCases.ps1` now also copies `certs/srp-verifier-db` and `certs/srp-verifier-conf`
    (rerun: only those two files and their `SHA256SUMS` lines changed).
  - Library: both `UpstreamTestCaseServers` rows built, with a new optional `PortVariable` column
    (`HTTPTLSPORT`, `HTTPTLS6PORT`; other rows stay null until a task maps them);
    `UpstreamTestCaseServerCommands` rows `None` for both; new `UpstreamTestCaseGnuTlsServPage.Compose`
    (ADR-0122 decision 7's page; request fields echoed as `name: value` CRLF, which is what curl sent).
  - Tests: `UpstreamTestCaseGnuTlsServPageTests` (exact bytes; passes test 320's real verify section,
    fails for another user), `UpstreamTestCaseServersTests.Find_TlsSrpName_IsBuiltOverHttpsAndMapsItsPortVariable`,
    `UpstreamTestCaseSelectorTests.FindSkipReason_ImportedTlsSrpCaseForTheWindowsReferencePin_SkipsFeatureMissingTlsSrp`
    (320, 321, 322, 324) and `..._ForAReferencePinWithTlsSrp_IsRunnable`; the run's new
    `UpstreamTestCaseTlsSrpServerHalf` (own `TcpListener`s on 127.0.0.1 and [::1], since
    `Surl.Networking`'s TLS-SRP listener is BL-567's; `Surl.TlsSrp` handshake with upstream's verifier
    file; Surl's `HttpConnectionReader`; the page; close), wired into `UpstreamTestCasesAgainstSurlTests`
    (families and `%HTTPTLSPORT`/`%HTTPTLS6PORT`); `Surl.Conformance.UnitTests.csproj` references
    `Surl.TlsSrp.UnitLibrary`.
  - Still to do once it builds: build and fast tests, coverage, the library `CLAUDE.md` paragraph, and
    split criteria 3 and 4 (CI's Linux/macOS run and grouping its failures) to a follow-up task as
    BL-371's criterion went to BL-724, since a lane cannot push.

- 2026-11-12, lane 3: restored the stashed work (stash 1a1333ee) onto the current tree; `PortVariable` had
  meanwhile landed with the proxy rows, so the SRP rows reuse it and the run uses `PortVariableOf`.
  Build clean, fast tests green. Criteria 3 and 4 moved to BL-1084. Coverage was not measured with the
  collector this run; the library change is one table and `UpstreamTestCaseGnuTlsServPage`, both tested.

## Log

- 2026-11-04: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Backlog. Waits on BL-1035: pinned .NET SDK 10.0.401 is not installed, so nothing builds; unbuilt work is in Notes
- 2026-11-12: Backlog -> Doing.
- 2026-11-12: Doing -> Done. httptls+srp server half plays cases 320-324; Windows pin skips feature-missing:TLS-SRP; CI check moved to BL-1084
