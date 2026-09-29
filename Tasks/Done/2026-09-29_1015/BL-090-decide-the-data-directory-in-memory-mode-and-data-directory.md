---
id: BL-090
title: Decide the data directory, in-memory mode and data-directory lock and record ADR-0031
priority: High
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions, Documentation/Product/Requirements.md]
requirement: FR-022
created: 2026-09-29
completed: 2026-09-29
---
# BL-090 — Decide the data directory, in-memory mode and data-directory lock and record ADR-0031

## Goal

An Accepted ADR-0031, marked "Decided by Claude under Stewart's delegation", fixes every
open detail of Surl's data directory, in-memory mode and data-directory lock, and
`Requirements.md` carries FR-022 to FR-025 for them, so the implementation tasks
BL-091 to BL-098 can be built without asking a question.

## Context

Stewart approved the feature on 2026-09-29 (his words, summarised):

1. `surl --directory <path> <url>...` persists everything the services store under
   `<path>`, surviving restarts: plain files (HTTP, TFTP, Gopher, DICT uploads and served
   content) at the top of the path; non-file service state under `<path>/.surl/`
   (`.surl/mqtt/` for MQTT retained messages now; later `.surl/mail/`, LDAP and so on).
   `.surl` is never served, even with `--serve-dot-files`.
2. `surl <url>...` without `--directory` serves an in-memory file system (a production
   `IContentFileSystem` in `Surl.Content.UnitLibrary`) and keeps service state in memory,
   empty at start, lasting for the process lifetime; nothing touches the disk. This
   supersedes ADR-0007's default of `.` (the current directory): a breaking change.
3. Several surl processes run in parallel without interfering when their paths differ:
   no shared temp folders, global locks, named mutexes or machine-wide state; each
   in-memory instance is isolated.
4. The same path used by two running processes at once: the second is refused through a
   lock file the first holds for its lifetime (e.g. `<path>/.surl/lock`), with a clear
   stderr message and an exit code this ADR decides.
5. Exposure defaults are unchanged: uploads still need `--allow-uploads`, in memory too.

Where the code is today:
- `Surl.Cli.UnitLibrary/SurlCommandLine.cs`: `ServedDirectory` is a `string` defaulting to
  `"."`; `HelpText.cs` says `Directory to serve (default: current directory)`.
- `Surl.Console/CommandLineRunner.cs`: `ServeAsync` probes the directory
  (`ServedDirectoryProbe.CanOpen`, 37 on failure), `ComposeContentStore` builds
  `new ContentStore(Path.GetFullPath(...), new DiskContentFileSystem(), ...)`, and
  `ComposeProtocolServers` builds `new MqttProtocolServer(new MqttRetainedMessages())`.
- `Surl.Content.UnitLibrary/IContentFileSystem.cs` is the seam; `DiskContentFileSystem` is
  the only class in Surl.Content that touches the disk. `ContentStore` hides dot-files
  unless `ContentExposureOptions.ServeDotFiles`, and writes uploads to a temporary
  dot-file beside the target, then renames it (BL-086).
- Of today's servers only TFTP accepts uploads (`TftpWriteTransfer`); the HTTP server
  answers `PUT` with `405` (`HttpRequestResponder.MethodsRefusedWithAllow`). The ADR
  covers uploads by any server that later accepts them, through `ContentStore`.
- `Surl.Protocol.Mqtt.UnitLibrary/MqttRetainedMessages.cs` keeps retained messages in a
  dictionary, bounded by `MaxTopics` and `MaxTotalPayloadBytes` (ADR-0014 decision 7).
  The MQTT library references only `Surl.Protocol.Abstractions.UnitLibrary` today; ADR-0002
  lets it reference `Surl.Content.UnitLibrary`.
- Inputs: ADR-0002, ADR-0005 (exit-code table), ADR-0006 (hardening: every store a peer
  can fill is bounded), ADR-0007 (sections 2, 5, 6 and "Alternatives considered", which
  rejected requiring `--directory`), ADR-0014, ADR-0015, ADR-0018.

The implementation tasks are already filed on these homes, which this ADR records as
decided (they follow ADR-0002 and root `CLAUDE.md`):
- `InMemoryContentFileSystem` (production, `Surl.Content.UnitLibrary`), plus a
  `CreateDirectory(string path)` member on `IContentFileSystem` (default throws
  `NotSupportedException`, like the other write members) implemented by
  `DiskContentFileSystem` and `InMemoryContentFileSystem` (BL-091).
- The `.surl` refusal lives in `ContentStore` (BL-092).
- `SurlCommandLine`'s directory becomes nullable, `null` when `--directory` is absent, and
  `Surl.Console` composes the in-memory file system then (BL-093).
- MQTT persistence is a seam in `Surl.Protocol.Mqtt.UnitLibrary` that reads and writes
  through `IContentFileSystem`, so the MQTT library never touches the disk itself (BL-094),
  wired in `Surl.Console` (BL-095).
- The lock is a class in `Surl.Console` (the only other place real-disk code already lives,
  beside `ServedDirectoryProbe`), behind a delegate the runner is given (BL-096).

## Acceptance criteria

- [x] `Documentation/Planning/Decisions/ADR-0031-the-data-directory-and-in-memory-mode.md`
      exists (the next free number if 0031 is taken; then use that number in this task's
      follow-ups), Status Accepted, dated 2026-09-29 or later, "Decided by Claude under
      Stewart's delegation", and cites Stewart's approval of 2026-09-29.
- [x] It states that it supersedes ADR-0007's `--directory` default of `.`, and ADR-0007
      gains one line under its Status naming ADR-0031 as superseding that default (no
      other change to ADR-0007).
- [x] It decides and states each of these, with the reason:
      1. The glossary term for the directory `--directory` names (keep "served directory"
         or rename, e.g. "data directory"), and the `SurlCommandLine` property name and
         type (`string?`, `null` when absent).
      2. The new help line for `--directory` (exact text, same column layout as
         `HelpText.cs`).
      3. Whether a `--directory` path that does not exist is refused with 37 as today or
         created; and what happens when `<path>/.surl` cannot be created (exit code and
         stderr text).
      4. The in-memory file system: the full path it uses as the served root on each
         platform (it touches no disk, so any rooted path the store accepts under ADR-0018),
         its bound on total bytes held (a constructor parameter with a named default; no
         new command-line option in this work), what an upload past that bound gets, and
         the last-write time it reports (from the injected `TimeProvider`).
      5. How `.surl` is recognised in a request path (first segment only; ordinal or
         ordinal-ignore-case, given case-insensitive file systems on Windows and macOS),
         and that it is answered exactly as a missing entry for reads, listings (omitted)
         and uploads (refused as not permitted), whatever `--serve-dot-files` and
         `--follow-symlinks` say, including a followed symbolic link whose final target
         lies inside `<root>/.surl`.
      6. The MQTT retained-message file: its path (`<path>/.surl/mqtt/<name>`), its byte
         format, that every change is written through a temporary file renamed into place,
         when it is written (every change that alters the store), that it is loaded once at
         start, and what surl does at start when the file is unreadable or malformed (exit
         code and stderr text, or start empty with a stderr note).
      7. The lock: its path (`<path>/.surl/lock`), that it is an exclusively opened file
         held for the process lifetime (`FileShare.None`, which .NET maps to an advisory
         `flock` on Linux and macOS), that a stale file left by a killed process never
         refuses the next start, whether the file is deleted on exit, where in
         `ServeAsync` it is taken (before any listener binds), and the exact stderr text
         and `SurlExitCode` of a refusal. If the code is a new `SurlExitCode` member, the
         ADR adds its row to ADR-0005's table by the same rules; if it reuses a curl
         number, it cites that `CURLE_*` meaning from
         https://curl.se/libcurl/c/libcurl-errors.html for curl 8.21.0.
      8. That no production code uses a named mutex or semaphore, `Path.GetTempPath`,
         `Path.GetTempFileName`, `Directory.CreateTempSubdirectory` or any other
         machine-wide state, so processes with different paths never interfere.
- [x] `Documentation/Planning/Decisions/README.md`'s index lists ADR-0031, and ADR-0007's
      row notes the superseded default.
- [x] `Documentation/Product/Requirements.md` gains FR-022 (no `--directory`: an in-memory
      file system and in-memory service state, empty at start, nothing written to disk,
      exposure defaults unchanged), FR-023 (`--directory <path>`: files at the top of the
      path and service state under `<path>/.surl/`, MQTT retained messages surviving a
      restart), FR-024 (`.surl` is never served, listed or uploaded to) and FR-025 (two
      processes with different paths never interfere; a second process on a path in use
      is refused with ADR-0031's exit code and text), each citing ADR-0031, Status Draft;
      FR-010 lists the refusal's exit code if it is new; and the "A `file://` listen URL"
      out-of-scope row still reads true.
- [x] No HTML comment remains in the ADR, and every statement in it about current code
      names a file that exists.

## Notes

Help text, glossary, README and project `CLAUDE.md` files change in the tasks that change
the behaviour (BL-093) and in the closing docs task (BL-098), not here.

Decided in ADR-0031 (number 0031 was free), written in this session rather than by
`align-and-document` because the work is the decisions themselves; summary:
- Term "data directory", `SurlCommandLine.DataDirectory` (`string?`); the content store
  root stays "served root".
- Missing `--directory` path: 37 as today, never created. `.surl` or `.surl/lock` that
  cannot be created: new `CouldNotWriteFile` = 23 (`CURLE_WRITE_ERROR`), measured
  2026-09-29 with `Record-CurlExchange.ps1 -NoServer` and `--create-dirs -o` into a path
  under a file: curl 8.21.0 exits 23. Consequence recorded: a read-only directory can no
  longer be served with `--directory`.
- In memory: root `C:\surl` / `/surl`, `DefaultMaxTotalBytes` 256 MiB, past it the write
  throws `IOException` (TFTP answers disk full), last-write time at stream dispose.
- `.surl`: first segment, OrdinalIgnoreCase on every platform.
- MQTT file `.surl/mqtt/retained-messages`: header `SURL-MQTT-RETAINED-1\n`, records
  u16 BE topic length + UTF-8 + u32 BE payload length + payload, ordinal order; malformed
  or unreadable refuses start with 37.
- Lock `.surl/lock`, `FileShare.None`, not deleted on exit (deleting races); taken after
  the scheme check and before state load and binding; refusal new `DataDirectoryInUse` =
  124, `surl: (124) Directory <path> is in use by another surl process`.
- Found while measuring: `powershell -File Record-CurlExchange.ps1 ... -CurlArgs @('-o',...)`
  fails because `-o` binds to `-OutDirectory`; call it with `&` in-process instead.

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. ADR-0031 decides the data directory, in-memory mode, .surl rule, MQTT file and lock (exit 23 and 124); FR-022 to FR-025 filed
