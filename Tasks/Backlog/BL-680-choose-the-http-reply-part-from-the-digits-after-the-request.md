---
id: BL-680
title: Choose the HTTP reply part from the digits after the request target's last slash
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-392]
touches: [Protocols/Http/Surl.Protocol.Http.UnitLibrary, Protocols/Http/Surl.Protocol.Http.UnitTests]
requirement: none
created: 2026-11-04
completed:
---
# BL-680 — Choose the HTTP reply part from the digits after the request target's last slash

## Goal

`HttpUpstreamTestCasePartNumber.FromTarget` takes the reply part number from the run of digits
that starts right after the last `/` of the request target, query included, so the 16 upstream
test cases listed below get the `<dataNUM>` part they name instead of part 0.

## Context

- Governing decision: ADR-0080 (`Documentation/Planning/Decisions/ADR-0080-how-surl-plays-the-server-half-of-upstream-curls-test-cases.md`),
  decision 2: the HTTP server half interprets FILEFORMAT's `<dataNUM>` selection. Surl is written
  from FILEFORMAT.md, RFCs and measurement, never by translating upstream's test servers (`sws.c`);
  upstream curl 8.21.0 is the only oracle.
- Code: `Protocols/Http/Surl.Protocol.Http.UnitLibrary/HttpUpstreamTestCasePartNumber.cs`,
  `FromTarget(string method, string target)`. Today, for a non-CONNECT request it cuts the query
  (`PathOf`) and takes the digits the path *ends* in (`TrailingDigitCount`), keeping the last four
  digits when more than four remain after leading zeros.
- The defect, with the imported corpus (`Conformance/Surl.Conformance.UnitTests/UpstreamTestCases/data/`)
  as evidence: these targets give part 0 today and must give part 2:
  - `/data/10240002.txt` (digits followed by `.txt`);
  - `/want/data/data/110002.txt?coolsite=yes` (suffix and query);
  - `/we/are/all/moo.html/?name=x&testcase=/11380002` (the last `/` is inside the query).
  The rule the corpus shows: the number is the run of digits right after the last `/` of the whole
  request target, query included, ending at the first non-digit. BL-391's Notes recorded the
  ends-in rule as a choice; this task replaces it, citing these cases.
- Keep the CONNECT branch (`HostOf`) and the credential addends (`WithCredentials`) unchanged.
- Update the XML doc comments of `FromTarget` and the class, and the part-number rule in
  `Protocols/Http/Surl.Protocol.Http.UnitLibrary/CLAUDE.md`, to state the new rule.
- Failing cases this fixes (Windows, 2026-11-04 run of BL-392's test): 11, 28, 40, 43, 187, 276,
  309, 1024, 1025, 1031, 1051, 1052, 1053, 1067, 1138, 1168.
- Integration run: `dotnet test Conformance/Surl.Conformance.UnitTests --filter "FullyQualifiedName~UpstreamTestCasesOverHttp" --logger "console;verbosity=detailed"`;
  it writes `upstream-test-case-results.win-x64.txt` and logs each failing case's first difference.

## Acceptance criteria

- [ ] `Protocols/Http/Surl.Protocol.Http.UnitTests/HttpUpstreamTestCasePartNumberTests.cs` has tests
      that pin part 2 for `/data/10240002.txt`, `/want/data/data/110002.txt?coolsite=yes` and
      `/we/are/all/moo.html/?name=x&testcase=/11380002`, and the existing tests (CONNECT, credentials,
      bare numbers, numbers at or under 10000) still pass.
- [ ] The XML doc comments on `HttpUpstreamTestCasePartNumber.FromTarget` and
      `Protocols/Http/Surl.Protocol.Http.UnitLibrary/CLAUDE.md` state the digits-after-the-last-slash
      rule, query included, and nothing states the old ends-in rule.
- [ ] In the integration run above, cases 11, 28, 40, 43, 187, 276, 309, 1024, 1025, 1031, 1051,
      1052, 1053, 1067, 1138 and 1168 are `pass` in `upstream-test-case-results.win-x64.txt`, and no
      case that passed before (651 on 2026-11-04) now fails; Notes record the new counts.
- [ ] `Surl.Protocol.Http.UnitLibrary` keeps 100% line and branch coverage.
- [ ] `dotnet build Protocols/Http/Surl.Protocol.Http.UnitLibrary -warnaserror` is clean and
      `dotnet test --filter "TestCategory!=Integration"` is green.

## Notes

## Log

- 2026-11-04: Created.
