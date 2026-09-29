---
id: BL-054
title: Refuse writes by default, bound uploads and answer refusals in Surl.Protocol.Tftp
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-037, BL-046, BL-047]
touches: [Surl.Protocol.Tftp.UnitLibrary, Surl.Protocol.Tftp.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-054 — Refuse writes by default, bound uploads and answer refusals in Surl.Protocol.Tftp

## Goal

The TFTP server (BL-037) refuses a `WRQ` with error 2 unless uploads are allowed, writes
an allowed upload through `Surl.Content` and answers one past `MaxUploadBytes` with
error 3 and no file left behind, and can answer a flow past a connection limit with
error 0 - with every datagram one pinned upstream curl 8.21.0 was fed and recorded.

## Context

- Specification: `Documentation/Planning/Decisions/ADR-0006-hardening-for-internet-facing-use.md`,
  sections 1 ("A datagram flow (TFTP) counts as one connection"; "TFTP needs no size
  limit" on its request; "Maximum upload"; "A server that can say the limit before the
  body arrives does so"), 2 (uploads off; "TFTP error 2" for a refused upload), 3 (fixed
  error text) and 5 (the TFTP cells: error packet 0 through `IDatagramRefusalWriter`, then the flow ends, for too many
  connections; error 3 for an upload too large, partial upload deleted). Contract types
  come from BL-046; the upload path, its "not permitted" and "upload too large" results,
  and partial-file deletion come from BL-047.
- `WRQ` with uploads off: RFC 1350 `ERROR` code 2 (access violation) as the first reply,
  and no file created. BL-037 answered every `WRQ` with an error; replace that with this.
- `WRQ` with uploads on: receive `DATA` and `ACK` it as RFC 1350 section 6 says, writing
  through `Surl.Content`'s upload path. When the store returns "upload too large", send
  `ERROR` code 3 (disk full or allocation exceeded) and end the flow; the store has
  already deleted the partial file. If the `WRQ` carries a `tsize` option (RFC 2349)
  over `MaxUploadBytes`, answer error 3 before any `DATA`.
- Refusal: the TFTP server implements `IDatagramRefusalWriter` (ADR-0006 section 6,
  added by BL-046), sending one `ERROR` code 0 datagram with the server's fixed text for
  either `ConnectionRefusal`, so `Surl.Core` (BL-025, BL-032) can call it.
- One flow, one connection: `Surl.Core` counts one `IDatagramFlow` as one connection.
  The server serves a whole transfer, read or write, inside one `ServeAsync` on that one
  flow (using `MoveToNewLocalPortAsync` for RFC 1350's new transfer identifier) and never
  opens another; state that in the server's XML doc.
- Error text: each `ERROR` packet's message is the server's fixed text, never a path,
  exception message or OS error.
- Measurement (ADR-0003): with `Record-CurlExchange.ps1 -Tftp` (BL-030) and the pinned
  build, record `curl -T <file> tftp://127.0.0.1:<P>/up.txt` fed error 2 as the first
  reply (`-TftpReply`), fed error 3 as the first reply, an accepted write, and a read fed
  error 0 as the first reply; commit each under
  `Surl.Protocol.Tftp.UnitTests/Fixtures/<case>/` as `EmbeddedResource` with the command
  line and build SHA-256 in its `README.md`, recording curl's exit code and stderr as the
  build reported them.
- Tests drive the server with BL-037's in-memory datagram flow and a `Surl.Content` store
  over the in-memory file-system fake.

## Acceptance criteria

- [ ] `WriteRequestTests.Wrq_WithUploadsOff_AnswersError2AndCreatesNoFile` passes.
- [ ] `WriteRequestTests.Wrq_WithUploadsOn_WritesTheFile` passes, its `ACK` datagrams
      equal the accepted write recording's.
- [ ] `UploadLimitTests.UploadOverMaxUploadBytes_AnswersError3AndLeavesNoFile` and
      `UploadLimitTests.TsizeOverMaxUploadBytes_AnswersError3BeforeAnyData` pass.
- [ ] `ConnectionRefusalTests` prove both `ConnectionRefusal` values send exactly one
      `ERROR` code 0 datagram and end the flow.
- [ ] A test proves a whole write transfer is served on the one flow it was given.
- [ ] Recordings for each case above are committed; each test's expected datagrams equal
      the datagrams that recording fed to pinned upstream curl 8.21.0.
- [ ] `ProtocolIsolationTests` pass. `dotnet build Surl.Protocol.Tftp.UnitLibrary
      -warnaserror` is clean, the fast tests are green with no `Integration` test in
      `Surl.Protocol.Tftp.UnitTests`, and `Measure-CodeQuality.ps1` reports no failing
      member in `Surl.Protocol.Tftp.UnitLibrary`.

## Notes

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
