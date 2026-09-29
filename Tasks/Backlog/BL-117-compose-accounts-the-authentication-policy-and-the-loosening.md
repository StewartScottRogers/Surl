---
id: BL-117
title: Compose accounts, the authentication policy and the loosening-option warnings in surl
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-107, BL-108, BL-110, BL-111, BL-113, BL-114, BL-115]
touches: [Surl.Console, Surl.Console.UnitTests]
requirement: FR-014
created: 2026-09-29
completed:
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

- [ ] `CommandLineRunnerTests` prove, each by name: a `--user-file` that is missing,
      unreadable or malformed ends surl before any listener binds with ADR-0032's exit code
      and exact `surl: ` text (naming the line for a malformed file); a good file and
      `--user` accounts both reach the composed policy.
- [ ] Tests prove that with no accounts an HTTP request carrying `Authorization: Basic ...`
      and an MQTT CONNECT with credentials get ADR-0032's refusal through the composed
      servers, and with an account they are accepted (over a `TlsSession` for Basic, or with
      `--allow-plaintext-auth`).
- [ ] Tests prove each of `--allow-anonymous`, `--allow-plaintext-auth` and `--auth <methods>`
      writes its exact ADR-0032 warning line on start, at ADR-0033's level, and that `-s`
      hides them only if ADR-0033 says so; with none of them no warning is written.
- [ ] `dotnet build Surl.Console -warnaserror` is clean; the fast tests pass; `Surl.Console`
      keeps 100% line and branch coverage; the real-file read is the only path allowed to be
      `[TestCategory("Integration")]`.

## Notes

## Log

- 2026-09-29: Created.
