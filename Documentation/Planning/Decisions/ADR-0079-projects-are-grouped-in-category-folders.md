# ADR-0079 — Projects are grouped in category folders

- **Status:** Accepted
- **Date:** 2026-11-04
- **Decided by:** Stewart (asked for the projects to be grouped into category folders and
  approved the categories); the placements below decided by Claude under Stewart's delegation
- **Amends:** the flat layout of `CLAUDE.md`, "Repository layout" (from ADR-0001 and
  ADR-0002), and ADR-0048's note that the `Surl.Cryptography` prefix groups the primitives
  in a flat, ordinal project run

## Context

Until now every project was a directory immediately under the repository root: 80
projects in one alphabetical run, with no solution folders in `Surl.slnx`. With 15
protocol servers, 10 cryptography libraries and the shared libraries beside them, the run
no longer shows which projects belong together. Phase 6 adds HTTP/2, QUIC, TLS 1.3, HPACK
and QPACK libraries, making the run longer still.

Stewart asked for projects that do the same job to be grouped together, as SMTP, IMAP
and POP3 already are by the libraries they share, and for each group to be a subfolder.

## Decision

1. **Every project sits in a category folder; each `.UnitTests` twin sits in the same
   folder as its library.** Project names, assembly names and namespaces do not change;
   only directories move.

   | Folder | Projects |
   | --- | --- |
   | `Application/` | `Surl.Console`, `Surl.Cli` |
   | `Conformance/` | `Surl.Conformance` |
   | `Cryptography/` | `Surl.Cryptography`, its nine hand-built primitives (ADR-0048, ADR-0051, ADR-0061), `Surl.Kerberos`, `Surl.Kerberos.TestKdc` |
   | `Foundation/` | `Surl.Protocol.Abstractions`, `Surl.Core`, `Surl.Networking`, `Surl.Content`, `Surl.Authentication`, `Surl.Cookies`, `Surl.Output` |
   | `Protocols/FileTransfer/` | `Surl.Protocol.Ftp`, `Surl.Protocol.Ssh`, `Surl.Protocol.Tftp`, `Surl.Protocol.Smb` |
   | `Protocols/Http/` | `Surl.Protocol.Http`, `Surl.Protocol.Ws`, `Surl.Protocol.Rtsp`, `Surl.HttpMessage` |
   | `Protocols/Lookup/` | `Surl.Protocol.Dict`, `Surl.Protocol.Gopher` |
   | `Protocols/Mail/` | `Surl.Protocol.Smtp`, `Surl.Protocol.Imap`, `Surl.Protocol.Pop3`, `Surl.MailStore`, `Surl.LineProtocol` |
   | `Protocols/Messaging/` | `Surl.Protocol.Ldap`, `Surl.Protocol.Mqtt` |
   | `Protocols/Terminal/` | `Surl.Protocol.Telnet` |

2. **Categories are by job, not by wire shape.** FTP and DICT talk in CRLF lines with
   numeric replies like SMTP, but they sit with file transfer and lookups; a library
   shared by servers sits with the servers that share it (`Surl.HttpMessage` in `Http`,
   `Surl.MailStore` and `Surl.LineProtocol` in `Mail`).

3. **Where a new project goes.** A protocol server goes in the `Protocols/` category for
   its job. A library shared by servers in one category goes beside them. A hand-built
   primitive goes in `Cryptography/`, and a service any server may use goes in
   `Foundation/`. A new category is decided by an ADR. Phase 6's HTTP/2 and HTTP/3
   libraries (HPACK, QPACK, QUIC, TLS 1.3) are expected in `Protocols/Http/` or
   `Cryptography/`; the HTTP/2 and HTTP/3 ADRs decide which.

4. **`Surl.slnx` mirrors the disk.** Each project is listed inside a solution folder
   named after its category folder (`/Protocols/Mail/`), with folders and projects in
   ordinal order. `Solution Items` and `Scripts` still hold loose files only.

5. **Nothing that checks the rules depends on a project being at the root.**
   `ProtocolIsolationTests` finds each project's csproj wherever it sits and requires
   each protocol server's test project beside it. Task `touches:` paths name a project
   by its category path (`Protocols/Http/Surl.Protocol.Http.UnitLibrary`) so the board's
   overlap check still compares like with like. The `.claude/rules` path globs match at
   any depth.

## Consequences

- Project references, the five conformance tests that read fixtures through
  `RepositoryRoot()`, the file-based apps' `#:project` lines, `release.yml`'s path filter,
  open tasks and every document that names a project path were rewritten in the same
  commit. Finished tasks under `Tasks/Done` keep the paths that were true when they ran.
- Paths grow by one or two segments. Names do not change, so `dotnet test --filter` and
  coverage reports, which are by assembly, are unaffected.
- A lane worktree made before this change rebases onto it. Git follows the renames for
  edited files, but a new file it created under an old root folder must be moved by hand.

## Alternatives considered

- **Solution folders only, disk stays flat.** Nothing breaks and running work is
  undisturbed, but the grouping is visible only in Visual Studio, not in the repository
  an agent reads. Rejected: Stewart asked for subfolders.
- **Group by wire shape** (text lines, HTTP-style heads, binary messages). It shows
  which code could be shared, but splits the file-transfer servers four ways and puts
  FTP beside SMTP. Rejected in favour of grouping by job.
