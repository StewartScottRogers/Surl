---
id: BL-1569
title: Give every delay-hold unit test a scripted client that stays open, so no test's hold ends early because its client looks gone
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [Protocols/Messaging/Surl.Extended.Protocol.Amqp1.UnitTests]
requirement: none
created: 2026-11-21
completed: 2026-11-21
---
# BL-1569 — Give every delay-hold unit test a scripted client that stays open, so no test's hold ends early because its client looks gone

## Goal

No delay-fault unit test in the solution can fail because the hold ended early when its scripted client ran out of
input. The AMQP 1.0 flake is fixed here, and every other affected test project gets its own follow-up task.

## Context

- Third flake of one kind in a day:
  - BL-1556: DNS over TCP, every platform
  - BL-1566: SFTP, Ubuntu
  - Now `Surl.Extended.Protocol.Amqp1.UnitTests`:
    `ServeAsync_ADelayRuleForAPerformative_HoldsItForTheDelayThenHandlesItAsUsual("transfer")` failed on
    macos-latest in PR #131's CI (run 44965454643; rerun started).
- The cause each time: since BL-1529 (`efd200fea`), a delay hold ends when `IConnection.WatchForPeerCloseAsync`
  reports the client gone (ADR-0141 amendment 2). A scripted test client that has no more input looks gone, so
  whether the hold is still on when the test checks it depends on timing. The shared `InMemoryConnection` half-closes
  by default (`InMemoryConnection.cs:198-203`, BL-1556). Protocol test projects that script their own channel may do
  the same.
- Test projects with delay-rule tests (`DelayRule` or `UnderADelay`, from a search of `*Tests.cs`): Abstractions; Redis,
  AzureStorage, GoogleCloudStorage, OciRegistry, S3, Ftp, Smb, Ssh (done in BL-1566), Tftp; Coap, Grpc, McpServer,
  SignalR, SocketIo, WebTransport, HttpMessage, Rtsp, Ws; DnsQuery (done in BL-1556), Dict, Gopher; Imap, Pop3, Smtp;
  Kafka, Nats, Stomp, Xmpp, Ldap; Amqp1, and possibly more. That's too many for one task.

## Acceptance criteria

- [x] The AMQP 1.0 delay tests use a client that stays open through the hold; the changed tests pass 20 runs in a row
      locally (`--filter "FullyQualifiedName~DelayRule"` in a loop).
- [x] The Log records the decision: change the shared test doubles' default (e.g. `InMemoryConnection` stays open
      unless a test asks for a half-close), or fix each project. Prefer the shared default if one double serves most
      projects; note which projects script their own channel.
- [x] task-planner files follow-up tasks (at most two projects each) for every other project above whose delay tests
      can see an ended input during a hold, or one task for the shared double if that covers them; each is listed in
      the Log. Projects already fixed (DnsQuery, Ssh) are skipped.
- [x] `dotnet build` clean and the full fast suite green.

## Notes

- Behaviour stays as ADR-0141 amendment 2 decided; only test clients change.

## Log

- 2026-11-21: Created.
- 2026-11-21: Backlog -> Doing.
- 2026-11-21: Decision (Claude, under Stewart's delegation): the shared `InMemoryConnection` default (half-close) stays, because many tests across the solution rely on reads returning 0 at the end of a script; each affected project passes `peerHalfClosesWhenExhausted: false` in its delay tests instead. Amqp1 uses the shared double. Fixed here: the AMQP 1.0 delay test now keeps its client open, waits for the stored delivery, then cancels the exchange and awaits `serving`; 20 of 20 runs passed with `--filter "FullyQualifiedName~DelayRule"`.
- 2026-11-21: Follow-ups filed by task-planner: BL-1570 (Abstractions, Redis), BL-1571 (AzureStorage, GoogleCloudStorage), BL-1572 (OciRegistry, S3), BL-1573 (Ftp, Smb), BL-1574 (Tftp, HttpMessage), BL-1575 (Coap, Grpc), BL-1576 (McpServer, SignalR), BL-1577 (SocketIo, WebTransport), BL-1578 (Rtsp, Ws), BL-1579 (Dict, Gopher), BL-1580 (Imap, Pop3), BL-1581 (Smtp, Ldap), BL-1582 (Nats, Stomp), BL-1583 (Xmpp). Kafka skipped (its delay test asserts the rule is not applied). DnsQuery and Ssh were already fixed.
- 2026-11-21: Doing -> Done. AMQP 1.0 delay-hold tests keep their client open; 20/20 runs; 14 follow-up tasks BL-1570..1583 filed for other projects
