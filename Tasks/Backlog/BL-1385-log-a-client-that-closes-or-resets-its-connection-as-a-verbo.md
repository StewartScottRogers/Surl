---
id: BL-1385
title: Log a client that closes or resets its connection as a verbose note, not a protocol server failure
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Foundation/Surl.Core.UnitLibrary, Foundation/Surl.Core.UnitTests]
requirement: none
created: 2026-11-18
completed:
---
# BL-1385 — Log a client that closes or resets its connection as a verbose note, not a protocol server failure

## Goal

When a client resets or aborts its connection while a protocol server reads or writes - a port check or health
check that connects and closes without sending anything - the serving engine notes it as a plain client close,
written only from `-v` up, instead of `Exchange N ended because the protocol server threw IOException: ...` at
the default level.

## Context

- Finding 7 of the Surly cookbook bug report of 2026-11-18: `surl --allow-anonymous smtp://127.0.0.1:2525/ imap://127.0.0.1:1143/`,
  then a TCP connect and close with no bytes, logs `Exchange N ended because the protocol server threw IOException:
  ... aborted by the software in your host machine` (Windows' WSAECONNABORTED). Every readiness and load-balancer
  check will log it.
- Where: `ServingEngine.DescribeHowTheExchangeEnded` (`Foundation/Surl.Core.UnitLibrary/ServingEngine.cs:473-483`)
  treats any exception other than cancellation and `TlsHandshakeException` as a server failure: the note above,
  and `failed` true, which makes the caller abort the connection (`ServingEngine.cs:742-745`). That note's prefix
  is one of the few the default `Info` level writes (`Foundation/Surl.Output.UnitLibrary/LevelledExchangeLogFactory.cs:85-92`,
  `WrittenNotePrefixes`), so a port check is noisy by default.
- What a client close looks like: the socket stream from `Surl.Networking` throws `IOException` whose
  `InnerException` is a `SocketException` with `SocketErrorCode` `ConnectionReset`, `ConnectionAborted` or
  `Shutdown` (a broken pipe on Linux and macOS); confirm against `Foundation/Surl.Networking.UnitLibrary`'s
  connection and pin the same three codes `AcceptFailureClassifier.cs:14-22` already treats as a peer going away.
  Recognise only that shape, so an `IOException` a server throws for its own reasons is still a failure.
- The new note must not start with any prefix in `WrittenNotePrefixes`, so `LevelledExchangeLogFactory` writes it
  only at `Verbose` and `Trace`, with no change there. Suggested text:
  `Exchange <id> ended: the client closed the connection (<SocketError>).`
- Stand-in: no store or fault change. The `exchange_ended` event on the stream is unchanged; only the log note and
  the abort change.

## Acceptance criteria

- [ ] A test in `Foundation/Surl.Core.UnitTests` (beside `ServingEngineTests.Limits.cs`'s
      `ServeAsync_ServerThatThrowsSomethingElseAfterTheIdleTimeout_HasItsConnectionAborted`) shows a server that
      throws `IOException` wrapping `SocketException(SocketError.ConnectionAborted)` gets the client-close note,
      no "protocol server threw" note, and no `Abort()` on its connection.
- [ ] Data rows cover `ConnectionReset` and `Shutdown` the same way.
- [ ] A test shows a bare `IOException` with no `SocketException` inside, and one wrapping another `SocketError`
      (e.g. `AccessDenied`), still gets the "protocol server threw IOException" note and an aborted connection.
- [ ] No fast test needs `TestCategory=Integration`, and the new tests pass on Windows, Linux and macOS (no
      platform error text asserted).
- [ ] `dotnet build Foundation/Surl.Core.UnitLibrary -warnaserror` is clean, the fast tests are green, and
      `Surl.Core.UnitLibrary` stays at 100% line and branch coverage with no method over complexity 10.

## Notes

- `Conformance/Surl.Conformance.UnitTests/CurlPortRunSetup.cs:104` filters the old IOException line out of compared
  notes; leave it (it still matches a real server failure). If it becomes dead, file a follow-up rather than
  widening this task.
- The document stage updates `Foundation/Surl.Core.UnitLibrary/CLAUDE.md` if it describes exchange endings, and any
  ADR-0033 or ADR-0007 section 8 table of notes.

## Log

- 2026-11-18: Created.
