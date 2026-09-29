# Surl

<a href="https://stewartscottrogers.github.io/Surl/" target="_blank"><img src="https://github.com/StewartScottRogers/Surl/raw/gource/gource.gif" alt="Gource animation of Surl's commit history across every branch - click to watch in 8K, full screen" width="800"></a>

### [▶ Watch in 8K, full screen](https://stewartscottrogers.github.io/Surl/)

*Every commit on every branch, human and AI, drawn by [Gource](https://gource.io) at
7680 × 4320 and re-rendered after new commits and at least once a day. The viewer plays
the best quality your screen can show - 8K, 4K or HD - with a 4K MP4 and an 8K still to
download. Ctrl-click (⌘-click on a Mac) to open it in its own tab.*

### Code coverage

[![Code coverage: lines and branches covered across every production library - click for the full report](https://github.com/StewartScottRogers/Surl/raw/gource/coverage/badge.svg)](https://stewartscottrogers.github.io/Surl/coverage/)

*Every production library is held to 100% line and branch coverage, cyclomatic complexity
of at most 10 and a CRAP score of at most 30. The [full report](https://stewartscottrogers.github.io/Surl/coverage/)
shows each library against those gates and every member outside one, measured on Windows
and regenerated on the same schedule as the video above.*

### [▦ Live task board](https://stewartscottrogers.github.io/Surl/board/)

*The [live task board](https://stewartscottrogers.github.io/Surl/board/) shows every task
by state and refreshes itself every few minutes. One card per dark factory lane joins it
once the dark factory publishes its lane status ([ADR-0029](Documentation/Planning/Decisions/ADR-0029-the-live-task-board-page-reads-the-task-tree-and-a-board-branch-status-json.md)).*

## What this is

Surl ("Server URL") is the server-side mate of [curl](https://curl.se): for every request
upstream curl can make, protocol for protocol, Surl is the server that answers it. It is
written in C# on .NET 10, publishes as a single native ahead-of-time (AOT) executable named
`surl`, and depends on nothing but the .NET base class library.

Its command line mirrors curl's. Where `curl [options] <url>` names what to fetch,
`surl [options] <url>` names what to listen on: the scheme picks the protocol, the host
and port pick the bind address, and several URLs mean several listeners at once.

```
surl --directory site https://127.0.0.1:8443/ --cert server.pem --key server.key
curl --cacert server.pem https://127.0.0.1:8443/hello.txt
```

Here `site` is a directory holding `hello.txt`, and `server.pem` and `server.key` are a
certificate for `127.0.0.1` and its private key. A secure listen URL needs a certificate:
without `--cert`, surl ends with exit code 58 before it listens, unless `--self-signed`
asks for a throwaway certificate that no client can verify, so curl must skip verification:

```
surl --directory site --self-signed https://127.0.0.1:8443/
curl -k https://127.0.0.1:8443/hello.txt
```

The aim is every scheme upstream curl can request. **Today `surl` answers** `http` and `https` (HTTP/1.1, `GET` and
`HEAD`), `dict`, `gopher` and `gophers`, `mqtt` and `mqtts`, `telnet` and `tftp`, until
Ctrl+C; every other scheme is refused with exit code 1 until its server lands.
`surl --version` lists the schemes a build serves.

What it serves depends on `--directory` (ADR-0031):

- `surl http://127.0.0.1:8080/` serves an empty in-memory store. Everything the services
  keep - uploaded files, MQTT retained messages - lives in memory for as long as the
  process runs, and nothing is written to disk.
- `surl --directory <path> http://127.0.0.1:8080/` serves the files under `<path>` and
  persists what the services keep there across restarts: files at the top of the path,
  every other kind of service state (today MQTT retained messages) under `<path>/.surl/`.
  `.surl` is never served, even with `--serve-dot-files`. One surl process holds a path
  at a time: a second one given the same path is refused with exit code 124.

Uploads still need `--allow-uploads`, in memory too.

## Logging in

Surl is secure by default ([ADR-0032](Documentation/Planning/Decisions/ADR-0032-secure-by-default-authentication-accounts-and-self-signed.md)).
With no account configured, HTTP `GET` and `HEAD` need no login and every login is
refused; once any account is configured, every HTTP request needs one. Every other HTTP
method, and every MQTT `CONNECT`, needs one either way. An account is `-u`/`--user <user:password>` (repeatable), or a line
of the file `--user-file` names - one `user:password` per line, `#` lines skipped - which
keeps the password out of the process list. An empty user name holds a Bearer token.
Here `accounts.txt` holds the line `alice:s3cret`:

```
surl --directory site --user-file accounts.txt http://127.0.0.1:8080/
curl --digest -u alice:s3cret http://127.0.0.1:8080/hello.txt
```

```
surl --directory site --self-signed --user alice:s3cret https://127.0.0.1:8443/
curl -k -u alice:s3cret https://127.0.0.1:8443/hello.txt
```

Surl checks HTTP Basic, Bearer, Digest (MD5, SHA-256 and SHA-512-256), NTLM, Negotiate
carrying NTLM, and AWS Signature Version 4, and an MQTT `CONNECT`'s user name and
password. A password or token sent in clear - Basic or Bearer over `http://`, an MQTT
password over `mqtt://` - is refused without being checked (`403 Forbidden`, `CONNACK` 5).

Four *loosening options* turn a secure default off for a test, and each writes a
`surl: warning:` line on every start: `--allow-anonymous` (accept every request and login
unchecked), `--allow-plaintext-auth` (check passwords sent in clear), `--auth <methods>`
(the methods accepted, `basic,bearer,digest,aws-sigv4` by default; `ntlm` and `negotiate`
only when named) and `--self-signed`. `surl --help testing` says what each loosens and why
none is the default.

## Logging

Surl logs at one of five levels, as curl does
([ADR-0033](Documentation/Planning/Decisions/ADR-0033-console-log-levels-trace-dumps-and-the-log-file.md)):

| Level | Selected by | What it writes |
| --- | --- | --- |
| `none` | `-s` | nothing, not even the `Listening on` lines or failure messages |
| `error` | `-s -S` | the `surl: (N)` failure messages, and a protocol server's failures |
| `info` | the default | the `Listening on` lines, the startup warnings, one line per exchange |
| `verbose` | `-v` | a line for every exchange event: bytes received, bytes sent, notes |
| `trace` | `--trace <file>`, `--trace-ascii <file>` | a dump of every byte to `<file>`, `-` for stdout |

`--log-level <level>` picks a level by name, and the last level option given wins.
`--trace-time` stamps each exchange log line with the time. The log goes to stderr, or is appended
to the file `--log-file <file>` names; failure messages always go to stderr.

```
surl -v --trace-time --directory site http://127.0.0.1:8080/
surl --trace-ascii - --directory site http://127.0.0.1:8080/
surl --log-file surl.log --directory site http://127.0.0.1:8080/
```

`surl --help` lists the common options and the help categories, `surl --help all` every
option, `surl --help <category>` one category (`surl --help logging`), and
`surl --help <option>` one option (`surl --help --user`). `surl --manual` holds the longer
text: a deployment checklist, the data directory, accounts, log levels, limits and exit
codes.

## Upstream curl validates Surl; Surl later validates the Curl port

Surl is measured against **upstream curl** - the original C implementation at
[github.com/curl/curl](https://github.com/curl/curl), release 8.21.0 - and nothing else.
A Surl behaviour is right when a pinned upstream curl build completes the exchange
against it. Each build is pinned by path and SHA-256 in
[`UpstreamCurlBuilds.json`](UpstreamCurlBuilds.json), because a bare `curl` on a command
line can be any number of other programs.

Once Surl stands on its own, it becomes the instrument that measures
[the Curl port](https://github.com/StewartScottRogers/Curl), a C# port of curl: the port
runs the same conversations against Surl beside upstream curl, and wherever the two
disagree, upstream is right. The port is never used to validate Surl, so that check can
never come out circular. See
[ADR-0003](Documentation/Planning/Decisions/ADR-0003-upstream-curl-is-surls-only-oracle.md).

## Built by a dark factory

Surl is built the way the Curl port is: by a *dark factory* - an unattended production
line of Claude Code agents shaped for this one job, not a general-purpose coding bot.

1. **Plan.** The work is decomposed into small tasks on the [task board](Tasks/README.md),
   one Markdown file per task, each with dependencies and checkable acceptance criteria.
2. **Run.** `RunDarkFactory.cmd` takes the next ready task and hands it to a headless
   Claude Code run that is not allowed to ask a question. It repeats until nothing is
   ready or the shift ends.
3. **Specialise.** Each run uses agents shaped for a server mate of curl, in
   `.claude/agents`: a protocol architect and implementer, a test writer, a build fixer, a
   code reviewer, a coverage auditor, and a conformance auditor that points pinned
   upstream curl at Surl and checks every exchange.
4. **Gate.** Nothing lands unless it builds with warnings as errors and holds every
   library to 100% line and branch coverage, cyclomatic complexity of at most 10 and a
   Change Risk Anti-Patterns (CRAP) score of at most 30.
5. **Escalate.** Anything that needs a human decision is moved to `Blocked` with the
   question written down, and the shift ends with an alarm until someone answers it.

The lights stay off; a person sets direction and answers blocked questions.

## Download

No release has been published yet. The first `v*` tag will put native binaries for
Windows, Linux and macOS, on x64 and Arm64, on the [**download page**](DOWNLOAD.md), with
one-line installers:

```sh
curl -fsSL https://raw.githubusercontent.com/StewartScottRogers/Surl/master/install.sh | sh    # Linux, macOS
```

```powershell
irm https://raw.githubusercontent.com/StewartScottRogers/Surl/master/install.ps1 | iex          # Windows
```

## Build and test

Needs the .NET 10 SDK. Builds and tests on Windows, Linux and macOS.

```
dotnet build
dotnet test --filter "TestCategory!=Integration"
```

Run a factory shift (Windows):

```
RunDarkFactory.cmd -Hours 4 -MaxTasks 3
```

## Read more

- [Download and install](DOWNLOAD.md) - every supported platform, installers and checksums (no release yet)
- [Product overview](Documentation/Product/Product-Overview.md) - scope, architecture, phases and the oracle
- [Task board](Tasks/README.md) - what is being worked on, one Markdown file per task
- [Decisions](Documentation/Planning/Decisions/README.md) - every architecture decision record

## Licence

See [LICENSE.txt](LICENSE.txt).
