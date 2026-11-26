---
id: BL-1758
title: Withhold every byte the HTTP/2 redactor receives that does not open with the client preface
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1717]
touches: [Protocols/Http/Surl.Http2.UnitLibrary, Protocols/Http/Surl.Http2.UnitTests, Documentation/Planning/Decisions/ADR-0182-surl-withholds-client-passwords-and-tokens-from-its-exchange-log-and-trace-dumps.md]
requirement: none
created: 2026-11-25
completed: 2026-11-25
---
# BL-1758 — Withhold every byte the HTTP/2 redactor receives that does not open with the client preface

## Goal

An HTTP/1.1 request, or any other bytes that are not the HTTP/2 client preface, sent to a `grpc` listen URL is logged
with none of its credentials, API keys or session tokens.

## Context

- Found while working BL-1717 by reading the code, not by running it, so the first step is a failing test.
- `Http2RequestBytesRedactor` (`Protocols/Http/Surl.Http2.UnitLibrary`) writes the first 24 bytes as the preface without
  comparing them (`PassThroughPreface`) and reads the rest as frames, so text after them passes through as frames of
  unknown type. `GrpcProtocolServer` answers such bytes with `GOAWAY` `PROTOCOL_ERROR`, but they are still logged.
- How the other users reach the redactor, as read on 2026-11-25:
  - `DnsConnectionBytesRedactor` compares the preface before it makes one: not affected.
  - `HttpProxyConnectionBytesRedactor` and `SocksTextTunnelBytesRedactor` make one only after finding the exact
    preface, and feed it that preface: not affected.
  - `HttpConnectionBytesRedactor` (the http and https servers) compares the first bytes, but after a `101` to
    `Upgrade: h2c` hands the next client bytes to the redactor uncompared: affected in the same way.
  - `GrpcProtocolServer.CreateRedactor` returns it directly: affected.
- So the fix belongs in `Surl.Http2`, which covers both affected users: compare each preface byte as it arrives and,
  from the first one that differs, fail closed (one `****`, nothing more), as `HttpProxyWithheldBytesRedactor` does.
  Redacting such bytes as HTTP/1.x heads in gRPC instead is not recommended: it needs a new project reference there
  and leaves the `101` case open.
- Stand-in: only the levelled log and trace dumps change. The event stream keeps raw bytes (ADR-0182 decision 4), so a
  test still sees what the client sent; no store or fault is involved.

## Acceptance criteria

- [x] `Http2RequestBytesRedactorTests.Redact_Http1RequestInPlaceOfThePreface_WritesOnlyAMask` is seen to fail before the
      fix and passes after it: a redactor made with `HttpWithheldHeaderNames.ApiKeysAndSessionTokens`, given a `GET`
      request carrying `Authorization` and `x-api-key`, writes `****` and nothing else.
- [x] `Http2RequestBytesRedactorTests.Redact_BytesThatLeaveThePrefacePartWay_WritesTheMatchingBytesThenOneMask` passes
      for a `PUT` request delivered whole and one byte at a time: `P`, then `****`, and nothing for later bytes or from
      `Flush`.
- [x] The tests that `Http2RequestBytesRedactorTests` held before this task pass with their expected output unchanged.
- [x] The class remarks of `Http2RequestBytesRedactor` and a new ADR-0182 amendment (the next free number) say what is
      logged when the bytes do not open with the preface.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green (the http, gRPC, DNS, HTTP proxy and SOCKS tests
      unchanged), and `Surl.Http2.UnitLibrary` stays at 100% line and branch coverage.

## Notes

- Failing first: before the fix the new tests failed, the `GET` and `PUT` requests logged as received (4 failures of 50).
- Fix: `Http2RequestBytesRedactor.ReadPreface` compares each preface byte; the first that differs writes one `****`
  and stops all output. Recorded as ADR-0182 amendment 17 (decided by Claude under Stewart's delegation). No upstream
  curl measurement: no byte Surl sends or expects changes, only what its log writes.
- Verified: build `-warnaserror` clean, format clean, 29558 fast tests passed in 83 test projects, `Surl.Http2` 100%
  line and branch, 0 failing members. No test project but `Surl.Http2.UnitTests` changed.
- Help text: the last paragraph of the `logging` topic (`AiHelpProse.cs`) and of the manual (`ManualText.cs`) still
  says bytes sent in place of the preface are logged as received. That sentence is now false and is left to BL-1764,
  which depends on this task and owns it (`Surl.Cli` is outside this task's `touches`). This task closes the whole
  sentence, both the `grpc` and the `101` cases.
- Review: `code-reviewer` found no code defect; its two ADR wording fixes and one summary qualification are applied.
  Its suggested consumer tests (gRPC `CreateRedactor`, the `101` path) were not filed: both consumers pass bytes
  straight to the redactor, whose tests cover the rule.

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing.
- 2026-11-25: Doing -> Done. Bytes that do not open with the HTTP/2 preface are logged as the matching bytes then one ****, so HTTP/1.1 secrets sent to grpc or after an h2c 101 stay out of the log
