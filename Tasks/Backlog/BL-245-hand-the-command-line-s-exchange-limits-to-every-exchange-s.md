---
id: BL-245
title: Hand the command line's exchange limits to every exchange's ExchangeContext
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Surl.Core.UnitLibrary, Surl.Core.UnitTests, Surl.Console, Surl.Console.UnitTests]
requirement: FR-012
created: 2026-09-30
completed:
---
# BL-245 — Hand the command line's exchange limits to every exchange's ExchangeContext

## Goal

`ServingEngine` builds every `ExchangeContext` with the `ExchangeLimits` the command line parsed (`SurlCommandLine.Limits`: `--head-timeout`, `--max-request-head`, `--max-line`, `--max-message`, `--max-filesize`), so each protocol server's `context.Limits` holds what the operator gave rather than `ExchangeLimits.Default`.

## Context

Found by BL-210, measured 2026-09-30 with the pinned Windows reference build, upstream curl 8.21.0. Limits are ADR-0006 section 1 (NFR-012 to NFR-016) and their refusals FR-012.

- `Surl.Core.UnitLibrary/ServingEngine.cs` `OpenExchange` creates `new ExchangeContext(exchangeId, listenUrl, localEndPoint, remoteEndPoint, log, timeProvider, deadlines.Token)` and never sets `Limits`, so `ExchangeContext.Limits` is always `ExchangeLimits.Default`. `Surl.Console/CommandLineRunner.cs` (around line 648) constructs the engine with `ComposeConnectionLimits(commandLine)` only; `commandLine.Limits` reaches only the content store's `MaxUploadBytes` and the mail store's `maxMessageBytes`, never the engine.
- Effect seen: `surl --allow-anonymous --max-filesize 10 smtp://127.0.0.1:0/` still advertises `250-SIZE 104857600` in its EHLO reply and accepts `MAIL FROM:<a@x> SIZE=53`; the mail store then refuses the body with `552 5.3.4 Message exceeds the size limit` after DATA and curl exits 8, where ADR-0053 decisions 2, 4 and 10 require `SIZE 10` and `552 5.3.4 Message size exceeds the size limit` at MAIL (curl exit 55, "MAIL failed: 552").
- The same defect means `--max-line` and `--head-timeout` (and `--max-filesize` for any server that reads `context.Limits.MaxUploadBytes`) are ignored by every protocol server and by the engine's own TLS-handshake head timeout (`ServingEngine.cs` around line 540).
- Suggested shape: add an `ExchangeLimits` parameter to the engine (a new constructor overload or the options already passed), defaulting to `ExchangeLimits.Default` for the existing overloads; set `Limits = exchangeLimits` in `OpenExchange`; pass `commandLine.Limits` from `CommandLineRunner`. Keep the quality gates (100% line and branch coverage, CA1502 at most 10).

## Acceptance criteria

- [ ] A `ServingEngineTests` fast test in `Surl.Core.UnitTests` proves an exchange's `ExchangeContext.Limits` equals the `ExchangeLimits` the engine was given (a fake protocol server captures the context).
- [ ] A fast test in `Surl.Console.UnitTests` proves `CommandLineRunner` passes `--max-filesize`, `--max-line` and `--head-timeout` through to the engine's exchanges' `ExchangeContext.Limits`.
- [ ] The engine's TLS-handshake head timeout uses the given `ExchangeLimits` head timeout, not `ExchangeLimits.Default`'s, proven by a `ServingEngineTests` fast test.
- [ ] `dotnet build -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is green; no new test needs `TestCategory=Integration`.
- [ ] Unblocks BL-210: `Send_MaxFilesize10_ExitsSendErrorAndStoresNothing` and `Send_MessageOverAMebibyteWithMaxFilesize1000_ExitsSendErrorAndStoresNothing` in `Surl.Conformance.UnitTests/UpstreamCurlSendsMailToSurlOverSmtpTests.cs` can pass once BL-210 lands (that test file is BL-210's, not this task's to change).

## Notes

Found by BL-210 (dark factory lane 6, 2026-09-30).

## Log

- 2026-09-30: Created.
