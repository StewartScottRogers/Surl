---
id: BL-1173
title: Apply delay, drop and chosen-error fault rules in the HTTP and HTTPS proxy
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1153]
touches: [Protocols/Http/Surl.Protocol.HttpProxy.UnitLibrary, Protocols/Http/Surl.Protocol.HttpProxy.UnitTests, Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests, Documentation/Planning/Decisions/ADR-0124-surl-is-a-stand-in-for-tests.md]
requirement: FR-062
created: 2026-11-14
completed: 2026-11-15
---
# BL-1173 — Apply delay, drop and chosen-error fault rules in the HTTP and HTTPS proxy

## Goal

A `delay`, `drop` or `error` fault rule that BL-1153's ADR says reaches the proxy makes the HTTP or HTTPS proxy hold, cut off or answer a `CONNECT` or a forwarded request on purpose, publishing one `FaultAppliedEvent` per application.

## Context

- `Protocols/Http/Surl.Protocol.HttpProxy.UnitLibrary` never asks `ExchangeContext.Faults` today. The request paths are `HttpProxyExchange.cs`, `HttpProxyExchange.Connect.cs`, `HttpProxyExchange.Forwarding.cs`, `HttpProxyExchange.Ftp.cs`, `HttpProxyExchange.Http2.cs`; `HttpProxyStatus.cs` writes the proxy's own statuses.
- Use the shared helpers: `CommandStageFaults.FindAndApplyAsync` (`Surl.Protocol.Abstractions`, ADR-0141) for delay and drop, and `HttpCommandFaults.TryReadChosenStatus`/`PublishApplied` (`Protocols/Http/Surl.HttpMessage.UnitLibrary/HttpCommandFaults.cs`) for an HTTP status - with the `FaultPoint` BL-1153's ADR defines. If the ADR adds a reply shape, add it to `Application/Surl.Cli.UnitLibrary/FaultReplyShapes.cs` with its accept and refuse tests, and add the row to ADR-0124 decision 4's table.
- No byte the proxy sends without a rule may change: every existing proxy test and upstream test case (`HttpProxyUpstreamTestCaseServer`) must pass unchanged.

## Acceptance criteria

- [x] Named tests: a `CONNECT` matching an `error` rule gets exactly the chosen status line and no tunnel; a forwarded `GET` matching one gets the chosen status and the origin is never contacted; one `FaultAppliedEvent` each.
- [x] Named tests: `delay;stage=command` holds the request for `ms` on a fake `TimeProvider` (the idle clock paused, per ADR-0141) and then serves it; `drop;stage=command` aborts the connection with nothing sent.
- [x] Named tests: a rule BL-1153's ADR says misses the proxy (for example `scheme=http` without `listener`, if so decided) changes nothing.
- [x] With no rule, every existing `Surl.Protocol.HttpProxy.UnitTests` test passes unchanged.
- [x] `dotnet build Surl.slnx -warnaserror` is clean, the fast tests are green, and the touched libraries keep 100% line and branch coverage with no method over complexity 10.

## Notes

- ADR-0160 followed as written. The proxy builds its own `FaultPoint` (`ProxyCommandFault`) because `HttpCommandFaults` takes the scheme from the listen URL; scheme is `http-proxy` or `https-proxy`. Asked after the login verdict (account from the verdict) and after the target is judged well formed, before any origin connect, on HTTP/1.1 CONNECT and forwarded (http and ftp) requests and on HTTP/2 streams. Drop aborts the connection (HTTP/2: RST_STREAM INTERNAL_ERROR); chosen error uses the proxy's own AnswerAsync (no challenge on 407). Reason-phrase table copied from the HTTP server (private there). Cli: `http-proxy`/`https-proxy` accepted by `--fault` scheme and given reply shapes; `--aihelp proxy` topic gained a paragraph. ADR-0124 table already had the rows. The ADR-0160 after-bytes tunnel-cut path was not changed: the relay already aborts both links on a failed tunnel. `dotnet format` reports pre-existing import-order errors in other projects, not touched here.

## Log

- 2026-11-14: Created.
- 2026-11-15: Backlog -> Doing.
- 2026-11-15: Doing -> Done. HTTP and HTTPS proxy apply delay, drop and chosen-error fault rules at the command stage
