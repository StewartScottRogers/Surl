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
completed:
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

- [ ] ADR-0114 decision 1's `version` bullet says a major version of 1 opens a `1.0` stream and any other major, a missing or a malformed version is `unsupported-version`, citing RFC 6120 4.9.3.25.
- [ ] ADR-0114 decision 7 says `--max-time` is answered `connection-timeout` like the idle timeout, because the exchange context cannot tell them apart.
- [ ] No row in `Documentation/Conformance/Extended/Xmpp.md` names `BL-536`; each names the test that proves it.

## Notes

## Log

- 2026-11-05: Created.
- 2026-11-11: Backlog -> Doing.
