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
completed:
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

- [ ] Named unit tests, with no socket, show that transcripts per connection are kept in connection order,
      that two transcripts differing only in a field the ADR normalises agree, and that any other byte
      difference is a finding that names the connection index and the first differing offset.
- [ ] An `[TestCategory("Integration")]` test sends a known request through `ClientBytesRelay` to a live
      `surl http://` with pinned upstream curl, and gets the exact request bytes back. The expected bytes come
      from the existing HTTP/1.1 conformance test's measured request, not from the port.
- [ ] BL-578's comparison includes the transcripts. Its smoke test now compares request bytes.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and `Surl.Conformance.UnitLibrary` keeps
      100% line and branch coverage.

## Notes

## Log

- 2026-11-04: Created.
