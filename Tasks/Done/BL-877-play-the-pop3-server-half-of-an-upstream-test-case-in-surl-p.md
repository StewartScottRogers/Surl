---
id: BL-877
title: Play the POP3 server half of an upstream test case in Surl.Protocol.Pop3
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-875]
touches: [Protocols/Mail/Surl.Protocol.Pop3.UnitLibrary, Protocols/Mail/Surl.Protocol.Pop3.UnitTests]
requirement: none
created: 2026-11-05
completed: 2026-11-05
---
# BL-877 — Play the POP3 server half of an upstream test case in Surl.Protocol.Pop3

## Goal

`Pop3UpstreamTestCaseServer` (public, in `Surl.Protocol.Pop3`) is Surl's own `Pop3ProtocolServer` playing one
upstream test case through BL-875's `MailUpstreamTestCaseReplies`. Unit tests drive it without a network.

## Context

- Part of BL-395's split (timed out after 120 min; Stewart asked for the split 2026-11-05). BL-875 supplies
  `MailUpstreamTestCaseReplies`. BL-879 composes this half into the conformance run.
- Decision: ADR-0080 decision 2. The half is Surl's own server with its own replies (ADR-0056). The case
  supplies only what FILEFORMAT.md (tag `curl-8_21_0`, lines 367 to 395) makes the case's:
  - `REPLY welcome` replaces the greeting, and a `REPLY`'d line (command or SASL response) is answered with
    the case's text and not run.
  - `CAPA` lists the case's capabilities and `SASL` mechanisms from `AUTH`.
  - `RETR` and `TOP` send the case's `<data>`/`<dataNUM>` for the message number, and `LIST`/`UIDL` send the
    case's listing.
  - `USER`, `PASS` and `APOP` accept any login, as upstream's test server does. `STAT`, `RSET`, `DELE` and
    `NOOP` answer for the case's one message.
  - Every command line is recorded in `ReceivedBytes`.
  A SASL `AUTH` that is not `REPLY`'d goes to the `AnonymousAuthenticationPolicy` passed in. FTP precedent:
  BL-394's `FtpUpstreamTestCaseServer`.
- With no case, `Pop3ProtocolServer` behaves exactly as before. Every existing POP3 test passes unchanged.
- **Reuse the stash.** `git stash list` and find `darkfactory BL-395`; call it `stash@{N}`. Take these files:
  - Untracked files: `git checkout "stash@{N}^3" -- Protocols/Mail/Surl.Protocol.Pop3.UnitLibrary/Pop3Session.UpstreamTestCase.cs Protocols/Mail/Surl.Protocol.Pop3.UnitLibrary/Pop3UpstreamTestCaseServer.cs`,
    then `git reset` those paths.
  - Tracked edits: `git diff "stash@{N}^1" "stash@{N}" -- Protocols/Mail/Surl.Protocol.Pop3.UnitLibrary/Pop3ProtocolServer.cs Protocols/Mail/Surl.Protocol.Pop3.UnitLibrary/Pop3Session.cs | git apply -3`.
    These add the internal `UpstreamTestCaseReplies` init property, the partial class, and the greeting and
    line hooks.
  The code compiled in the lane but has **no tests**. Read `Pop3Session.UpstreamTestCase.cs` to confirm which
  commands it answers from the case, and pin exactly those in the tests. Ignore the stash's other files. Do not
  apply or drop the stash.
- Stand-in (ADR-0124): none applies. This is a test-only half with its own empty store, so it has no shared
  store, events or faults.

## Acceptance criteria

- [x] `Protocols/Mail/Surl.Protocol.Pop3.UnitTests` has `Pop3UpstreamTestCaseServerTests`, which drive the half
      over an in-memory connection with inline cases. They cover `REPLY welcome`, a `REPLY`'d command not being
      run, a `COUNT`-limited reply, `CAPA` and `AUTH` in the `CAPA` answer, each command the half answers from
      the case's data (`RETR` and `TOP` with `<dataNUM>` selection, `LIST`, `UIDL`, `STAT`), `USER`/`PASS`
      and `APOP` accepting any login, and `ReceivedBytes`.
- [x] A named test shows that `Pop3ProtocolServer` composed without a case still greets and answers `CAPA` with
      its own text.
- [x] `dotnet build Protocols/Mail/Surl.Protocol.Pop3.UnitLibrary -warnaserror` is clean,
      `dotnet test --filter "TestCategory!=Integration"` is green, and `Surl.Protocol.Pop3.UnitLibrary` keeps 100%
      line and branch coverage.

## Notes

- Took `Pop3Session.UpstreamTestCase.cs`, `Pop3UpstreamTestCaseServer.cs` and the `Pop3ProtocolServer`/`Pop3Session`
  hooks from the BL-395 stash (`0359f486`, by `git show`/`git diff` on its SHA, not applied or dropped); they
  applied cleanly and compiled unchanged.
- The half answers from the case: `CAPA` (case capabilities but `APOP`, then `SASL` from `AUTH`; not a command
  with neither), `USER`, `PASS`, `APOP` (only when the case's `CAPA` lists it), `STAT`, `LIST`, `UIDL`, `RETR`,
  `TOP`, `DELE`, `RSET`, `NOOP`. `QUIT` and `AUTH` stay the server's own; an un-`REPLY`'d `AUTH` runs on the
  `AnonymousAuthenticationPolicy`. `Pop3UpstreamTestCaseServerTests` pins each, plus the argument checks.
- `ReceivedBytes` is pinned for command lines read by the command loop; SASL response lines were not pinned here.
- Measured: `Surl.Protocol.Pop3.UnitLibrary` 100% line, 100% branch, worst CRAP 10; 215 POP3 tests pass.

## Log

- 2026-11-05: Created.
- 2026-11-05: Split from BL-395 (timed out after 120 min; Stewart asked for the split 2026-11-05).
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. Pop3UpstreamTestCaseServer plays one upstream test case over Surl's own POP3 server, pinned by Pop3UpstreamTestCaseServerTests
