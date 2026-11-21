---
id: BL-1508
title: Parse folders=<name>=<use> in the --mailbox option
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-1486]
touches: [Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests, Documentation/Planning/Decisions/ADR-0101-surls-configuration-surface-the-config-file-repeatable-item-options-and-one-configuration-model.md]
requirement: none
created: 2026-11-20
completed:
---
# BL-1508 — Parse folders=<name>=<use> in the --mailbox option

## Goal

`--mailbox "a@x:pw;folders=Sent=sent,Drafts=drafts,Archive"` parses into the configured mailbox with its folder uses, and a bad use is refused with the message the ADR names.

## Context

ADR-0055 Amendment 1, decision 2, and ADR-0101's `--mailbox` row. Code: `Application/Surl.Cli.UnitLibrary/ConfiguredMailboxReader.cs` and `ConfiguredMailbox.cs`. Split each `folders=` item at the last `=`; the use is one of all, archive, drafts, flagged, junk, sent, trash, written without a backslash and compared without regard to case. `folders=Sent` without `=` is today's folder with no use. `--shared-folder` is unchanged. The ADR-0101 file is edited only for the table row (`Documentation/Planning/Decisions/ADR-0101-surls-configuration-surface-...md`). Mapping to `MailboxSettings` is BL-1509's job, not this one's. The `-K` file takes the same text through the same reader.

## Acceptance criteria

- [ ] `ConfiguredMailbox` carries the folder uses (folder name to use), empty by default, and `folders=Sent=sent,Drafts=drafts,Archive` yields Sent and Drafts with those uses and Archive with none.
- [ ] A use is accepted in any letter case and stored in one canonical form.
- [ ] `folders=Sent=bogus` is refused with `folder use bogus is not one of all, archive, drafts, flagged, junk, sent, trash`, returning the exit code of row 10's other refusals.
- [ ] `folders=A=sent,B=sent` is refused with `use sent is given to two folders` (row 14's pattern).
- [ ] The ADR-0101 `--mailbox` row shows `folders` as `<name>[=<use>],...`.
- [ ] Tests for each case exist and pass, coverage stays 100%, and `dotnet build` of both projects with `-warnaserror` is clean.

## Notes

## Log

- 2026-11-20: Created.
- 2026-11-20: Backlog -> Doing.
