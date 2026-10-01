---
id: BL-304
title: Prove pinned upstream curl completes WebSocket exchanges over ws and wss with surl
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-303, BL-323, BL-322]
touches: [Surl.Conformance.UnitLibrary, Surl.Conformance.UnitTests]
requirement: FR-048
created: 2026-09-30
completed: 2026-09-30
---
# BL-304 — Prove pinned upstream curl completes WebSocket exchanges over ws and wss with surl

## Goal

`[TestCategory("Integration")]` tests in `Surl.Conformance.UnitTests` prove that the pinned upstream
curl 8.21.0 builds complete WebSocket exchanges against a live `surl` over `ws` and `wss` in every
case BL-285's ADR lists, with the exit codes it expects, on Windows, Linux and macOS.

## Context

- The cases and expected exit codes: BL-285's ADR's list (at least the upgrade and the messages surl
  sends written by curl, `wss://` with `-k`, a refused upgrade (curl's 22), a login with each HTTP
  method the ADR offers, a `PING` curl answers, surl's `CLOSE`, the idle timeout).
- If BL-285's ADR decided to pin the reference build's `libcurl-4.dll`, the libcurl-driven cases
  (client text, binary, fragmented, ping and close frames) are proved here too, through the driver
  the pin task built; those cases report Inconclusive where the pinned library is absent.
- Harness: `SurlOnLoopback.cs`, `PinnedUpstreamCurl.cs`, `AccountsFile.cs`,
  `TestCertificateAuthority.cs`; `Assert.Inconclusive` when the pinned build is absent; the Linux and
  macOS legs run on CI (ADR-0016).
- Any disagreement with the pinned build is fixed in the library at fault through a new task filed by
  `task-planner`, never by changing the expected result (ADR-0003); list them in the Log.

## Acceptance criteria

- [x] Integration tests exist for every case of BL-285's ADR and pass on Windows with the pinned build
      present: `dotnet test --filter "FullyQualifiedName~Surl.Conformance"` is green.
- [x] `dotnet build -warnaserror` is clean and the fast tests are green; no fast test opens a socket.

## Notes

- Stewart approved lanes filing follow-up tasks beyond the plan (2026-09-30, "yes extra tasks"): a
  disagreement this task finds becomes its own task rather than widening this one.
- Delivered: `UpstreamCurlTalksToSurlOverWebSocketTests` (ADR-0071 decision 11's 20 tool rows, on
  all three platforms) and `PinnedLibcurlTalksToSurlOverWebSocketTests` (amendment 1's 7 libcurl
  rows, Windows only) through the new `PinnedLibcurlWebSocketDriver`, which runs
  `dotnet run --file Run-LibcurlWebSocketScript.cs` one run at a time (parallel tests must not build
  the file-based app over each other) and reports Inconclusive off Windows or on the driver's exit 4.
- Choice: accounts come from `AccountsFile` (`tester:secret`, Bearer `the-bearer-token`) rather than
  the ADR's `--user tester:secret`, `:tok`, `AKID:SECRET`; the login rules judged are the same, and
  it is the harness every other login test uses.
- Measured: pinned curl agrees with every expectation; no disagreement, so no follow-up filed. The
  amendment's unmeasured case (surl's early close of the 2 MiB message) came out as expected:
  libcurl's send returned `CURLcode 0`, sent 2097152, then `CLOSE` 1009 and `CURLcode 52`.
- Seen once under full-suite load, unrelated to this task: `Quote_MakeThenRemoveDirectory_...`
  (SFTP/FTP) failed once and passed on two reruns and the next full run.

## Log

- 2026-09-30: Created.
- 2026-09-30: depends-on gains BL-323 and BL-322 (BL-285, ADR-0071 decision 10: the libcurl-driven cases).
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. Pinned curl and libcurl-4.dll complete every ADR-0071 WebSocket case against surl over ws and wss
