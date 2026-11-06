---
id: BL-579
title: Record the bytes each client sends to surl through a loopback relay for Phase 7 comparisons
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-576, BL-578]
touches: [Conformance/Surl.Conformance.UnitLibrary, Conformance/Surl.Conformance.UnitTests]
requirement: none
created: 2026-11-04
completed: 2026-11-04
---
# BL-579 — Record the bytes each client sends to surl through a loopback relay for Phase 7 comparisons

## Goal

A Phase 7 case can put a recording relay between the client and `surl`. Both the upstream run and the port
run then come back with the bytes the client sent on each connection, in order, and BL-578's comparison
compares those bytes as BL-576's ADR decision 4 says. The comparison covers which schemes, the plaintext
ones only or more, and the normalisation rules that apply.

## Context

- Decision: BL-576's ADR, decision 4. If the ADR decides that client request bytes are not compared, it
  defers this task and this task is not run.
- Success criterion 1 of `Documentation/Product/Product-Overview.md` names "the bytes on the wire". `surl` has
  no option that dumps what it receives, so the recording sits outside `surl`.
- Only `Surl.Networking.UnitLibrary` constructs a `Socket` or `TcpListener` in production code (root `CLAUDE.md`).
  The relay that opens sockets therefore lives in `Conformance/Surl.Conformance.UnitTests`, beside the existing
  relays `ClientKexInitRecorder` and `DigestChallengeRelay`, which show the shape. The planning name is
  `ClientBytesRelay`. The logic that needs no socket lives in `Conformance/Surl.Conformance.UnitLibrary` and is
  unit tested: one transcript per connection (planning name `ClientByteTranscript`), and comparing two
  transcripts after normalisation.
- The relay forwards both directions unchanged, records only client-to-server bytes, and closes each side
  when the other closes. The client is pointed at the relay's port and the relay at `surl`'s port. A scheme
  whose listen URL fixes the port in the request (FTP's `PASV` and `EPSV` data ports, for example) is
  either handled or listed by the ADR as not comparable. Record which in the ADR's table.

## Acceptance criteria

- [x] Named unit tests, with no socket, show that transcripts per connection are kept in connection order,
      that two transcripts differing only in a field the ADR normalises agree, and that any other byte
      difference is a finding that names the connection index and the first differing offset.
- [x] An `[TestCategory("Integration")]` test sends a known request through `ClientBytesRelay` to a live
      `surl http://` with pinned upstream curl, and gets the exact request bytes back. The expected bytes come
      from the existing HTTP/1.1 conformance test's measured request, not from the port.
- [x] BL-578's comparison includes the transcripts. Its smoke test now compares request bytes.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and `Surl.Conformance.UnitLibrary` keeps
      100% line and branch coverage.

## Notes

- Built (2026-11-04): `ClientByteTranscript` (library; one entry per connection in accept order,
  thread-safe), `RequestBytesNormalisation` (library, internal, behind
  `CurlPortResultNormaliser.NormaliseRequestBytes`), `CurlPortSurlSide.RelayPort` and `ClientFacingPort`,
  `CurlPortMeasurementCase.RecordsClientBytes` and `PlaintextTcpSchemes`; in the tests `ClientBytesRelay`,
  `PinnedCurlPortMeasurement`, and `PinnedCurlPortBesideUpstreamCurl` now relays every recorded case.
- Decision (sensible default, no ADR needed - ADR-0089 decision 4 already says which): recording is on by
  default for the plaintext TCP schemes (dict, ftp, gopher, http, imap, ldap, mqtt, pop3, rtsp, smb, smtp,
  telnet, ws) and off for TLS, SSH and UDP ones; a case that upgrades to TLS part way sets
  `RecordsClientBytes = false`, because the bytes after the upgrade are its TLS library's. FTP data
  connections bypass the relay (ADR-0089 decision 4 already records this, so its table needed no edit).
- Decision: with a relay the client sees the relay's port, never surl's, so stdout, stderr and request
  bytes are normalised with `ClientFacingPort` (relay port, else surl port); surl's notes keep surl's port
  plus the loopback-port rule.
- Decision: NTLM is normalised by zeroing the LM and NT responses, the encrypted session key and the MIC
  (MIC only when the payload starts at 88 or later, MS-NLMP 2.2.1.3), raw or inside base64, so the user,
  domain and workstation are still compared. SASL `DIGEST-MD5` lines are decoded, normalised and
  re-encoded, so a difference's offset is in the base64 form.
- The HTTP/1.1 conformance test asserts exit code and output only, not request bytes, so the
  integration test's expected request is upstream curl 8.21.0's own as measured and recorded in ADR-0007
  (`GET /hello.txt HTTP/1.1`, `Host`, `User-Agent: curl/8.21.0`, `Accept: */*`). Live run 2026-11-04:
  exact match; the smoke test's verdict was agreement with request bytes compared.
- Coverage of `Surl.Conformance.UnitLibrary`: 100% line, 100% branch. The two complexity failures the
  measurement shows (`UpstreamCurlBuildFeatures`) predate this task and are BL-599.

## Log

- 2026-11-04: Created.
- 2026-11-04: Backlog -> Doing.
- 2026-11-04: Doing -> Done. Phase 7 runs relay each plaintext TCP connection through ClientBytesRelay and compare normalised client request bytes per connection
