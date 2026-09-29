---
id: BL-117
title: Compose accounts, the authentication policy and the loosening-option warnings in surl
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-107, BL-108, BL-110, BL-111, BL-113, BL-114, BL-115]
touches: [Surl.Console, Surl.Console.UnitTests, Surl.Conformance.UnitTests]
requirement: FR-014
created: 2026-09-29
completed: 2026-09-29
---
# BL-117 — Compose accounts, the authentication policy and the loosening-option warnings in surl

## Goal

`surl` builds the accounts from `--user` and the `--user-file` it reads, builds
`Surl.Authentication`'s policy from `--allow-anonymous`, `--allow-plaintext-auth` and
`--auth`, hands it to the HTTP (`http`, `https`) and MQTT (`mqtt`, `mqtts`) servers, and
writes ADR-0032's warning line for each loosening option given, on every start.

## Context

FR-014 and ADR-0032's rows; ADR-0032 (BL-100) decisions 1, 2 (file refusals: exit code and
text), 9 (warning lines); ADR-0033 (BL-101) decision 7 (how warnings are written). Built on:
BL-108 (options parsed), BL-110 (accounts, policy, `--user-file` parser), BL-111 and BL-113
(Basic, Bearer, Digest), BL-114 and BL-115 (servers take the contract), BL-107 (log levels).

- `Surl.Console/CommandLineRunner.cs`: `ComposeProtocolServers(contentStore,
  retainedMessages)` constructs `HttpProtocolServer`, `ImplicitTlsSchemeServer(httpServer,
  "https")` and `MqttProtocolServer`; `ServeAsync` checks the data directory, schemes and
  lock before `LoadServiceStateAndServeAsync`. Read `--user-file` before any listener binds,
  through a delegate the runner is given (as `canOpenDataDirectory` is), so tests need no
  disk; `Program.RunAsync` passes the real reader.
- `ComposeVersionText` also calls `ComposeProtocolServers`; give it a policy that needs no
  file (the one with no accounts).
- Tests: `Surl.Console.UnitTests/CommandLineRunnerTests.cs` with `FakeListenerFactory`;
  drive an HTTP exchange through the composed servers with `InMemoryConnection` where a
  test must prove the policy reached the server.

## Acceptance criteria

- [x] `CommandLineRunnerTests` prove, each by name: a `--user-file` that is missing,
      unreadable or malformed ends surl before any listener binds with ADR-0032's exit code
      and exact `surl: ` text (naming the line for a malformed file); a good file and
      `--user` accounts both reach the composed policy.
- [x] Tests prove that with no accounts an HTTP request carrying `Authorization: Basic ...`
      and an MQTT CONNECT with credentials get ADR-0032's refusal through the composed
      servers, and with an account they are accepted (over a `TlsSession` for Basic, or with
      `--allow-plaintext-auth`).
- [x] Tests prove each of `--allow-anonymous`, `--allow-plaintext-auth` and `--auth <methods>`
      writes its exact ADR-0032 warning line on start, at ADR-0033's level, and that `-s`
      hides them only if ADR-0033 says so; with none of them no warning is written.
- [x] `dotnet build Surl.Console -warnaserror` is clean; the fast tests pass; `Surl.Console`
      keeps 100% line and branch coverage; the real-file read is the only path allowed to be
      `[TestCategory("Integration")]`.

## Notes

- **Plan.** A new `Surl.Console/AuthenticationComposition` (beside `ServerTlsComposition`)
  refuses unimplemented `--auth` words, reads the `--user-file` through the runner's new
  `readUserFile` seam, parses it with `UserFileParser` after the `--user` accounts, and builds
  `AuthenticationPolicy` over Basic, Bearer and Digest. `CommandLineRunner` hands it to
  `HttpProtocolServer` (so `https` too) and `MqttProtocolServer`, and writes the loosening
  warnings to the log stream just before the `--self-signed` one. `--version` and the scheme
  check compose the servers with the no-accounts policy (`ComposeWithoutAccounts`).
- **Order of start-up refusals** (sensible default): data directory, listen URLs, then
  `--auth` words and the `--user-file`, then the lock. All come before any listener binds, as
  ADR-0032 requires; configuration faults come before the lock is taken so a bad file never
  leaves a `.surl` behind. Not an ADR: ADR-0032 already fixes the texts and "before any
  listener binds"; this is only the order among them.
- **`--auth` words not yet implemented** (`ntlm`, `negotiate`, `aws-sigv4`) get ADR-0032
  section 1's `surl: (2) --auth <word> is not available in this build`. The default set still
  holds `aws-sigv4`; the policy offers only the methods it is given, and AWS SigV4 is never
  offered in a challenge anyway, so the default needs no refusal.
- **`readUserFile` is optional** and defaults to `File.ReadAllBytes`, as `openLogFile`
  defaults to `LogFile.Open`, so `Program.RunAsync` passes nothing new. A missing file, a
  directory or an unreadable one (`IOException`, `UnauthorizedAccessException`) is 37.
- **Tests** are in `CommandLineRunnerAuthenticationTests` (the project names each runner
  concern's class `CommandLineRunner<Concern>Tests`, like `CommandLineRunnerLogTests`).
  `FakeConnection` now records what the server writes, hands out the request across small
  reads (MQTT reads the fixed header two bytes at a time) and completes a TLS handshake for
  an `https` listen URL. The two real-file tests are `[TestCategory("Integration")]`.
- **Touches widened** to `Surl.Conformance.UnitTests`: secure by default makes the MQTT server
  refuse curl's credential-less `CONNECT`, so the MQTT conformance tests now start surl with
  `--allow-anonymous` (the recordings are of an anonymous broker); `SurlOnLoopback.StartOverDirectoryAsync`
  takes options. No task in `Doing` names that project. All 109 conformance tests pass.
- **Not done here, filed as BL-131:** ADR-0032 section 6 says BL-117 removes the servers'
  one-argument constructors that default to `AnonymousAuthenticationPolicy`. They live in the
  HTTP and MQTT libraries, outside this task's `touches` and inside BL-125's (in `Doing`).
  `surl` no longer calls them.
- Quality: `Measure-CodeQuality.ps1 -Library Surl.Console` gives 100% line, 100% branch, 0
  failing members, worst CRAP 10 (`ServeAsync` was split into `ServeAsync` and
  `LockThenServeAsync` to keep complexity at 10 or less).

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. surl composes the --user and --user-file accounts and Surl.Authentication's policy into the HTTP and MQTT servers and warns for each loosening option
