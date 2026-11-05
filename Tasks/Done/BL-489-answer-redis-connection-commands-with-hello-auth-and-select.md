---
id: BL-489
title: Answer Redis connection commands with HELLO, AUTH and SELECT
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-488]
touches: [Protocols/DataStore/Surl.Extended.Protocol.Redis.UnitLibrary, Protocols/DataStore/Surl.Extended.Protocol.Redis.UnitTests, Documentation/Conformance/Extended/Redis.md]
requirement: none
created: 2026-11-04
completed: 2026-11-05
---
# BL-489 — Answer Redis connection commands with HELLO, AUTH and SELECT

## Goal

The Redis server answers `HELLO` (switching between RESP2 and RESP3, with its `AUTH` and `SETNAME` arguments),
`AUTH` (one- and two-argument forms, checked against the accounts), `SELECT`, `PING`, `ECHO`, `QUIT`, `RESET` and
`CLIENT`. Before login it answers every other command with `-NOAUTH`, and it answers an unknown command with its
error.

## Context

- Decision: BL-487's ADR, decisions 1, 2, 4 and 5.
- Command pages: `HELLO` (the map reply's fields `server`, `version`, `proto`, `id`, `mode`, `role`, `modules`;
  `-NOPROTO` for an unsupported version), `AUTH` (`-WRONGPASS invalid username-password pair or user is disabled.`),
  `SELECT` (`-ERR DB index is out of range`), `PING` and `ECHO`, `QUIT`, `RESET`.
- Logins go through the `Surl.Protocol.Abstractions` contracts (`IAuthenticationPolicy` or the policy the ADR
  names).
- BL-488 built `RespValue` and `RedisCommandReader` but no server class; this task creates the
  `IConnectionProtocolServer` and so also owns, from ADR-0106 decision 6 and `Redis.md`: writing a reader
  `ProtocolError`'s reply then closing, closing with no bytes on `CommandTooLarge`, `--head-timeout`
  (Redis:Surl-Limits-7), the `-ERR max number of clients reached` refusal writer (Redis:Surl-Limits-8), and the
  unauthenticated limits (Redis:Surl-Limits-3), which the reader does not apply yet.

## Acceptance criteria

- [x] Named tests show:
      - a command before `AUTH` gets `-NOAUTH`;
      - `AUTH` with a good password gets `+OK`, and with a bad one `-WRONGPASS`;
      - `HELLO 3` answered with a RESP3 map, and later replies in RESP3;
      - `HELLO 4` gets `-NOPROTO`;
      - `HELLO 3 AUTH user pass` logs in;
      - `SELECT` of an out-of-range database refused;
      - `PING` and `PING msg` in both protocols;
      - `QUIT` answered `+OK` then the connection closed;
      - an unknown command gets the ADR's error text;
      - anonymous use allowed only under `--allow-anonymous`.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

- Delivered `RedisServer` (`IConnectionProtocolServer` + `IConnectionRefusalWriter`, schemes `redis`
  and `rediss`), `RedisServerConnection`, `RedisSession`, `RedisLogins`, `RedisCommandTable` with
  `RedisCommand`/`RedisCommandContainer`/`RedisArity`, `RedisConnectionCommands`, `RedisErrorReplies`
  and `RedisIntegers`. The reader gained `AppliesUnauthenticatedLimits` (Redis:Surl-Limits-3) and
  `WaitForCommandStartAsync`. 202 Redis tests, 100% line and branch coverage (measured with
  `Measure-CodeQuality.ps1 -SkipTestRun` over this project's Cobertura run).
- Choices within ADR-0106 (no new ADR needed; each follows its decisions or Redis 7.4's own order):
  - Whether anonymous use is allowed is asked of the policy once per connection with an anonymous
    `PasswordLogin` (user and password null), `AcceptedUnchecked` meaning `--allow-anonymous`, as
    `KafkaBroker` does: the policy is the only place that knows the option.
  - As Redis's `processCommand`, an unknown command, an unknown `CLIENT` subcommand and a wrong
    argument count are answered before the login check, so they come back as their own errors and
    not `-NOAUTH`. A subcommand's wrong count names it `client|<sub>`, as Redis does.
  - `HELLO` checks in Redis's order: version (`-NOPROTO`, also for a non-integer, as the ADR says),
    options (`-ERR syntax error` for an unknown or incomplete one, the ADR's text rather than
    Redis's "Syntax error in HELLO option"), the login, then `-NOAUTH`.
  - `CLIENT SETINFO` accepts `LIB-NAME` and `LIB-VER` (any case) and keeps nothing; any other
    attribute is `-ERR syntax error`. An empty name from `SETNAME` clears the name.
  - `SELECT` reads a 32-bit integer first, as Redis's `getIntFromObjectOrReply` does, so
    `4294967296` is "not an integer", and then the 0-15 check.
  - `--head-timeout`: the first command is timed from accept (the anonymous check included); a
    later one from its first byte, which `WaitForCommandStartAsync` waits for without a bound (the
    engine's idle timeout covers that wait). Blank inline lines skipped inside a timed read count
    against that command.
  - `QUIT` and a protocol error half-close with `CompleteWritesAsync` after the reply; a
    `CommandTooLarge` and a head timeout return with no bytes, and the exchange log notes why.
- Added `Documentation/Conformance/Extended/Redis.md` to `touches` (no task in Doing names it): the
  matrix's BL-489 rows now name their tests, as the matrix asks each task to do.
- Not changed: `Measure-CodeQuality.ps1` still flags BL-488's `RedisCommandReader.ReadBulkStringAsync`
  (18), `TryParseHeaderNumber` (16) and `RedisInlineCommandSplitter.ReadEscape` (20) for Cobertura
  complexity. The build's CA1502 gate passes; those methods were flagged before this task, and this
  task's own additions to the reader went into a separate helper.

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. The Redis server answers HELLO (RESP2/RESP3, AUTH, SETNAME), AUTH, SELECT, PING, ECHO, QUIT, RESET and CLIENT, with -NOAUTH before login, the unknown-command error, head timeout and the max-clients refusal
