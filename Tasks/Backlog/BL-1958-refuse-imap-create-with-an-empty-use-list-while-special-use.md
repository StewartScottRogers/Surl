---
id: BL-1958
title: Refuse IMAP CREATE with an empty USE list while SPECIAL-USE is not offered
priority: Low
assignee: Claude
pipeline: protocol
depends-on: []
touches: [Protocols/Mail/Surl.Protocol.Imap.UnitLibrary, Protocols/Mail/Surl.Protocol.Imap.UnitTests]
requirement: none
created: 2026-11-27
completed:
---
# BL-1958 — Refuse IMAP CREATE with an empty USE list while SPECIAL-USE is not offered

## Goal

While `SPECIAL-USE` is not offered, `CREATE x (USE ())` answers `NO [USEATTR] The use cannot be given to this mailbox`
and creates no mailbox, as ADR-0055, Amendment 1, point 5 says of any `USE`.

## Context

Found while working BL-1956 (2026-11-27), read from source, not run. Start by reproducing it with a failing test.

All paths are under `Protocols/Mail/Surl.Protocol.Imap.UnitLibrary`.

- `ImapSession.Changes.cs`, `CreateMailbox` (about lines 113-126), starts `uses` as an empty list and replaces it only
  when a parameter follows the name. `ImapArguments.ReadCreateUseList` (about lines 136-155) also returns an empty list
  for `(USE ())`.
- `CreateMailboxWithUses` (about lines 128-138) takes any empty list as "no parameter" and creates the mailbox without
  asking `mailStore.HoldsAnyFolderUse`. So an absent parameter and an empty use list cannot be told apart.
- The comment at about lines 110-112 says the parameter "is read only while SPECIAL-USE is offered". It is always
  parsed; a use is refused at about line 135.

What decides it:

- ADR-0055, Amendment 1, point 5: `CREATE <name> (USE (\Sent))` is accepted when `CREATE-SPECIAL-USE` is advertised,
  "so is any `USE` when the capability is not advertised" answered `NO [USEATTR]`. An empty list is a `USE` parameter,
  so the code is what changes; no ADR change is needed.
- RFC 6154 (https://www.rfc-editor.org/rfc/rfc6154.txt), section 6:
  `create-param =/ "USE" SP "(" [use-attr *(SP use-attr)] ")"`, so the empty list is well formed. Section 3: "Clients
  MUST NOT use the "USE" parameter unless the server advertises the capability."
- While the capability is offered, `(USE ())` keeps creating a mailbox with no use
  (`ImapSpecialUseTests.ServeAsync_CreateWithAUse_SucceedsAndTheMailboxListsWithIt`).

"Offered" is what the capability list asks: today `MailboxStore.HoldsAnyFolderUse` (`ImapSession.cs`, about lines
115-116). BL-1959 decides when that is evaluated; this task keeps whatever predicate the code uses when it runs.

Upstream curl (8.21.0) never sends a use attribute on its own (`Protocols/Mail/Surl.Protocol.Imap.UnitTests/Fixtures/README.md`,
"SPECIAL-USE") and sends an `-X` command as given (ADR-0055, "What upstream curl 8.21.0 does"). The refusal line is the
one the server already sends for a non-empty list, so no new measurement is needed. The Curl port is not evidence.

Stand-in: the mailbox lives in the shared `MailboxStore`. A refusal changes nothing, so no store event is published. A
test seeds the store as `ImapSpecialUseTests` does (a store holding no use, or `StoreWithUses`), observes with `LIST`,
and command-stage faults reach `CREATE` as before. No new reset or fault point.

## Acceptance criteria

- [ ] A new test in `ImapSpecialUseTests` shows that on a store holding no use, `CREATE x (USE ())` after a login answers `NO [USEATTR] The use cannot be given to this mailbox`, and that a following `LIST "" x` lists no mailbox `x`.
- [ ] A test shows that on a store holding no use, `CREATE x` with no parameter still answers `OK CREATE completed`.
- [ ] `ServeAsync_CreateWithAUse_SucceedsAndTheMailboxListsWithIt` and `ServeAsync_CreateWithMalformedParameters_IsRefused` pass with their expected answers unchanged.
- [ ] The comment on `CreateMailbox` says the parameter is parsed whether or not `SPECIAL-USE` is offered, and that any well-formed `USE`, the empty list included, is `NO [USEATTR]` while it is not offered.
- [ ] `dotnet build Protocols/Mail/Surl.Protocol.Imap.UnitLibrary -warnaserror` is clean and the `Surl.Protocol.Imap.UnitTests` fast tests pass, none of the new ones marked `Integration`.
- [ ] The IMAP library stays at 100% line and branch coverage.

## Notes

## Log

- 2026-11-27: Created.
