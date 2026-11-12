---
id: BL-830
title: Record what BL-411 settled for extended CONNECT and HTTP datagrams in ADR-0086
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-411, BL-1035]
touches: [Documentation/Planning/Decisions/ADR-0086-how-the-extended-servers-share-http-2-http-3-and-quic.md, Documentation/Planning/Decisions/README.md]
requirement: none
created: 2026-11-05
completed:
---
# BL-830 — Record what BL-411 settled for extended CONNECT and HTTP datagrams in ADR-0086

## Goal

ADR-0086 gains "Amendment 2 - what BL-411 built for decision 3", stating each detail below as the
code in `Surl.Http3` does it, marked "Decided by Claude under Stewart's delegation".

## Context

BL-411 built ADR-0086 decision 3 in `Protocols/Http/Surl.Http3.UnitLibrary`, but could not edit the
ADR while BL-569 held `Documentation/Planning/Decisions`. The decisions are in BL-411's Notes and
the library's `CLAUDE.md`; this task copies them into the ADR, checking each against the code.

1. `IHttp3ExtendedConnectHandler`'s signal and stream type are `ulong?`, not the planning `long?`:
   they are QUIC variable-length integers, as `QuicStream.Id` is.
2. The interface gains `ServeStreamWithoutSessionAsync(Http3SessionStream, CancellationToken)`:
   a claimed stream naming no open session of that handler goes to it, so the WebTransport server
   (ADR-0117 decision 3) holds or resets it with its own codes; `Surl.Http3` knows no WebTransport code.
3. An extended `CONNECT` for an unregistered `:protocol` is answered `:status 501`, the `server`
   field when set, `content-length: 0`, FIN, then `STOP_SENDING` `H3_NO_ERROR` (RFC 9220 section 3:
   "the server SHOULD respond to the request with a 501 (Not Implemented) status code").
4. `:protocol` is accepted only when a handler is registered, only on `CONNECT`, and only with
   `:scheme`, `:authority` and a non-empty `:path`; otherwise `H3_MESSAGE_ERROR`, as before.
5. `SETTINGS` order: `MAX_FIELD_SECTION_SIZE`, `ENABLE_CONNECT_PROTOCOL` (0x08) = 1,
   `H3_DATAGRAM` (0x33) = 1, then `Http3ServerOptions.FurtherSettings` in order.
6. HTTP datagrams: a quarter stream ID cut short or past 2^60 - 1 closes `H3_DATAGRAM_ERROR`
   (0x33, RFC 9297 section 2.1); one naming no open session is dropped with a log note (section
   2.1 allows dropping). A session is open from its `CONNECT`'s arrival until `ServeSessionAsync`
   returns, so datagrams before the answer wait in it (128, oldest dropped); datagrams that arrive
   before the `CONNECT` itself are dropped, which narrows ADR-0117 decision 3's 64-datagram hold to
   that window.
7. A session ID that is not a client-initiated bidirectional stream ID closes `H3_ID_ERROR` (0x108).
   A registered bidirectional signal anywhere but a request stream's first integer, including as a
   frame type after `HEADERS`, is `H3_FRAME_ERROR` (0x106).
8. Capsules: `ReadCapsuleAsync` returns every type and the handler drops unknown ones (RFC 9297
   section 3.2); a capsule longer than `--max-message` resets the stream `H3_EXCESSIVE_LOAD`, and a
   FIN inside one resets it `H3_MESSAGE_ERROR` (section 3.3).
9. The client's `SETTINGS` are kept and exposed as `Http3ExtendedConnectSession.ReadClientSettingsAsync`,
   so a protocol can wait for and check them (ADR-0117 decision 0).

## Acceptance criteria

- [ ] ADR-0086 has an "Amendment 2" section stating items 1 to 9, each true of
      `Protocols/Http/Surl.Http3.UnitLibrary` as it is.
- [ ] `Documentation/Planning/Decisions/README.md` needs no change, or is updated if it lists amendments.

## Notes

- 2026-11-11 (lane 6): the ADR-0086 Amendment 2 and README row are written and re-checked against
  `Surl.Http3` (`ulong?` signals, `ServeStreamWithoutSessionAsync`, `ReadClientSettingsAsync`,
  `ReceivedDatagramCapacity` 128, BL-894's `HeldDatagramCapacity` 64). They are on branch
  `factory/BL-830-lane-4-20261111-060243` (commit afec1d51): cherry-pick its two Documentation files.
  Not finished only because `dotnet build` cannot run: `global.json` pins SDK 10.0.401, not
  installed (BL-1035), so the task waits on BL-1035.

## Log

- 2026-11-05: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Backlog. Lane 4 could not integrate: build failed after rebasing onto the other lanes' work. The work is on branch factory/BL-830-lane-4-20261111-060243; start with git cherry-pick --no-commit factory/BL-830-lane-4-20261111-060243 and fix it.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Backlog. Waits on BL-1035: pinned .NET SDK 10.0.401 is not installed, so dotnet build cannot run; the ADR text is ready on factory/BL-830-lane-4-20261111-060243
- 2026-11-11: Backlog -> Doing.
