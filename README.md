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

The aim is every scheme upstream curl can request. **Today `surl` answers** `http` and
`https` (HTTP/1.1, `GET` and `HEAD`), `dict`, `ftp` and `ftps`, `gopher` and `gophers`,
`imap` and `imaps`, `mqtt` and `mqtts`, `pop3` and `pop3s`, `scp` and `sftp`, `smtp` and
`smtps`, `telnet` and `tftp`, until Ctrl+C; every other scheme is refused with exit code 1
until its server lands. `surl --version` lists the schemes a build serves.

What it serves depends on `--directory` (ADR-0031):

- `surl http://127.0.0.1:8080/` serves an empty in-memory store. Everything the services
  keep - uploaded files, MQTT retained messages, mail - lives in memory for as long as the
  process runs, and nothing is written to disk.
- `surl --directory <path> http://127.0.0.1:8080/` serves the files under `<path>` and
  persists what the services keep there across restarts: files at the top of the path,
  every other kind of service state (MQTT retained messages and the mail store) under
  `<path>/.surl/`.
  `.surl` is never served, even with `--serve-dot-files`. One surl process holds a path
  at a time: a second one given the same path is refused with exit code 124.

Uploads still need `--allow-uploads`, in memory too.

## Logging in

Surl is secure by default ([ADR-0032](Documentation/Planning/Decisions/ADR-0032-secure-by-default-authentication-accounts-and-self-signed.md)).
With no account configured, HTTP `GET` and `HEAD` need no login and every login is
refused; once any account is configured, every HTTP request needs one. Every other HTTP
method, every MQTT `CONNECT`, every FTP and SSH login - curl's own anonymous FTP login
included - and every SMTP `MAIL`, IMAP mailbox command and POP3 maildrop command needs one
either way. An account is `-u`/`--user <user:password>` (repeatable), or a line
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
carrying NTLM, and AWS Signature Version 4; an MQTT `CONNECT`'s user name and password; FTP
`USER` and `PASS`; SSH password, keyboard-interactive and public-key logins, a public key
against the OpenSSH `authorized_keys` file `--authorized-keys <user:file>` names; and the mail
servers' SASL mechanisms, IMAP `LOGIN`, POP3 `USER`/`PASS` and `APOP`. A password or token
sent in clear - Basic or Bearer over `http://`, an MQTT password over `mqtt://`, an FTP
password over `ftp://` before `AUTH TLS` - is refused without being checked
(`403 Forbidden`, `CONNACK` 5, FTP `530`); the mail servers do not offer a clear-password login
or mechanism on a connection without TLS, and refuse one sent anyway. An SSH password is never
sent in clear.

Five *loosening options* turn a secure default off for a test, and each writes a
`surl: warning:` line on every start it takes effect in: `--allow-anonymous` (accept every request and login
unchecked), `--allow-plaintext-auth` (check passwords sent in clear), `--auth <methods>`
(the HTTP and mail methods accepted), `--self-signed` and `--throwaway-hostkey`.
`surl --help testing` says what each loosens and why none is the default.

## FTP, FTPS, SCP and SFTP

The FTP server answers `ftp` and `ftps`
([ADR-0052](Documentation/Planning/Decisions/ADR-0052-how-the-ftp-server-answers-and-the-ftp-data-connection-seam.md)),
over passive or active data connections (curl's `-P`) to the client's own address only.
curl's anonymous FTP login is refused unless `--allow-anonymous` is given, which is for tests:

```
surl --directory site --allow-anonymous ftp://127.0.0.1:2121/
curl ftp://127.0.0.1:2121/hello.txt
```

With an account, protect the password with TLS: `AUTH TLS` on `ftp://` (curl's `--ssl-reqd`),
or implicit TLS on `ftps://`. Either needs `--cert` or `--self-signed`. Listings need
`--list-directories`, and uploads and `-Q` commands `--allow-uploads`:

```
surl --directory site --self-signed --user alice:s3cret --allow-uploads ftp://127.0.0.1:2121/
curl -k --ssl-reqd -u alice:s3cret -T hello.txt ftp://127.0.0.1:2121/copy.txt
```

```
surl --directory site --self-signed --user alice:s3cret ftps://127.0.0.1:9990/
curl -k -u alice:s3cret ftps://127.0.0.1:9990/hello.txt
```

The SSH server answers `scp` and `sftp` alike
([ADR-0051](Documentation/Planning/Decisions/ADR-0051-the-ssh-transport-host-keys-and-user-authentication.md),
[ADR-0054](Documentation/Planning/Decisions/ADR-0054-how-the-ssh-server-answers-upstream-curls-scp-and-sftp-requests.md)).
An `scp` or `sftp` listen URL needs a host key: `--hostkey <file>`, or `--throwaway-hostkey`
for a throwaway RSA key made at start, which a client must pin or skip the check for (`-k`):

```
surl --directory site --throwaway-hostkey --user alice:s3cret sftp://127.0.0.1:2222/
curl -k -u alice:s3cret sftp://127.0.0.1:2222/hello.txt
```

With `-v`, surl writes `* Serving SSH host key <key type>, --hostpubsha256 <base64>
--hostpubmd5 <hex>` for each host key, so curl can pin it. Here `host_key` is an RSA key
written by `ssh-keygen -t rsa -b 3072 -N "" -f host_key`; curl's Windows build offers only RSA
host-key algorithms, so an ECDSA or Ed25519 host key alone does not serve it:

```
surl -v --directory site --hostkey host_key --user alice:s3cret scp://127.0.0.1:2222/
curl --hostpubsha256 <base64> -u alice:s3cret scp://127.0.0.1:2222/hello.txt
```

SFTP listings need `--list-directories`; uploads over either scheme, and SFTP `-Q` commands
such as `rm` and `rename`, need `--allow-uploads`. `--allow-weak-ssh-algorithms` also
offers the weak SHA-1, MD5, CBC, RC4, 3DES and 1024-bit Diffie-Hellman algorithms (and RSA keys
shorter than 2048 bits and DSA keys) for peers that support nothing stronger, and warns
`surl: warning: --allow-weak-ssh-algorithms: SHA-1, MD5, CBC, RC4, 3DES and 1024-bit
Diffie-Hellman SSH algorithms are offered`. `--hostcert <file>` serves an OpenSSH host
certificate (`ssh-keygen -s <ca> -h`) for a `--hostkey` key, offered under the key's
`*-cert-v01@openssh.com` algorithms for clients that trust the CA.

## SMTP, IMAP and POP3

The SMTP server answers `smtp` and `smtps`
([ADR-0053](Documentation/Planning/Decisions/ADR-0053-how-the-smtp-server-answers-upstream-curl.md)),
the IMAP server `imap` and `imaps`
([ADR-0055](Documentation/Planning/Decisions/ADR-0055-how-the-imap-server-answers-upstream-curl.md))
and the POP3 server `pop3` and `pop3s`
([ADR-0056](Documentation/Planning/Decisions/ADR-0056-how-the-pop3-server-answers-upstream-curl.md)).
All three share one mail store
([ADR-0050](Documentation/Planning/Decisions/ADR-0050-the-mail-store-and-the-line-machinery-the-mail-servers-share.md)):
SMTP delivers into it, and IMAP and POP3 read it, so give one `surl` all the listen URLs you
need. Here `mail.txt` is a message (`From: a@example.com`, `Subject: hi`, an empty line, then
the body). With `--allow-anonymous`, which is for tests, every recipient's copy goes to one
anonymous `INBOX` and no login is needed:

```
surl --allow-anonymous smtp://127.0.0.1:2525/ imap://127.0.0.1:1143/ pop3://127.0.0.1:1110/
curl --mail-from a@example.com --mail-rcpt b@example.com -T mail.txt smtp://127.0.0.1:2525/example.com
curl "imap://127.0.0.1:1143/INBOX;UID=1"
curl pop3://127.0.0.1:1110/1
```

With accounts, a message is stored in the `INBOX` of each recipient whose local part names an
account; a recipient that names none is answered alike and its copy discarded, so no peer learns
which accounts exist. curl's `-u` then logs in with `CRAM-MD5` by default, which sends no password
in clear ([ADR-0049](Documentation/Planning/Decisions/ADR-0049-the-mail-servers-sasl-and-apop-logins.md)):

```
surl --user alice:s3cret smtp://127.0.0.1:2525/ imap://127.0.0.1:1143/ pop3://127.0.0.1:1110/
curl -u alice:s3cret --mail-from a@example.com --mail-rcpt alice@example.com -T mail.txt smtp://127.0.0.1:2525/example.com
curl -u alice:s3cret imap://127.0.0.1:1143/
curl -u alice:s3cret "imap://127.0.0.1:1143/INBOX?SUBJECT%20hi"
curl -u alice:s3cret pop3://127.0.0.1:1110/1
```

The first IMAP command lists the mailboxes and the second searches `INBOX`; `curl -T <file>
imap://.../INBOX` appends a message, and `-X` sends any other command (`-X 'STORE 1 +FLAGS
\Deleted'`, `-X EXPUNGE`, `-X UIDL` or `-X 'DELE 1'` over POP3). With `--cert` or
`--self-signed`, `smtp://` and `imap://` offer `STARTTLS` and `pop3://` `STLS` (curl's
`--ssl-reqd`), and `smtps`, `imaps` and `pop3s` are TLS from the first byte; over TLS `PLAIN`,
`LOGIN`, `XOAUTH2`, `OAUTHBEARER`, IMAP `LOGIN` and POP3 `USER`/`PASS` are offered too:

```
surl --self-signed --user alice:s3cret smtp://127.0.0.1:2525/ imaps://127.0.0.1:9930/ pop3s://127.0.0.1:9950/
curl -k --ssl-reqd -u alice:s3cret --mail-from a@example.com --mail-rcpt alice@example.com -T mail.txt smtp://127.0.0.1:2525/example.com
curl -k -u alice:s3cret "imaps://127.0.0.1:9930/INBOX;UID=1"
curl -k -u alice:s3cret --login-options AUTH=PLAIN pop3s://127.0.0.1:9950/1
```

`--auth` chooses the mechanisms; `digest-md5`, `ntlm`, `apop` and `gssapi` are off until named,
and `gssapi` needs `--keytab`. Without `--directory` the mail lives in memory; with it, the mail
store is kept in `<path>/.surl/mail` and survives a restart. `--allow-uploads` gates neither SMTP
delivery nor IMAP `APPEND`: mail is not a served file.

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

## Help for AI agents

An AI agent learning to call surl should read `surl --aihelp` first: Markdown on stdout,
exit 0, generated from the same option table and help categories as `surl --help`, so the
two cannot drift. It has no upstream curl equivalent
([ADR-0046](Documentation/Planning/Decisions/ADR-0046-surl-aihelp-markdown-help-for-ai-agents.md)).

```
surl --aihelp
surl --aihelp listen-urls
surl --aihelp http
surl --aihelp exit-codes
surl --aihelp all
```

The first writes the overview - what surl is, how a command line is built, and the topic
list. `surl --aihelp <topic>` writes one topic: every help category is a topic, plus
`exit-codes` and `listen-urls`. Each topic page has the same five sections - About,
Schemes, Options (argument type, default, allowed values, whether it loosens security),
Exit codes (with what to do next) and Examples (exact command lines and what surl prints).
`surl --aihelp all` writes the overview and every topic in one document; an unknown topic
gets the topic list.

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
