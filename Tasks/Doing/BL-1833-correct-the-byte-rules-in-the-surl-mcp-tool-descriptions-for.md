---
id: BL-1833
title: Correct the byte rules in the surl-mcp tool descriptions for set_key plus the read-back tools
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Application/Surl.Mcp.UnitLibrary, Application/Surl.Mcp.UnitTests]
requirement: none
created: 2026-11-26
completed:
---
# BL-1833 — Correct the byte rules in the surl-mcp tool descriptions for set_key plus the read-back tools

## Goal

The descriptions `surl-mcp` sends to agents say when bytes come back as base64, and `set_key`'s says its 16 MiB
bounds the key and value together.

## Context

Found while running BL-1823 (checked against the code on 2026-11-26).

- `SetKeyTool.Description` says "at most 16 MiB each". `RedisKeyspace.SeedKey` refuses a key and value that
  together hold more than `RedisKeyspace.MaxSeedByteCount` (16 MiB; ADR-0158 decision 3).
- `ReadBackJson.WriteBytes` writes text only for well-formed UTF-8 with no control character other than tab, CR
  and LF, otherwise base64. `SetKeyTool.Description` and the `<summary>` on `SetKeyTool` say `key_base64` is for
  a non-UTF-8 key, leaving out the control-character case.
- The descriptions of `BrowseQueueTool`, `ReadFileTool`, `ReadKeyTool`, `ReadLogTool`, `ReadMessageTool` and
  `ReadRetainedMessageTool` leave out the same case ("text when UTF-8 or else base64", in those or like words).
- All in `Application/Surl.Mcp.UnitLibrary`. No test pins these texts as literals:
  `ProgramTests.RunAsync_ToolsList_IncludesEachStoreAndFaultToolWithAnObjectInputSchemaAndADescription` compares
  `tools/list` with each `Description` constant. `StartServerToolTests` shows how a test asserts that a
  description names a limit.
- `event_name`'s `\xHH` spelling of non-UTF-8 bytes (`SetKeyTool.NameOf`) is true as written and stays.
- The host README and the library `CLAUDE.md` are BL-1823's and BL-1832's.

No store, event or fault changes, and no tool's behaviour changes: this task changes description and doc
comment text, with tests that pin it.

## Acceptance criteria

- [ ] `SetKeyTool.Description` says the key and value hold at most 16 MiB together, and no longer says "each" of
      that limit.
- [ ] `SetKeyTool.Description` and the class `<summary>` say `key_base64` is answered for a key that is not
      UTF-8 or that holds a control character other than tab, CR and LF.
- [ ] The `Description` of each of the six read-back tools named in Context says bytes are text only when they are
      UTF-8 with no control character other than tab, CR and LF, else base64.
- [ ] A test in `Application/Surl.Mcp.UnitTests` asserts the control-character wording in each of the seven
      descriptions, and another asserts `set_key`'s "together" limit.
- [ ] The tests of `Surl.Mcp.UnitTests` pass with no `TestCategory=Integration`, and
      `dotnet build Application/Surl.Mcp.UnitTests -warnaserror` is clean.

## Notes

## Log

- 2026-11-26: Created.
- 2026-11-27: Backlog -> Doing.
