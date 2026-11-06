---
id: BL-710
title: Hold HTTP/3 answer bodies back until the client's QUIC credit allows them
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-383]
touches: [Protocols/Http/Surl.Quic.UnitLibrary, Protocols/Http/Surl.Quic.UnitTests, Protocols/Http/Surl.Http3.UnitLibrary, Protocols/Http/Surl.Http3.UnitTests, Protocols/Http/Surl.Protocol.Http.UnitLibrary, Protocols/Http/Surl.Protocol.Http.UnitTests, Documentation/Planning/Decisions/ADR-0082-how-surl-serves-http-3-over-quic.md]
requirement: none
created: 2026-11-05
completed: 2026-11-06
---
# BL-710 — Hold HTTP/3 answer bodies back until the client's QUIC credit allows them

## Goal

Serving a content-store file over HTTP/3 holds no more than a bounded amount of it in memory:
`Http3DataStream` (in `Surl.Http3` since BL-410) waits for the client's QUIC credit before it hands over the next piece, as
HTTP/2's `Http2DataStream` waits for its windows.

## Context

- Today `QuicApplicationConnection.WriteAsync` (`Surl.Quic`) queues whatever it is given and
  returns at once ("the rest goes as credit arrives"), so `Http3StreamAnswerWriter.CopyFileAsync`
  copies a whole file into the stream's unsent buffer however large it is. ADR-0082 Amendment 1,
  item 7, records the gap.
- `QuicStream.UnsentLength` says how much is held back; `Surl.Quic` needs a way to wait until it
  drops below a bound (or until the stream is reset or the connection ends), under the connection's
  one lock, as `ReadAsync` waits for bytes.
- Filed by BL-383.

## Acceptance criteria

- [x] A named `Surl.Quic.UnitTests` test: a write waits while the stream's unsent bytes are past
      the bound, and completes once the client's `MAX_STREAM_DATA` or `MAX_DATA` lets them go; it
      throws `OperationCanceledException` once the connection ends.
- [x] A named `Surl.Protocol.Http.UnitTests` test: a file larger than the client's
      `initial_max_stream_data_bidi_local` is sent whole only as the client grants credit, and the
      server never holds more than the bound unsent.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and both libraries keep 100%
      line and branch coverage.

## Notes

- Plan, done in one method of `Surl.Quic` (no new seam): `QuicApplicationConnection.WriteAsync`
  now queues, under the connection lock, only as much as keeps `QuicStream.UnsentLength` within the
  new public `QuicApplicationConnection.MaxUnsentLength` (65536), the `FIN` only with the last
  piece, transmits, and waits on the connection's change signal (as `ReadAsync` does) before
  queuing more. The connection ending throws `OperationCanceledException`; a stream reset
  meanwhile throws `InvalidOperationException`, as writes to such a stream always have.
  `Http3DataStream` and `Http3StreamAnswerWriter` needed no change: they already await the write.
- Decision (ADR-0082 amendment 3, decided by Claude under Stewart's delegation): the bound is
  64 KiB - twice curl's recorded `initial_max_stream_data_bidi_local` (32768) and under the store's
  81920-byte copy buffer. No byte on the wire changes, so there is nothing to measure from upstream curl.
- Learned: bytes the client's credit allows leave `UnsentLength` as `STREAM` frames at once, even
  when the congestion window holds them, so an answer holds the bound plus the client's
  outstanding credit plus one copy buffer. The HTTP test therefore checks at each credit stall
  (where credit equals what was sent) that the file was read no more than bound + copy buffer past
  it; with the bound removed it fails (read 262144, allowed 180224). Bounding the congestion-held
  frames too is filed as BL-928.
- Tests: `QuicApplicationConnectionTests.WriteAsync_MoreThanTheBoundPastTheClientsCredit_WaitsWithTheBoundUnsentUntilTheClientsCreditLetsItGo`
  (two rows: `MAX_STREAM_DATA`, and `MAX_DATA` with the client's two credits swapped),
  `QuicApplicationConnectionTests.WriteAsync_WaitingForCreditWhenTheConnectionEnds_ThrowsOperationCanceled`,
  `Http3ConnectionServerTests.ServeAsync_FileLargerThanTheClientsStreamCredit_IsSentWholeOnlyAsTheClientGrantsCredit`.
  `UnitTestInMemoryContentFileSystem` now records the streams it opens, so the test can see how far the file was read.
- Coverage (`Measure-CodeQuality.ps1`): `Surl.Quic.UnitLibrary` and `Surl.Protocol.Http.UnitLibrary`
  are at 100% line and branch. The members it lists as failing (complexity in `QuicLossDetection`,
  `QuicStreams`, `QuicServerConnection.Closing`, `HttpRequestAnswerer.Uploads`; one line in
  `Http3ConnectionServer.DeliverDatagramsAsync`) were failing before this task, and it touches none of them.
- Touches: added `Documentation/Planning/Decisions/ADR-0082-how-surl-serves-http-3-over-quic.md` for
  amendment 3; no task in Doing names it.

## Log

- 2026-11-05: Created.
- 2026-11-06: Backlog -> Doing.
- 2026-11-06: Doing -> Done. QUIC writes wait for client credit past a 64 KiB unsent bound, so HTTP/3 file answers hold a bounded amount
