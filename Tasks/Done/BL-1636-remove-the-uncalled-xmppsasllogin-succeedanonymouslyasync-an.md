---
id: BL-1636
title: Remove the uncalled XmppSaslLogin.SucceedAnonymouslyAsync and cover MechanismIsPlain
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Protocols/Messaging/Surl.Extended.Protocol.Xmpp.UnitLibrary, Protocols/Messaging/Surl.Extended.Protocol.Xmpp.UnitTests]
requirement: none
created: 2026-11-24
completed: 2026-11-24
---
# BL-1636 — Remove the uncalled XmppSaslLogin.SucceedAnonymouslyAsync and cover MechanismIsPlain

## Goal

`Measure-CodeQuality.ps1 -Library Surl.Extended.Protocol.Xmpp.UnitLibrary` reports 100% line and 100% branch with no
failing member, and the library holds no uncalled method.

## Context

- Found by the coverage-auditor's audit of 2026-11-24 (two full fast runs, identical): Xmpp 99.53% line, 99.84% branch,
  2 failing members.
- `async XmppSaslLogin.SucceedAnonymouslyAsync()`,
  `Protocols/Messaging/Surl.Extended.Protocol.Xmpp.UnitLibrary/XmppSaslLogin.cs:228`, 0% line (lines 229-234). It has
  **no caller**: commit daad7dad2 added it, but SASL ANONYMOUS is served through `policy.StartSaslExchange` in `StartAsync`
  (line 76), and `XmppAnonymousLoginTests` passes through that path. This is dead code, not a missing test.
- `XmppSaslPlainRedactor.MechanismIsPlain(string)`, `XmppSaslPlainRedactor.cs:176`, 87.5% branch. Line 185
  (`rest.Length == 0 || rest[0] != '='`) is 3 of 4: one of these is never tried, either an `<auth` tag where `mechanism`
  is the last thing (nothing after it), or one where `mechanism` is followed by something other than `=`.

## Acceptance criteria

- [x] `SucceedAnonymouslyAsync` is deleted. `XmppAnonymousLoginTests` still passes unchanged, which shows ANONYMOUS still
      logs in as a new `anon-` JID.
- [x] Named `Surl.Extended.Protocol.Xmpp.UnitTests` tests redact an `<auth` element whose `mechanism` has no `=` after it
      and one whose `mechanism` ends the tag text, and each asserts the element is logged as a non-PLAIN one.
- [x] `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Surl.Extended.Protocol.Xmpp.UnitLibrary` reports
      `100 | 100` with 0 failing members.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` is green.

## Notes

- Before deleting, confirm with a search that nothing (including `XmppServerConnection`) calls it. If a caller was meant
  to exist, the anonymous path's log line `Logged in as …` may be missing from the real path. Check that the
  `StartAsync` path logs it, and file a task if it does not.

- Done 2026-11-24: nothing called SucceedAnonymouslyAsync (grep); the StartAsync path logs 'Logged in as' in SucceedAsync. Removed it. In MechanismIsPlain the held tag always ends in '>', so 'rest.Length == 0' was unreachable; replaced by StartsWith("=") (same behaviour, no dead branch). Added two redaction tests (no '=' after mechanism; mechanism ending the attribute text before '>').

## Log

- 2026-11-24: Created.
- 2026-11-24: Backlog -> Doing.
- 2026-11-24: Doing -> Done. Xmpp library at 100/100 coverage: dead SucceedAnonymouslyAsync removed, MechanismIsPlain branches covered
