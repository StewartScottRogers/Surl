---
id: BL-064
title: Add CertificateProblem (58) and CaCertificateBadFile (77) to SurlExitCode
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-002]
touches: [Surl.Protocol.Abstractions.UnitLibrary, Surl.Protocol.Abstractions.UnitTests, Documentation/Product/Requirements.md, Surl.Networking.UnitTests]
requirement: FR-021
created: 2026-09-28
completed: 2026-09-28
---
# BL-064 — Add CertificateProblem (58) and CaCertificateBadFile (77) to SurlExitCode

## Goal

`SurlExitCode` has the two rows ADR-0010 section 3 adds to ADR-0005's table,
`CertificateProblem = 58` and `CaCertificateBadFile = 77`, so BL-012 and the startup
composition can return them.

## Context

- Specification: `Documentation/Planning/Decisions/ADR-0010-the-server-side-tls-contract.md`,
  section 3, "Exit codes": `CertificateProblem` 58 is upstream `CURLE_SSL_CERTPROBLEM`
  (`--cert` or `--key` missing, unreadable, not in the named format, key not matching the
  certificate, a missing or wrong `--pass`, or a key type Surl cannot serve; measured:
  curl 8.21.0 returns 58 for a `--cert` file that does not exist).
  `CaCertificateBadFile` 77 is upstream `CURLE_SSL_CACERT_BADFILE` (`--cacert` exists but
  holds no certificate Surl can read). A `--cacert` that does not exist stays
  `FailedInit` (2). Numbers per https://curl.se/libcurl/c/libcurl-errors.html, curl
  8.21.0.
- `SurlExitCode` lives in `Surl.Protocol.Abstractions.UnitLibrary/SurlExitCode.cs`; its
  members are in ascending numeric order, each with a `<summary>` naming the failure and
  the upstream `CURLE_*` it shares. Its `<remarks>` says one member per row of ADR-0005's
  table; ADR-0005's "Consequences" lets a later ADR add rows, and ADR-0010 is that ADR.
- Tests: `Surl.Protocol.Abstractions.UnitTests/SurlExitCodeTests.cs` has a `DataRow` per
  member and a count test (`Assert.HasCount(8, ...)`).
- `Documentation/Product/Requirements.md` row FR-010 lists every exit code by name and
  number; it must stay true of the enum.
- Nothing returns the new members yet: BL-012 loads the files and maps its failures.

## Acceptance criteria

- [x] `SurlExitCode` has `CertificateProblem = 58` between `BindFailed` (45) and
      `InternalError` (125), and `CaCertificateBadFile = 77` after it, each with a
      `<summary>` stating ADR-0010's meaning and upstream's `CURLE_SSL_CERTPROBLEM` /
      `CURLE_SSL_CACERT_BADFILE`; the enum's `<remarks>` names ADR-0010 as adding rows to
      ADR-0005's table.
- [x] `SurlExitCodeTests.Value_EachMember_HasTheNumberTheAdrAssigns` has
      `DataRow(SurlExitCode.CertificateProblem, 58)` and
      `DataRow(SurlExitCode.CaCertificateBadFile, 77)`, and the count test expects 10.
- [x] `Documentation/Product/Requirements.md` FR-010 lists `CertificateProblem` 58 and
      `CaCertificateBadFile` 77 (from ADR-0010) in numeric order among the others.
- [x] `dotnet build Surl.Protocol.Abstractions.UnitLibrary -warnaserror` is clean, the
      fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green, and
      `powershell -NoProfile -File Measure-CodeQuality.ps1` reports no failing member in
      `Surl.Protocol.Abstractions.UnitLibrary`.

## Notes

- `SurlExitCode`, its tests and FR-010 gained the two ADR-0010 rows as specified. The
  enum's `<remarks>` now says ADR-0010 (section 3) adds rows to ADR-0005's table.
- `Surl.Networking.UnitTests` added to `touches` (no task in Doing names it). The fast-test
  gate could not pass without it: `ServerTlsSettingsTests.CreateAuthenticationOptions_Intermediates_AreInTheCertificateContext`
  failed on every platform (CI run 36531859090 on Linux and macOS: `Sequence contains no
  elements`; Windows: `An unknown chain building error occurred`). Two causes:
  1. The "intermediate" was a self-signed CA, which `SslStreamCertificateContext` treats as
     a root and drops from `IntermediateCertificates`. Fix: a new
     `TestCertificates.CreateIntermediateAuthority` signs a real intermediate under a root.
  2. On Windows `SslStreamCertificateContext.Create` copies intermediates into the user's
     `CurrentUser\CA` store, where they outlive the test. Earlier runs had left about 55
     self-signed `CN=surl test intermediate` certificates there, and chain building with the
     same subject failed. Fix: the test's root and intermediate subjects carry a per-run
     GUID. The leftover certificates on Stewart's machine are harmless now and were not
     deleted (they are his store's contents; removable with
     `Get-ChildItem Cert:\CurrentUser\CA | ? Subject -like 'CN=surl test intermediate*' | Remove-Item`).

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. SurlExitCode has CertificateProblem 58 and CaCertificateBadFile 77 (ADR-0010), tested and listed in FR-010; the intermediate-certificate test in Surl.Networking.UnitTests passes on every platform
