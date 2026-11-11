---
id: BL-985
title: Answer a chosen protocol error by fault in the extended Redis server
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-823]
touches: [Protocols/DataStore/Surl.Extended.Protocol.Redis.UnitLibrary, Protocols/DataStore/Surl.Extended.Protocol.Redis.UnitTests]
requirement: FR-062
created: 2026-11-06
completed: 2026-11-11
---
# BL-985 — Answer a chosen protocol error by fault in the extended Redis server

## Goal

An `error` fault rule naming `redis` or `rediss` (or a listener of one) makes the Redis server answer the matching
command with the chosen text as a RESP error `-<text>`.

## Context

- Second of the three tasks BL-836 was split into (see BL-984).
- Decision: ADR-0124 decisions 4 and 9 and its extended reply table: Redis text sent as a RESP error `-<text>`;
  `command` is the command name and `path` the key.
- Unit tests replay bytes written from the RESP specification (ADR-0083). Consult `ExchangeContext.Faults` per
  command; publish `FaultAppliedEvent` (BL-823's contracts and doubles).
- Stand-in: this is the fault-injection point for the Redis server; the event is how a test observes it.

## Acceptance criteria

- [x] Named tests: a command matching an `error` rule gets exactly the chosen RESP error, a non-matching one is
      answered, and one `FaultAppliedEvent` is published.
- [x] With no rule, every existing test of the library passes unchanged.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

- Delivered: `RedisCommandFaults` (internal) and `RedisCommandFaultOutcome`, called from
  `RedisServerConnection.ReplyToAsync`. Tests: `RedisCommandFaultsTests` (12), e.g.
  `ServeAsync_GetUnderAnErrorRule_GetsExactlyTheChosenRespErrorAndPublishesOneEvent` (`-BUSY fault injected\r\n`),
  `ServeAsync_CommandTheRuleDoesNotName_IsAnsweredAndPublishesNothing`. Expected bytes written from the RESP page
  ("Simple errors"). Library coverage 100% line, 100% branch; Redis tests 389 passed, every existing one unchanged.
- Choices (ADR-0124 fixes reply, command and path in outline; recorded here as defaults):
  - Asked only once a command would run: unknown command, wrong argument count, `-NOAUTH` and the subscribed-context
    refusal keep their own errors, so a fault never masks a refusal or answers before a login.
  - `command` is Redis's own full command name upper-cased (`GET`, `CLIENT|SETNAME`, the `COMMAND INFO`/ACL
    notation); the plan matches it case-insensitively.
  - `path` is the first key (argument 1, Latin-1 as the keyspace names keys); PUBLISH/SUBSCRIBE/PSUBSCRIBE name their
    first channel, as MQTT names its topic. Connection commands (`HELLO`, `AUTH`, `SELECT`, `PING`, `ECHO`, `QUIT`,
    `RESET`, `CLIENT …`) and whole-database ones (`KEYS`, `SCAN`, `DBSIZE`, `FLUSHDB`, `FLUSHALL`) name none.
  - `account` is the account logged in as, absent before a login.
  - The reply is sent verbatim after `-`: Redis convention is an error-code word first (`BUSY …`, `ERR …`), but the
    RESP page only forbids CR and LF. An empty reply or one with CR or LF is noted as not applied and the command
    runs (BL-986 checks the shape in `Surl.Cli`).
  - Delay and drop rules at a command are applied through the shared `CommandStageFaults` (one call, a TCP
    connection can be aborted), rather than noted as not applied as UDP DNS does.
- Also covered the three untested exception kinds of `RedisReplyWriter.DeliverAsync`'s filter (aborted, disposed,
  cancelled) in `RedisReplyWriterTests`, to keep branch coverage at 100% (it measured 99.8% before).
- Environment: the pinned SDK 10.0.401 is no longer installed on this machine (only 10.0.100-rc.1 and 11.0.100);
  build and tests ran with SDK 11.0.100 by invoking `dotnet` from outside the repository (`$env:TEMP`), targeting
  net10.0 on runtime 10.0.15. `dotnet format` could not run (its MSBuild host honours global.json).

## Log

- 2026-11-06: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Done. An error fault rule naming redis or rediss answers the matching command with -<reply> and publishes one FaultAppliedEvent; delay and drop apply at commands too
