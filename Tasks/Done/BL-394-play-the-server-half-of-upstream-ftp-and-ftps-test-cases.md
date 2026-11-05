---
id: BL-394
title: Play the server half of upstream ftp and ftps test cases
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-393]
touches: [Conformance/Surl.Conformance.UnitLibrary, Conformance/Surl.Conformance.UnitTests, Protocols/FileTransfer/Surl.Protocol.Ftp.UnitLibrary, Protocols/FileTransfer/Surl.Protocol.Ftp.UnitTests, Documentation/Conformance]
requirement: none
created: 2026-11-04
completed: 2026-11-05
---
# BL-394 — Play the server half of upstream ftp and ftps test cases

## Goal

The upstream test-case run covers cases whose `<server>` is `ftp`, `ftp-ipv6` or `ftps`. Surl plays
their server half as BL-386's ADR defines, and the pass-rate file counts them.

## Context

- Decision: BL-386's ADR, decision 2, for the FTP family. `touches` assumes the code sits in
  `Surl.Conformance` and `Surl.Protocol.Ftp`. Follow the ADR if it says otherwise.
- FILEFORMAT.md (tag `curl-8_21_0`) lists the FTP `<servercmd>` instructions at lines 367 to 395:
  `REPLY`, `REPLYLF`, `COUNT`, `DELAY`, `RETRWEIRDO`, `RETRNOSIZE`, `RETRSIZE`, `NOSAVE`, `SLOWDOWN`,
  `SLOWDOWNDATA`, `PASVBADIP`, `CAPA`, `AUTH` and `STOR`. The `<size>` and `<mdtm>` reply sections
  apply here too.
- How Surl's FTP server answers: ADR-0052, with the data connection through `IDataConnectionOpener`.
  The runner, checker and pass-rate file are BL-392 and BL-393's.

## Acceptance criteria

- [x] Unit tests drive the FTP server half with inline cases for each `<servercmd>` the ADR keeps.
      Each excluded instruction gives its named skip reason.
- [x] The run covers every runnable FTP-family case on Windows. The pass-rate file's rows include
      them, and Notes record the counts.
- [x] Failing cases are grouped by cause into new tasks filed with `task-planner`, with their IDs in
      Notes.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and every touched library keeps
      100% line and branch coverage.

## Notes

- **Where the code went** (ADR-0080 decision 2, as `touches` assumed). `Surl.Protocol.Ftp`:
  `FtpUpstreamTestCaseServer` (public; Surl's own `FtpProtocolServer` through a new internal
  constructor taking the case's replies), `FtpUpstreamTestCaseReplies` (public; parts, servercmd,
  received bytes, uploads), `FtpUpstreamTestCaseServerCommands` (internal parser),
  `ReceivedBytesRecordingConnection` (control connection only, as runtests' `server.input`), and
  `FtpCommandResponder.UpstreamTestCase.cs` (the commands the case answers in place of the content
  store). `Surl.Conformance`: `UpstreamTestCaseServerCommands.Ftp` row for `ftp`, `ftp-ipv6`,
  `ftps`. Tests: `HttpUpstreamTestCaseServerHalves` became `UpstreamTestCaseServerHalves` (adds FTP
  on 127.0.0.1 and [::1], implicit-TLS `ftps`, `SocketDataConnectionOpener`, accept-all
  `AnonymousAuthenticationPolicy`); `UpstreamTestCasesOverHttpTests` became
  `UpstreamTestCasesAgainstSurlTests`, since one run must cover every composed server for the one
  passing list (CI filters by category, so `ci.yml` is unchanged). No contract outside `touches`
  changed and no new ADR: every choice below is implementation detail inside decision 2.
- **Server commands kept** (unit tests: `FtpUpstreamTestCaseServerTests`,
  `FtpUpstreamTestCaseRepliesTests`, `FtpUpstreamTestCaseServerCommandsTests`): `Testnum`,
  `REPLY` (incl. `welcome`, Perl escapes `\r \n \t \xHH \<char>`), `REPLYLF`, `COUNT`,
  `RETRNOSIZE`, `RETRSIZE`, `NOSAVE`, `STOR`. **Excluded**, each refused by the replies with
  `servercmd-unsupported:<word>` and skipped by the selector: `DELAY` (1 runnable case),
  `RETRWEIRDO` (1), `SLOWDOWN` (3), `SLOWDOWNDATA` (2), `PASVBADIP` (1) and the older
  `NODATACONN`/`NODATACONN150`/`421`/`425` (3). `CAPA` and `AUTH` are the mail servers', not FTP's.
- **Choices with a sensible default** (decided by Claude under Stewart's delegation, from
  FILEFORMAT.md at tag `curl-8_21_0` and what the cases verify; recorded here and in the XML docs):
  - Surl's own replies everywhere (`220 surl FTP server ready`, `257 "/" is the current directory`,
    `250 Directory changed`, `150 Opening data connection for N (n bytes)`, `226`, `550 No such file`);
    only `REPLY`'d commands send the case's text, and they are not run (so `REPLY EPSV 500` opens no
    listener), as upstream's server does.
  - No command needs a login first (upstream's server has no login gate; `REPLY welcome 230` and
    `REPLY USER 230` cases skip `PASS`). `PASS` still goes to the policy.
  - `CWD` enters any directory; `DELE` deletes nothing and answers `250 File deleted` (tests 120, 121
    verify a successful `-Q "-DELE"`); `AUTH` stays refused on `ftp` (upstream's `ftp` server has no TLS).
  - `RETR` sends all of `<data>` whatever `REST` asked for: tests 110, 135, 336, 337, 2307 write the
    resumed remainder as `<data>` ("expected to be a file without the first part"). The file's
    number is the first digit run of the `RETR` name; over 10000 picks `<dataNUM>` with NUM its
    remainder. An empty `<data>` without `sendzero="yes"` is no file.
  - `LIST`/`NLST` send `<data>` as the listing whatever the path (FILEFORMAT asks cases to `CWD
    test-N` so upstream knows the case; Surl's half already does).
  - `SIZE`: `<size>`, `-1` missing, else `<data>`'s length; `MDTM`: `<mdtm>` as written, `-1` or
    absent missing.
  - `<protocol>` of a case naming both servers is its first server's bytes then the other's
    (runtests logs both to one `server.input`): fixed 1028, 1055, 1057, 973, 975.
  - `FILE_PWD` gets runtests' leading `/` on Windows (`file://localhost/Z:/...`): fixed 2000, 2001, 390.
  - `%FTPTIME2` is 2 (runtests' `$ftpchecktime * 2`, minimum 1 s); `%HOST6IP`/`%CLIENT6IP` `[::1]`,
    `%CLIENT6IP-NB` `::1`.
- **Counts**, Windows, reference build `C:\Program Files\Git\mingw64\bin\curl.exe` (SHA-256
  0E7737...8778), 2026-11-05, two identical runs of the final code (about 95 s each): of 2,013
  cases **888 ran: 829 pass, 59 fail; 1,125 skip**. The FTP family (`ftp`, `ftp-ipv6`, `ftps`, and
  http+ftp cases): **202 ran, 178 pass, 24 fail**. The HTTP family is unchanged at 686 ran, 651
  pass (its 35 failures are BL-680 to BL-683's). `upstream-test-cases-passing.win-x64.txt` gains
  the 178 (829 lines) and `Upstream-Test-Case-Pass-Rate.md`'s `win-x64` row and skip table say so
  (93.4%). Remaining FTP-family skips: `servercmd-unsupported` above, `server-not-built` for the
  proxy/SOCKS cases, `feature-missing` per the pin.
- **Failures, grouped and filed:** the 24 FTP failures are one cause - cases 1349-1354, 1357-1362,
  1379-1384, 1387-1392 verify upstream ftpserver.pl's ASCII-art greeting in curl's `-D` file
  (`file2`) - filed as **BL-769** (list them as `test-server-choice` exclusions; depends on BL-689).
- **Coverage** (`Measure-CodeQuality -Library ...`): `Surl.Protocol.Ftp.UnitLibrary` 100% line,
  100% branch, 0 failing members (worst CRAP 10). `Surl.Conformance.UnitLibrary` 100% line, 100%
  branch; its 9 members over 10 in Cobertura's complexity predate this task (BL-391/BL-393 list them).
- `dotnet format --verify-no-changes` reports only files this task does not touch
  (`Surl.MessageBus.UnitLibrary` final newlines, `Surl.Mcp.UnitLibrary/PathConfinement.cs` charset).
- FILEFORMAT.md was read with `Invoke-WebRequest` (a document, not a run against Surl).

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. Surl.Protocol.Ftp plays the server half of upstream ftp, ftp-ipv6 and ftps test cases (REPLY, REPLYLF, COUNT, RETRNOSIZE, RETRSIZE, NOSAVE, STOR; others skip by name); Windows run: 202 FTP-family cases ran, 178 pass, 24 banner failures filed as BL-769
