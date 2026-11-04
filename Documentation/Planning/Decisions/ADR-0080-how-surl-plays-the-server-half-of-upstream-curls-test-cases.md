# ADR-0080 — How Surl plays the server half of upstream curl's test cases

- **Status:** Accepted
- **Date:** 2026-11-04
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-11-04,
  in BL-386 (FR-053, success criterion 2).
- **Amends:** nothing. [ADR-0003](ADR-0003-upstream-curl-is-surls-only-oracle.md) (only pinned
  builds run, never the Curl port), [ADR-0002](ADR-0002-mirror-the-curl-ports-project-map.md)
  decision 3 (`Surl.Conformance` references Abstractions only; protocol servers never reference each
  other), [ADR-0010](ADR-0010-the-server-side-tls-contract.md) (the TLS contract, client certificates
  included) and [ADR-0026](ADR-0026-rtsp-conformance-on-macos-where-the-pinned-build-has-no-rtsp.md)
  (macOS's reference build has no `rtsp`) are applied as written. No contract in
  `Surl.Protocol.Abstractions` changes.

## Context

Success criterion 2 of the [product overview](../../Product/Product-Overview.md): "Surl plays the
server half of upstream curl's test cases, with a pinned upstream build as the client, and each
case's verify section passes. The pass rate is stated per release and only rises." Its
"Constraints" add that upstream test data copied in keeps curl's notice beside it, and that Surl is
written from specifications, curl's documentation and measurement, not by translating upstream's
test servers. BL-387 to BL-397 build what this ADR decides.

**The corpus, measured** (tag `curl-8_21_0`, commit `68720b4837284335b2d63cb358f8f6ce65f5bc55`,
checked out with `git` on 2026-11-04 and counted with PowerShell): `tests/data` holds 2,041 files,
2,498,651 bytes - 2,013 test cases (`test<N>`), `DISABLED`, `Makefile.am`, `.gitignore` and 25
data files that cases name (`data1400.c`, `data1705-1.md`, `data320.html` and the like). The
format is `docs/tests/FILEFORMAT.md` at the tag (823 lines, read 2026-11-04). Classifying each case
by the first rule below that applies to it:

| Class | Cases |
| --- | --- |
| Listed in `tests/data/DISABLED` | 19 |
| A unit test (`<tool>` `unit...`, or `<features>` `unittest`) | 82 |
| A libtest program (`<tool>` `lib%TESTNUMBER`, `lib<N>`, `tool%TESTNUMBER`) | 352 |
| Needs a debug build (`<features>` `Debug` or `TrackMemory`) | 50 |
| `<command type="perl">` (no case uses `type="shell"`) | 27 |
| Has a `<precheck>` | 19 |
| Has a `<postcheck>` | 14 |
| Names no server, or only `file` | 87 |
| Names a server Surl has not built (`http-proxy` 24, `socks5` 17, `https-proxy` 5, `socks4` 5, `httptls+srp` 4, `http/2` 4, `dns` 3, `socks5unix` 3, `http/3` 2, `http-unix` 2) | 67 |
| **Left: runnable against the servers Surl has today, before features** | **1,296** |

The 1,296 by first server: `http` 771, `ftp` 191, `smtp` 81, `imap` 60, `pop3` 47, `https` 34,
`sftp` 24, `mqtt` 20, `tftp` 16, `scp` 14, `http-ipv6` 9, `ftps` 8, `ftp-ipv6` 6, `gopher` 3,
`https-mtls` 2, `smb` 2, and one each of `dict`, `telnet`, `gophers`, `gopher-ipv6`, `imaps`,
`pop3s`, `smtps` and `mqtts`. Each pinned build's features then remove more (decision 3).

`<strippart>` and `<stripfile>` hold 322 Perl lines across the corpus: 194 `s` substitutions (190
delimited by `/`, 4 by `:`, flag `g` the only flag), 99 `$_ = '' if /regex/` lines, 4
`$_ = '' if(...)` lines combining `$_ =~ /regex/` and `$_ !~ /regex/` with `and` and `||`, one
`if(s/...//) { ... map ... sort split ... }` (test 1286's Digest field sort) and comments. 56 of
the 1,296 runnable cases use `<strippart>`.

**What a `<reply>` is.** Upstream's test servers do not compute replies; they send the case's
canned bytes. For HTTP and RTSP, `<data>` is the whole response, status line and headers included,
sent verbatim, `<dataNUM>` chosen by the request path's number and by the request's authorisation
scheme (Digest adds 1000, NTLM type 1 adds 1001, type 3 adds 1002, Basic over 1000 adds 1,
Negotiate counts up), `swsbounce` moving to the next part, `<connect>` answering `CONNECT`.
For FTP, `<data>` is the transfer's payload (a listing only after `CWD test-<N>`), `<size>` and
`<mdtm>` answer `SIZE` and `MDTM`, and the command replies are the server's own unless a
`<servercmd>` `REPLY` replaces them. Most of what a case verifies (`<protocol>`, `<stdout>`,
`<errorcode>`) is therefore about the client: an upstream test case tests curl, with a server
following a script. Surl cannot be judged on bytes the script decides, so the question is which
bytes Surl decides.

**How runtests changes each command** (`tests/runner.pm` at the tag): `-q` first unless
`option="no-q"` (line 975), `--output` into the log directory unless the case verifies stdout or
says `no-output` (lines 829 to 832), `--include` unless `no-include` (line 868), and
`--trace-ascii` into the log directory (line 877).

**Certificates.** `tests/certs` at the tag holds no certificate: it holds `.prm` descriptions
(`test-ca.prm`, `test-localhost.prm`, `test-client-cert.prm` and others), `test-ca.cnf` and
`genserv.pl`, which runtests runs (Perl and `openssl`) to generate keys and certificates. 41
references to `%CERTDIR/certs/...` name the generated files (`test-ca.crt` 25 times).

## Decision

### 1. Where the data lives, how it is fetched, its notice and its bytes

- **Where:** `Conformance/Surl.Conformance.UnitTests/UpstreamTestCases/`:
  - `data/` - every file of `tests/data` at the tag but `.gitignore` (it would change what git
    ignores in Surl's tree) and `Makefile.am` (upstream's build glue): the 2,013 cases, `DISABLED`
    and the 25 data files, byte for byte, under their own names;
  - `certs/` - the `.prm` files and `test-ca.cnf` of `tests/certs`, which decision 2's certificate
    generation reads;
  - `COPYING` - upstream curl's `COPYING` from the same commit, verbatim: the notice the files
    carry;
  - `README.md` - the tag, the full commit, the import date, what was left out and why, and how to
    re-import;
  - `SHA256SUMS` - one line per imported file, its SHA-256 and path, written by the importer.
  The test project copies the folder into its output (`CopyToOutputDirectory`), so the tests read
  it beside the test assembly on every platform. It is test data of the test project, not a
  resource of `Surl.Conformance.UnitLibrary`: the library parses whatever cases it is given.
- **How it is fetched:** `Conformance/Surl.Conformance.UnitTests/UpstreamTestCases/Import-UpstreamTestCases.ps1`
  clones `https://github.com/curl/curl` at tag `curl-8_21_0` with `git` (depth 1, sparse:
  `tests/data`, `tests/certs`, `COPYING`) and `-c core.autocrlf=false -c core.eol=lf`, so the
  checkout is the committed bytes; refuses to copy unless `git rev-parse HEAD` is
  `68720b4837284335b2d63cb358f8f6ce65f5bc55`; then replaces the folder's contents and rewrites
  `SHA256SUMS`. It uses no curl at all, so ADR-0003's pinning does not arise: this is source text,
  not a build. Importing another tag is a new ADR, as pinning another build is.
- **Line endings:** `.gitattributes` gains
  `Conformance/Surl.Conformance.UnitTests/UpstreamTestCases/data/** -text` and the same for `certs/**`, so git
  never converts them (FILEFORMAT's `crlf=` attributes, `%CR` and the binary bytes some cases carry
  need the exact bytes). A fast test in `Surl.Conformance.UnitTests` hashes every imported file
  and compares it with `SHA256SUMS`, so a normalisation anywhere - git, an editor, `dotnet format`
  - fails on every platform.
- **Licence:** curl's licence is MIT-style and asks that its notice be kept with copies; `COPYING`
  beside the data is that. Surl's `LICENSE.txt` does not change.

### 2. What "Surl plays the server half" means, server by server, and where the code lives

**The meaning.** The server half of a case is **Surl's own protocol server for the case's
scheme**, on loopback, its real code reading every byte curl sends - request heads and bodies,
commands, packets, TLS handshakes - and keeping or closing connections as it does for any client.
The case's `<reply>` supplies only what FILEFORMAT says the upstream server takes from the case
rather than decides itself, and Surl sends it the way FILEFORMAT documents for that protocol:

- **Verbatim-reply protocols** (`http`, `http-ipv6`, `https`, `https-mtls`, `rtsp`, `rtsp-ipv6`):
  the case's part is the whole response, so Surl writes it unchanged after reading the request with
  its real reader. What the pass rate measures here is Surl's request reading, body framing
  (`Content-Length`, chunked, `Expect: 100-continue`), connection persistence and TLS - not its
  response generation, which success criterion 1's corpus (`Surl.Conformance.UnitTests`'
  `UpstreamCurl...Tests`) already measures.
- **Payload protocols** (`ftp`, `ftp-ipv6`, `ftps`, `imap`, `imaps`, `pop3`, `pop3s`, `smtp`,
  `smtps`, `dict`, `gopher`, `gopher-ipv6`, `gophers`, `telnet`, `mqtt`, `mqtts`, `tftp`): the
  case's part is the content of a transfer - a file, a listing, a message, a definition, a
  published payload - and Surl's server sends it through its own protocol code with its own replies.
  `<size>`, `<mdtm>` and the `<servercmd>` instructions FILEFORMAT lists for these servers
  (`REPLY`, `REPLYLF`, `COUNT`, `DELAY`, `CAPA`, `AUTH`, `RETRNOSIZE`, `RETRSIZE`, `RETRWEIRDO`,
  `PASVBADIP`, `NOSAVE`, `SLOWDOWN`, `SLOWDOWNDATA`, `STOR`, TFTP's `writedelay`) change the named
  reply or timing only. Logins succeed for any user and password unless a `<servercmd>` refuses
  them, through a test-only accept-all authentication policy composed by the test project - a case
  tests curl's login, not Surl's accounts, which ADR-0032's tests prove.
- **File-system protocols** (`scp`, `sftp`, `smb`): no `<reply>`; Surl's server serves the case's
  directory (`%PWD`, `%SFTP_PWD`, the files `<file>` creates) with its own code. `Surl.Conformance`
  generates the SSH client and host keys a case's `%LOGDIR/server/...` paths name and computes
  `%SSHSRVMD5` and `%SSHSRVSHA256` from Surl's host key.

Where a server's own reply differs from the one the upstream test server chose, and the case pins
that byte, the case fails; decision 5 says what triage may do with it. Each `<servercmd>`,
`<postcmd>` or reply attribute a server half does not implement is a named skip
(`servercmd-unsupported:<command>`), never a silent pass.

**Server by server** (FILEFORMAT's 32 names, plus `dns`, `http/3`, `imaps`, `pop3s`, `smtps` and
`mqtts`, which cases name although the list does not):

| `<server>` | Server half | Mode |
| --- | --- | --- |
| `http`, `http-ipv6` | `Surl.Protocol.Http` on `127.0.0.1` or `[::1]` | verbatim reply |
| `https` | `Surl.Protocol.Http` over Surl's TLS with the generated `test-localhost` certificate | verbatim reply |
| `https-mtls` | as `https`, requiring a client certificate issued by the generated `test-ca` (ADR-0010) | verbatim reply |
| `rtsp`, `rtsp-ipv6` | `Surl.Protocol.Rtsp`, `rtp:` server commands included | verbatim reply |
| `ftp`, `ftp-ipv6`, `ftps` | `Surl.Protocol.Ftp`; `ftps` is implicit TLS, as upstream's stunnel-wrapped server | payload |
| `imap`, `pop3`, `smtp` and their `s` forms | `Surl.Protocol.Imap`, `Surl.Protocol.Pop3`, `Surl.Protocol.Smtp`; SMTP records the message for `<upload>` | payload |
| `dict`, `gopher`, `gopher-ipv6`, `gophers`, `telnet`, `mqtt`, `mqtts`, `tftp` | their `Surl.Protocol.<Name>` server | payload |
| `scp`, `sftp`, `smb` | `Surl.Protocol.Ssh`, `Surl.Protocol.Smb` | file system |
| `http/2`, `http/3` | Phase 6's HTTP/2 and HTTP/3 servers, once built | verbatim reply |
| `http-proxy`, `https-proxy`, `socks4`, `socks5`, `socks5unix` | the proxies the overview's "Also in scope" names; not built | `server-not-built` |
| `http-unix` | HTTP over a Unix domain socket listener; not built | `server-not-built` |
| `httptls+srp`, `httptls+srp-ipv6` | TLS-SRP, which `SslStream` cannot offer, so hand-built; not built | `server-not-built` |
| `dns` | the test DNS server `<dns>` sections talk to; not built | `server-not-built` |
| `file`, or no server | curl reads the file system; there is no server half | `no-server-half` |

One table in `Surl.Conformance.UnitLibrary` maps each name to its port variable and whether its
server half exists, so building a server is one row's change. BL-399 plans the four unbuilt rows'
servers; this ADR does not decide them.

**Where the code lives.**

- **`Surl.Conformance.UnitLibrary`** (Abstractions only, ADR-0002): parsing a case into an
  `UpstreamTestCase` (planning name) with FILEFORMAT's preprocessing (`%b64[]`, `%hex[]`,
  `%repeat[]`, `%include`, `%if`/`%else`/`%endif`, `crlf=`, `nonewline=`) and variable
  substitution; the server-name table; selection with skip reasons (decision 3); building the
  command line as runtests does; generating the test certificates from the `.prm` files with the
  BCL's `CertificateRequest`, and the SSH keys; the verify checker with the strip interpreter
  (decision 4); the result lines and the pass-rate file's reading (decision 5). It hands a case's
  reply to a server half as BCL types only - its parts by name as bytes and its `<servercmd>`
  lines as strings - so it needs no protocol library.
- **Each protocol library** gains the optional seam that lets its server take a case's reply
  (planning name `<Protocol>UpstreamTestCaseReplies`), interpreting FILEFORMAT's rules for its
  own protocol - `<dataNUM>` selection and `swsclose` in `Surl.Protocol.Http`, `REPLY` in
  `Surl.Protocol.Ftp` - and recording the bytes it received in arrival order for `<protocol>` and
  `<upload>`. The protocol knows how to read its own requests, so the rules for answering them
  belong beside that code, held to the same quality gates. Shared line-protocol instructions go in
  `Surl.LineProtocol` (BL-395's `touches` already names it). No protocol library references another
  or `Surl.Conformance`.
- **`Surl.Conformance.UnitTests`** composes each server half as `Surl.Console` composes a server
  - the protocol library's server on `Surl.Networking`'s loopback listener - with the seam and the
  accept-all policy injected, and runs the pinned build. **`Surl.Console` never composes the seam:**
  the shipped `surl` has no replay mode and no option for one, and native AOT trims the unused code.

### 3. Which cases are out of scope, each class with its skip reason

A case is skipped with the first reason that applies, in this order; each reason is a fixed
string in the result lines and the pass-rate file, and a skip is never counted as a pass:

| Reason | Applies when | Why it is out |
| --- | --- | --- |
| `disabled-upstream` | the case is listed in `data/DISABLED` | upstream does not run it either |
| `unittest` | `<tool>` names a `unit` program, or `<features>` has `unittest` | it tests libcurl's internals with no server |
| `libtest` | `<tool>` names any other program | it needs a program built against libcurl's test hooks; the pinned builds are the `curl` tool (two libraries pinned for one driver each, ADR-0071 decision 10, are not libtest) |
| `debug-build` | `<features>` has `Debug` or `TrackMemory` | the pinned builds are release builds, as is every build curl's users run |
| `perl-command` | `<command type="perl">` (and `type="shell"`, which no case uses at the tag) | no Perl and no shell scripts run (root `CLAUDE.md`, BCL only) |
| `needs-perl` | the command or a file names `%PERL`, a `.pl` script or `%SRCDIR/..` outside the imported data | same, and the imported data is `tests/data` and `tests/certs` only |
| `precheck` | the case has a `<precheck>` | a precheck is a program or Perl script outside the imported data |
| `postcheck` | the case has a `<postcheck>` | same |
| `no-server-half` | no `<server>`, or only `file` | there is nothing for Surl to answer |
| `server-not-built:<name>` | a named server's row says not built | it counts again the day the row flips |
| `protocol-missing:<scheme>` | the scheme is not in the pinned build's `protocols` line | the build cannot speak it (macOS and `rtsp`, ADR-0026) |
| `feature-missing:<name>` | a required feature is absent, or a `!feature` present | runtests skips it too |
| `servercmd-unsupported:<command>` | decision 2's server half does not implement a server command or reply attribute the case uses | not a silent pass |
| `strippart-unsupported` | a strip line falls outside decision 4's subset | not a silent pass |
| `test-server-choice` | decision 5's exclusion list names the case | the verified byte is the upstream test server's arbitrary choice |

**Features.** Only each platform's **reference** build runs the suite: the pass rate is the
reference release's, and a supplementary build (ADR-0017) never stands in for it. A pin's
features are its `features` line, its `protocols` line (each scheme a feature, as runtests makes
them), its TLS backend from its `version` line (`OpenSSL`, `Schannel`), `win32` on Windows, and
the build-option features runtests reads from a debug build's `curlinfo` (`proxy`, `cookies`,
`Mime`, `digest`, `crypto`, `form-api`, `headers-api`, `verbose-strings`, `--libcurl`, `manual`,
`aws`, `ipfs`, `local-http`, `codeset-utf8` and the like): `Surl.Conformance` holds one table of
those a default release build of 8.21.0 enables, and BL-389 measures each against each reference
pin with a probe command before relying on it, recording the probes in its Notes. `Debug`,
`TrackMemory` and `unittest` are never present.

### 4. `<strip>`, `<strippart>` and `<stripfile>` without Perl

`Surl.Conformance` interprets the subset of Perl the corpus uses, with .NET's `Regex` over
Latin-1-decoded lines (so every byte is one character and `\d`, `\w` and `\s` match only ASCII),
`RegexOptions.CultureInvariant`:

- `<strip>`: each line is a regular expression; a protocol line matching it is removed.
- `s<d>pattern<d>replacement<d>flags`, any delimiter `<d>`, flags `g` and `i` (anything else is
  unsupported); the replacement is a Perl double-quoted string with `$1` to `$9` and `${n}` for
  groups and `\r`, `\n`, `\t`, `\\` and `\<d>` for escapes, anything else unsupported.
- `$_ = '' if <condition>;` (removes the line), where a condition is `/regex/` (matched against the
  line), `$_ =~ /regex/`, `$_ !~ /regex/`, parentheses, `and`, `or`, `&&`, `||` and `not`/`!`.
- Blank lines and lines starting with `#` are ignored.

A line outside the subset - test 1286's `map`/`sort`/`split` is the one at the tag - makes the case
skip `strippart-unsupported`, naming the line. Translating such a line into C# for one case is not
done: the subset grows only when a new form appears in several cases, by amending this decision.

### 5. The pass rate: where it lives, per platform and per release, and the guard

- **Per-run results.** Each run writes `upstream-test-case-results.<platform>.txt` (`<platform>`
  is the pin's: `win-x64`, `linux-x64`, `osx-arm64`) to the test results directory, one line per
  case in test-number order: `test<N> pass`, `test<N> fail <first differing section>` or
  `test<N> skip <reason>`. CI uploads it as an artifact.
- **The stated rate.** `Documentation/Conformance/Upstream-Test-Case-Pass-Rate.md` holds:
  - a **current** table, one row per platform: the commit, the reference pin's SHA-256, total
    (2,013), runnable, passed, failed, skipped by reason, and `test-server-choice` exclusions
    counted on their own so they cannot hide among the skips; the pass rate is passed over
    runnable;
  - a **releases** table, one row per release and platform, added in the commit that is tagged;
  - beside it, `Documentation/Conformance/upstream-test-cases-passing.<platform>.txt`, the
    numbers of the cases that pass, one per line - the list the guard reads.
  A platform not yet run says "not run" rather than a number (Windows today: the reference build
  is missing on Stewart's machine since 2026-10-05).
- **How a release states it.** Before Stewart tags `v<version>`, the release commit adds the
  releases rows for that version. `.github/workflows/release.yml` copies them into the draft
  release's notes and fails when the file has no row for the tag's version.
- **The guard ("only rises").**
  - The suite's integration test fails when a case listed in its platform's passing list does not
    pass, naming each such case and its first differing section. A case that passes but is not
    listed is written to the test output with the line to add, so the list rises by a copy; it does
    not fail the run.
  - A fast test in `Surl.Conformance.UnitTests` checks that the current table's passed counts equal
    the passing lists' line counts, and that each platform's passed count never falls from one
    releases row to the next.
  - Removing a number from a passing list is how a regression would be hidden, so it is allowed
    only together with a `test-server-choice` entry for that case.
- **`test-server-choice` exclusions.** A failing case whose verified byte the upstream test server
  chose arbitrarily - not the protocol's specification, not what curl needs (a banner's wording, a
  fixed `MDTM` time) - may be listed in `Documentation/Conformance/Upstream-Test-Case-Exclusions.md`,
  one row per case naming the byte, upstream's choice, Surl's answer and why a correct server need
  not send upstream's. Anything else that fails is a Surl defect until measurement shows otherwise
  (success criterion 1) and becomes a task.

### 6. Where the suite runs

- **CI, Linux and macOS.** A job `Upstream test cases` in `.github/workflows/ci.yml`, a matrix of
  `ubuntu-latest` and `macos-latest` with each leg's reference build fetched and verified as the
  conformance step does today, runs the suite's tests (`TestCategory` `Integration` and
  `UpstreamTestCases`); the existing conformance step excludes `UpstreamTestCases`, so its time is
  unchanged. Being part of `CI`, a red suite blocks the dark factory's merge to `master`, which is
  what "only rises" needs.
- **Time budget.** 20 minutes per leg for the suite, inside a 30-minute job timeout. Cases run in
  parallel, each with its own server halves on ephemeral ports and its own `%LOGDIR`, up to the
  processor count; each case is ended after its `<command timeout=>` or 60 seconds and recorded
  `fail timeout`. When a leg outgrows 20 minutes, the job shards by server name across matrix
  entries; that is a task, not a new ADR.
- **Windows.** CI's Windows leg has no pinned build (the runner's Git for Windows is not the pinned
  file), so the Windows row comes from a local run where the reference build is installed, its date
  in the row.
- **Lanes.** A lane runs the fast tests only, as today. A task that works on the suite runs its
  own server's cases locally (`--filter` on the suite's class and a server name) and leaves the
  full run to CI.

## Alternatives considered

- **A replay engine in `Surl.Conformance`** that sends each case's bytes without Surl's servers.
  Rejected: the pass rate would measure the engine, and every case would pass the day it was
  written. The criterion is about Surl.
- **Surl's servers answering with their own responses, the case's reply only as content.** For
  HTTP and RTSP every `<stdout>` carries the case's status line and headers (`Server:`, `Date:`),
  so none of the 814 runnable HTTP cases could pass, and the rate would say nothing. Kept for the
  payload protocols, where FILEFORMAT itself makes the reply content.
- **A `surl` option that loads a case** (a scripted-reply mode in the shipped executable).
  Rejected: it puts a test harness into the product and onto its command line, with `--aihelp`
  facts and an attack surface, for no user of curl; the test project composes the seam instead.
- **One new library holding every server half**, referencing the protocol libraries. Rejected: the
  rules for answering a protocol's requests need that protocol's internals, which would have to be
  made public for it, and the planned tasks' `touches` already put each in its protocol library.
- **Running upstream's own Perl and Python servers or runtests.** Rejected: no Perl or Python runs
  here, and the point is to test Surl.
- **Fetching the data at test time instead of committing it.** Rejected: tests would need the
  network and a moving source; committed bytes with `SHA256SUMS` are reproducible offline.
- **Translating the one complex `<strippart>` (test 1286) into C#.** Rejected: a per-case shim is
  a second oracle; the case skips with its reason.

## Consequences

- BL-387 imports the data and writes the importer, `SHA256SUMS`, the `.gitattributes` lines and
  the hash test; BL-388 parses; BL-389 selects, with decision 3's reasons and the feature probes;
  BL-390 checks verify sections with decision 4's interpreter; BL-391, BL-394 to BL-397 build the
  server halves of decision 2 in each protocol library; BL-392 runs them and adds the CI job of
  decision 6; BL-393 writes decision 5's files, guard and `release.yml` step.
- `Surl.Conformance.UnitLibrary/CLAUDE.md` names what the library holds; the glossary defines
  "upstream test case", "server half", "pass rate" and "skip reason"; FR-053 states the
  requirement.
- BL-399 plans the servers no task builds yet: the HTTP and HTTPS proxies, SOCKS4 and SOCKS5
  (and over a Unix socket), HTTP over a Unix socket, TLS-SRP and the test DNS server - 67 cases at
  the tag wait on them, besides `http/2` and `http/3`, which Phase 6's tasks build.
- The pass rate starts with empty passing lists and rises as each server half lands; nothing
  blocks a merge until a case is first listed.
