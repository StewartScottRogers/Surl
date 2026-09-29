---
id: BL-113
title: Challenge and verify HTTP Digest with MD5, SHA-256 and SHA-512-256 in Surl.Authentication
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-110, BL-112]
touches: [Surl.Authentication.UnitLibrary, Surl.Authentication.UnitTests]
requirement: FR-014
created: 2026-09-29
completed:
---
# BL-113 — Challenge and verify HTTP Digest with MD5, SHA-256 and SHA-512-256 in Surl.Authentication

## Goal

`Surl.Authentication` issues the HTTP Digest challenges ADR-0032 decision 4 fixes (RFC 7616:
`MD5`, `SHA-256`, `SHA-512-256` and their `-sess` forms, `qop="auth"`) with nonces bound to
an injected `TimeProvider`, and verifies the `Authorization: Digest ...` values upstream curl
8.21.0 sends, on encrypted and unencrypted connections alike.

## Context

FR-014; ADR-0032 (BL-100) decisions 3 and 4 (which algorithms are offered, in what order,
the realm, nonce lifetime and `stale=true`, `userhash`), 7 (whether `Surl.Authentication`
references `Surl.Cryptography` for SHA-512/256) and 8. BL-110 built accounts and policy;
BL-112 built SHA-512/256. MD5 and SHA-256 are in the BCL (`MD5.HashData`, `SHA256.HashData`).

Measured 2026-09-29 while planning (recorded in BL-100's Context), pinned Windows reference
build `C:\Program Files\Git\mingw64\bin\curl.exe` (curl 8.21.0, SSPI):
`-sS --digest -u a:b http://127.0.0.1:P/x` against a `401` with
`WWW-Authenticate: Digest realm="r", nonce="abc", algorithm=MD5, qop="auth"` exit 0, second
request carrying
`Authorization: Digest username="a",realm="r",nonce="abc",uri="/x",cnonce="41962db74966964144d159ea6dd08e0f",nc=00000001,algorithm=MD5,response="e5b7098755fc95f5145e9b59f25d81e6",qop="auth"`;
`algorithm=SHA-256` and `SHA-512-256` exit 94. The Linux and macOS reference builds use
curl's own Digest code.

- Measure and save as fixtures (`Surl.Authentication.UnitTests/Fixtures/<case>/` with a
  `README.md` of command line, build SHA-256, date) with `Record-CurlExchange.ps1`,
  `-Connections 2`: MD5, MD5-sess, a user name with non-ASCII characters, a request URI
  with a query, and `-X POST -d x`. SHA-256 and SHA-512-256 answers cannot come from the
  Windows build; verify them with RFC 7616 section 3.9.1's worked example (SHA-256 and MD5)
  and pin curl's own SHA-512-256 answer later in BL-118's Linux/macOS conformance case.
- The `response` check uses `CryptographicOperations.FixedTimeEquals` on the hex digits.

## Acceptance criteria

- [ ] Tests replay each measured MD5 fixture (its fixed nonce and cnonce, with the account
      `a:b` or the one used) and prove it verifies; a changed `response`, `uri`, `nc`, realm
      or user is refused.
- [ ] Tests reproduce RFC 7616 section 3.9.1's example responses for MD5 and SHA-256, and a
      SHA-512-256 response computed from the same inputs via BL-112 is accepted.
- [ ] Tests prove nonces are unpredictable (from `RandomNumberGenerator`), expire after the
      ADR's lifetime using a fake `TimeProvider`, and an expired but otherwise right answer
      gets the `stale=true` challenge ADR-0032 decides.
- [ ] Tests pin the exact challenge header values ADR-0032 decision 4 gives, in order.
- [ ] `dotnet build Surl.Authentication.UnitLibrary -warnaserror` is clean; the fast tests
      pass; 100% line and branch coverage; no method exceeds complexity 10.

## Notes

## Log

- 2026-09-29: Created.
