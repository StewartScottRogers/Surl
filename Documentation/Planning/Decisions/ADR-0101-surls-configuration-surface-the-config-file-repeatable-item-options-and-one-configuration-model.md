# ADR-0101 — surl's configuration surface: the config file, repeatable item options and one configuration model

- **Status:** Accepted
- **Date:** 2026-11-04
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-11-04,
  in BL-641. Stewart approved the direction on 2026-11-04 ("Layer 2 - a configuration command line
  for real servers"): `surl` gets a command line that configures a real server, mailboxes, shares,
  topics and groups among it. That approval is his and is not reopened here; this ADR decides the
  surface.
- **Builds on:** [ADR-0003](ADR-0003-upstream-curl-is-surls-only-oracle.md) (upstream curl is the
  only oracle), ADR-0005 (exit codes), ADR-0006 (every input is bounded), ADR-0007 (the command line,
  its refusals and `try` line), ADR-0032 (accounts, `--user`, `--user-file`, warnings), ADR-0033 (log
  levels), ADR-0034 (help categories), ADR-0046 (`--aihelp` completeness),
  [ADR-0099](ADR-0099-one-server-composition-for-surl-and-surl-api-and-surl-apis-programming-interface.md)
  (BL-611: `Surl.Composition` composes from `Surl.Cli`'s parsed `SurlCommandLine`, and every
  `Surl.Api` property is a spelling of a command-line option) and
  [ADR-0100](ADR-0100-the-stores-synergetic-protocols-share-files-mail-the-message-bus-and-accounts.md)
  (the shared stores whose seams layer 2 fills).
- **Leaves to BL-642's ADR:** what each mail, file, message and group option *means* to the stores
  (domain matching, quotas, share access defaults, the replies a denied client gets). This ADR
  decides only the surface they share: names, argument grammar, the config file, refusals, bounds
  and help.
- **Acted on by:** BL-644, BL-645, BL-646, BL-653, BL-654, BL-663, BL-666, BL-668 and BL-684
  (listed under Consequences).

## Context

`surl` today is configured for tests: listen URLs, a data directory, `--user` accounts, a
`--user-file`, loosening options, TLS files and limits. A real server needs hundreds of mailboxes,
named shares, declared topics and groups of accounts, and an operator wants them in a file rather
than on one command line. Three hosts fill the same configuration: the command line, `Surl.Api`
(ADR-0099: its properties are spelled as options and parsed by `CommandLineParser`) and later
`Surl.Mcp` (BL-612).

Upstream curl already has a config-file syntax every curl user knows, `-K`/`--config`, and Surl's
command line mirrors curl's (ADR-0007). The syntax was read in `docs/cmdline-opts/config.md` and
`src/tool_parsecfg.c` at tag `curl-8_21_0` of https://github.com/curl/curl, and each row below was
measured against the pinned Windows reference build:

- **Build:** `C:\Program Files\Git\mingw64\bin\curl.exe`, curl 8.21.0 (x86_64-w64-mingw32), SHA-256
  `0E773709C3A44DB47B88B71351D902027682ED87C3BD3821009E454BACCA8778` (`UpstreamCurlBuilds.json`, role
  `reference`).
- **How:** `Record-CurlExchange.ps1` gained `-ConfigFile <name>=<text>` for this ADR: each config file
  is written beside the fixtures with the script's backslash escapes decoded, and curl runs in that
  folder, so files name each other by bare name. Every measurement is one call of the form

  ```powershell
  & .\Record-CurlExchange.ps1 -Port <port> -OutDirectory <dir> -ConfigFile <files> -CurlArgs <args>
  ```

  with the rows of the table below (in a `-ConfigFile` text `\n` is a line feed, `\r` a carriage
  return, `\t` a tab, `\"` a double quote and `\\` one backslash, so the config file's own `\t`
  is written `\\t`):

| # | Case | `-ConfigFile` | `-CurlArgs` | Result |
| --- | --- | --- | --- | --- |
| M1 | Escapes in a quoted value | `a.cfg=url = \"http://127.0.0.1:18641/\"\nheader = \"X-A: a\\tb\\\\c\\\"d\\qe\\re\\vf\" trailing\n` | `-sS -K a.cfg` | exit 0; header sent `X-A: a<TAB>b\c"dqe<CR>e<VT>f`: `\t`, `\\`, `\"`, `\r`, `\v` are escapes, `\q` is `q`, and the text after the closing quote is dropped with no warning |
| M2 | Separators, dashes, comments, CR LF | `a.cfg=url http://127.0.0.1:18642/\r\nheader: \"X-B: colon\"\r\nheader=X-C:eq\r\n--header \"X-D: dashed\"\r\n-H \"X-E: short\"\r\n   # indented comment\r\n\t\r\nheader \"X-F: ws\"\r\nheader ==: \"X-G: many separators\"\n` | `-sS -K a.cfg` | exit 0; headers `X-B: colon`, `X-C:eq`, `X-D: dashed`, `X-E: short`, `X-F: ws`, `X-G: many separators` in file order |
| M3 | `=` after a dashed name | `a.cfg=url = http://127.0.0.1:18643/\n--header=X-H:h\n` | `-sS -K a.cfg` | exit 0; header `X-H:h` (the line is read as the command-line word `--header=X-H:h`) |
| M4 | Text after an unquoted value; warnings | `a.cfg=url = http://127.0.0.1:18660/\n# comment\n\nheader X-I:i more words\nheader X-J:j # comment\nheader = \'X-Q:q\'\n` | `-S -o out.bin -K a.cfg` | exit 0; headers `X-I:i`, `X-J:j`, `'X-Q:q'`; stderr `Warning: a.cfg:2 Option 'header' uses argument with unquoted whitespace. This may cause side-effects. Consider double quotes.` and `Warning: a.cfg:4 Option 'header' uses argument with leading single quote. It is probably a mistake. Consider double quotes.` (each wrapped at 79 columns) - the trailing `# comment` draws no warning, and the line numbers count only option lines: physical lines 4 and 6 are reported as 2 and 4 |
| M5 | Nesting | `a.cfg=config = b.cfg\nheader = \"X-A: a\"\n`, `b.cfg=url = http://127.0.0.1:18645/\nheader = \"X-B: b\"\n` | `-sS -K a.cfg` | exit 0; `X-B: b` then `X-A: a`: the nested file is read in place |
| M6 | A file that includes itself | `s.cfg=config = s.cfg\n` | `-sS -K s.cfg http://127.0.0.1:18646/` | exit 2; stderr `curl: Max config file recursion level reached (5)`, five lines `curl: s.cfg:1 config file option 'config' is badly used here`, `curl: option -K: is badly used here`, the `try` line |
| M7 | Two files including each other | `a.cfg=config = b.cfg\n`, `b.cfg=config = a.cfg\n` | `-sS -K a.cfg http://127.0.0.1:18657/` | exit 2; the same recursion line at level 5, no cycle detected as such |
| M8 | Unknown option, undashed | `a.cfg=url = http://127.0.0.1:18647/\nnosuchoption = 1\n` | `-sS -K a.cfg` | exit 2; `curl: a.cfg:2 config file option 'nosuchoption' is unknown`, `curl: option -K: found an unknown config option`, the `try` line |
| M9 | Unknown option, dashed, after blank lines | `a.cfg=\n\n--nosuchoption\n` | `-sS -K a.cfg http://127.0.0.1:18648/` | exit 2; `curl: a.cfg:1 config file option '--nosuchoption' is unknown` (physical line 3) |
| M10 | A URL on its own line | `a.cfg=http://127.0.0.1:18653/\n` | `-sS -K a.cfg` | exit 2; `curl: a.cfg:1 config file option 'http' is unknown` |
| M11 | Missing file | `x.cfg=` | `-sS -K missing.cfg http://127.0.0.1:18649/` | exit 26; `curl: cannot read config from 'missing.cfg'`, `curl: option -K: error encountered when reading a file`, the `try` line |
| M12 | Missing nested file | `a.cfg=\nconfig = nothere.cfg\n` | `-sS -K a.cfg http://127.0.0.1:18658/` | exit 26; `curl: cannot read config from 'nothere.cfg'`, `curl: a.cfg:1 config file option 'config' error encountered when reading a file`, `curl: cannot read config from 'a.cfg'`, then as M11 |
| M13 | Standard input | `x.cfg=` | `-sS -K -`, with `-StandardInput 'url = http://127.0.0.1:18650/\nheader = \"X-S: stdin\"\n'` | exit 0; header `X-S: stdin` |
| M14 | File after the command line's option | `a.cfg=user-agent = file\n` | `-sS -A cmdline -K a.cfg http://127.0.0.1:18651/` | `User-Agent: file` |
| M15 | File before it | `a.cfg=user-agent = file\n` | `-sS -K a.cfg -A cmdline http://127.0.0.1:18652/` | `User-Agent: cmdline` |
| M16 | Empty quoted value | `a.cfg=url = http://127.0.0.1:18654/\nuser-agent = \"\"\n` | `-sS -K a.cfg` | exit 0; no `User-Agent` (an empty argument reached `-A`) |
| M17 | No value | `a.cfg=url = http://127.0.0.1:18655/\nuser-agent\n` | `-sS -K a.cfg` | exit 2; `curl: a.cfg:2 config file option 'user-agent' requires parameter`, `curl: option -K: requires parameter`, the `try` line |
| M18 | Two `-K` | `a.cfg=header = \"X-A: a\"\n`, `b.cfg=header = \"X-B: b\"\n` | `-sS -K a.cfg -K b.cfg http://127.0.0.1:18656/` | exit 0; `X-A: a` then `X-B: b` |

`config.md` at `curl-8_21_0` says, besides what the table shows: "Options and their parameters
must be specified on the same line in the file, separated by whitespace, colon, or the equals
sign"; "Long option names can optionally be given in the config file without the initial double
dashes"; "If the option is specified with one or two dashes, there can be no colon or equals
character between the option and its parameter"; "If the first non-blank column of a config line is
a '#' character, that line is treated as a comment"; the URL must be given "using the --url option,
and not by writing the URL on its own line"; "Within double quotes the following escape sequences
are available: \\, \", \t, \n, \r and \v"; "Specify the filename to --config as minus "-" to make curl
read the file from stdin"; "A single line is required to be no more than 10 megabytes (since
8.2.0)"; it is `Multi: append`; and "(unless --disable is used)" curl reads a default `.curlrc`.

Constraints: base class library only; `Surl.Cli` never touches the disk or the console (its
`CLAUDE.md`), so files reach it through a seam; secrets are never echoed
(`CommandLineOption.ArgumentHoldsSecret`); secure by default (ADR-0032, Stewart's "secure by
default, loosen for tests"); every new option must pass the `--aihelp` completeness tests.

## Decision

### 1. `-K`/`--config <file>`

A new option, `-K`/`--config <file>`, argument type `path` (`OptionArgumentReader.Path`), help
category `surl`, repeatable (each adds a file), widening nothing by itself. It reads options from a
file **in place**: the file's options are parsed as if their words stood on the command line where
the `-K` stands, left to right, so a later option overrides an earlier scalar and an earlier one is
overridden by a later (M14, M15), and repeating options add in order (M18). Every option `surl`
takes may stand in a file, and behaves there exactly as on the command line, `--help`, `--aihelp`,
`--manual` and `--version` included.

The syntax, row by row. "As curl" means as upstream curl 8.21.0 does, by the cited row; "Surl" marks
a departure and its reason.

| # | Row | Decision | Rests on |
| --- | --- | --- | --- |
| 1 | Encoding | UTF-8, an optional byte-order mark skipped; a line that is not UTF-8 is refused. Surl: curl reads bytes; `surl`'s values are .NET strings and `--user-file` already reads UTF-8 (ADR-0032 section 2). | Surl |
| 2 | Lines | Split at LF; one CR right before the LF, or at the end of the file, is dropped. | M2 (CR LF file read whole) |
| 3 | Blank and comment lines | A line that is empty or only spaces and tabs is skipped; a line whose first character that is not a space or tab is `#` is a comment. | config.md; M2, M4 |
| 4 | One option per line | The line starts with the option (after any spaces and tabs) and holds at most its one value. | config.md |
| 5 | Name without dashes | A name not starting with `-` is a long option name: `header` is `--header`. It ends at the first space, tab, `=` or `:`. Then any run of spaces, tabs, `=` and `:` separates it from the value. | config.md; M2 (`header: `, `header=`, `header `, `header ==: `) |
| 6 | Name with dashes | A name starting with `-` (`--header`, `-H`) ends at the first space or tab only, and the separator is spaces and tabs only. `--name=value` with no space is the command-line word itself, so it is read as ADR-0007 reads `--name=value`. | config.md; M2 (`--header`, `-H`), M3 |
| 7 | Unquoted value | Runs to the first space or tab. After it, only spaces and tabs, optionally followed by a `#` comment, may follow. **Surl: anything else after the value is refused** (row 5 of decision 5). curl takes the first word and warns (M4); a server configuration that silently drops `;readonly` or the second half of a path opens more than it says, so it is refused instead. | M4; Surl |
| 8 | Quoted value | A value starting with `"` runs to the next `"` not escaped by `\`. After the closing quote, only spaces and tabs, optionally followed by a `#` comment, may follow; **Surl: anything else is refused**, and so is a **missing closing quote**. curl drops the text silently (M1) and reads an unterminated value to the end of the line (`unslashquote` in `tool_parsecfg.c`); both hide a typing mistake. | M1; `tool_parsecfg.c`; Surl |
| 9 | Escapes | Inside double quotes: `\\` a backslash, `\"` a double quote, `\t` tab, `\n` line feed, `\r` carriage return, `\v` vertical tab; a backslash before any other character is dropped and the character kept (`\q` is `q`). Outside quotes a backslash is an ordinary character. | config.md; M1 |
| 10 | Single quote | Not a quote: `'x'` is the three characters, as curl reads it. curl warns (M4); `surl` neither warns nor refuses, since a password may start with `'`. | M4 |
| 11 | Empty value | `""` is the empty argument, refused or accepted as the option's argument type says (ADR-0007). No value at all is a missing argument: `option <name>: requires parameter` for an option that takes one. | M16, M17 |
| 12 | Listen URLs | A listen URL is given as `url = <listen URL>` (or `--url <listen URL>` on the command line): a new option `--url <listen URL>`, help category `surl`, repeatable, each adding a listen URL exactly as a bare argument does. A URL alone on a line is an unknown option, as in curl. | config.md; M10 |
| 13 | Standard input | `-K -` reads the file from standard input; named `<stdin>` in refusals, as curl names it. **Surl:** standard input can be read once, so a second `-` in one start is refused (row 9 of decision 5). | config.md; M13; `tool_parsecfg.c` (`<stdin>`) |
| 14 | Nesting | `config = <file>` inside a file reads that file in place (M5). A relative path is relative to the working directory, not to the including file, as curl opens it (`curlx_fopen(filename)` in `tool_parsecfg.c`). At most **5 files open at once**, the command line's own `-K` counted, as curl's `Max config file recursion level reached (5)` (M6). **Surl:** a file that includes a file already open (compared by full path, case as the platform's file system compares it) is refused at once as a cycle; curl only stops at the depth limit (M6, M7). | M5, M6, M7; `tool_parsecfg.c`; Surl |
| 15 | Line numbers | `<line>` counts every physical line from 1, blank and comment lines included. **Surl:** curl counts only option lines (M4, M9), which points an operator at the wrong line in an editor. | M4, M9; Surl |
| 16 | Largest line | 10 MiB (10485760 bytes, not counting the line end), as curl. | config.md ("no more than 10 megabytes (since 8.2.0)") |
| 17 | Largest file | 16 MiB (16777216 bytes) per file. **Surl:** curl has no file bound; ADR-0006 bounds every input. | Surl |
| 18 | Default file | None; see decision 2. | Surl |

A refusal is decided per line in file order and the first one ends the parse, as curl stops at its
first error (`while(!err && ...)` in `tool_parsecfg.c`).

**Where the reading happens.** `Surl.Cli` expands each `-K` while it parses, through an injected
file-reading seam (BL-644 names it; planning `IConfigFileSource`, returning the bytes or "could not
read"), so the parse result is the same `SurlCommandLine` whichever host built the words.
`Surl.Console` supplies the disk reader and standard input. `SurlCommandLine` keeps the paths it read
(`ConfigFiles`), for the warning of decision 5.

### 2. No default config file

`surl` reads no file it was not told to read: no `.surlrc`, no `_surlrc`, no `CURL_HOME` or
`XDG_CONFIG_HOME` lookup, and so no `-q`/`--disable`. curl reads a default `.curlrc` (config.md); a
client that picks up settings silently costs a surprising request, but a server that picks up a stray
`allow-anonymous` or `share` from a home directory serves data to strangers. Secure by default wins:
what `surl` serves is on its command line or in a file the command line names.

`Surl.Api` accepts config files too: `SurlServerOptions.ConfigFiles` (`IList<string>`), each mapped to
`--config <path>`, placed after the typed properties and before `AdditionalArguments` (ADR-0099
decision 4's word order), so a file can be overridden by `AdditionalArguments` and overrides the
properties. `-` is an `ArgumentException` there: a test process's standard input is not the test's
configuration. Built by BL-668. `Surl.Mcp` decides its own in BL-612.

### 3. The item grammar the layer-2 options share

Every layer-2 option's argument is one **item**:

```
item      = head *( ";" attribute )
head      = field [ separator field ]        ; the option's separator: "=" or ":" (table below)
attribute = key [ "=" value ]                 ; a bare key is a flag
value     = field / list
list      = field *( "," field )              ; only where the option's table row says <...>,...
member    = field / "@" field                 ; in member lists: an account name, or @ and a group name
field     = *( any character but ";" and "%" / pct-encoded )
```

- **Splitting.** The item is split at every `;` into the head and attributes. The head is split at
  the **first** occurrence of the option's separator, so the part after it may hold the separator
  (`--mailbox ann@example.com:pa:ss` is the password `pa:ss`, as `--user` splits). A list is split at
  every `,`. An attribute is split at its first `=`.
- **Escaping is percent-encoding**, as in URLs (RFC 3986 section 2.1): `%` and two hex digits, in
  either case, stand for one byte, and the bytes of a field after decoding must be UTF-8. It is
  needed only for `%` itself (`%25`), `;` (`%3B`) anywhere, `,` (`%2C`) inside a list member, the
  separator inside the part before the first one, `@` (`%40`) at the start of an account name in a
  member list, and `=` (`%3D`) in an attribute key. Decoding happens after splitting, so an encoded
  delimiter never splits. Percent-encoding was chosen over backslashes because Windows paths
  (`C:\data`, `\\server\share`) then need nothing, it survives every shell and the config file's
  own backslash escapes unchanged, and curl users already read it in URLs.
- **Keys and flags** are lower-case ASCII words, matched case-insensitively, as `--auth` words are
  (ADR-0032 section 1). Each option's table row lists its own; **an unknown key or flag is
  refused**, never ignored: a misspelt `readonly` must not leave a share writable. A key given twice
  in one item is refused, as is a flag given a value or a key given none.
- **Control characters** (U+0000 to U+001F, U+007F) after decoding are refused in every name, key,
  list member and group name; a password and a path may hold them except U+0000.
- **Empty fields.** An empty head name, an empty list member, an empty key, `;;` and a trailing `;`
  or `,` are refused.
- **`--aihelp`.** Each option's reader is an `OptionArgumentReading<T>` whose `OptionArgumentType` is
  named by the option's shape (the Argument column below, as `user:password` names `--user`'s) and
  whose allowed-values text is the option's own keys followed by the one shared sentence: "fields
  percent-encode %, ; and the separator, and , inside a list; an unknown key or flag is refused".
  BL-645 builds the reader and the shared sentence; each option task builds its row.

### 4. The options

Stewart's proposed names are confirmed, with three refinements: `--topic-access` grants `pub`, `sub`
or `pubsub` (one grant per member, so both rights need a third word, not a repeated member);
`--share-access` grants `r` or `rw`, as proposed; and `--mailbox-file` repeats add, unlike
`--user-file`'s "last value wins", so hundreds of mailboxes can be split across files.

| Option | Argument | Keys and flags | Help categories | Default | Widens access | Secret |
| --- | --- | --- | --- | --- | --- | --- |
| `-K`, `--config` | `<file>` | | `surl` | none read | no | no |
| `--url` | `<listen URL>` | | `surl` | none | no | no |
| `--mail-domain` | `<domain>` | | `mail` | none: ADR-0050's store, the domain ignored | no | no |
| `--mailbox` | `<address>:<password>[;protocols=<scheme>,...][;quota=<bytes>][;folders=<name>,...]` (separator `:`) | `protocols`, `quota` (ADR-0007's `<bytes>` syntax), `folders` | `mail`, `auth` | none | no: it adds an account with a password | yes |
| `--mailbox-file` | `<file>` (CSV, below) | | `mail`, `auth` | none | no | no |
| `--mail-alias` | `<address>=<address>,...` (separator `=`) | | `mail` | none | no | no |
| `--mail-catch-all` | `<domain>=<address>` (separator `=`) | | `mail`, `security` | none | **yes**: mail to every address of the domain is accepted | no |
| `--shared-folder` | `<name>=<member>,...` (separator `=`) | | `mail` | none | no: only the members named | no |
| `--share` | `<name>=<path>[;readonly][;protocols=<scheme>,...]` (separator `=`) | `readonly`, `protocols` | `content` | none: one served root, ADR-0031 | no: who may use it is `--share-access`'s | no |
| `--share-access` | `<share>=<member>:<r\|rw>,...` (separator `=`; each list member split at its last `:`) | | `content` | none | no: only the members named | no |
| `--bucket` | `<name>=<path>` (separator `=`) | | `content` | none | no | no |
| `--topic` | `<filter>[;retain][;qos=<0\|1\|2>]` | `retain`, `qos` | `messages` | none | no | no |
| `--queue` | `<name>[;durable]` | `durable` | `messages` | none | no | no |
| `--topic-map` | `<scheme>:<name>=<scheme>:<name>` (separator `=`; each side split at its first `:`) | | `messages` | none: ADR-0100 decision 3.2's mapping | no | no |
| `--topic-access` | `<filter>=<member>:<pub\|sub\|pubsub>,...` (separator `=`; each list member split at its last `:`) | | `messages` | none | no: only the members named | no |
| `--group` | `<name>=<member>,...` (separator `=`) | | `auth` | none | no | no |

- `<scheme>` in `protocols=` and `--topic-map` is a scheme name in lower case. A scheme no registered
  server speaks (an extended scheme under `SURL_EXTENSIONS=0` included) is refused, so a mailbox
  restricted to a scheme that cannot log in is not silently unreachable.
- Whether a member-list option given a member that grants every caller (if BL-642 defines one) widens
  access is BL-642's to decide; if it does, that member is warned at start like `--mail-catch-all`.
- `--mail-catch-all` writes at every start, after the command line is read and before any
  `Listening on` line, in ADR-0032 section 9's order after `--self-signed`'s line, at the level
  ADR-0033 gives start-up warnings, one line per domain:
  `surl: warning: --mail-catch-all: every address at <domain> is delivered to <address>`.

**`--mailbox-file`** is RFC 4180 CSV: UTF-8 with an optional byte-order mark, records ending in CR LF
or LF, fields optionally double-quoted with `""` for a quote inside, a quoted field holding `,`, CR or
LF as data. The first record is the **header row**, naming columns from `address`, `password`,
`protocols`, `quota` and `folders` in any order and case; `address` and `password` are required, each
name at most once, and an unknown name is refused. Each later record is one mailbox, read exactly as
`--mailbox` reads the same keys, with no percent-encoding (CSV quoting does that job); an empty
`protocols`, `quota` or `folders` field means the key was not given, and `protocols` and `folders`
are `,`-lists inside a quoted field. Blank records are skipped; there are no comments (RFC 4180 has
none). The file is read when `surl` starts serving, before any listener binds, as `--user-file` is.

**The configuration model.** The layer-2 items are part of the one model every host fills: new
`SurlCommandLine` properties in `Surl.Cli`, each a list in command-line order, of records beside
`CommandLineAccount`:

| Property | Record |
| --- | --- |
| `ConfigFiles` | `string` (the paths read, for decision 5's warning) |
| `MailDomains` | `string` |
| `Mailboxes` | `ConfiguredMailbox(string Address, string Password, IReadOnlyList<string>? Protocols, long? QuotaBytes, IReadOnlyList<string> Folders)` |
| `MailboxFiles` | `string` |
| `MailAliases` | `ConfiguredMailAlias(string Address, IReadOnlyList<string> Targets)` |
| `MailCatchAlls` | `ConfiguredMailCatchAll(string Domain, string Address)` |
| `SharedFolders` | `ConfiguredSharedFolder(string Name, IReadOnlyList<ConfiguredMember> Members)` |
| `Shares` | `ConfiguredShare(string Name, string Path, bool IsReadOnly, IReadOnlyList<string>? Protocols)` |
| `ShareAccess` | `ConfiguredShareAccess(string Share, IReadOnlyList<ConfiguredGrant> Grants)` |
| `Buckets` | `ConfiguredBucket(string Name, string Path)` |
| `Topics` | `ConfiguredTopic(string Filter, bool IsRetained, int? MaxQos)` |
| `Queues` | `ConfiguredQueue(string Name, bool IsDurable)` |
| `TopicMaps` | `ConfiguredTopicMap(string FromScheme, string FromName, string ToScheme, string ToName)` |
| `TopicAccess` | `ConfiguredTopicAccess(string Filter, IReadOnlyList<ConfiguredGrant> Grants)` |
| `Groups` | `ConfiguredGroup(string Name, IReadOnlyList<ConfiguredMember> Members)` |

with `ConfiguredMember(string Name, bool IsGroup)` and `ConfiguredGrant(ConfiguredMember Member,
string Access)` (`Access` the word as given, lower-cased). A null `Protocols` means "not
restricted". The records hold what was written, decoded; meanings (expanding groups, matching
domains) belong to `Surl.Composition` and BL-642's ADR. `Surl.Api`'s layer-2 properties (BL-668) are
spellings of these options, as ADR-0099 decision 4 requires.

### 5. Repeats, duplicates, conflicts and refusals

**Repeats add**, for every option of decision 4 and for `-K` and `--url`. **The same name twice is
refused after the whole command line, every config file included, is read**, as `--user`'s duplicate
check (ADR-0032 section 1): a mailbox address (compared case-insensitively; a `--mailbox` and a
`--mailbox-file` row count together, the file's checked when it is read), a mail domain
(case-insensitively), an alias address, a catch-all domain, a shared-folder, share, bucket, queue,
topic filter, `--share-access` share, `--topic-access` filter or group name (ordinally), and a
`--topic-map` source (`<scheme>:<name>`). A member named twice in one list is refused.

**Conflicts**, also checked after the whole line is read:

- a `--mailbox` address that is also a `--user` name (the account it creates would be given twice);
- a `--mailbox`, `--mail-alias` or `--mail-catch-all` address, or catch-all domain, whose domain no
  `--mail-domain` names, and any of those three without a `--mail-domain`;
- a `--mail-alias` address that is also a mailbox;
- a `--share` and a `--bucket` with the same name (one top-level folder, ADR-0100 decision 2.1);
- a `--share-access` naming no `--share`.

A member naming no account or group, a group cycle, and a `--share` path that is missing are refused
at composition by the tasks that build them (BL-646, BL-663), with the exit code and text their
table rows here give.

**Refusals.** Every text follows `surl: `. Rows marked "try" are followed by ADR-0007's line `try
'surl --help' for more information`; the others, as `--user-file`'s (ADR-0032 section 2), are not.
`<name>` is the option as written without its value (`--mailbox`, or `--mailbox` for an undashed
`mailbox` line in a file). **No text ever holds an argument's value**: they name options, keys, files,
lines and the names the duplicate rows quote, never a password, path field or other value, so
`ArgumentHoldsSecret` holds for every one. A refusal inside a config file is the command-line
refusal's text prefixed by `<file>:<line>: `, `<file>` as given to `-K` or `config` (`<stdin>` for
`-`).

| # | Case | Exit | Text after `surl: ` | try |
| --- | --- | --- | --- | --- |
| 1 | A config file missing, a directory or unreadable | `CouldNotReadFile` (37) | `(37) Could not read config file <file>`, prefixed by `<including file>:<line>: ` when a `config` line named it | no |
| 2 | Larger than 16 MiB | `FailedInit` (2) | `<file>: the file is larger than 16777216 bytes` | yes |
| 3 | A line longer than 10 MiB | 2 | `<file>:<line>: the line is longer than 10485760 bytes` | yes |
| 4 | Not UTF-8 | 2 | `<file>:<line>: not UTF-8` | yes |
| 5 | Text after a value | 2 | `<file>:<line>: text follows the value of <name>; quote a value that holds a space` | yes |
| 6 | No closing quote | 2 | `<file>:<line>: the value of <name> has no closing double quote` | yes |
| 7 | Any command-line refusal of the option on the line (unknown option, requires parameter, bad value, ...) | 2 | `<file>:<line>: ` then ADR-0007's or the option's own text, e.g. `a.cfg:2: option --nosuchoption: is unknown` | yes |
| 8 | Nesting past 5 | 2 | `<file>:<line>: option --config: config files nest more than 5 deep` | yes |
| 9 | A cycle, or a second `-K -` | 2 | `<file>:<line>: option --config: <path> is already being read` (`<stdin>` for `-`); on the command line, without the prefix | yes |
| 10 | A malformed item | 2 | `option <name>: ` then one of: `expected <shape>` (the separator missing; `<shape>` the Argument column), `the name is empty`, `an empty field`, `unknown key <key>`, `unknown flag <flag>`, `key <key> is given twice`, `flag <flag> takes no value`, `key <key> needs a value`, `a malformed percent escape`, `not UTF-8 after percent decoding`, `a control character`, `unknown scheme <scheme>` (a scheme is not a secret), or a key's value refused as its type says (`quota` as ADR-0007's `<bytes>` row, `qos` `is badly used here`) | yes |
| 11 | An item past its length bound | 2 | `option <name>: the item is longer than 4096 characters` | yes |
| 12 | A list past its bound | 2 | `option <name>: more than 1000 members` | yes |
| 13 | An option past its count bound | 2 | `option <name>: given more than 10000 times` | yes |
| 14 | A duplicate | 2 | `option <name>: <noun> <value> is given twice`, `<noun>` one of `mailbox`, `mail domain`, `alias`, `catch-all domain`, `shared folder`, `share`, `bucket`, `topic`, `queue`, `topic map`, `group`, `member` (a password is never a `<value>`) | yes |
| 15 | A conflict | 2 | `option <name>: <noun> <value> ` then `is also a --user account`, `is not in any --mail-domain`, `needs --mail-domain`, `is also a mailbox`, `is also a --share`/`--bucket`, `names no --share` | yes |
| 16 | `--mailbox-file` missing or unreadable | 37 | `(37) Could not read mailbox file <file>` | no |
| 17 | A malformed `--mailbox-file` record | 2 | `(2) Mailbox file <file>, line <n>: ` then a header problem (`expected a header row with address and password`, `unknown column <name>`, `column <name> is given twice`), `not UTF-8`, `a quoted field has no closing quote`, `expected <k> fields`, or row 10's or 14's reason; `<n>` the physical line the record starts on; past 16 MiB or 10000 records `(2) Mailbox file <file> is larger than 16777216 bytes` / `holds more than 10000 mailboxes` | no |
| 18 | A member naming no account or group, a group cycle (BL-646) | 2 | `(2) option --<option>: member <member> names no account or group`, `(2) option --group: group <name> includes itself` | no |
| 19 | A `--share` path missing or not a directory (BL-663) | 37 | `(37) Could not open directory <path> for share <name>` (as ADR-0007's `--directory` row) | no |

Exit codes: 2 (`FailedInit`) for every configuration `surl` cannot act on and 37
(`CouldNotReadFile`) for a file it cannot open, ADR-0005's rows, as `--user-file` uses them. **Surl:**
curl exits 26 for an unreadable config file (M11, M12); 26 is curl's "read error" on a transfer's
upload source, which `surl` has never used, and `surl`'s files all use 37.

**A file readable by other users** is warned, because a config file or `--mailbox-file` may hold
passwords: on Linux and macOS, a `-K` file or `--mailbox-file` whose Unix mode grants group or other
read writes, at start with the other warnings and at their level,
`surl: warning: <file> is readable by other users and may hold passwords`. Windows ACLs have no
equivalent the BCL reads simply, so no warning there. `--user-file` gets the same warning (BL-684).

### 6. Bounds

| Bound | Value | Refusal |
| --- | --- | --- |
| Config file size | 16 MiB per file | row 2 |
| Config line length | 10 MiB, as curl | row 3 |
| Nesting depth | 5 files open at once, as curl | row 8 |
| Item length | 4096 characters before decoding (a path's length on Linux, with room for keys) | row 11 |
| Members in one list | 1000 | row 12 |
| Occurrences of one option | 10000, across the command line and every config file; `--mailbox` and the `--mailbox-file` records count together | row 13 |
| `--mailbox-file` | 16 MiB and 10000 records per file | row 17 |

10000 mailboxes of 100 bytes each are about 1 MB of configuration: far above "hundreds", far below
anything that strains a start. Each bound is a constant in `Surl.Cli`, not an option.

### 7. Help

- **New help categories:** `mail` ("Mail domains, mailboxes and aliases") and `messages` ("Topics,
  queues and topic access"), both with no schemes. Shares join `content`, groups join `auth`,
  `--config` and `--url` join `surl`, `--mail-catch-all` also joins `security`. So the `--aihelp`
  topic list in `AiHelpTextTests.Topics_AreTheAdrsTopicsAndEachProtocolAddedInOrdinalOrder` grows by
  exactly `mail` and `messages`, knowingly.
- **Each category lands with the task that makes its behaviour true:** `mail`, with its about text
  and `AiHelpExamples` entry, with BL-653's first mail option; `messages` with BL-666. `--config` and
  `--url` (BL-644), shares (BL-663) and `--group` (BL-646) add their options to existing topics, each
  with its `OptionHelp`, `ManualText`, `AiHelpProse` and, where its category needs one, an
  `AiHelpExamples` entry passing `CommandLineRunnerAiHelpTests`. No category or example is added
  before the behaviour it describes exists.

## Alternatives considered

- **A JSON or TOML configuration file.** JSON is in the BCL and would carry structure natively, but
  it is a second syntax beside the command line, every option would need a second spelling, and
  `surl` mirrors curl, whose users know `-K`. A file whose lines are the command line's own words is
  one surface, tested once.
- **Copying curl's leniency exactly** (ignored trailing text, an unterminated quote read to the end,
  counting only option lines). Rejected per row: each hides a mistake in a server's configuration
  where hiding it widens access or misleads the operator. The syntax curl accepts with a correct
  file is accepted unchanged.
- **The planning depth of 8.** Rejected for curl's measured 5: no reason to differ from what curl
  users meet, and 5 is ample.
- **Backslash escaping in items.** Rejected: Windows paths are full of backslashes, and the config
  file already gives backslashes a meaning inside quotes, so items would need double escaping there.
- **Ignoring unknown item keys** (forward compatibility). Rejected: secure by default; a typo must
  not drop `readonly`.
- **A default `.surlrc`.** Rejected (decision 2).

## Consequences

- `surl` can be configured from files in curl's syntax and with real-server items, through one model
  that `Surl.Composition`, `Surl.Api` and later `Surl.Mcp` all read. Upstream curl stays the only
  oracle: every syntax row is measured or quoted from 8.21.0, and every departure says why.
- `Record-CurlExchange.ps1` has `-ConfigFile`, so any later config-file measurement is one call.
- The build tasks, and where this ADR changes their Goal or option names:
  - **BL-644** builds `-K`/`--config` (decisions 1, 2, 5 rows 1 to 9, 6). Changes: it also adds
    `--url` (decision 1 row 12); the depth is 5, not 8; an unreadable file exits 37; the
    readable-by-others warning for config files is its.
  - **BL-645** builds the item reader: percent-encoding, not backslashes; unknown keys and flags
    refused; decision 5 rows 10 to 13.
  - **BL-646** builds `--group` (category `auth`), members as `@name`, rows 14, 15 and 18.
  - **BL-653** builds `--mail-domain`, `--mailbox`, `--mailbox-file` and adds the `mail` help
    category with its example. Changes: `--mailbox-file` repeats add; its CSV has a required header
    row; it warns when readable by other users.
  - **BL-654** builds `--mail-alias`, `--mail-catch-all` (also in `security`, warned) and
    `--shared-folder`, and the `quota` and `folders` keys.
  - **BL-663** builds `--share`, `--share-access` (`r`/`rw`) and `--bucket`, all in `content`, and
    row 19.
  - **BL-666** builds `--topic`, `--queue`, `--topic-map` and `--topic-access` and adds the `messages`
    help category with its example. Change: `--topic-access` grants `pub`, `sub` or `pubsub`.
  - **BL-668** adds the layer-2 properties to `SurlServerOptions` and `ConfigFiles` (decision 2).
  - **BL-684** (filed with this ADR) warns when `--user-file` is readable by other users.
- BL-642's ADR decides the meanings behind these names and may add keys; a new key is added to an
  option's row by the task that builds it, under this grammar.
- `surl` deliberately differs from curl where the rows say so; an operator moving a curl config habit
  over meets a refusal naming the line, never silently different behaviour.
