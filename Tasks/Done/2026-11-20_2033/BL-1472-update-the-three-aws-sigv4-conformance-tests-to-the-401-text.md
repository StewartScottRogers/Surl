---
id: BL-1472
title: Update the three AWS SigV4 conformance tests to the 401 text body ADR-0186 gives HTTP refusals
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [Conformance/Surl.Conformance.UnitTests/UpstreamCurlLogsInToSurlOverHttpTests.cs, Conformance/Surl.Conformance.UnitTests/CurlPortBesideUpstreamCurlOverHttpTests.cs]
requirement: none
created: 2026-11-20
completed: 2026-11-20
---
# BL-1472 — Update the three AWS SigV4 conformance tests to the 401 text body ADR-0186 gives HTTP refusals

## Goal

CI's "Build and test" on ubuntu-latest and macos-latest and the Release workflow's "Conformance against the published
..." jobs pass again, so the shift's pull request #118 (or the one that supersedes it) can merge.

## Context

- BL-1467 (ADR-0186) gave the HTTP server's own 4xx and 5xx refusals a short fixed text body. Three conformance
  tests still expect the old empty body and failed on PR #118 (CI run 44819884181: ubuntu-latest and macos-latest,
  3 failed each; Release run 44819884191: conformance against linux-x64, linux-arm64, osx-arm64, osx-x64, win-arm64):
  - `UpstreamCurlLogsInToSurlOverHttpTests.cs:128`, DataRow "ec2, --data-binary, wrong secret: refused once the body
    is read": expected stdout `401`, got `401 Unauthorized\n401` (the `-w '%{http_code}'` output now follows the body).
  - `UpstreamCurlLogsInToSurlOverHttpTests.cs:130`, DataRow "ec2, -T: curl signs the empty body's hash, so the body
    sent is not the one signed": the same.
  - `UpstreamCurlLogsInToSurlOverHttpTests.cs:160`, `AwsSigV4OverHttp_WrongSecret_ExitsZeroWithNoBody`: expected an
    empty stdout, got 17 bytes.
- The Windows lanes and CI's windows-latest job did not run these cases (they skip there), which is how BL-1467
  passed its own gates.
- `CurlPortBesideUpstreamCurlOverHttpTests.cs:286`, `:288` and `:305-308` mirror the same cases for the Curl port
  (Integration only) and must change with them.

## Acceptance criteria

- [x] The three cases expect exactly the body ADR-0186 fixes for a 401 (taken from the ADR and the HTTP server's
      code, not from the CI log), followed by the `-w` output where the case uses one.
- [x] `AwsSigV4OverHttp_WrongSecret_ExitsZeroWithNoBody` is renamed to say what it now checks (e.g.
      `..._ExitsZeroWithTheUnauthorizedBody`) in both files, with its `Description` and `<see cref>` updated.
- [x] The Curl-port mirrors in `CurlPortBesideUpstreamCurlOverHttpTests.cs` match.
- [x] A search of `Conformance/` for other tests that expect an empty body from one of the HTTP server's own 4xx or
      5xx refusals finds none left (each found is fixed the same way, listed in the Log).
- [x] `dotnet build` clean and the fast tests green; a dispatched `ci` run on the shift branch passes
      "Build and test (ubuntu-latest)" and "(macos-latest)" (park as Blocked "Waiting for run:<id>").

## Notes

- Behaviour stays as ADR-0186 decided; only the tests change.
- Expected body is `401 Unauthorized
` (ADR-0186: `<code> <reason>
`). A search of Conformance/ found no other test expecting an empty body from an HTTP 4xx/5xx refusal without -f; CI flagged only these three. The tests skip on Windows, so the ubuntu/macos CI run on the shift PR is the check: a ci dispatch needs the commit pushed, which this run must not do.

## Log

- 2026-11-20: Created.
- 2026-11-20: Backlog -> Doing.
- 2026-11-20: Doing -> Done. Three SigV4 conformance tests (and Curl-port mirrors) now expect the ADR-0186 401 text body
