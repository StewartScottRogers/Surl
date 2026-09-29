# ADR-0031 — The data directory and in-memory mode

- **Status:** Accepted
- **Date:** 2026-09-29
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-09-29.
  Stewart approved the feature on 2026-09-29 (BL-090); the details he left to this ADR.
- **Supersedes:** ADR-0007's default for `--directory` (section 2's option table, `.`, the
  current directory). Everything else in ADR-0007 stands.

## Context

Stewart's approval of 2026-09-29, summarised:

1. `surl --directory <path> <url>...` keeps everything the services store under `<path>`,
   surviving restarts: plain files (content served and uploaded over HTTP, TFTP, Gopher and
   DICT) at the top of the path, and every other kind of service state under
   `<path>/.surl/` (`.surl/mqtt/` for MQTT retained messages now; later `.surl/mail/`, LDAP
   and so on). `.surl` is never served, even with `--serve-dot-files`.
2. `surl <url>...` without `--directory` serves an in-memory file system and keeps service
   state in memory: empty at start, lasting for the process lifetime, touching no disk.
3. Several surl processes run at once without interfering when their paths differ: no
   shared temporary folders, global locks, named mutexes or machine-wide state.
4. A second process given a path another running surl holds is refused through a lock file
   the first holds for its lifetime, with a clear stderr message and an exit code this ADR
   decides.
5. Exposure defaults are unchanged: uploads still need `--allow-uploads`, in memory too.

The code this changes, as it is today:

- `Surl.Cli.UnitLibrary/SurlCommandLine.cs`: `ServedDirectory` is a `string` defaulting to
  `"."`; `Surl.Cli.UnitLibrary/HelpText.cs` says `Directory to serve (default: current directory)`.
- `Surl.Console/CommandLineRunner.cs`: `ServeAsync` probes the directory
  (`Surl.Console/ServedDirectoryProbe.cs`, `CouldNotReadFile` 37 with
  `(37) Could not open directory <path>` on failure), then checks every scheme, then serves.
  `ComposeContentStore` builds a `ContentStore` over `DiskContentFileSystem` at the
  directory's full path; `ComposeProtocolServers` builds
  `new MqttProtocolServer(new MqttRetainedMessages())`.
- `Surl.Content.UnitLibrary/IContentFileSystem.cs` is the seam every served byte passes
  through; `Surl.Content.UnitLibrary/DiskContentFileSystem.cs` is the only class in
  `Surl.Content` that touches the disk. `Surl.Content.UnitLibrary/ContentStore.cs` hides
  dot-files unless `ContentExposureOptions.ServeDotFiles`, and writes an upload to a
  temporary `.surl-upload-<guid>` dot-file beside the target before renaming it into place
  (BL-086). A write that throws is rethrown after the temporary file is deleted.
- Of today's servers only TFTP accepts uploads
  (`Surl.Protocol.Tftp.UnitLibrary/TftpWriteTransfer.cs`, which answers an `IOException`
  with TFTP error 3, "disk full"); the HTTP server answers `PUT` with `405`.
- `Surl.Protocol.Mqtt.UnitLibrary/MqttRetainedMessages.cs` keeps retained messages in a
  dictionary bounded by `MaxTopics` (10000) and `MaxTotalPayloadBytes` (100 MiB)
  (ADR-0014 decision 7); `Retain` is its only mutator. The MQTT library references only
  `Surl.Protocol.Abstractions.UnitLibrary` today; ADR-0002 lets it reference
  `Surl.Content.UnitLibrary`.
- `Surl.Protocol.Abstractions.UnitLibrary/SurlExitCode.cs` holds ADR-0005's table plus
  ADR-0010's rows; ADR-0005 section 3 says a failure with a server-side meaning curl also
  has reuses curl's number, and one with none counts down from 125.

Inputs: ADR-0002, ADR-0005, ADR-0006 (every store a peer can fill is bounded), ADR-0007
(sections 2, 5 and 6, and "Alternatives considered", which rejected *requiring*
`--directory`; this ADR keeps it optional and changes only what its absence means),
ADR-0014, ADR-0015, ADR-0018 (the served root is a full path).

### What upstream curl 8.21.0 returns when it cannot create a directory

- Build: `C:\Program Files\Git\mingw64\bin\curl.exe`, curl 8.21.0 (x86_64-w64-mingw32),
  SHA-256 `0E773709C3A44DB47B88B71351D902027682ED87C3BD3821009E454BACCA8778`
  (`UpstreamCurlBuilds.json`).
- Tool: `Record-CurlExchange.ps1 -NoServer`, 2026-09-29, with
  `--create-dirs -o <tmp>\afile\sub\out.txt file:///<tmp>/src.txt`, where `afile` is a file,
  so the directory `afile\sub` cannot be created.
- Result: exit **23**, stderr `curl: Error creating directory <tmp>\afile\sub` then
  `curl: (23) Failed writing received data to disk/application`. 23 is `CURLE_WRITE_ERROR`
  ("An error occurred when writing received data to a local file, or an error was returned
  to libcurl from a write callback", https://curl.se/libcurl/c/libcurl-errors.html, curl 8.21.0).

## Decision

### 1. The term and the property

The directory `--directory` names is the **data directory**: it now holds service state as
well as served files, so "served directory" would say less than it does. The
`SurlCommandLine` property is `DataDirectory`, of type `string?`, `null` when `--directory`
is absent and the text as given otherwise (still `FailedInit` when empty, ADR-0007
section 2). The content store's root keeps its name, the **served root**: the data
directory's full path with `--directory`, and the in-memory root (decision 4) without it.
The glossary row changes with BL-098.

### 2. The help lines

Same layout as `HelpText.cs` (five spaces, the option padded to 40 columns, the text):

```
     --allow-uploads                         Accept uploads into the served files
     --directory <directory>                 Serve and keep state in <directory> (default: in memory)
```

`--allow-uploads` changes too because "the served directory" no longer names a directory
in every run.

### 3. A missing data directory, and a `.surl` that cannot be created

- A `--directory` path that is missing, not a directory, or unreadable is refused exactly as
  today: `CouldNotReadFile` (37), stderr `surl: (37) Could not open directory <path>`,
  `<path>` as given. It is not created: a mistyped path would otherwise start an empty
  server that looks healthy and writes uploads somewhere nobody meant.
- Once the path opens, surl creates `<path>/.surl` if missing (and each service creates its
  own folder under it). When `<path>/.surl` cannot be created, or `<path>/.surl/lock`
  cannot be created for lack of permission, surl returns a new member
  **`CouldNotWriteFile` = 23**, reusing `CURLE_WRITE_ERROR` (measured above: 23 is what
  curl returns when it cannot create a directory it was told to write into), with stderr
  `surl: (23) Could not create <full path>: <exception message>`, `<full path>` being
  `<path>/.surl` or `<path>/.surl/lock` joined with the platform's separator. The exception
  message goes only to the local operator's stderr, never to a peer (ADR-0006 section 3).
- Consequence, recorded on purpose: a read-only directory can no longer be served with
  `--directory`, because the lock (decision 7) needs a writable `.surl`. Serving read-only
  media means copying it or serving a writable directory above it.

### 4. The in-memory file system

`InMemoryContentFileSystem`, a production `IContentFileSystem` in `Surl.Content.UnitLibrary`
(BL-091):

- **Served root:** the constant `InMemoryContentFileSystem.RootPath`, `C:\surl` on Windows
  and `/surl` on Linux and macOS (chosen by `OperatingSystem.IsWindows()`). It is a full
  path, as ADR-0018 requires of every served root, and not a drive or file-system root, so
  `ContentStore`'s containment arithmetic (root plus separator) is the same as for a disk
  directory. Nothing is created at that path on disk; it is a name. The instance starts
  holding that one empty directory.
- **Bound:** a constructor parameter `maxTotalBytes`, default the constant
  `DefaultMaxTotalBytes` = 268435456 (256 MiB): room for two uploads of ADR-0006's 100 MiB
  default at once, one of them replacing a file. It counts the bytes of every file held,
  temporary upload files included. No command-line option sets it in this work.
- **Past the bound:** the write stream throws `IOException` ("The in-memory file system is
  full.") on the write that would pass it, and nothing past the bound is kept. `ContentStore`
  deletes the temporary file and rethrows, as for any write that throws, and each protocol
  answers as it answers a full disk today (TFTP: error 3, "disk full"). The bound is the
  in-memory equivalent of a full disk, so it is answered as one.
- **Last-write time:** a file's last-write time is `TimeProvider.GetUtcNow()` when its
  write stream is disposed; `MoveFileReplacing` keeps the moved file's time, as a rename on
  disk does. A directory's is the time it was created.
- **Isolation:** each instance holds its own tree; nothing is static.

### 5. The `.surl` folder is never served

`ContentStore` (BL-092) reserves `.surl` as the **first segment** of a request path, after
the path is split and normalised as today, compared with `StringComparison.OrdinalIgnoreCase`
on every platform. Windows and macOS file systems are case-insensitive by default, so
`/.SURL/lock` reaches the same file there; on Linux hiding `.SURL` as well costs nothing
and keeps one rule and one set of tests on all three. Only the whole segment matches:
`/.surl-upload-<guid>` (the store's own temporary name), `/.surlx` and a `.surl` below the
top (`/sub/.surl/x`) are ordinary dot-files, governed by `--serve-dot-files` as today.

Whatever `--serve-dot-files` and `--follow-symlinks` say:

- a read of `/.surl`, `/.surl/` or anything under it is answered exactly as a missing entry;
- a listing of `/` leaves `.surl` out;
- an upload to `/.surl` or anything under it is `NotPermitted`;
- a symbolic link whose final target (after `--follow-symlinks` resolves it) has `.surl` as
  the first segment of its path relative to the served root is answered as missing.

The rule applies in memory too, where no `.surl` exists, so an upload can never create one.

### 6. The MQTT retained-message file

- **Path:** `<path>/.surl/mqtt/retained-messages`, `<path>` being the data directory's full
  path. Without `--directory` there is no file and the store is memory-only, as today.
- **Byte format:** the 21 ASCII bytes `SURL-MQTT-RETAINED-1` followed by LF (0x0A), then one
  record per kept topic in ordinal order of topic, then end of file. A record is the topic's
  UTF-8 byte length as a 2-byte big-endian unsigned integer, the topic's UTF-8 bytes (MQTT's
  own string encoding, so the length limit is the protocol's 65535), the payload's length as
  a 4-byte big-endian unsigned integer, then the payload bytes. An empty store is the header
  alone. Ordinal order makes the bytes a function of the store's contents, so a test can pin
  them.
- **Written:** after every change that alters the store (a retained message kept, replaced,
  or removed by an empty payload for a topic that was held), not after a refused or no-op
  one. Each write replaces the whole file: it is written to
  `<path>/.surl/mqtt/.retained-messages-<guid>` and renamed over the file with
  `IContentFileSystem.MoveFileReplacing`, so a crash leaves either the old file or the new
  one, never a partial one. Writes are serialised, so the file always holds a state the
  store held, and the last change wins. The MQTT library reads and writes only through
  `IContentFileSystem` (ADR-0002: protocol servers never touch the disk themselves).
- **A write that fails** while serving leaves the change in memory, is noted in the exchange
  log (`IExchangeLog.Note`), and does not end the connection or the process; the next change
  rewrites the whole file.
- **Loaded:** once, at start, before any listener binds (BL-095). A missing file is an empty
  store.
- **Unreadable or malformed at start:** surl refuses to start with `CouldNotReadFile` (37)
  and stderr `surl: (37) Could not read <file path>: <reason>`, where `<reason>` is the
  exception message for a file that cannot be read, and `not a retained-message file` for one
  that does not parse: a wrong header, a truncated record, trailing bytes, an empty or
  invalid topic name, a topic that is not valid UTF-8, a repeated topic, an empty payload, or
  more topics or payload bytes than `MaxTopics` and `MaxTotalPayloadBytes` allow. Starting
  empty instead would overwrite the file on the first publish and destroy the state
  silently; 37 is ADR-0005's meaning, a file surl was told to use cannot be read.
  Leftover `.retained-messages-<guid>` files from a crash are ignored.

### 7. The data-directory lock

- **Path:** `<path>/.surl/lock`.
- **How:** a class in `Surl.Console` (BL-096) creates `<path>/.surl` if missing, then opens
  the file with `FileMode.OpenOrCreate`, `FileAccess.ReadWrite`, `FileShare.None`, and holds
  the open stream for the process lifetime. .NET maps `FileShare.None` to a share-mode lock
  on Windows and an advisory `flock` on Linux and macOS; both refuse a second open, from
  another process or the same one, and both are released by the operating system when the
  holder exits or is killed. So a stale file left by a killed process never refuses the next
  start: only a live holder does.
- **On exit:** the stream is disposed and the file is **not** deleted. Deleting it would race
  with a second process that opens it between the close and the delete: on Linux and macOS
  the second process would then hold a lock on an unlinked file while a third locks a new
  one, and both would serve the directory.
- **When:** in `ServeAsync`, in this order: the directory probe (37); the scheme check (1);
  create `.surl` and take the lock (23 or 124); load service state such as MQTT retained
  messages (37); compose and bind listeners. The lock comes before any state is read, so a
  second process never reads a file the first is writing, and before any listener binds, so
  a refused process never answers a peer. No lock is taken without `--directory`, or for
  `--help` or `--version`.
- **Refusal:** an `IOException` opening the lock means another holder. surl returns a new
  member **`DataDirectoryInUse` = 124**, the next number counting down from 125 (ADR-0005
  section 3): upstream curl holds no lock on anything and has no `CURLE_*` meaning a
  resource is in use by another process, and borrowing `BindFailed` (45) would tell a
  script a listen address failed. Stderr:
  `surl: (124) Directory <path> is in use by another surl process`, `<path>` as given.
  An `UnauthorizedAccessException` is decision 3's 23 instead.
- **ADR-0005's table** gains these rows, by its own rules:

| `SurlExitCode` member | Number | Upstream `CURLE_*` reused | Failure it covers |
| --- | --- | --- | --- |
| `CouldNotWriteFile` | 23 | `CURLE_WRITE_ERROR` | The data directory's `.surl` folder or its lock file cannot be created (decision 3). Measured: curl returns 23 when it cannot create a directory it was told to write into. |
| `DataDirectoryInUse` | 124 | none | Another running surl holds the data directory's lock (decision 7). |

### 8. Nothing machine-wide

No production code uses a named `Mutex` or `Semaphore`, `Path.GetTempPath`,
`Path.GetTempFileName`, `Directory.CreateTempSubdirectory`, a fixed well-known path, an
environment variable written by surl, or any other state shared across the machine. Every
file surl writes is under the data directory it was given; without one it writes none.
Processes with different paths, and in-memory processes, therefore never interfere; the
lock is the one deliberate point of contact, and it is scoped to one path. Test code may
use temporary directories.

## Alternatives considered

- **Keep `.` as the default.** Rejected by Stewart's approval: a bare `surl <url>` exposing
  the working directory, and writing state into it, surprises; in memory it touches nothing.
- **Create a missing `--directory`.** Rejected in decision 3: a typo would serve an empty
  directory silently.
- **Start empty on a malformed MQTT file.** Rejected in decision 6: the first publish would
  overwrite it.
- **A named mutex keyed by the path.** Rejected: machine-wide state (decision 8), not
  portable across processes on every platform, and not released cleanly by a killed process
  everywhere. A lock file dies with its holder.
- **Delete the lock file on exit.** Rejected in decision 7: it races.
- **Case-sensitive `.surl` on Linux.** Rejected in decision 5: one rule on every platform.
- **Reuse `BindFailed` (45) or `CouldNotReadFile` (37) for a directory in use.** Rejected:
  neither means another process holds it.

## Consequences

- A breaking change: `surl http://127.0.0.1:0/` no longer serves the current directory.
- `SurlExitCode` gains `CouldNotWriteFile = 23` and `DataDirectoryInUse = 124` (BL-096,
  which touches `Surl.Protocol.Abstractions.UnitLibrary`); FR-010 lists them.
- BL-091 builds decision 4, BL-092 decision 5, BL-093 decisions 1 to 3, BL-094 and BL-095
  decision 6, BL-096 decision 7, BL-097 proves decisions 7 and 8, and BL-098 brings the
  glossary, READMEs and project `CLAUDE.md` files in line.
- `Documentation/Product/Requirements.md` gains FR-022 to FR-025.
- A later service keeps its state under `<path>/.surl/<service>/` by decision 6's pattern:
  one whole-file write through a temporary name, loaded at start, a malformed file refused.
