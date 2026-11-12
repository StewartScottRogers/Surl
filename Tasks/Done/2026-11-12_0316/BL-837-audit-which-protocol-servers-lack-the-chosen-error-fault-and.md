---
id: BL-837
title: Audit which protocol servers lack the chosen-error fault and file one task per family
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-980, BL-981, BL-982, BL-983, BL-1015, BL-835, BL-984, BL-985, BL-986, BL-793, BL-794, BL-795, BL-796, BL-797, BL-798]
touches: [Tasks/Backlog]
requirement: FR-062
created: 2026-11-05
completed: 2026-11-12
---
# BL-837 — Audit which protocol servers lack the chosen-error fault and file one task per family

## Goal

Every protocol server composed by `Surl.Composition` either answers ADR-0124's `error` fault or has a task on the
board that makes it, so no server built after ADR-0124 is left without it.

## Context

- Decision: [ADR-0124](../../Documentation/Planning/Decisions/ADR-0124-surl-is-a-stand-in-for-tests.md) decision 9:
  servers filed before the ADR and not built when it was accepted (STOMP, XMPP, gRPC, OCI, WebTransport,
  Socket.IO, SOCKS and any other) are caught here.
- Method: list the schemes `ComposeProtocolServers` and `ExtendedProtocolServers` register; list the schemes the
  `--fault` reply check in `Surl.Cli` accepts; for each scheme in the first and not the second, file one
  `feature` task per family (as BL-833 to BL-836 are written), depending on BL-823 and BL-785, adding the family's
  row to ADR-0124's reply table in the same task.
- This task changes no code; its record is the filed tasks and the Notes below.

## Acceptance criteria

- [x] Notes list every registered scheme with either its wiring task (Done) or the task filed for it.
- [x] Every filed task has exact `touches`, `depends-on` BL-823 and BL-785, and checkable acceptance criteria.

## Notes

Audit run 2026-11-12 (lane 1). Compared the schemes `SchemeDefaultPorts` and `ExtendedSchemeDefaultPorts` list
against the keys of `FaultReplyShapes` (`Surl.Cli`), and the registrations in `ProtocolServerRegistrations` and
`ExtendedProtocolServers`.

Has a reply shape (wired by a Done task):
- http, https, rtsp: BL-793. ftp, ftps, tftp: BL-794. scp, sftp, smb, smbs: BL-795. smtp, smtps, imap, imaps, pop3,
  pop3s: BL-796. mqtt, mqtts, ldap, ldaps: BL-797. dict, gopher, gophers, telnet: BL-798. ws, wss: BL-903.
- coap, coaps, mcp, mcps, signalr, signalrs: BL-835 (ADR-0144). s3, az, abfs, abfss, gs: BL-981 and BL-983.
- dns, redis, rediss: BL-986. amqp, amqps, amqp1, amqp1s, kafka, kafkas, mqtt5, mqtt5s, mqtt5+ws, nats, natss:
  BL-1012, BL-1013, BL-1015.

Registered with no reply shape, so a task is filed (each depends on BL-823 and BL-785):
- socks4, socks4a, socks5, socks5h: BL-1061.
- stomp, stomps: BL-1062.
- xmpp, xmpps: BL-1063.
- socketio, socketios: BL-1064.
- webtransport: BL-1065.

Not registered by `Surl.Composition` yet, so nothing to file here: grpc, grpcs (serve task BL-445) and oci
(BL-1028). Each of those tasks must add its reply shape when it is built; ADR-0124 decision 9 covers it, and the
next run of this audit catches it if it does not.

Decision: one task per family (the sizing rule, one protocol each), all Low priority like this audit. BL-1061 notes
that SOCKS is an original protocol, so its exit codes come from pinned upstream curl, not a specification alone.

## Log

- 2026-11-05: Created.
- 2026-11-05: Filed by BL-822 (ADR-0124 decision 9).
- 2026-11-06: depends-on BL-833 replaced by BL-980, BL-981, BL-982, BL-983 (BL-833 split for the 60-minute size limit; Stewart asked for a backlog audit 2026-11-06).
- 2026-11-06: depends-on BL-836 replaced by BL-984, BL-985, BL-986 (BL-836 split for the 60-minute size limit; Stewart asked for a backlog audit 2026-11-06).
- 2026-11-06: depends-on BL-834 replaced by BL-1015 (BL-834 split into BL-1011 to BL-1015 after timing out; BL-1015 depends on the five broker tasks; Stewart asked for the split 2026-11-06).
- 2026-11-12: Backlog -> Doing.
- 2026-11-12: Doing -> Done. Audit done: all registered schemes traced; BL-1061 to BL-1065 filed for SOCKS, STOMP, XMPP, Socket.IO and WebTransport
