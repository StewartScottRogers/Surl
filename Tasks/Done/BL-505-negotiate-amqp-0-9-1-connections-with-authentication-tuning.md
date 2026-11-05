---
id: BL-505
title: Negotiate AMQP 0-9-1 connections with authentication, tuning and heartbeats
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-504]
touches: [Protocols/Messaging/Surl.Extended.Protocol.Amqp0.UnitLibrary, Protocols/Messaging/Surl.Extended.Protocol.Amqp0.UnitTests, Documentation/Conformance/Extended/Amqp0.md]
requirement: none
created: 2026-11-04
completed: 2026-11-05
---
# BL-505 — Negotiate AMQP 0-9-1 connections with authentication, tuning and heartbeats

## Goal

The AMQP 0-9-1 broker runs the connection class: `start`/`start-ok` with the accounts check, `tune`/`tune-ok`,
`open`/`open-ok` for its virtual hosts, and `close`/`close-ok` both ways. It sends heartbeats and closes a silent
peer.

## Context

- Decision: BL-503's ADR, decisions 2, 3, 4 and 7.
- AMQP 0-9-1, section 1.4.2 (the connection class methods), section 4.2.7 (heartbeats: a peer silent for two
  intervals is dead), and the XML's reply codes (`403` ACCESS_REFUSED, `530` NOT_ALLOWED for an unknown virtual
  host, `501`, `503` COMMAND_INVALID, `504` CHANNEL_ERROR, `505` UNEXPECTED_FRAME). A failed login ends as the
  ADR decides: `connection.close` with `403`, or closing the socket. Cite the specification text the ADR relies on.
- Logins go through the `Surl.Protocol.Abstractions` contracts (`PLAIN` response `\0user\0pass`; the `AMQPLAIN`
  table with `LOGIN` and `PASSWORD`).
- Time from `ExchangeContext.TimeProvider`.

## Acceptance criteria

- [x] Named tests show (all in `Amqp0BrokerTests`):
      - a full connection opened with `PLAIN`, and with `AMQPLAIN`
        (`ServeAsync_PlainLogin_OpensTheConnectionAndAnswersCloseWithCloseOk`,
        `ServeAsync_AmqplainLogin_GoesToThePolicyAsPlainAndOpens`);
      - a bad password closed as the ADR says (`ServeAsync_WrongPassword_IsClosedWith403NamingStartOk`);
      - an unknown virtual host gets `530` (`ServeAsync_UnknownVirtualHost_IsClosedWith530NamingOpen`);
      - `tune-ok` above the server's maximums refused (`ServeAsync_TuneOkAboveTheMaximums_ClosesWithNoFurtherBytes`);
      - heartbeats sent at the negotiated interval (`ServeAsync_HeartbeatNegotiated_IsSentAfterTheIntervalWithNothingWritten`);
      - a peer silent for two intervals closed (`ServeAsync_PeerSilentForTwoIntervals_IsClosedWithoutConnectionClose`);
      - a method on an unopened channel gets `504` (`ServeAsync_MethodOnUnopenedChannel_IsClosedWith504`);
      - `close` from the client answered with `close-ok` (`ServeAsync_PlainLogin_OpensTheConnectionAndAnswersCloseWithCloseOk`).
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage (`Measure-CodeQuality.ps1 -Library Surl.Extended.Protocol.Amqp0.UnitLibrary`: 100 / 100, 0 failing
      members, worst CRAP 10).

## Notes

- Built: `Amqp0Broker` (`IConnectionProtocolServer`, schemes `amqp`/`amqps`), the internal
  `Amqp0BrokerConnection` (login, heartbeats, open-connection partials), `Amqp0BrokerOptions`, and the
  connection-method codecs `Amqp0ConnectionStart`, `...StartOk`, `...Tuning`, `...Open`, `...Close` with
  `Amqp0ConnectionMethodId`. Everything follows ADR-0112 decisions 2, 3, 4 and 7; no new decision was needed.
- Defaults taken, each inside what the ADR leaves open:
  - The idle timeout (for the offered heartbeat) and whether `ANONYMOUS` is offered come in as
    `Amqp0BrokerOptions` (default 120 s, off), since `ExchangeContext` carries neither; BL-507 sets them from
    `--idle-timeout` and `--allow-anonymous`.
  - An empty `start-ok` response goes to the policy as no initial response, so challenge-first mechanisms
    (CRAM-MD5) start with a challenge instead of being refused.
  - The `authzid` check of `PLAIN` is left to the shared policy (`Surl.Authentication`'s `PlainSaslExchange`
    already enforces it); a malformed `AMQPLAIN` table, or a NUL inside `LOGIN`/`PASSWORD`, is refused as 403
    without asking the policy.
  - Heartbeats start after `tune-ok`; frames the client sends during the negotiation may be heartbeats and
    are skipped. Silence is measured per frame read (a read that takes `2h` ends the connection).
  - After `open-ok`, with no channel open yet, any frame on a channel other than 0 is 504; a content frame on
    channel 0 is 505; any method on channel 0 other than `close`/`close-ok` is 503; an unasked `close-ok`
    ends the connection. BL-506 replaces the 504 path with channels.
  - The client's `connection.close` is answered `close-ok` whatever its arguments say.
- Touches widened: `Documentation/Conformance/Extended/Amqp0.md`, to replace this task's ID with the named tests
  in the requirement matrix as ADR-0112's Consequences ask. No task in `Doing` names it.
- Follow-up filed: BL-743, `connection.close` 320 `CONNECTION_FORCED` on the idle timeout, `--max-time` and
  shutdown (rows `Amqp0:Surl-Idle-1`, `Amqp0:Surl-Limits-2`): `ExchangeContext` does not say which limit fired,
  so ADR-0112's fixed text needs a decision first. The 540 half of `Amqp0:Surl-Exception-3` and the channel half
  of `Amqp0:Connection-close-1` sit with BL-506, which serves channels.
- A coverage scratch directory `Z:\amqp0cov-tmp-lane5` (outside the repository) was left behind: the harness
  refused to delete it.

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. The AMQP 0-9-1 broker negotiates connections: PLAIN/AMQPLAIN/SASL logins, tune limits, virtual host /, heartbeats and close both ways
