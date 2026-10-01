---
id: BL-244
title: Rename AiHelpTextTests.Topics_AreTheAdrsSixteenInOrdinalOrder now that smtp makes seventeen
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Surl.Cli.UnitTests, CLAUDE.md]
requirement: none
created: 2026-09-30
completed: 2026-09-30
---
# BL-244 — Rename AiHelpTextTests.Topics_AreTheAdrsSixteenInOrdinalOrder now that smtp makes seventeen

## Goal

The test that pins the `--aihelp` topic list has a name that stays true as protocol topics are
added, and the root `CLAUDE.md` names it by that name.

## Context

- BL-207 added the `smtp` topic, so `AiHelpTextTests.Topics_AreTheAdrsSixteenInOrdinalOrder`
  (`Surl.Cli.UnitTests/AiHelpTextTests.cs`) now pins seventeen topics; its name says sixteen.
  BL-207 kept the name because the root `CLAUDE.md` ("`surl --aihelp` is how an agent learns the
  command line") names the test and was outside its `touches`; it corrected the comment above
  `AdrTopicNames` only.
- IMAP and POP3 (BL-208, BL-209) will add more topics, so a count in the name goes stale again:
  name it for what it checks, such as `Topics_AreTheAdrsTopicsAndEachProtocolAddedInOrdinalOrder`.
- The same holds for `AiHelpExamplesTests.Examples_AreTheAdrsNineteenAndSmtpsInItsOrder` and
  `AiHelpFactsTests.OnlyTheSevenProtocolCategories_HaveSchemes`, which BL-207 renamed with a count.

## Acceptance criteria

- [x] The three tests named above have names without a count, and pass.
- [x] The root `CLAUDE.md` names the topic-list test by its new name; `grep -r "AdrsSixteen"` over
      the repository outside `Tasks/Done` finds nothing.

## Notes

- New names: `AiHelpTextTests.Topics_AreTheAdrsTopicsAndEachProtocolAddedInOrdinalOrder`,
  `AiHelpExamplesTests.Examples_AreTheAdrsExamplesAndEachAddedSinceInItsOrder` (was
  `Examples_AreTheAdrsNineteenSmtpsSshFtpKeytabImapAndPop3InItsOrder` by now) and
  `AiHelpFactsTests.OnlyTheProtocolCategories_HaveSchemes` (was `OnlyTheElevenProtocolCategories_HaveSchemes`).
  None carries a count, so adding a protocol topic changes only the pinned arrays. The grep still
  matches this task file itself, which ends in `Tasks/Done`.

## Log

- 2026-09-30: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. The aihelp topic-list, examples and protocol-category tests are named without a count, and CLAUDE.md names the topic-list test by its new name
