---
id: BL-061
title: Add CertificateProblem (58) and CaCertificateBadFile (77) to SurlExitCode
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-002]
touches: [Surl.Protocol.Abstractions.UnitLibrary, Surl.Protocol.Abstractions.UnitTests, Documentation/Product/Requirements.md]
requirement: FR-021
created: 2026-09-28
completed:
---
# BL-061 — Add CertificateProblem (58) and CaCertificateBadFile (77) to SurlExitCode

## Goal

`SurlExitCode` has the two rows ADR-0008 section 3 adds to ADR-0005's table,
`CertificateProblem = 58` and `CaCertificateBadFile = 77`, so BL-012 and the startup
composition can return them.

## Context

- Specification: `Documentation/Planning/Decisions/ADR-0008-the-server-side-tls-contract.md`,
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
  table; ADR-0005's "Consequences" lets a later ADR add rows, and ADR-0008 is that ADR.
- Tests: `Surl.Protocol.Abstractions.UnitTests/SurlExitCodeTests.cs` has a `DataRow` per
  member and a count test (`Assert.HasCount(8, ...)`).
- `Documentation/Product/Requirements.md` row FR-010 lists every exit code by name and
  number; it must stay true of the enum.
- Nothing returns the new members yet: BL-012 loads the files and maps its failures.

## Acceptance criteria

- [ ] `SurlExitCode` has `CertificateProblem = 58` between `BindFailed` (45) and
      `InternalError` (125), and `CaCertificateBadFile = 77` after it, each with a
      `<summary>` stating ADR-0008's meaning and upstream's `CURLE_SSL_CERTPROBLEM` /
      `CURLE_SSL_CACERT_BADFILE`; the enum's `<remarks>` names ADR-0008 as adding rows to
      ADR-0005's table.
- [ ] `SurlExitCodeTests.Value_EachMember_HasTheNumberTheAdrAssigns` has
      `DataRow(SurlExitCode.CertificateProblem, 58)` and
      `DataRow(SurlExitCode.CaCertificateBadFile, 77)`, and the count test expects 10.
- [ ] `Documentation/Product/Requirements.md` FR-010 lists `CertificateProblem` 58 and
      `CaCertificateBadFile` 77 (from ADR-0008) in numeric order among the others.
- [ ] `dotnet build Surl.Protocol.Abstractions.UnitLibrary -warnaserror` is clean, the
      fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green, and
      `powershell -NoProfile -File Measure-CodeQuality.ps1` reports no failing member in
      `Surl.Protocol.Abstractions.UnitLibrary`.

## Notes

## Log

- 2026-09-28: Created.
