---
id: BL-1061
title: Hand the SOCKS5 server a Kerberos keytab from the command line
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-765]
touches: [Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests, Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests]
requirement: none
created: 2026-11-12
completed:
---
# BL-1061 — Hand the SOCKS5 server a Kerberos keytab from the command line

## Goal

A `socks5://` or `socks5h://` listener started with the keytab option the HTTP server's Negotiate and the mail servers' GSSAPI already use builds a `SocksGssApiLogin` and passes it to `SocksProtocolServer`, so `--socks5-gssapi` completes against `surl`.

## Context

- BL-765 built the server half (`SocksGssApiLogin`, ADR-0121 amendment 2); `SocksProtocolServer` has a three-argument constructor taking it, and no composition code passes one yet.
- Reuse the existing keytab option and `KerberosAcceptor` composition; the service name defaults to `SocksGssApiLogin.DefaultService` (`rcmd`) and follows `--socks5-gssapi-service` semantics.
- Adding the wiring needs the `socks` `--aihelp` prose and example to say so (CLAUDE.md, `--aihelp` rule).

## Acceptance criteria

- [ ] With a keytab configured, `ProtocolServerRegistrations.Original` registers the SOCKS server with a `SocksGssApiLogin`; without one it registers as today.
- [ ] The `socks` `--aihelp` topic states that a keytab turns on GSS-API (method `01`), and its completeness tests pass.
- [ ] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

- Filed by BL-765.

## Log

- 2026-11-12: Created.
