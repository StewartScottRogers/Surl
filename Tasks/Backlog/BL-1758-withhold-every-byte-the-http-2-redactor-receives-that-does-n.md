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
completed:
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

- [ ] `Http2RequestBytesRedactorTests.Redact_Http1RequestInPlaceOfThePreface_WritesOnlyAMask` is seen to fail before the
      fix and passes after it: a redactor made with `HttpWithheldHeaderNames.ApiKeysAndSessionTokens`, given a `GET`
      request carrying `Authorization` and `x-api-key`, writes `****` and nothing else.
- [ ] `Http2RequestBytesRedactorTests.Redact_BytesThatLeaveThePrefacePartWay_WritesTheMatchingBytesThenOneMask` passes
      for a `PUT` request delivered whole and one byte at a time: `P`, then `****`, and nothing for later bytes or from
      `Flush`.
- [ ] The tests that `Http2RequestBytesRedactorTests` held before this task pass with their expected output unchanged.
- [ ] The class remarks of `Http2RequestBytesRedactor` and a new ADR-0182 amendment (the next free number) say what is
      logged when the bytes do not open with the preface.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green (the http, gRPC, DNS, HTTP proxy and SOCKS tests
      unchanged), and `Surl.Http2.UnitLibrary` stays at 100% line and branch coverage.

## Notes

## Log

- 2026-11-25: Created.
