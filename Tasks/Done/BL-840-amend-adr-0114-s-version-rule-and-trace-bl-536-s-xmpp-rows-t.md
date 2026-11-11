---
id: BL-840
title: Amend ADR-0114's version rule and trace BL-536's XMPP rows to their tests
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-536]
touches: [Documentation/Planning/Decisions/ADR-0114-how-the-xmpp-server-answers-xmpp-and-xmpps.md, Documentation/Conformance/Extended/Xmpp.md]
requirement: none
created: 2026-11-05
completed: 2026-11-11
---
# BL-840 — Amend ADR-0114's version rule and trace BL-536's XMPP rows to their tests

## Goal

ADR-0114 decision 1 states the version rule the XMPP server applies, and every `Xmpp.md` row
BL-536 covers names its test instead of `BL-536`.

## Context

- ADR-0114 decision 1 says "`1.0`, or a higher major or minor, opens a `1.0` stream". RFC 6120
  4.7.5 and 4.9.3.25 (whose example refuses `version='11.0'`) and BL-536's own acceptance criterion
  (`version="2.0"` gets `unsupported-version`) say a different major is unsupported. BL-536 built
  the RFC's rule: a major version of 1 with any minor opens a `1.0` stream; no version, a malformed
  one, or any other major is `unsupported-version`
  (`XmppServerTests.ServeAsync_VersionOtherThanMajor1_IsUnsupportedVersion`).
- BL-536 could not edit either document: BL-396 (`Documentation/Conformance`) and BL-822
  (`Documentation/Planning/Decisions`) held them in Doing.
- Rows to trace in `Documentation/Conformance/Extended/Xmpp.md`: every row whose "Covered by" is
  `BL-536`. Tests are in `Protocols/Messaging/Surl.Extended.Protocol.Xmpp.UnitTests`
  (`XmppServerTests`, `XmppStreamReaderTests`, `XmppDomainsTests`, `XmppParseFailuresTests`).
- Rows BL-536 did not fully prove, to keep naming a task rather than a test:
  `Xmpp:RFC6120-4.9.3.4-1` (idle timeout) is proven only as "a limit cancelling the exchange is
  `connection-timeout`" (`XmppServerTests.ServeAsync_CancelledWhileOpen_SaysFarewellWithItsError`);
  the engine cancels for `--max-time` the same way, so that limit is also answered
  `connection-timeout`, not ADR-0114 decision 7's "close with no bytes". Say so in the ADR.

## Acceptance criteria

- [x] ADR-0114 decision 1's `version` bullet says a major version of 1 opens a `1.0` stream and any other major, a missing or a malformed version is `unsupported-version`, citing RFC 6120 4.9.3.25.
- [x] ADR-0114 decision 7 says `--max-time` is answered `connection-timeout` like the idle timeout, because the exchange context cannot tell them apart.
- [x] No row in `Documentation/Conformance/Extended/Xmpp.md` names `BL-536`; each names the test that proves it.

## Notes

- Delivered in the session rather than through `align-and-document`: two documents, every edit
  checked against the tests by name (a PowerShell check confirmed each cited test method exists in
  `Surl.Extended.Protocol.Xmpp.UnitTests`).
- ADR-0114 amended in place (decision 1's `version` bullet, decision 6's table rows for
  `unsupported-version` and `connection-timeout`, decision 7's `--max-time` row) with an "Amended"
  header line and an "Amendment 1" section, decided by Claude under Stewart's delegation. The
  `ExchangeContext` only exposes `IsCancelledByLimit` and `ShutdownToken`, so idle timeout and
  `--max-time` really are indistinguishable to the server.
- `Xmpp:RFC6120-4.7.5-1`'s requirement text was corrected to the RFC rule as well, since it said
  "major below 1".
- `Xmpp:RFC6120-4.9.3.4-1` (idle timeout) names the cancellation test for the answer and BL-540
  (serving `xmpp` from `surl` behind `Surl.Core`'s idle clock) for the idle timer itself, as the
  Context asked: the unit test does not drive the idle clock.
- `dotnet build` cannot run on this machine on 2026-11-11: `global.json` pins SDK 10.0.401, which
  is not installed (BL-1035). This task changes only two Markdown files, which no project compiles
  and no test reads (checked with a search of every `.cs` file), and the docs pipeline runs
  `verify` only when a `.cs` or project file changes, so the task is filed Done without a build.

## Log

- 2026-11-05: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Done. ADR-0114 states the RFC 6120 major-version rule and the --max-time connection-timeout answer, and every BL-536 row in Xmpp.md names its test
