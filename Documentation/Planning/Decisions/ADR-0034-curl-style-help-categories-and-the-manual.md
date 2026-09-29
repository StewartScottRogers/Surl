# ADR-0034 — Curl-style help: `--help` and its categories, `--help <option>`, and `--manual`

- **Status:** Accepted
- **Date:** 2026-09-29
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-09-29,
  in BL-102. Stewart approved the feature on 2026-09-29 - help modelled on upstream curl
  8.21.0's, measured before any text is pinned; the details he left to this ADR.
- **Supersedes in part:** [ADR-0007](ADR-0007-the-phase-1-command-line-surface.md) section 6's
  `--help` text and its paragraph on order and column, section 1's "`-h` takes no argument in
  surl" and its rule that `-h`/`--help` ends reading where it stands, and section 2's `-h` row
  ("Print the help (section 6)"); the help lines of
  [ADR-0031](ADR-0031-the-data-directory-and-in-memory-mode.md) decision 2; and the `--help`
  descriptions of [ADR-0032](ADR-0032-secure-by-default-authentication-accounts-and-self-signed.md)
  section 1. Everything else in those ADRs stands.

## Context

Stewart's approval of 2026-09-29, summarised: `--help` is a short list plus a pointer to
categories; `--help all` lists every option; `--help <category>` with the categories `auth`,
`testing`, `security`, `tls`, `logging`, `content`, `limits` and one per protocol (e.g.
`--help mqtt`); `--help testing` explains every loosening option and why none is the
default; `--manual` holds the longer text (deployment checklist, data directory, in-memory
mode). Measure the pinned build first.

Where the code is on 2026-09-29:

- `Surl.Cli.UnitLibrary/HelpText.cs` holds one static `Lines` array and `Text`: a
  `Usage: surl [options...] <url>...` line and 28 option lines, alphabetical by long name,
  every description in column 46 (ADR-0007 section 6 with ADR-0010's `--cert-type`,
  `--key-type` and `--pass` rows and ADR-0031 decision 2's lines).
- `Surl.Cli.UnitLibrary/CommandLineOptions.cs` is the option table; `help` is
  `CommandLineOptionKind.Help` and takes no argument. `Surl.Cli.UnitLibrary/CommandLineParser.cs`
  returns `CommandLineParseResult.ShowHelp`, a static property with no subject, the moment it
  reads `-h` or `--help`. `Surl.Cli.UnitLibrary/CommandLineOutcome.cs` has `Serve`,
  `ShowHelp`, `ShowVersion` and `Refused`.
- `Surl.Console/CommandLineRunner.cs` writes `HelpText.Text` to stdout for `ShowHelp`.
- `Surl.Console/CommandLineRunner.cs` composes the servers that claim `http`, `https`,
  `dict`, `gopher`, `gophers`, `mqtt`, `mqtts`, `telnet` and `tftp`.
- ADR-0032 (BL-100) adds `-u`/`--user`, `--user-file`, `--allow-anonymous`,
  `--allow-plaintext-auth`, `--auth` and `--self-signed`; ADR-0033 (BL-101) adds
  `-s`/`--silent`, `-S`/`--show-error`, `--log-level`, `--trace`, `--trace-ascii`,
  `--trace-time` and `--log-file`. Neither is parsed yet (BL-108, BL-104).

### What upstream curl 8.21.0 does

- Build: `C:\Program Files\Git\mingw64\bin\curl.exe`, curl 8.21.0 (x86_64-w64-mingw32),
  SHA-256 `0E773709C3A44DB47B88B71351D902027682ED87C3BD3821009E454BACCA8778`
  (`UpstreamCurlBuilds.json`; the hash recomputed on 2026-09-29 matches the pin).
- Tool: `Record-CurlExchange.ps1 -NoServer -CurlArgs <args> -OutDirectory <dir>`,
  2026-09-29. stdout is redirected to a file, so curl sees no terminal.
- **Line endings:** in every output below, stdout holds exactly as many CR bytes as LF bytes
  as lines, each LF preceded by a CR: every line ends CR LF on this Windows build, the last
  line included.
- "Column" is 1-based: the column a description starts in.

| # | Arguments (after `curl`) | Exit | stdout lines | First line of stdout | Last line of stdout | Columns |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | `--help` | 0 | 23 | `Usage: curl [options...] <url>` | `Use "--help [option]" to view documentation for a given option` | descriptions in 31; widest line 78 |
| 2 | `--help category` | 0 | 25 | ` auth        Authentication methods` | ` verbose     Tracing, logging etc` | descriptions in 14 |
| 3 | `--help auth` | 0 | 38 | `auth: Authentication methods` | ` -u, --user <user:password>         Server user and password` | descriptions in 37; widest line 79 |
| 4 | `--help nosuch` | 0 | 27 | `Unknown category provided, here is a list of all categories:` | ` verbose     Tracing, logging etc` | as row 2 |
| 5 | `--help all` | 0 | 274 | `     --abstract-unix-socket <path>  Connect via abstract Unix domain socket` | `     --xattr                       Store metadata in extended file attributes` | descriptions in 36; widest line 79 |
| 6 | `--help --silent` | 0 | 15 | `    -s, --silent` | (empty) | body lines a TAB and 4 spaces, justified; widest 72 |
| 7 | `--manual` | 0 | 7849 | `          _   _ ____  _` | (empty) | widest line 79 |
| 8 | `--help http://127.0.0.1:1/` | 0 | 27 | byte for byte row 4 | | |
| 9 | `-h all` | 0 | 274 | byte for byte row 5 | | |

Row 1 whole (14 option lines). The first two lines of the category list end `ldap, ` and
`tls, `, each with a trailing space that the block below does not show: 78 and 77
characters.

```
Usage: curl [options...] <url>
 -d, --data <data>            HTTP POST data
 -f, --fail                   Fail fast with no output on HTTP errors
 -I, --head                   Show document info only
 -H, --header <header/@file>  Pass custom header(s) to server
 -h, --help <subject>         Get help for commands
 -o, --output <file>          Write to file instead of stdout
 -O, --remote-name            Write output to file named as remote file
 -i, --show-headers           Show response headers in output
 -s, --silent                 Silent mode
 -T, --upload-file <file>     Transfer local FILE to destination
 -u, --user <user:password>   Server user and password
 -A, --user-agent <name>      Send User-Agent <name> to server
 -v, --verbose                Make the operation more talkative
 -V, --version                Show version number and quit

This is not the full help; this menu is split into categories.
Use "--help category" to get an overview of all categories, which are:
auth, connection, curl, deprecated, dns, file, ftp, global, http, imap, ldap,
output, pop3, post, proxy, scp, sftp, smtp, ssh, telnet, tftp, timeout, tls,
upload, verbose.
Use "--help all" to list all options
Use "--help [option]" to view documentation for a given option
```

- The short list is alphabetical by long name; so are `--help all` and every category page.
  Categories are listed alphabetically.
- Row 4 is `Unknown category provided, here is a list of all categories:`, an empty line, then
  row 2's 25 lines. `--help important` (a category curl keeps hidden) and `--help silent`
  (an option name without its dashes) give the same.
- Row 6 is the manual's paragraph for the option: the line `    -s, --silent`, the body with
  every line starting with a TAB and four spaces, paragraphs separated by empty lines, then
  one empty line. `--help -s` and `--help --no-silent` give the same bytes.
- Row 7 is an ASCII-art banner, then the sections `NAME`, `SYNOPSIS`, `DESCRIPTION`, `URL`,
  `GLOBBING`, `VARIABLES`, `OUTPUT`, `PROTOCOLS`, `PROGRESS METER`, `VERSION`, `OPTIONS`,
  `ALL OPTIONS`, `FILES`, `ENVIRONMENT`, `PROXY PROTOCOL PREFIXES`, `EXIT CODES`, `BUGS`,
  `AUTHORS`, `WWW` and `SEE ALSO`: each heading in capitals at column 1, followed by an empty
  line, its body indented four spaces and justified to 79 columns. `-M` and
  `--manual --silent` give the same 299744 bytes.

**The column rule.** Every option line of rows 1, 3 and 5, and of `--help http`,
`--help tls` and `--help verbose` (6 outputs, 460 option lines), is reproduced exactly by
this rule, checked with a PowerShell script over the recorded bytes. The *left side* of a line is `-x, --name <arg>`
when the option has a short name and four spaces then `--name <arg>` when it has none. For
the lines of one page, with `cols` = 79:

1. `longopt` = the longest left side, at least 5; `longdesc` = the longest description, at
   least 5.
2. If `longopt + longdesc > cols`, `longopt = cols - longdesc`.
3. For each line: `opt = longopt`; if `opt + description length >= cols - 2`, then
   `opt = (cols - 3) - description length` when that length is below `cols - 2`, else 0.
4. The line is one space, the left side padded with spaces to `opt` characters (never cut),
   two spaces, the description.

The category list (rows 2 and 4) is one space, the name padded to the longest name, two
spaces, the description.

**How curl reads the subject.** Same build and tool, each exit 0 unless stated:

| Arguments | What curl wrote |
| --- | --- |
| `--help auth` | the `auth` page |
| `--help AUTH`, `--help Auth` | the `auth` page: categories match in any case |
| `--help ALL`, `--help Category` | row 5, row 2 |
| `--help=auth` | the `auth` page |
| `--help=`, `--help ""` | row 1 |
| `-vh auth` | the `auth` page: `-h` ending a bundle takes the next argument |
| `-h -v` | the `--verbose` option page: the next argument is the subject whatever it looks like |
| `--help http://127.0.0.1:1/` | row 4: a URL is a subject too (row 8) |
| `--help auth tls`, `--help auth --nosuch` | the `auth` page only: nothing after the subject is read |
| `-v --help`, `http://127.0.0.1:1/ --help` | row 1 |
| `--nosuch --help` | exit 2, `curl: option --nosuch: is unknown`: an earlier error still wins |
| `--help -h`, `--help --help` | the `--help` option page, 31 lines |
| `--help --user` | the `--user` option page, 40 lines |
| `--help --nosuch`, `--help --`, `--help -`, `--help -sv`, `--help --silent=x` | stdout empty; stderr `Incorrect option name to show help for, see curl -h` (no `curl: ` prefix) |
| `-hauth`, `-hv`, `-hx` | exit 2, `curl: (2) no URL specified` and the `try` line: `-h` followed by more letters in the same argument shows no help at all |
| `--no-help` | exit 2, `curl: option --no-help: the given option cannot be reversed with a --no- prefix` |

Upstream's documentation of the option: https://curl.se/docs/manpage.html#-h (curl 8.21.0).
Every `try` line curl writes for a command-line error is
`curl: try 'curl --help' or 'curl --manual' for more information` (ADR-0007).

## Decision

### 1. The categories

| Name | Description |
| --- | --- |
| `auth` | Accounts and authentication methods |
| `content` | Served files and the data directory |
| `dict` | DICT protocol |
| `gopher` | GOPHER and GOPHERS protocol |
| `http` | HTTP and HTTPS protocol |
| `limits` | Connection, time and size limits |
| `logging` | Log levels, tracing and the log file |
| `mqtt` | MQTT and MQTTS protocol |
| `security` | Options that widen what a peer may do |
| `surl` | The command line tool itself |
| `telnet` | TELNET protocol |
| `testing` | Loosening options for tests (warned) |
| `tftp` | TFTP protocol |
| `tls` | TLS certificates and versions |

- **Order:** ordinal (alphabetical), as curl lists its categories.
- **Stewart's seven** are there. `logging` is his name for what curl calls `verbose`; `surl`
  is curl's `curl` category ("The command line tool itself") under surl's name, the home of
  `-h`, `-M` and `-V`.
- **One per scheme family surl serves today**, named after the scheme without its secure
  `s`, described in curl's words (`HTTP and HTTPS protocol`, `TELNET protocol`): `http`
  (`http`, `https`), `dict`, `gopher` (`gopher`, `gophers`), `mqtt` (`mqtt`, `mqtts`),
  `telnet`, `tftp`.
- **What a protocol category holds:** every option that changes what that protocol's server
  answers - the options its own code reads from `ExchangeLimits`, from the content store's
  exposure options (through `ContentStore`), and from the authentication policy it is given.
  Engine-wide options (connection limits, `--idle-timeout`, `--max-time`, TLS, logging) are
  not repeated in protocol categories; they differ by nothing per protocol. Each membership
  in decision 2 was checked against the server's code on 2026-09-29.
- **How a protocol's category joins:** the task that registers a new protocol server in
  `Surl.Console` adds its category row (the scheme family's name, `<NAME> protocol` or
  `<NAME> and <NAME>S protocol`) and adds that category to every option its server reads, in
  the same change. The row takes its alphabetical place and the pointer lines re-wrap by
  decision 3's rule. No ADR is needed for that; an ADR is needed only for a new option, a
  new non-protocol category or a change to any description here.
- **Every category is always listed**, even one with no parsed option yet (`auth` and
  `testing` until BL-108): its page is then the heading line alone.

### 2. Every option: its categories, its description, the short list

The *left side* is decision 3's. **Short** marks the options in the short `--help` list.
**Default** is what `--help <option>` shows (decision 3); none is shown where the cell is
empty.

| Left side | Description | Categories | Short | Default |
| --- | --- | --- | --- | --- |
| `--allow-anonymous` | `Accept any login, or none (warns)` | auth, http, mqtt, security, testing | | `off` |
| `--allow-plaintext-auth` | `Accept passwords in clear (warns)` | auth, http, mqtt, security, testing | | `off` |
| `--allow-uploads` | `Accept uploads into served files` | content, security, tftp | yes | `off` |
| `--auth <methods>` | `Authentication methods accepted` | auth, http, security, testing | | `basic,bearer,digest,aws-sigv4` |
| `--cacert <file>` | `CA certificates for client certs` | tls | | `none` |
| `--cert <file>` | `Server certificate file` | tls | yes | `none` |
| `--cert-type <type>` | `Format of --cert: PEM, DER or P12` | tls | | `PEM` |
| `--directory <directory>` | `Data directory, else in memory` | content, dict, gopher, http, mqtt, tftp | yes | `in memory` |
| `--follow-symlinks` | `Follow links that stay in the root` | content, dict, gopher, http, security, tftp | | `off` |
| `--head-timeout <seconds>` | `Time to send a request head` | dict, gopher, http, limits, mqtt | | `30` |
| `-h, --help <subject>` | `Get help for commands` | surl | yes | |
| `--idle-timeout <seconds>` | `Close an exchange idle this long` | limits | | `120` |
| `--key <file>` | `Private key for --cert` | tls | yes | `the key in the --cert file` |
| `--key-type <type>` | `Format of --key: PEM or DER` | tls | | `PEM` |
| `--list-directories` | `Answer directory listings` | content, gopher, security | yes | `off` |
| `--log-file <file>` | `Append the log to <file>` | logging | | `stderr` |
| `--log-level <level>` | `Set the log level` | logging | | `info` |
| `-M, --manual` | `Display the full manual` | surl | | |
| `--max-connections <number>` | `Connections at once, all listeners` | limits | | `1024` |
| `--max-connections-per-address <number>` | `Connections at once per address` | limits | | `100` |
| `--max-filesize <bytes>` | `Largest upload accepted` | http, limits, mqtt, tftp | | `100M` |
| `--max-line <bytes>` | `Longest command line accepted` | dict, gopher, limits, telnet | | `8192` |
| `--max-message <bytes>` | `Largest framed message accepted` | limits, mqtt | | `1M` |
| `--max-request-head <bytes>` | `Largest HTTP or RTSP request head` | http, limits | | `100k` |
| `-m, --max-time <seconds>` | `Longest time one exchange may take` | limits | | `3600` |
| `--pass <phrase>` | `Passphrase for the private key` | tls | | `none` |
| `--self-signed` | `Throwaway certificate (warns)` | security, testing, tls | | `off` |
| `--serve-dot-files` | `Serve names that start with a dot` | content, dict, gopher, http, security, tftp | | `off` |
| `-S, --show-error` | `Show error even when -s is used` | logging | | `off` |
| `-s, --silent` | `Silent mode` | logging | yes | `off` |
| `--tls-max <version>` | `Highest TLS version accepted` | tls | | `1.3` |
| `--tlsv1.0` | `Accept TLS 1.0 or later` | security, tls | | |
| `--tlsv1.1` | `Accept TLS 1.1 or later` | security, tls | | |
| `--tlsv1.2` | `Accept TLS 1.2 or later (default)` | tls | | |
| `--tlsv1.3` | `Accept TLS 1.3 or later` | tls | | |
| `--trace <file>` | `Write a debug trace to <file>` | logging | | `none` |
| `--trace-ascii <file>` | `Like --trace, but without hex` | logging | | `none` |
| `--trace-time` | `Add time stamps to log lines` | logging | | `off` |
| `-u, --user <user:password>` | `Add an account (repeatable)` | auth, http, mqtt | yes | `no accounts` |
| `--user-file <file>` | `Read accounts from a file` | auth, http, mqtt | yes | `none` |
| `-v, --verbose` | `Log every exchange event` | logging | yes | `off` |
| `-V, --version` | `Show version number and quit` | surl | yes | |

- **Descriptions are at most 34 characters**, because the longest left side in `--help all`
  is `    --max-connections-per-address <number>` (42) and 42 + 34 = 76 keeps every line of every
  page inside curl's 79 columns without the column rule shortening any row. Defaults leave
  the one-line descriptions for that reason (curl's one-liners carry none either) and appear
  in `--help <option>` instead. A later option's description is decided by the ADR that adds
  it, within the same limit; a left side longer than 42 lowers the limit and is that ADR's
  to settle.
- **Where the words come from:** curl's own text where the meaning carries over (`-h`, `-M`,
  `-V`, `-s`, `-S`, `--pass`); ADR-0007 section 6's, ADR-0010's and ADR-0032 section 1's
  wording otherwise, cut to 34 characters; `(warns)` marks the options that write a startup
  warning (ADR-0032 section 9).
- **An option appears in help only once the parser accepts it.** Help is generated from the
  option table, so `-s`, `-S`, `--log-level`, `--trace`, `--trace-ascii`, `--trace-time` and
  `--log-file` join with BL-104, the ADR-0032 options with BL-108, and `-M` with BL-123. No
  help page names an option `surl` would refuse.

### 3. The layouts

Every page is written to stdout with `Environment.NewLine` after every line, the last
included (ADR-0007 section 5; curl's CR LF on Windows is that platform's convention). No line
has trailing spaces. Width is 79 columns always: curl measured with its stdout redirected
uses 79, and `surl` does not read the terminal's width, so each page is one fixed text a test
can pin.

**Option lines** follow the measured column rule above with `cols` = 79, exactly. The left
side is `-x, --name <arg>` for an option with a short name and four spaces then `--name <arg>`
for one without; lines are in ordinal order of the long name.

**`--help`** (no subject, or an empty one): `Usage: surl [options...] <url>...` (ADR-0007
section 6's first line), the option lines of the short list, an empty line, then curl's
pointer lines with `surl` for `curl`: the category names joined with `, ` and ended with
`.`, wrapped so each line holds as many names as fit in 79 columns counting the `, ` after
each, then written without curl's trailing space. With every option parsed:

```
Usage: surl [options...] <url>...
     --allow-uploads          Accept uploads into served files
     --cert <file>            Server certificate file
     --directory <directory>  Data directory, else in memory
 -h, --help <subject>         Get help for commands
     --key <file>             Private key for --cert
     --list-directories       Answer directory listings
 -s, --silent                 Silent mode
 -u, --user <user:password>   Add an account (repeatable)
     --user-file <file>       Read accounts from a file
 -v, --verbose                Log every exchange event
 -V, --version                Show version number and quit

This is not the full help; this menu is split into categories.
Use "--help category" to get an overview of all categories, which are:
auth, content, dict, gopher, http, limits, logging, mqtt, security, surl,
telnet, testing, tftp, tls.
Use "--help all" to list all options
Use "--help [option]" to view documentation for a given option
```

**`--help category`**: one line per category, one space, the name padded to the longest name,
two spaces, the description (curl's layout; descriptions in column 12 today):

```
 auth      Accounts and authentication methods
 content   Served files and the data directory
 dict      DICT protocol
 gopher    GOPHER and GOPHERS protocol
 http      HTTP and HTTPS protocol
 limits    Connection, time and size limits
 logging   Log levels, tracing and the log file
 mqtt      MQTT and MQTTS protocol
 security  Options that widen what a peer may do
 surl      The command line tool itself
 telnet    TELNET protocol
 testing   Loosening options for tests (warned)
 tftp      TFTP protocol
 tls       TLS certificates and versions
```

**`--help all`**: every option line, no heading (curl's layout). With every option parsed the
descriptions start in column 46:

```
     --allow-anonymous                       Accept any login, or none (warns)
     --allow-plaintext-auth                  Accept passwords in clear (warns)
     --allow-uploads                         Accept uploads into served files
     --auth <methods>                        Authentication methods accepted
     --cacert <file>                         CA certificates for client certs
     --cert <file>                           Server certificate file
     --cert-type <type>                      Format of --cert: PEM, DER or P12
     --directory <directory>                 Data directory, else in memory
     --follow-symlinks                       Follow links that stay in the root
     --head-timeout <seconds>                Time to send a request head
 -h, --help <subject>                        Get help for commands
     --idle-timeout <seconds>                Close an exchange idle this long
     --key <file>                            Private key for --cert
     --key-type <type>                       Format of --key: PEM or DER
     --list-directories                      Answer directory listings
     --log-file <file>                       Append the log to <file>
     --log-level <level>                     Set the log level
 -M, --manual                                Display the full manual
     --max-connections <number>              Connections at once, all listeners
     --max-connections-per-address <number>  Connections at once per address
     --max-filesize <bytes>                  Largest upload accepted
     --max-line <bytes>                      Longest command line accepted
     --max-message <bytes>                   Largest framed message accepted
     --max-request-head <bytes>              Largest HTTP or RTSP request head
 -m, --max-time <seconds>                    Longest time one exchange may take
     --pass <phrase>                         Passphrase for the private key
     --self-signed                           Throwaway certificate (warns)
     --serve-dot-files                       Serve names that start with a dot
 -S, --show-error                            Show error even when -s is used
 -s, --silent                                Silent mode
     --tls-max <version>                     Highest TLS version accepted
     --tlsv1.0                               Accept TLS 1.0 or later
     --tlsv1.1                               Accept TLS 1.1 or later
     --tlsv1.2                               Accept TLS 1.2 or later (default)
     --tlsv1.3                               Accept TLS 1.3 or later
     --trace <file>                          Write a debug trace to <file>
     --trace-ascii <file>                    Like --trace, but without hex
     --trace-time                            Add time stamps to log lines
 -u, --user <user:password>                  Add an account (repeatable)
     --user-file <file>                      Read accounts from a file
 -v, --verbose                               Log every exchange event
 -V, --version                               Show version number and quit
```

**`--help <category>`**: the line `<name>: <description>`, then the option lines of that
category, the column computed over those lines only (curl's layout). With every option
parsed, for example:

```
tls: TLS certificates and versions
     --cacert <file>      CA certificates for client certs
     --cert <file>        Server certificate file
     --cert-type <type>   Format of --cert: PEM, DER or P12
     --key <file>         Private key for --cert
     --key-type <type>    Format of --key: PEM or DER
     --pass <phrase>      Passphrase for the private key
     --self-signed        Throwaway certificate (warns)
     --tls-max <version>  Highest TLS version accepted
     --tlsv1.0            Accept TLS 1.0 or later
     --tlsv1.1            Accept TLS 1.1 or later
     --tlsv1.2            Accept TLS 1.2 or later (default)
     --tlsv1.3            Accept TLS 1.3 or later
```

```
telnet: TELNET protocol
     --max-line <bytes>  Longest command line accepted
```

`--help testing` adds the paragraphs of decision 5 after its option lines.

The pages above are the final ones, with every option parsed. Before BL-104, BL-108 and
BL-123 land, each page is the same rule applied to the rows that exist, so a column may sit
elsewhere; BL-103's tests pin the pages the rule gives for the options parsed then, and each
later task updates the pinned pages its options change.

**An unknown subject that is not an option** (`--help nosuch`, `--help silent`,
`--help http://127.0.0.1:1/`): curl's answer - `Unknown category provided, here is a list of
all categories:`, an empty line, then the `--help category` lines - on stdout, exit `Ok` (0),
as curl (rows 4 and 8).

**`--help <option>` is answered**, as curl 8.21.0 answers it (row 6). The page is:

1. four spaces and the left side without its padding (`    --max-line <bytes>`,
   `    -u, --user <user:password>`);
2. one paragraph: the description, `.`, and, when decision 2 gives a default, a space and
   `Default: <default>.`;
3. for the four loosening options only, an empty line and decision 5's paragraph;
4. an empty line and one paragraph `Categories: <names>.`, the option's categories joined
   with `, ` in ordinal order;
5. one empty line, as curl ends its option pages.

Each paragraph line is eight spaces and words wrapped greedily at single spaces so no line
passes 79 columns (a word too long for a line is put alone on one). surl indents with spaces,
not curl's TAB and four spaces, and does not justify: the justification is curl's manual
renderer's, and ragged spaces-only text has one width on every terminal and diffs cleanly.
For example:

```
    --max-line <bytes>
        Longest command line accepted. Default: 8192.

        Categories: dict, gopher, limits, telnet.

```

**An option subject that names no option** (`--help --nosuch`, `--help --`, `--help -`,
`--help -sv`, `--help --silent=x`): nothing on stdout; on stderr the line
`surl: Incorrect option name to show help for, see surl -h`; exit `Ok` (0), as curl. This
**differs** from curl only by the `surl: ` prefix, which ADR-0007 section 5 puts on every
message surl writes to stderr, so a script filtering surl's own lines catches it.

**Subject matching** (after decision 4 has read it):

1. No subject, or an empty one: `--help`.
2. A subject starting with `-` is an option: `--<long name>` spelled in full and matched
   ordinally, `--no-<long name>` of a negatable option (curl answers `--no-silent` with
   `--silent`'s page), or `-` and exactly one short-name character. Anything else starting
   with `-`, including `=value` and a bundle, is the incorrect-option answer.
3. Otherwise, matched with `StringComparison.OrdinalIgnoreCase` as curl matches: `all`,
   `category`, a category name, or the unknown-category answer.

Help is not log output (ADR-0033 section 1): every answer above is written whatever `-s` or
`--log-level` says, and each returns `Ok`.

### 4. How the parser reads the subject, and the result shape

This supersedes ADR-0007 section 1's "`-h` takes no argument in surl" and its rule that
`-h`/`--help` ends reading where it stands.

- **`--help=<text>`**: the subject is `<text>` (`--help=` is an empty subject). `=value` is
  accepted here because `--help` now takes an optional argument.
- **`--help` without `=`**: when another argument follows, it is the subject, whatever it
  is - an option (`-h -v`), a listen URL, `--`, or an empty string; when none follows there
  is no subject. As curl (rows 8 and 9, and the subject table).
- **`-h` ending its argument** (`-h`, `-vh`): the same, from the next argument (`-vh auth`).
- **`-h` with more characters after it in the same argument** (`-hauth`, `-hv`): the rest of
  the argument is the subject, as `-m30` takes `30`. This **differs** from curl, which shows
  no help and reads on (`-hv` ends in `(2) no URL specified`): a command line that says
  "help" and gets none is one that says something surl does not do, the reasoning of
  ADR-0007 section 1's refusal of `=value` on a flag.
- **Reading ends once the subject is taken.** Nothing after it is read (`--help auth
  --nosuch` answers `auth`, exit 0). An error before `-h`/`--help` is still reported, and
  `-V` or `-M` before it still wins, as ADR-0007 section 1 decides and curl does.
- **An empty subject is not refused**, unlike an empty option argument (ADR-0007 row 25): it
  means the short list, as curl.
- **`--no-help`** stays refused: `option --no-help: the given option cannot be reversed with a
  --no- prefix`, as curl.
- **`-M`, `--manual`** (BL-123) take no argument, are not negatable, refuse `=value` as every
  flag does (ADR-0007 section 1), and end reading where they stand, like `-V`.

The shapes BL-103 (and, for the manual, BL-123) add to `Surl.Cli`:

```csharp
public enum CommandLineOutcome { Serve, ShowHelp, ShowManual, ShowVersion, Refused }
// ShowManual is added by BL-123.

public sealed class CommandLineParseResult
{
    // Replaces the static property ShowHelp.
    public static CommandLineParseResult ShowHelp(string? subject);

    // Added by BL-123.
    public static CommandLineParseResult ShowManual { get; }

    // The subject when Outcome is ShowHelp: null when none was given or it was empty,
    // otherwise the argument exactly as written. Null for every other outcome.
    public string? HelpSubject { get; }
    // Outcome, CommandLine, Failure, ShowVersion, Serve and Refused are unchanged.
}

// Surl.Cli.UnitLibrary/HelpText.cs: HelpText.Text is removed.
public static class HelpText
{
    // What surl writes for a help subject: decision 3.
    public static HelpAnswer Answer(string? subject);
}

// Each is written as it is; string.Empty when nothing goes to that stream.
public sealed record HelpAnswer(string Output, string Error);
```

- `CommandLineOptionKind` gains `Manual` (BL-123) beside `Help` and `Version`.
- **One table.** `CommandLineOption` gains a last positional member `OptionHelp Help`, so an
  option cannot be added without its help:
  `internal sealed record OptionHelp(string? ArgumentName, string Description,
  IReadOnlyList<string> Categories, bool IsInShortList, string? Default, string? Explanation)`,
  `ArgumentName` being `<seconds>` and the like, `Explanation` decision 5's paragraph for the
  loosening options and `null` for every other. The categories are
  `internal sealed record HelpCategory(string Name, string Description)` rows in one ordinal
  list, `HelpCategories.All`.
- `Surl.Console/CommandLineRunner.cs` writes `answer.Output` to its output writer and
  `answer.Error` to its error writer for `ShowHelp`, and returns `SurlExitCode.Ok`.

### 5. The `--help testing` text

After the option lines of `testing`: an empty line, then for each loosening option in
ADR-0032 section 9's order (`--allow-anonymous`, `--allow-plaintext-auth`, `--auth`,
`--self-signed`) the line of four spaces and its left side, its paragraph laid out as in
decision 3, and an empty line. The same paragraph is that option's `Explanation` in
`--help <option>`. The paragraphs:

- **`--allow-anonymous`**: "Accepts every request and every login without checking
  credentials: HTTP serves every request as anonymous and sends no challenge, and MQTT
  answers every CONNECT with CONNACK 0 whatever it carries. A test uses it to fetch or
  publish without setting up accounts. It is not the default because anyone who can reach a
  listener then gets everything surl serves, and the uploads --allow-uploads accepts, with
  no login at all. surl warns on every start while it is on."
- **`--allow-plaintext-auth`**: "Accepts passwords and tokens sent over an unencrypted
  connection: HTTP Basic and Bearer over http:// and an MQTT password over mqtt:// are
  checked instead of refused unchecked (403 Forbidden, CONNACK 5), and Basic and Bearer are
  offered in a 401 over http://. A test uses it to log in without a certificate. It is not
  the default because anyone who can watch the network reads the password as it is sent.
  surl warns on every start while it is on."
- **`--auth <methods>`**: "Sets the HTTP authentication methods surl accepts and offers, a
  comma-separated list of basic, bearer, digest, ntlm, negotiate and aws-sigv4; the default
  is basic,bearer,digest,aws-sigv4. A test uses it to answer curl's --ntlm or --negotiate,
  or to offer one method alone. ntlm and negotiate are not in the default because an NTLM
  response is built on MD4 and HMAC-MD5 of the password and is open to relay and offline
  cracking, and Negotiate here carries NTLM. surl warns on every start while --auth is
  given, naming the methods it accepts."
- **`--self-signed`**: "Serves a throwaway self-signed certificate, made at start, for a
  listen URL of a scheme that starts with TLS (such as https) when no --cert is given;
  without it, and without --cert, such a URL is refused at start. A test uses it to serve a
  secure scheme without a certificate file; curl then needs -k. It is not the default
  because no client can verify the certificate, so a client cannot tell surl from anyone
  else on the path. It cannot be used with --cert. surl warns when it makes the
  certificate."

Each sentence restates ADR-0032 sections 3, 4, 5, 9 and 10. BL-123 writes the text only once
BL-116 and BL-117 have built that behaviour, and changes a sentence the code has made untrue
rather than landing it.

### 6. `--manual`

- **Parsing:** decision 4. **Output:** the whole text to stdout, exit `Ok` (0), at every log
  level; no subject (curl's `--manual --silent` prints the whole manual).
- **Where the text lives:** a `string[]` of lines in `Surl.Cli.UnitLibrary/ManualText.cs`,
  `public static class ManualText` with `public static string Text`, each line followed by
  `Environment.NewLine` - the shape `HelpText.cs` has today. Not an embedded resource: a
  source constant needs no resource-reading path under native AOT, has no read failure to
  handle or cover, and changes in the same diff as the code it describes.
- **Layout:** curl's manual layout without the banner and without justification. Each
  section heading in capitals at column 1, then an empty line, then its body indented four
  spaces, paragraphs separated by one empty line, an empty line before the next heading.
  Body text is wrapped greedily at single spaces so no line passes **79 columns**; no TAB,
  no trailing space. A list item starts `    - ` (a numbered step `    1. `) and its
  continuation lines align under its first word. curl's ASCII-art banner is left out: it
  carries no information and is bytes to pin for nothing.
- **Sections, in this order:**

| # | Heading | What it covers |
| --- | --- | --- |
| 1 | `NAME` | `surl - the server-side mate of curl` |
| 2 | `SYNOPSIS` | `surl [options] <url>...` |
| 3 | `DESCRIPTION` | What surl is: for each request upstream curl makes, the server that answers it; `surl --version` names the schemes this build serves (the manual does not list them, so it stays true as servers land). |
| 4 | `LISTEN URLS` | `scheme://host[:port][/]`, port 0 for an ephemeral port, the `Listening on` line, several URLs (ADR-0007 sections 4 and 7). |
| 5 | `DEPLOYMENT CHECKLIST` | Numbered steps for running surl where others can reach it: bind a specific address rather than `0.0.0.0` or `[::]` unless every interface should answer; give `--directory` for state that must survive a restart; give `--cert` and `--key` for secure schemes, never `--self-signed`; configure accounts with `--user-file` rather than `--user`, which other local users can read in the process list, and protect the file; leave uploads, listings, links and dot-files off unless needed; review the limits; start with no loosening option, and treat any `surl: warning:` line at start as one left on; pick a log level and `--log-file`; stop with Ctrl+C or SIGTERM, which exits 0. |
| 6 | `DATA DIRECTORY` | `--directory <path>`: files served from the top; service state under `<path>/.surl/`, which is never served, even with `--serve-dot-files`; the lock `<path>/.surl/lock` and exit 124 for a directory another surl holds; exit 37 for a missing path, 23 for a `.surl` that cannot be created; a read-only directory cannot be served (ADR-0031). |
| 7 | `IN-MEMORY MODE` | Without `--directory`: an in-memory file system that starts empty, holds at most 256 MiB, touches no disk and is lost at exit; uploads still need `--allow-uploads` (ADR-0031). |
| 8 | `ACCOUNTS` | `-u`/`--user user:password` (repeatable, split at the first `:`, an empty name a Bearer token), `--user-file` and its format, no accounts meaning every login refused, anonymous HTTP reads only while no account is configured (ADR-0032 sections 1, 2 and 4). |
| 9 | `LOOSENING OPTIONS` | Names the four, says each writes a warning on every start, and points to `surl --help testing` for what each loosens and why none is the default; it does not repeat those paragraphs. |
| 10 | `LOG LEVELS` | `none` (`-s`), `error` (`-s -S`), `info` (the default), `verbose` (`-v`), `trace` (`--trace`, `--trace-ascii`); `--log-level`, the last level option winning; `--trace-time`; `--log-file`, appended; what stays on stderr (ADR-0033). |
| 11 | `LIMITS` | The connection, time and size limits with their defaults and 0 for no limit (ADR-0006 section 1), and that a limit is answered in the protocol's own words and never ends surl. |
| 12 | `EXIT CODES` | Each `SurlExitCode` surl returns, its number and the failure it means (ADR-0005 and the ADRs that add rows). |
| 13 | `SEE ALSO` | `surl --help all`, `surl --help category`, `curl(1)`, https://github.com/StewartScottRogers/Surl |

- **Truth:** every statement in the manual is true of the code on the day it lands, checked
  against the source by BL-123, not copied from an ADR. Behaviour that is not built yet is
  not described; when later work changes what a section says, that work updates the section
  in the same change. Every option the manual names exists in `CommandLineOptions.All`
  (BL-123's test).
- **The `try` line** gains the manual once `--manual` exists, as curl's does:
  `surl: try 'surl --help' or 'surl --manual' for more information` replaces ADR-0007
  section 5's `try 'surl --help' for more information`, in BL-123.

## Alternatives considered

- **Keep one page (ADR-0007 section 6).** Rejected by Stewart's approval: with the ADR-0032
  and ADR-0033 options the single page grows past 40 lines, and curl's own answer to that
  is categories.
- **A fixed description column (46, as today) with descriptions as long as they need.**
  Rejected: several lines would pass 79 columns, and curl's measured rule already says where
  every column goes. Applying curl's rule to today's longer descriptions instead would cap
  the column and leave ragged pages; 34-character descriptions keep every page aligned.
- **Read the terminal's width, as curl does on a terminal.** Rejected: the text would depend
  on the console it runs in, and tests could not pin it; 79 is what curl uses off a terminal.
- **Leave `--help <option>` out.** Rejected: curl 8.21.0 answers it, the defaults that left
  the one-liners need a home, and the loosening paragraphs are then reachable per option.
- **Follow curl on `-hauth` (no help, read on).** Rejected in decision 4.
- **Curl's exact `Incorrect option name ...` line without a prefix.** Rejected in decision 3:
  one rule for surl's stderr lines.
- **Hide categories that have no option yet.** Rejected: the category list would change with
  every option task, and a heading with no rows says truly that nothing is there.
- **Put the manual in an embedded resource or a Markdown file.** Rejected in decision 6.
- **Curl's ASCII-art banner and justified text.** Rejected in decisions 3 and 6.

## Consequences

- BL-103 builds decisions 1 to 4 for the options parsed then: `OptionHelp` on every row of
  `CommandLineOptions`, `HelpCategories`, `HelpText.Answer`, `HelpAnswer`, the subject in
  `CommandLineParseResult`, and `CommandLineRunner` writing both streams.
- BL-104 and BL-108 give their options their decision 2 help and update the pinned pages.
- BL-123 builds decisions 5 and 6: `-M`/`--manual`, `ShowManual`, `ManualText`, the
  `--help testing` paragraphs and the new `try` line.
- A protocol server's task adds its category (decision 1). A new option, a new non-protocol
  category, or a change to any text here is a new ADR that supersedes the affected part.
- FR-009 now names this ADR; FR-008 lists `-M`/`--manual`.
