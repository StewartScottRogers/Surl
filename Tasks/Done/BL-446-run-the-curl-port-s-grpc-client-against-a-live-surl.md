---
id: BL-446
title: Run the Curl port's grpc client against a live surl
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-1093, BL-406]
touches: [Conformance/Surl.Conformance.UnitTests, Documentation/Conformance/Extended/Grpc.md]
requirement: none
created: 2026-11-04
completed: 2026-11-12
---
# BL-446 — Run the Curl port's grpc client against a live surl

## Goal

`Integration` tests run the pinned Curl port client against a live `surl grpc://` and `surl grpcs://`. They make
a unary call described by reflection, a call with a `.proto` file, a call to an unknown method, and an
unauthenticated call. Each assertion comes from PROTOCOL-HTTP2.md and statuscodes.md, and the matrix is fully
covered.

## Context

- Rules: BL-543's ADR, decisions 1 to 3 (the port is only a live client; report a port defect in Notes).
- The locator and runner: BL-406. A live `surl`: `SurlOnLoopback`. Command lines: the port's
  `curl --extended-help grpc` and its cases in `Curl.Conformance.Extended.UnitTests/Grpc/`.
- `[TestCategory("Integration")]`, and inconclusive without the pinned client.

## Acceptance criteria

- [x] The test class exists with the four exchanges, each passing on Windows with the pinned client present.
- [x] `Documentation/Conformance/Extended/Grpc.md`: every row is covered by a named test or marked `n/a` with a reason.
- [x] The fast tests are green, and `dotnet build -warnaserror` is clean.

## Notes
- `CurlPortClientCallsMethodsOnSurlOverGrpcTests` (Integration): Echo by reflection, Echo with `--grpc-proto`, Echo over `grpcs` (`--self-signed`, client `-k`), unknown method (`UNIMPLEMENTED`, 12), no credentials (`UNAUTHENTICATED`, 16), and plaintext credentials refused unless `--allow-plaintext-auth`. Six tests, all pass on Windows with the pinned client.
- Expected values come from PROTOCOL-HTTP2.md and statuscodes.md via Grpc.md; the client's `grpc-status N` stderr text is only used to read the status the server was required to send.
- Port defect (not evidence): the port's grpc client ignores `-u`, so the login is sent as `-H "Authorization: Basic ..."`.
- Decision: plaintext credentials without `--allow-plaintext-auth` answer `PERMISSION_DENIED` (7), Grpc:Surl-Auth-2's Forbidden verdict; pinned as such.
- Grpc.md: the BL-444 and BL-445 rows now name unit tests or this class; `Grpc:Reflection-8` is `n/a` with a reason.

## Log

- 2026-11-04: Created.
- 2026-11-12: depends-on BL-445 repointed to BL-1093 (BL-445 split into BL-1094, BL-1095, BL-1093).
- 2026-11-12: Backlog -> Doing.
- 2026-11-12: Doing -> Done. The pinned Curl port's grpc client makes reflection, .proto, TLS, unknown-method and unauthenticated calls against a live surl; Grpc.md rows all covered
