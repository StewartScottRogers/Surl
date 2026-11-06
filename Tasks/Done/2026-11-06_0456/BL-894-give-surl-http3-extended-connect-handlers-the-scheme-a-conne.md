---
id: BL-894
title: Give Surl.Http3 extended CONNECT handlers the scheme, a connection close, early datagrams and a drain before GOAWAY
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-459]
touches: [Protocols/Http/Surl.Http3.UnitLibrary, Protocols/Http/Surl.Http3.UnitTests]
requirement: none
created: 2026-11-05
completed: 2026-11-06
---
# BL-894 — Give Surl.Http3 extended CONNECT handlers the scheme, a connection close, early datagrams and a drain before GOAWAY

## Goal

An `IHttp3ExtendedConnectHandler` can do four things ADR-0117 asks of the WebTransport server and BL-459
found `Surl.Http3` does not offer: read the CONNECT's `:scheme`, close the whole connection with an error
code of its choosing, receive HTTP datagrams that arrived before their CONNECT, and send capsules on its
sessions at shutdown before `GOAWAY` and keep serving them through the shutdown grace.

## Context

- ADR-0117 decision 0 (`WT_REQUIREMENTS_NOT_MET` closes the connection), decision 1 (`:scheme` not `https`
  is `:status 400`), decision 3 (up to 64 datagrams a connection held for a session not yet established,
  for at most `--head-timeout`), decision 6 (`WT_DRAIN_SESSION`, then `GOAWAY`, sessions served during the
  grace, then `WT_CLOSE_SESSION` code 0; wait for the client to close each CONNECT stream before
  `CONNECTION_CLOSE`).
- Today: `Http3RequestHead` drops `:scheme`; `Http3ConnectionException` is internal, so a handler cannot
  close the connection; `Http3ExtendedConnect.Deliver` drops a datagram for no open session; and on
  cancellation `Http3ConnectionServer` cancels every handler at once and sends `GOAWAY` only after they
  return (`ServeAsync`, `EndAsync`).
- Keep the HTTP server's bytes unchanged: everything new is opt-in for extended CONNECT handlers.

## Acceptance criteria

- [x] `Http3RequestHead` carries `:scheme` for requests that have one; a named test reads it on an extended CONNECT.
- [x] A handler can close the connection with a code it chooses (e.g. on `Http3ExtendedConnectSession`); a named test sees `CONNECTION_CLOSE` with that code.
- [x] A datagram whose quarter stream ID names a client bidirectional stream with no CONNECT yet is held (64 a connection, at most `--head-timeout`) and delivered when the session opens; a named test shows it delivered, another shows it dropped past the limit or the timeout.
- [x] On shutdown a handler learns of it before `GOAWAY` is sent (a shutdown token or callback distinct from the connection's end), may still write to its session during the grace, and `GOAWAY` follows; named tests show the order.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, `Surl.Protocol.Http.UnitTests` still passes unchanged, and the library keeps 100% line and branch coverage.

## Notes

Filed by BL-459, which built the WebTransport server on what `Surl.Http3` offers today; BL-895 then uses these.

What was built (2026-11-06), and the tests that show it:

- `Http3RequestHead.Scheme`: the `:scheme` as sent, null for a plain `CONNECT`
  (`Http3ExtendedConnectTests.ServeAsync_ExtendedConnectForARegisteredProtocol_ReachesItsHandler`,
  `Http3RequestHeadMappingTests.Map_WellFormedGet_...`, `Map_Connect_TargetsItsAuthority`).
- `Http3ExtendedConnectSession.CloseConnection(code, reason)`: the code goes through the same path
  as a client-caused connection error; the first close wins
  (`Http3ExtendedConnectSessionTests.CloseConnection_ACodeOfTheSessionsProtocol_ClosesTheConnectionWithThatCode`,
  `WT_REQUIREMENTS_NOT_MET` 0x212c0d48 cast to `Http3ErrorCode`).
- Held datagrams in `Http3ExtendedConnect`: a datagram for no open session is held for the
  session its quarter stream ID names, 64 a connection, each for at most the head timeout (none
  with no head timeout), and handed over in arrival order when that session opens
  (`ServeAsync_DatagramsBeforeTheirConnect_AreHeldThenDeliveredInOrderWhenTheSessionOpens`,
  `ServeAsync_DatagramPastTheSixtyFourHeld_IsDropped`,
  `ServeAsync_HeldDatagramPastTheHeadTimeout_IsDroppedAndTheSessionGetsOnlyLaterOnes`,
  `ServeAsync_HeldDatagramWithNoHeadTimeout_IsKeptUntilTheSessionOpens`).
- Drain at shutdown: `Http3ServerOptions.ShutdownGrace`, `Http3ExtendedConnectSession.IsShuttingDown`
  and `AddShutdownWrite` (`ServeAsync_ShutdownWithAGrace_RunsTheShutdownWritesThenGoAwayThenServesUntilTheGraceHasPassed`,
  `ServeAsync_ShutdownWithAGraceAndNoOpenStream_SendsGoAwayAndClosesWithoutWaitingForTheGrace`,
  `ServeAsync_SessionOpeningAfterShutdownBegan_IsShuttingDownAndTakesNoShutdownWrite`).

Choices made under Stewart's delegation (they implement ADR-0117 decisions 0, 1, 3 and 6, so no
new ADR; they are recorded here and in the library's `CLAUDE.md`):

- **The drain is opt-in through a nullable `ShutdownGrace`, default null.** Null keeps today's
  order exactly (streams cut off, then `GOAWAY`), so the HTTP server's bytes do not change and
  today's `Surl.Extended.Protocol.WebTransport` - which writes its drain when its token is
  cancelled and relies on `GOAWAY` coming after - still passes unchanged (203 tests). BL-895 sets
  the grace and moves the drain into `AddShutdownWrite`.
- **A callback, not only a token.** `AddShutdownWrite` lets the connection await the drain
  before it writes `GOAWAY`, which a token alone cannot order. `IsShuttingDown` covers a session
  opening during the grace (it starts marked, and `AddShutdownWrite` returns false). The writes'
  token is the connection's own (end or failure), not the grace, so a zero grace still drains.
- **The grace runs from the cancellation on the exchange's `TimeProvider`.** During it streams
  are still accepted (a WebTransport session's streams arrive after `GOAWAY`); it ends early once
  the request streams open at `GOAWAY` are done. The session handler's token is cancelled at
  its end, and the handler may still write its close with `CancellationToken.None` before it
  returns; `CONNECTION_CLOSE` waits for every handler.
- **Only a datagram that can still find its session is held**: one for a stream not yet
  accepted, or accepted and not yet decided. `Http3ExtendedConnect.Accept` and `Decide` track that
  (the accept loop, the request stream server once a head is an ordinary request, and its end);
  one for a decided stream - a request, a refused or ended session - is dropped at once, so a
  client still sending on an ended session cannot fill the 64 when there is no head timeout.
  The head timeout is checked lazily, on the next datagram or session opening; a datagram is
  never delivered after it.
- **The datagram reader no longer uses the connection's end as its token**, only shutdown and
  failure: when the connection ends it now always ends on the completed channel (null) rather
  than racing `Ended`'s cancellation, which had left `DeliverDatagramsAsync`'s end uncovered.

A `code-reviewer` pass found four defects, all fixed with a test each:
a shutdown write on a CONNECT stream that had already ended threw past `RunAsync` and faulted the
connection (now logged, `ServeAsync_ShutdownWriteOnAnEndedStream_IsLoggedAndTheDrainGoesOn`); the
`GOAWAY` stream ID and the list of streams the drain waits for were read apart from the accept
loop's update (now one step under the gate, the stream served only after); the 64 could fill for
good with no head timeout (above, `ServeAsync_DatagramForAnEndedSession_IsDroppedAtOnce`,
`ServeAsync_HeldDatagramForAStreamThatIsAnOrdinaryRequest_IsDroppedOnceItsHeadIsRead`); and the
shutdown writes were not bounded by the grace
(`ServeAsync_ShutdownWriteThatOutlastsTheGrace_IsCutOffAndGoAwayAndTheCloseFollow`). Its minor
point - rejecting ordinary requests above the `GOAWAY` ID with `H3_REQUEST_REJECTED` during the
grace - is left as is: a client that sends one after `GOAWAY` is served, which RFC 9114 section 5.2
allows, and WebTransport streams above it must be served anyway.

Coverage (three suites merged by `Measure-CodeQuality.ps1`: `Surl.Http3.UnitTests` 96,
`Surl.Protocol.Http.UnitTests` 542, `Surl.Extended.Protocol.WebTransport.UnitTests` 203, all green):
`Surl.Http3.UnitLibrary` 100% line, 100% branch. Its two complexity rows,
`Http3RequestStreamServer.HandAsync` and `Http3RequestHeadMapping.MapHead` at 14, are Cobertura's
count of the compiled method; neither gained a branch here, and CA1502, the build gate, passes.

## Log

- 2026-11-05: Created.
- 2026-11-05: Backlog -> Doing.
- 2026-11-06: Doing -> Done. Surl.Http3 extended CONNECT handlers read :scheme, close the connection with their own code, receive datagrams held before their CONNECT, and drain before GOAWAY with ShutdownGrace
