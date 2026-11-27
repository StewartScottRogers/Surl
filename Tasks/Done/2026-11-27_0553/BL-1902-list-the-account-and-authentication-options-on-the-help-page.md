---
id: BL-1902
title: List the account and authentication options on the help pages of every server that reads them
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests]
requirement: none
created: 2026-11-27
completed: 2026-11-27
---
# BL-1902 — List the account and authentication options on the help pages of every server that reads them

## Goal

`surl --help <topic>` and `surl --aihelp <topic>` list each account and authentication option on every server topic whose server reads it.

## Context

- Found by BL-1891, by reading `Application/Surl.Cli.UnitLibrary/CommandLineOptions.cs`.
- The categories of `--user`, `--user-file`, `--allow-anonymous`, `--allow-plaintext-auth` and `--auth` name neither `kafka` nor `xmpp`, though both servers log in with SASL under those options (their `AiHelpProse` paragraphs say so). `--allow-plaintext-auth` does not name `amqp`, though that paragraph names it too.
- Check each server in `Surl.Composition`'s `ProtocolServerRegistrations.Original` and `ExtendedProtocolServers.Registrations` for the options it reads, `mqtt5`, `redis` and the other extended ones included.

## Acceptance criteria

- [x] `surl --help kafka` and `surl --help xmpp` list `--user`, `--user-file`, `--allow-anonymous`, `--allow-plaintext-auth` and `--auth`.
- [x] `surl --help amqp` lists `--allow-plaintext-auth`.
- [x] Every other server topic lists each of those five options its server reads, as the code shows, and none it does not.
- [x] A named test in `Surl.Cli.UnitTests` pins the lists.
- [x] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

Options each server reads, from the code (`Surl.Authentication`'s `AuthenticationPolicy` and
`HttpAuthenticationSession`, and each server's login code). U = `--user` and `--user-file`, A =
`--allow-anonymous`, P = `--allow-plaintext-auth`, M = `--auth`.

| Topics | Lists |
| --- | --- |
| amqp, amqp1, grpc, gs, http, imap, kafka, ldap, mcp, mqtt5, pop3, rtsp, signalr, smtp, websocket, xmpp | U A P M |
| azure-storage, ftp, mqtt, nats, oci, redis, socketio, stomp | U A P |
| dns, smb, webtransport | U A M |
| coap, s3, ssh | U A |
| proxy, socks | A P |
| dict, gopher, telnet, tftp | none |

Decisions, and why:

- `proxy` counts as a server topic (it has no scheme, but a proxy listener reads A and P through
  `ComposeProxyPolicy`). Its accounts are `-U`'s and its methods `--proxy-auth`'s, so it lists no U or M.
- `socks` lists P because an RFC 1929 password in clear is refused without it, and A for the same login.
- `gs` lists M: a bearer token is judged as an ordinary HTTP method, so `--auth` without `bearer` refuses it.
  `azure-storage` and `s3` do not: their verifiers ignore `--auth`.
- `dns` (DNS over HTTPS) and `webtransport` list no P: their logins are always over TLS.
- `Surl.Cli.UnitTests`: `HelpTextTests.Answer_EveryServerTopic_ListsExactlyTheAccountAndAuthenticationOptionsItsServerReads`
  pins the table for `--help` and `--aihelp`; `Answer_EveryServerTopic_HasItsAccountAndAuthenticationOptionsPinned`
  fails when a new server topic has no row.
- Verified: `dotnet build -warnaserror` 0 warnings, `dotnet format --verify-no-changes` clean, fast tests green
  (`Surl.Cli.UnitTests` 2004 passed). No integration tests run: only help categories changed.
- Filed: BL-1915 (proxy account options on the SOCKS page), BL-1914 (explanations name only some servers).

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
- 2026-11-27: Doing -> Done. --help and --aihelp list the account and authentication options on every server topic that reads them
