---
id: BL-1076
title: Fix upstream http test cases 60, 173 and 329 (chunked upload size line, multipart Content-Length, Cookie from -b/-H)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-683]
touches: [Conformance/Surl.Conformance.UnitLibrary, Conformance/Surl.Conformance.UnitTests, Protocols/Http/Surl.Protocol.Http.UnitLibrary, Protocols/Http/Surl.Protocol.Http.UnitTests]
requirement: none
created: 2026-11-12
completed: 2026-11-12
---
# BL-1076 — Fix upstream http test cases 60, 173 and 329 (chunked upload size line, multipart Content-Length, Cookie from -b/-H)

## Goal

Upstream http test cases 60, 173 and 329 (curl 8.21.0) pass against Surl on win-x64.

## Context

Found by BL-683; causes under Notes.

## Acceptance criteria

- [x] Cases 60, 173 and 329 are in upstream-test-cases-passing.win-x64.txt and the pass-rate row is raised; UpstreamTestCasesAgainstSurlTests passes.
- [x] Each fix has a unit test; dotnet build clean and fast tests green.

## Notes

- 60: server half. The case sends Transfer-Encoding: chunked beside Content-Length: 1; HttpRequestBodyFraming (ADR-0024) calls the pair unreadable, so the body was never read and the chunk lines were missing. HttpUpstreamTestCaseRequestBody now reads chunks when one chunked field and HTTP/1.1 come with a Content-Length (RFC 9112 6.3 rule 3). The generic framing is unchanged.
- 173: harness. %DEV_NULL was NUL on Windows; the case uses it as a form file name and its Content-Length 378 counts /dev/null (9 characters against NUL's 3, twice in the body: 6 bytes). Case 173 now gets /dev/null; -o cases (3015) keep NUL.
- 329: harness. Two file parts with one name: upstream getpart concatenates them into one file under the first name; the harness kept only the first, losing tester=yes. UpstreamTestCaseCommandLine now concatenates.
- linux-x64 and osx-arm64 lists are not changed: lanes only run Windows.

## Log

- 2026-11-12: Created.
- 2026-11-12: Backlog -> Doing.
- 2026-11-12: Doing -> Done. Cases 60, 173 and 329 pass: chunked beside Content-Length read as chunks, /dev/null for 173, duplicate file parts concatenated
