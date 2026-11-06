---
id: BL-878
title: Play the IMAP server half of an upstream test case in Surl.Protocol.Imap
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-875]
touches: [Protocols/Mail/Surl.Protocol.Imap.UnitLibrary, Protocols/Mail/Surl.Protocol.Imap.UnitTests]
requirement: none
created: 2026-11-05
completed: 2026-11-05
---
# BL-878 — Play the IMAP server half of an upstream test case in Surl.Protocol.Imap

## Goal

`ImapUpstreamTestCaseServer` (public, in `Surl.Protocol.Imap`) is Surl's own `ImapProtocolServer` playing one
upstream test case through BL-875's `MailUpstreamTestCaseReplies`. Unit tests drive it without a network.

## Context

- Part of BL-395's split (timed out after 120 min; Stewart asked for the split 2026-11-05). BL-875 supplies
  `MailUpstreamTestCaseReplies`. BL-879 composes this half into the conformance run.
- Decision: ADR-0080 decision 2. The half is Surl's own server with its own replies (ADR-0055). The case
  supplies only what FILEFORMAT.md (tag `curl-8_21_0`, lines 367 to 395) makes the case's:
  - `REPLY welcome` replaces the greeting, and a `REPLY`'d command (matched on the text after the tag) or SASL
    response is answered with the case's text and not run.
  - `CAPABILITY` lists the case's `CAPA` and `AUTH=` mechanisms.
  - `FETCH` sends the case's `<data>`/`<dataNUM>` as the message, with `POSTFETCH` text before its closing
    parenthesis, and `LIST` sends the case's listing.
  - An `APPEND` literal is recorded in `UploadedBytes`, and every command line in `ReceivedBytes`.
  Logins go to the `AnonymousAuthenticationPolicy` passed in. FTP precedent: BL-394's `FtpUpstreamTestCaseServer`.
- With no case, `ImapProtocolServer` behaves exactly as before. Every existing IMAP test passes unchanged.
- **Reuse the stash.** `git stash list` and find `darkfactory BL-395`; call it `stash@{N}`. Take these files:
  - Untracked files: `git checkout "stash@{N}^3" -- Protocols/Mail/Surl.Protocol.Imap.UnitLibrary/ImapSession.UpstreamTestCase.cs Protocols/Mail/Surl.Protocol.Imap.UnitLibrary/ImapUpstreamTestCaseServer.cs`,
    then `git reset` those paths.
  - Tracked edits: `git diff "stash@{N}^1" "stash@{N}" -- Protocols/Mail/Surl.Protocol.Imap.UnitLibrary/ImapProtocolServer.cs Protocols/Mail/Surl.Protocol.Imap.UnitLibrary/ImapSession.cs | git apply -3`.
    `ImapSession.cs` changed on master after the stash was made (BL-675/BL-677 shared folders, quotas and
    change notices), so expect a three-way merge and resolve it by hand. The hooks are the constructor
    parameter, the greeting call and the two checks at the top of `AnswerNextCommandAsync`.
  The code compiled in the lane but has **no tests**. IMAP was the largest of the three halves. Ignore the
  stash's other files. Do not apply or drop the stash.
- Stand-in (ADR-0124): none applies. This is a test-only half with its own empty store, so it has no shared
  store, events or faults.

## Acceptance criteria

- [x] `Protocols/Mail/Surl.Protocol.Imap.UnitTests` has `ImapUpstreamTestCaseServerTests`, which drive the half
      over an in-memory connection with inline cases. They cover `REPLY welcome`, a `REPLY`'d command not being
      run, a `COUNT`-limited reply, `CAPABILITY` listing `CAPA` and `AUTH`, `FETCH` with `<dataNUM>` selection
      and `POSTFETCH`, `LIST` from the case, an `APPEND` literal landing in `UploadedBytes`, and
      `ReceivedBytes`.
- [x] A named test shows that `ImapProtocolServer` composed without a case still greets with its own
      `CAPABILITY` and answers `FETCH` from the mail store.
- [x] `dotnet build Protocols/Mail/Surl.Protocol.Imap.UnitLibrary -warnaserror` is clean,
      `dotnet test --filter "TestCategory!=Integration"` is green, and `Surl.Protocol.Imap.UnitLibrary` keeps 100%
      line and branch coverage.

## Notes

- Took the stash's two untracked files (`darkfactory BL-395`, 0359f486) as they were, and put the four hooks into the current `ImapSession.cs` by hand: the constructor parameter, `GreetAsync`, and the two checks at the top of `AnswerNextCommandAsync`. `ImapProtocolServer` gets the internal `UpstreamTestCaseReplies` init property.
- Coverage changes. `read.Tag ?? string.Empty` became `read.Tag!`: the reader always sets a tag on an `AppendMessage` result, so the fallback could never run. The `UID` string switch became `if` checks plus a `HashSet`, because the compiler-generated branch of a string switch can't be reached and so can't be covered.
- `POSTFETCH` text goes in as the case writes it, right after the message and with no space added. The tests pin the stash's behaviour. No upstream bytes were measured here; BL-879's conformance run against pinned upstream curl checks it.
- `ImapUpstreamTestCaseServerTests`: 47 cases (`DataRow` rows included). The no-case test is `ServeAsync_NoCase_GreetsWithItsOwnCapabilityAndFetchesFromTheMailStore`. Surl.Protocol.Imap.UnitLibrary keeps 100% line and branch coverage; the project's CLAUDE.md now describes the half.

## Log

- 2026-11-05: Created.
- 2026-11-05: Split from BL-395 (timed out after 120 min; Stewart asked for the split 2026-11-05).
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. ImapUpstreamTestCaseServer plays an upstream IMAP test case over Surl's own server, tested at 100% coverage
