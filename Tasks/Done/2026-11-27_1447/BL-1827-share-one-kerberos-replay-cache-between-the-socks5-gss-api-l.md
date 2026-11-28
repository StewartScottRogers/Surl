---
id: BL-1827
title: Share one Kerberos replay cache between the SOCKS5 GSS-API login and the other Kerberos logins
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests, Documentation/Planning/Decisions/ADR-0057-surls-kerberos-keytab-and-ap-req-check-for-negotiate-and-sasl-gssapi.md, Documentation/Planning/Decisions/README.md, Application/Surl.Console/CLAUDE.md]
requirement: none
created: 2026-11-26
completed: 2026-11-27
---
# BL-1827 — Share one Kerberos replay cache between the SOCKS5 GSS-API login and the other Kerberos logins

## Goal

Each started instance has one Kerberos replay cache, used by every listener that checks an AP-REQ.

## Context

- Found while working BL-1817. ADR-0057 decision 7 says there is one replay cache, shared by every listener, and the
  XML comment on `KeytabComposition.Read` says "one replay cache for the process". `Read` creates a new
  `KerberosReplayCache` on each call, and it is called twice per start: `AuthenticationComposition.Compose` and
  `ComposeSocksGssApiLogin` (`SurlServerComposition.Start.cs`). So SOCKS5 has its own cache.
- The cache belongs to the instance, never to a static field (root `CLAUDE.md`, stand-in rules: many instances side
  by side), so "process" in ADR-0057 decision 7 and in the XML comment is also to be said as the code then does it:
  add an ADR-0057 amendment and mention it in `Documentation/Planning/Decisions/README.md`.
- Reading the keytab once and passing one acceptor to both users also removes the second file read and the case in
  `ComposeSocksGssApiLogin`'s comment where that read fails.

## Acceptance criteria

- [x] A named test in `Surl.Composition.UnitTests` shows a start with `--keytab` reads the keytab file once.
- [x] A named test shows the SOCKS5 GSS-API login and the authentication policy use the same acceptor, or the same replay cache.
- [x] A named test shows two started instances do not share a replay cache.
- [x] ADR-0057 decision 7 (by amendment) and the `KeytabComposition.Read` XML comment say the cache is one per started instance.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and `Surl.Composition.UnitLibrary` keeps 100% line and branch coverage.

## Notes

- Plan: `ComposeSocksGssApiLogin` takes the acceptor already on `authentication.Policy.Settings.KerberosAcceptor`
  instead of reading `--keytab` again. No public signature changes, no byte on the wire changes. The pipeline's
  agent stages were not delegated: the change is a few lines in one library.
- Added `Application/Surl.Console/CLAUDE.md` to `touches`: it said "one `KerberosReplayCache` per process". No task
  in Doing names `Surl.Console` (BL-1914 touches only `Surl.Cli` and its tests).
- Decision recorded as ADR-0057 amendment 5 (Decided by Claude under Stewart's delegation).
- The two-instances test reads the acceptor's private replay-cache field by reflection: `KerberosAcceptor` shows no
  cache, and a behavioural test would need `Surl.Kerberos` internals, outside this task's `touches`.

## Log

- 2026-11-26: Created.
- 2026-11-27: Backlog -> Doing.
- 2026-11-27: Doing -> Done. Each started instance reads --keytab once and has one Kerberos replay cache shared by the authentication policy and the SOCKS5 GSS-API login
