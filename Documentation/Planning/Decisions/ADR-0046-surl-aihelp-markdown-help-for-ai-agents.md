# ADR-0046 — `surl --aihelp [topic]`: Markdown help for AI agents, generated from the option table

- **Status:** Accepted
- **Date:** 2026-09-29
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-09-29,
  in BL-137. Stewart approved the feature on 2026-09-29 - a second help system beside `--help`,
  in Markdown, for an AI agent learning to call the surl command line; the details he left to
  this ADR.
- **Extends:** [ADR-0034](ADR-0034-curl-style-help-categories-and-the-manual.md): its decision 2
  option table and short list gain the `--aihelp` row (decision 2 below), its decision 1
  categories gain their schemes (decision 6), and its decision 6 `SEE ALSO` gains
  `surl --aihelp` (decision 10). Everything else in ADR-0034 stands.

## Context

Stewart's approval of 2026-09-29, summarised:

- `surl --aihelp` writes an overview: what surl is, how a command line is built, and the topic
  list. `surl --aihelp <topic>` writes one topic in depth; `surl --aihelp all` writes
  everything in one document. It is a help system an agent reads to use surl outright or to
  wrap it in a skill; it is **not** a skill-file generator.
- Topics: at least `auth`, `testing`, `logging`, `content`, `limits`, `exit-codes` and one per
  registered protocol, aligned with ADR-0034 decision 1's categories, plus whatever else an
  agent needs.
- Output: Markdown only (no JSON), precise and complete, one stable structure identical on
  every topic, no colour, no paging, stdout, exit 0.
- Every option with its argument type, default, allowed values, and whether it loosens
  security (the secure-by-default rule: loosening options are for tests only). Exact example
  command lines and what surl prints back. Every exit code with its meaning and what an agent
  should do next.
- Generated from the option table and categories `--help` uses, so the two cannot drift; tests
  fail when an option, a topic, a registered protocol or an exit code is missing.
- An unknown topic answered as `--help <unknown>` is (ADR-0034 decision 3).
- `--aihelp` appears in `--help`, `--help all` and its category.

Where the code is on 2026-09-29, all in `Surl.Cli.UnitLibrary` unless named:

- `CommandLineOptions.cs` is the option table: 42 `CommandLineOption` rows, each carrying
  `OptionHelp(ArgumentName, Description, Categories, IsInShortList, Default, Explanation)`
  (`OptionHelp.cs`). An option with an argument is built by `WithArgument<T>`, given the
  `OptionArgumentReader` method that reads it (`ReadSeconds`, `ReadNumber`, `ReadBytes`,
  `ReadPath`, `ReadText`, `ReadTlsVersion`, `ReadCertificateType`, `ReadKeyType`,
  `ReadLogLevel`, `ReadAccount`, `ReadAuthenticationMethods`); a flag by `Flag`; `help`,
  `version` and `manual` by `new(...)`. `CommandLineOptionKind` has `Help`, `Version`, `Manual`,
  `Flag` and `Argument` (`CommandLineOption.cs`).
- `HelpCategories.cs` lists 14 `HelpCategory(Name, Description)` rows (`HelpCategory.cs`), with
  no schemes. `HelpText.Answer(string? subject)` returns `HelpAnswer(Output, Error)`;
  `HelpLayout.cs` holds the 79-column rules; `ManualText.cs` holds `--manual`, `EXIT CODES`
  and `SEE ALSO` included.
- `CommandLineParser.cs` ends reading at the first of `-h`/`--help` (taking the next argument,
  or the rest of a `-h` bundle, as its subject), `-V`/`--version` or `-M`/`--manual`;
  `CommandLineParseResult.ShowHelp(string? subject)` carries `HelpSubject`;
  `CommandLineOutcome` has `Serve`, `ShowHelp`, `ShowManual`, `ShowVersion` and `Refused`.
- `SurlExitCode` (`Surl.Protocol.Abstractions.UnitLibrary/SurlExitCode.cs`) has `Ok` (0),
  `UnsupportedProtocol` (1), `FailedInit` (2), `MalformedUrl` (3), `CouldNotResolveHost` (6),
  `CouldNotWriteFile` (23), `CouldNotReadFile` (37), `BindFailed` (45), `CertificateProblem`
  (58), `CaCertificateBadFile` (77), `DataDirectoryInUse` (124) and `InternalError` (125).
  `Surl.Cli.UnitLibrary` already references that project.
- `Surl.Console/CommandLineRunner.cs` composes the registered servers in
  `ComposeProtocolServers` (schemes `http`, `https`, `dict`, `gopher`, `gophers`, `mqtt`,
  `mqtts`, `telnet`, `tftp`) and switches on `CommandLineOutcome`. `Surl.Cli` does not know
  which servers are registered. `Surl.Cli.UnitLibrary` grants `InternalsVisibleTo` to
  `Surl.Cli.UnitTests` only.
- `Surl.Console.UnitTests/FakeListenerFactory.cs` binds nothing and reports every listener
  bound on `FakeListenerFactory.BoundPort` (49731); `CommandLineRunner` takes the data
  directory probe and lock as delegates, so a test can make the lock answer
  `DataDirectoryLockOutcome.InUse`.

### What upstream curl 8.21.0 does

There is no upstream equivalent. Measured on 2026-09-29 with
`Record-CurlExchange.ps1 -NoServer -CurlArgs "--aihelp"` against the pinned reference build
`C:\Program Files\Git\mingw64\bin\curl.exe`, curl 8.21.0, SHA-256
`0E773709C3A44DB47B88B71351D902027682ED87C3BD3821009E454BACCA8778` (`UpstreamCurlBuilds.json`):

| Exit | stdout | stderr |
| --- | --- | --- |
| 2 | empty | `curl: option --aihelp: is unknown`, then `curl: try 'curl --help' or 'curl --manual' for more information` |

curl 8.21.0's option list is https://curl.se/docs/manpage.html; the measurement above is the
evidence this ADR rests on. `--aihelp` is therefore a **deliberate addition with no upstream
curl equivalent**: nothing in it mirrors a curl option, so where it borrows a rule from
`--help` it says so and why.

## Decision

### 1. Parsing and the result shape

- **Long name `aihelp`, no short name.** Every free letter may be claimed later by a short
  option that mirrors one of curl's, and a letter spent on an option curl does not have would
  then block it.
- **The optional topic is read exactly as `--help` reads its subject** (ADR-0034 decision 4),
  so an agent that has learned one has learned both: `--aihelp=<topic>` takes `<text>`
  (`--aihelp=` is an empty topic); `--aihelp` without `=` takes the next argument as the topic
  whatever it is - an option, a listen URL, `--` or an empty string - and has no topic when
  none follows. An empty topic is not refused: it means the overview.
- **Reading ends once the topic is taken.** Nothing after it is read: `--aihelp mqtt --nosuch`
  answers `mqtt`, exit 0.
- **`--no-aihelp` is refused** as `--no-help` is: `aihelp` is not negatable, so the parser's
  existing rule writes `surl: option --no-aihelp: the given option cannot be reversed with a
  --no- prefix` and the `try` line, exit `FailedInit` (2).
- **Precedence**, mirroring ADR-0034 decision 4: reading is left to right, and the first of
  `-h`/`--help`, `-V`/`--version`, `-M`/`--manual` and `--aihelp` that is read ends it, with no
  priority among the four; an error read before any of them still wins. So `-V --aihelp` shows
  the version, `-M --aihelp` the manual, `-h --aihelp` the `--help --aihelp` option page, and
  `--aihelp -h` the `--aihelp` answer for the topic `-h` (decision 8). `--nosuch --aihelp`
  exits 2 with `surl: option --nosuch: is unknown`; `http://127.0.0.1:0/ --aihelp` writes the
  overview, as a valid listen URL before `--help` does.
- **Not log output.** As help (ADR-0033 section 1, ADR-0034 decision 3), every `--aihelp`
  answer is written whatever `-s` or `--log-level` says: `-s --aihelp` writes the overview.
- **The shapes BL-141 adds:**

```csharp
// Surl.Cli.UnitLibrary/CommandLineOutcome.cs
public enum CommandLineOutcome { Serve, ShowHelp, ShowAiHelp, ShowManual, ShowVersion, Refused }

// Surl.Cli.UnitLibrary/CommandLineParseResult.cs
public sealed class CommandLineParseResult
{
    // The --aihelp result, as ShowHelp(string?) is the --help one.
    public static CommandLineParseResult ShowAiHelp(string? topic);

    // The topic when Outcome is ShowAiHelp: null when none was given or it was empty,
    // otherwise the argument exactly as written. Null for every other outcome.
    public string? AiHelpTopic { get; }
}

// Surl.Cli.UnitLibrary/CommandLineOption.cs
internal enum CommandLineOptionKind { Help, AiHelp, Version, Manual, Flag, Argument }
```

  `CommandLineParser` treats `CommandLineOptionKind.AiHelp` as it treats `Help` for a long
  option (`ApplyOption` takes the attached text or the next argument); with no short name, the
  bundle path never meets it. `Surl.Console/CommandLineRunner.cs` answers `ShowAiHelp` beside
  `ShowHelp`, writing `AiHelpText.Answer(parsed.AiHelpTopic)`'s `Output` and `Error` as
  `WriteHelp` does and returning `SurlExitCode.Ok`.

### 2. Its `--help` row

| Left side | Description | Categories | Short | Default |
| --- | --- | --- | --- | --- |
| `--aihelp <topic>` | `Markdown help for AI agents` | surl | yes | |

- The description is 27 characters, inside ADR-0034 decision 2's limit of 34; the left side,
  `    --aihelp <topic>`, is 20 characters, below the 42 that set that limit. `<topic>` is the
  argument name, as `<subject>` is `--help`'s.
- **Categories: `surl` only** - "The command line tool itself", the home of `-h`, `-M` and
  `-V`. It widens nothing and limits nothing, so no other category holds it.
- **In the short list**, because an agent that runs `surl --help` first must see that the
  agent-facing help exists. **No default** (`Default: null`), as `--help` has none: an absent
  topic is not a value. **No `Explanation`**: it loosens nothing.
- It sorts first by ordinal long name (`aihelp` before `allow-uploads`), and moves no column:
  every page's longest left side is unchanged.

The short list (`surl --help`) becomes, every line as ADR-0034's column rule lays it out:

```
Usage: surl [options...] <url>...
     --aihelp <topic>         Markdown help for AI agents
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

`surl --help surl` becomes:

```
surl: The command line tool itself
     --aihelp <topic>  Markdown help for AI agents
 -h, --help <subject>  Get help for commands
 -M, --manual          Display the full manual
 -V, --version         Show version number and quit
```

`surl --help all` gains, as its first line, with its description in column 46 as every other:

```
     --aihelp <topic>                        Markdown help for AI agents
```

`surl --help --aihelp` is its option page by ADR-0034 decision 3:

```
    --aihelp <topic>
        Markdown help for AI agents.

        Categories: surl.

```

The pointer lines and the category list do not change: `--aihelp` is an option, not a
category.

### 3. The topics

Sixteen topics, in ordinal order of the name, the same order rule as ADR-0034's categories:

| Topic | Description (the page title) | Schemes | Comes from |
| --- | --- | --- | --- |
| `auth` | Accounts and authentication methods | | category |
| `content` | Served files and the data directory | | category |
| `dict` | DICT protocol | `dict` | category |
| `exit-codes` | Exit codes and what to do next | | added here |
| `gopher` | GOPHER and GOPHERS protocol | `gopher`, `gophers` | category |
| `http` | HTTP and HTTPS protocol | `http`, `https` | category |
| `limits` | Connection, time and size limits | | category |
| `listen-urls` | Listen URLs, ports and the Listening on line | | added here |
| `logging` | Log levels, tracing and the log file | | category |
| `mqtt` | MQTT and MQTTS protocol | `mqtt`, `mqtts` | category |
| `security` | Options that widen what a peer may do | | category |
| `surl` | The command line tool itself | | category |
| `telnet` | TELNET protocol | `telnet` | category |
| `testing` | Loosening options for tests (warned) | | category |
| `tftp` | TFTP protocol | `tftp` | category |
| `tls` | TLS certificates and versions | | category |

- **Every `--help` category is a topic**, with the category's name and description, so the two
  help systems name one thing one way. ADR-0034's 14 keep their relative order.
- **`exit-codes`** holds the whole exit-code table (decision 6); an agent that got a code
  needs it without knowing which topic caused it.
- **`listen-urls`** covers what no category owns and every agent needs first: the listen-URL
  syntax, the default port per scheme, port 0 and reading the bound port from the
  `Listening on` line, several listen URLs, UDP for `tftp`, and stopping with Ctrl+C or
  SIGTERM. No option belongs to it.
- **No other topic.** How a command line is built is the overview's (decision 4); `all` and
  `category` are not topics (`all` is decision 4's document; `category` gets decision 8's
  answer, which lists the topics).
- **A protocol category is a protocol topic.** A protocol category is one whose `Schemes`
  (decision 6) is not empty. The task that registers a protocol server in `Surl.Console` adds,
  in the same change, its category row with its schemes (ADR-0034 decision 1's rule, now with
  the schemes), its topic's `About` text and its example (decision 7); the completeness tests
  (decision 9) fail until it does.
- **Matching:** `StringComparison.OrdinalIgnoreCase`, as ADR-0034 decision 3 point 3 matches
  categories: `--aihelp MQTT` and `--aihelp All` are answered.

### 4. The one Markdown structure

**Every topic page** is exactly these lines, the five `##` sections always present and always
in this order:

````markdown
# surl --aihelp <topic>: <description>

## About

<the topic's paragraphs>

## Schemes

<the scheme table>

## Options

<the option table, then each listed option's Explanation>

## Exit codes

<the exit-code table>

## Examples

<the examples>
````

- The title names the command that writes the page, so each page is self-describing inside
  `all`.
- **A section with nothing in it** holds the one line `Nothing for this topic.` - never
  omitted, so every page has the same headings and a parser needs no special case.
- **`About`**: the topic's hand-written paragraphs (decision 6), each one line, separated by
  one empty line; a list item is one line starting `- `.
- **`Schemes`** (generated): `| Scheme | Default port |`, one row per scheme of the topic, the
  scheme in backticks and its port from `SchemeDefaultPorts`; `Nothing for this topic.` for a
  topic with no schemes.
- **`Options`** (generated): decision 5's table of the options whose `Categories` hold the
  topic, in ordinal order of the long name; then, for each of those options that has an
  `Explanation`, in the same order, an empty line and one line `` `--<name>`: <Explanation> ``.
  `Nothing for this topic.` when no option has the topic (`exit-codes`, `listen-urls`).
- **`Exit codes`** (generated): `| Code | Name | Meaning | What to do next |`, the rows of
  decision 6's table whose topics hold this topic, in numeric order; every row on
  `exit-codes`. The name is the `SurlExitCode` member's, in backticks.
- **`Examples`** (from decision 7's data), each example as:

````markdown
### <title>

Given: <precondition>.

```
surl <arguments>
```

stdout:

```
<each stdout line>
```

stderr:

```
<each stderr line>
```

Exit code: <number> (`<SurlExitCode member>`)<, once stopped with Ctrl+C or SIGTERM>.

Reach it with upstream curl:

```
curl <arguments>
```
````

  The `Given:` paragraph appears only for an example with a precondition. A stream the example
  writes nothing to is the one line `stdout: nothing.` or `stderr: nothing.` instead of its
  label and fence. The curl paragraph appears only for an example that has a curl command line
  (decision 7). Fences carry no info string.

**The overview** (`surl --aihelp`, and `--aihelp=` or `--aihelp ""`) is:

````markdown
# surl --aihelp: Overview and topic list

## About

<what surl is>

## Command line

<how a command line is built>

## Conventions

<how to read these pages>

## Topics

| Topic | Covers |
| --- | --- |
| `auth` | Accounts and authentication methods |
...one row per topic, in decision 3's order...

Run `surl --aihelp <topic>` for one topic, or `surl --aihelp all` for this overview and every topic.

## Examples

<the overview's example>
````

- `About` says what surl is (the server-side mate of curl: for each request upstream curl
  makes, the server that answers it) and that `surl --version` names the schemes this build
  serves. `Command line` gives `surl [options] <url>...`: options and listen URLs in any order,
  read left to right, the first error ends reading and nothing is served; `--` ends options;
  `--name=value` or `--name value`; `-x` bundles such as `-vm30`; `--no-<name>` on a negatable
  flag; a refusal is a `surl:` line and the `try` line on stderr. `Conventions` explains the
  columns of decision 5 and decision 6's tables, that `<port>` and `<path>` in an example are
  placeholders for the bound port and a directory, and that help and `--aihelp` are written at
  every log level.
- The topic table's rows are generated from the topic list (decision 6); the prose sections
  are hand-written (decision 6).

**`all`** is the overview, then each topic page in decision 3's order, each followed by one
empty line before the next; the last page ends with its own last line. Headings are **not**
demoted: every page in `all` is byte for byte the page `--aihelp <topic>` writes, so a test can
assert `all` is their concatenation, and an agent can cut `all` at each `# ` line.

**Lines:** `Environment.NewLine` after every line, the last included (ADR-0034 decision 3); no
line has trailing spaces; no TAB; no ANSI escape; no colour and no paging. **No wrap width:** a
Markdown table row cannot be wrapped without breaking the table, and a paragraph is reflowed by
any Markdown reader anyway, so each paragraph, list item and table row is one line of whatever
length it needs. The 79-column rule is `--help`'s, for a terminal; `--aihelp` is for a parser.

**Table cells:** every table row starts `| `, separates cells with ` | ` and ends ` |`; the
header is followed by `| --- |` with one `---` per column. A `|` inside any cell is written
`\|`, in a code span too, as GitHub Flavored Markdown's table extension requires
(https://github.github.com/gfm/#tables-extension-). **Backticks:** the generator puts exactly
these values in single backticks - the option (decision 5's first column), the topic in the
topic table, the scheme and the `SurlExitCode` name - and writes every other cell as plain
text. No fact text the generator reads (every `OptionHelp`, `OptionArgumentType`,
`HelpCategory` and `ExitCodeGuidance` text) may hold a backtick, a line break or a `|`, a rule
a test enforces (decision 9), so the generator never has to escape anything but that rule's
guard, and a fact cannot break a row.

### 5. The option table

`| Option | Argument type | Default | Allowed values | Loosens security | Categories | Description |`

| Column | Written as |
| --- | --- |
| Option | ADR-0034's left side without its padding, in backticks: `` `-m, --max-time <seconds>` ``, `` `--max-line <bytes>` ``, `` `--aihelp <topic>` `` |
| Argument type | The option's `OptionArgumentType.Name` (vocabulary below) |
| Default | `OptionHelp.Default` as written (`off`, `1024`, `in memory`); `not applicable` when it is null |
| Allowed values | The option's `OptionArgumentType.AllowedValues`; for an option in the `limits` category, followed by `; 0 means no limit` |
| Loosens security | `yes, for tests only` for an option in the `testing` category; otherwise `yes, widens what a peer may do` for an option in the `security` category; otherwise `no` |
| Categories | The option's categories, ordinal order, joined with `, ` |
| Description | `OptionHelp.Description` |

**The vocabulary**, one row per `OptionArgumentReader` method, and one per option kind that has
no reader:

| Read by | Argument type | Allowed values |
| --- | --- | --- |
| `ReadSeconds` | `seconds` | `0 to 2147483.647, digits with an optional decimal point and more digits` |
| `ReadNumber` | `number` | `0 to 2147483647, digits only` |
| `ReadBytes` | `bytes` | `digits with an optional decimal point and more digits, then at most one suffix k, m, g, t or p in either case, each 1024 times the one before; at most 9223372036854775807 bytes` |
| `ReadPath` | `path` | `any non-empty text` |
| `ReadText` | `text` | `any text, the empty string included` |
| `ReadTlsVersion` | `TLS version` | `1.0, 1.1, 1.2 or 1.3` |
| `ReadCertificateType` | `word` | `PEM, DER or P12, in any case` |
| `ReadKeyType` | `word` | `PEM or DER, in any case` |
| `ReadLogLevel` | `word` | `none, error, info, verbose or trace, in any case` |
| `ReadAccount` | `user:password` | `a user name, a colon and a non-empty password, split at the first colon; no control character in the user name` |
| `ReadAuthenticationMethods` | `word list` | `comma-separated, in any case, no empty item: ` then `OptionArgumentReader.AuthenticationMethodWords` joined with `, ` (today `negotiate, ntlm, digest, basic, bearer, aws-sigv4`) |
| `Flag`, negatable | `flag` | `--no-<name> turns it off`, the option's own name in place of `<name>` |
| `Flag`, not negatable | `flag` | `no --no- form` |
| `Help` | `optional subject` | `a category, all, category or an option; see surl --help category` |
| `AiHelp` | `optional topic` | `a topic or all; see surl --aihelp` |
| `Version`, `Manual` | `none` | `none` |

- **Why derived from the reader:** the reader is what accepts or refuses the argument, so text
  bound to the reader cannot say an option takes what its reader refuses. The texts above were
  checked on 2026-09-29 against `OptionArgumentReader.cs` (`HighestSeconds` is
  `int.MaxValue / 1000m`; `ReadNumber` uses `NumberStyles.None`; `ReadBytes` refuses above
  `long.MaxValue`; `ReadLogLevel`, `ReadCertificateType` and `ReadKeyType` use
  `OrdinalIgnoreCase`; `ReadTlsVersion` is ordinal and exact). BL-138 pins each at its edges.
- **`0 means no limit` from the `limits` category**, not from the reader: every option read by
  `ReadSeconds`, `ReadNumber` or `ReadBytes` today is in `limits`, and each is 0 for no limit
  (ADR-0006 section 1; `ManualText.cs`, `LIMITS`), but that is a fact about the option, which
  the category states, not about the reader.
- **What an option does with a `-`** (`--trace -`, `--log-file -` for stdout) or its other
  meanings are the topic's `About` text, not the vocabulary: the vocabulary says what the
  reader accepts.
- **Loosening, derived from the categories.** The four `testing` options (`--allow-anonymous`,
  `--allow-plaintext-auth`, `--auth`, `--self-signed`) are `yes, for tests only`: each loosens
  a secure default, writes a `surl: warning:` line at start, and has an `Explanation` saying
  why it is not the default (ADR-0032 section 9; ADR-0034 decision 5). The other `security`
  options (`--allow-uploads`, `--list-directories`, `--follow-symlinks`, `--serve-dot-files`,
  `--tlsv1.0`, `--tlsv1.1`) are `yes, widens what a peer may do` - the `security` category's
  own words - because each is a legitimate deployment choice (ADR-0034's deployment checklist
  leaves them off "unless they are needed") that writes no warning; calling it "for tests only"
  would be false. Every other option is `no`.

### 6. Where each fact lives

Every fact has one home in `Surl.Cli.UnitLibrary`, and `--help` and `--aihelp` read the same
one:

| Fact | Where | Added by |
| --- | --- | --- |
| Option names, short names, kind, negatable | `CommandLineOption` rows in `CommandLineOptions.cs`, as today | - |
| Argument name, description, categories, short list, default, explanation | `OptionHelp`, as today | - |
| Argument type and allowed values | `internal sealed record OptionArgumentType(string Name, string AllowedValues)` in `OptionArgumentType.cs`. `OptionArgumentReader` pairs each read method with its `OptionArgumentType` once, as a static member of `internal sealed record OptionArgumentReading<T>(ReadArgument<T> Read, OptionArgumentType Type)` (`OptionArgumentReading.cs`, which takes over the `ReadArgument<T>` delegate `CommandLineOptions` declares privately today); `WithArgument<T>` takes that pairing instead of the bare method. `CommandLineOption` gains `OptionArgumentType ArgumentType`, set by `WithArgument<T>` from the pairing, by `Flag` from `Negatable`, and given on the `help`, `aihelp`, `version` and `manual` rows, so no row lacks one. | BL-138 |
| The `limits` suffix and loosens security | Derived by the generator from `OptionHelp.Categories` (decision 5); no field | BL-139 |
| A category's schemes | `HelpCategory` gains a third member, `IReadOnlyList<string> Schemes`: `dict` [`dict`], `gopher` [`gopher`, `gophers`], `http` [`http`, `https`], `mqtt` [`mqtt`, `mqtts`], `telnet` [`telnet`], `tftp` [`tftp`], empty for the rest. `--help` does not print it, so no `--help` page changes. | BL-138 |
| The topic list | `public sealed record AiHelpTopic(string Name, string Description, IReadOnlyList<string> Schemes)` in `AiHelpTopic.cs`; `public static class AiHelpTopics` in `AiHelpTopics.cs` with `All` (every `HelpCategories.All` row, plus `exit-codes` and `listen-urls`, in ordinal order) and `TryFind(string, out AiHelpTopic?)`. Public, so `Surl.Console.UnitTests` reads the schemes without a new `InternalsVisibleTo`. | BL-139 |
| Exit-code guidance | `internal sealed record ExitCodeGuidance(SurlExitCode Code, string Meaning, string NextStep, IReadOnlyList<string> Topics)` in `ExitCodeGuidance.cs`; the rows in `internal static class ExitCodeGuidanceTable` (`ExitCodeGuidanceTable.cs`), `All`, one row per `SurlExitCode` member in numeric order. The number and name are the member's (`(int)Code`, `Enum.GetName`); `Topics` are the topics whose `Exit codes` section lists the row. | BL-138 |
| The generator | `public static class AiHelpText` in `AiHelpText.cs`: `public static HelpAnswer Answer(string? topic)`, returning the `HelpAnswer` record `--help` uses (`Error` always empty). The option's left side is shared with `HelpText`, not copied. | BL-139 |
| Hand-written prose | `internal static class AiHelpProse` in `AiHelpProse.cs`: the overview's `About`, `Command line` and `Conventions` paragraphs and each topic's `About` paragraphs, as `string[]` source constants - a source constant, as `ManualText.cs` is (ADR-0034 decision 6), for the same reasons: no resource-reading path under native AOT, nothing to fail, and it changes in the same diff as the code it describes. | BL-140 |
| Examples | `public sealed record AiHelpExample(string Topic, string Title, AiHelpExamplePrecondition Precondition, IReadOnlyList<string> Arguments, IReadOnlyList<string> Output, IReadOnlyList<string> Error, SurlExitCode ExitCode, bool ServesUntilStopped, IReadOnlyList<string> CurlCommandLines)` in `AiHelpExample.cs`; `public enum AiHelpExamplePrecondition { None, DataDirectoryExists, DataDirectoryHeldByAnotherSurl }`; `public static class AiHelpExamples` in `AiHelpExamples.cs` with `All`. `Topic` is the name of the topic whose page shows the example, or `overview` for the overview's own (not a topic name, so it cannot clash with one). Public, so `Surl.Console.UnitTests` runs them. | BL-140 |

**The exit-code guidance rows.** Meanings restate `ManualText.cs`'s `EXIT CODES`, which BL-123
checked against the code; BL-138 checks each meaning again against where `CommandLineRunner`
and `CommandLineParser` return the code and changes a word the code has made untrue rather than
landing it.

| Code | Name | Meaning | What to do next | Topics |
| --- | --- | --- | --- | --- |
| 0 | `Ok` | Help, the manual or the version was written, or surl was stopped by Ctrl+C or SIGTERM | Nothing: surl succeeded, or stopped cleanly when asked | `listen-urls`, `surl` |
| 1 | `UnsupportedProtocol` | A listen URL names a scheme this build does not serve | Run surl --version to list the schemes this build serves, and use one of them | `listen-urls` |
| 2 | `FailedInit` | The command line cannot be used: an option or its argument refused, no listen URL, a malformed --user-file, or a --cacert file that does not exist | Read the surl: line on stderr, which names what was refused, and fix it; the option tables give each option's allowed values | `auth`, `limits`, `surl`, `tls` |
| 3 | `MalformedUrl` | A listen URL is malformed | Write the listen URL as scheme://host[:port][/], with no user name, path, query or fragment | `listen-urls` |
| 6 | `CouldNotResolveHost` | A listen URL names a host that resolves to nothing | Use an IP address such as 127.0.0.1, or a host name that resolves | `listen-urls` |
| 23 | `CouldNotWriteFile` | The .surl folder or its lock file cannot be created, a log or trace file cannot be opened, or the trace file is the --log-file file | Make the data directory writable by the user surl runs as, or give a log or trace file that can be opened and is not the --log-file file | `content`, `logging` |
| 37 | `CouldNotReadFile` | The data directory cannot be opened, or the --user-file or the MQTT retained-message file cannot be read | Check the path exists and the user surl runs as can read it; surl creates neither | `auth`, `content`, `mqtt` |
| 45 | `BindFailed` | A listener cannot bind its address and port | Use another port, or port 0 and read the bound port from the Listening on line, and an address this machine has | `listen-urls` |
| 58 | `CertificateProblem` | A secure listen URL has no certificate, or the --cert or --key file cannot be used | Give --cert and --key files that load, with --pass when the key needs one, or --self-signed in a test | `tls` |
| 77 | `CaCertificateBadFile` | The --cacert file cannot be read | Give a --cacert file that holds certificates | `tls` |
| 124 | `DataDirectoryInUse` | Another surl holds the data directory | Stop the other surl, give another --directory, or serve in memory without --directory | `content` |
| 125 | `InternalError` | surl failed while serving | Report it with the command line and the stderr line: it is a defect in surl | `surl` |

BL-141, which makes `--aihelp` write something, adds `--aihelp` to row 0's meaning ("Help,
--aihelp, the manual or the version was written, ...") in the same change, so no row is ever
untrue.

### 7. Examples

Each example below is exact: its arguments, its precondition, every line it writes to stdout
and to stderr, its exit code, and the curl command line shown after it. `<port>` stands for the
bound port and `<path>` for a directory; an agent substitutes them, and the proof below does.

| # | Topic | Title | `surl` arguments | Given | stdout | stderr | Exit | curl command line shown |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 1 | overview | Serve HTTP on an ephemeral port | `http://127.0.0.1:0/` | | `Listening on http://127.0.0.1:<port>/` | | 0, once stopped | `curl http://127.0.0.1:<port>/` |
| 2 | `listen-urls` | Serve two listen URLs | `http://127.0.0.1:0/ tftp://127.0.0.1:0/` | | `Listening on http://127.0.0.1:<port>/`, then `Listening on tftp://127.0.0.1:<port>/` | | 0, once stopped | |
| 3 | `listen-urls` | A scheme this build does not serve | `ftp://127.0.0.1:0/` | | | `surl: (1) Protocol "ftp" not supported` | 1 | |
| 4 | `surl` | No listen URL | (none) | | | `surl: (2) no URL specified`, then `surl: try 'surl --help' or 'surl --manual' for more information` | 2 | |
| 5 | `content` | Serve in memory, with listings | `--list-directories http://127.0.0.1:0/` | | `Listening on http://127.0.0.1:<port>/` | | 0, once stopped | `curl http://127.0.0.1:<port>/` |
| 6 | `content` | Serve a data directory | `--directory <path> http://127.0.0.1:0/` | `<path>` is an existing directory no other surl holds | `Listening on http://127.0.0.1:<port>/` | | 0, once stopped | `curl http://127.0.0.1:<port>/<file>` |
| 7 | `content` | A data directory another surl holds | `--directory <path> http://127.0.0.1:0/` | another surl is serving `<path>` with `--directory` | | `surl: (124) Directory <path> is in use by another surl process` | 124 | |
| 8 | `auth` | Require a login | `-u alice:secret http://127.0.0.1:0/` | | `Listening on http://127.0.0.1:<port>/` | | 0, once stopped | `curl --digest -u alice:secret http://127.0.0.1:<port>/` |
| 9 | `testing` | For a test: accept a password over plain HTTP | `--allow-plaintext-auth -u alice:secret http://127.0.0.1:0/` | | `Listening on http://127.0.0.1:<port>/` | `surl: warning: --allow-plaintext-auth: passwords and tokens are accepted over unencrypted connections` | 0, once stopped | `curl -u alice:secret http://127.0.0.1:<port>/` |
| 10 | `tls` | For a test: serve HTTPS with a throwaway certificate | `--self-signed https://127.0.0.1:0/` | | `Listening on https://127.0.0.1:<port>/` | `surl: warning: --self-signed: serving a throwaway certificate; clients must skip verification (curl -k)` | 0, once stopped | `curl -k https://127.0.0.1:<port>/` |
| 11 | `tls` | A secure listen URL with no certificate | `https://127.0.0.1:0/` | | | `surl: (58) https://127.0.0.1:0/ needs a certificate: give --cert <file>, or --self-signed for a throwaway one` | 58 | |
| 12 | `logging` | Silent mode writes nothing | `-s http://127.0.0.1:0/` | | | | 0, once stopped | |
| 13 | `limits` | An argument the option refuses | `--max-time abc http://127.0.0.1:0/` | | | `surl: option --max-time: expected a proper numerical parameter`, then `surl: try 'surl --help' or 'surl --manual' for more information` | 2 | |
| 14 | `dict` | Serve DICT | `dict://127.0.0.1:0/` | | `Listening on dict://127.0.0.1:<port>/` | | 0, once stopped | `curl dict://127.0.0.1:<port>/d:surl` |
| 15 | `gopher` | Serve Gopher | `gopher://127.0.0.1:0/` | | `Listening on gopher://127.0.0.1:<port>/` | | 0, once stopped | `curl gopher://127.0.0.1:<port>/` |
| 16 | `http` | Serve HTTP | `http://127.0.0.1:0/` | | `Listening on http://127.0.0.1:<port>/` | | 0, once stopped | `curl -I http://127.0.0.1:<port>/` |
| 17 | `mqtt` | For a test: serve MQTT without accounts | `--allow-anonymous mqtt://127.0.0.1:0/` | | `Listening on mqtt://127.0.0.1:<port>/` | `surl: warning: --allow-anonymous: every request and login is accepted without checking credentials` | 0, once stopped | `curl mqtt://127.0.0.1:<port>/example` |
| 18 | `telnet` | Serve TELNET | `telnet://127.0.0.1:0/` | | `Listening on telnet://127.0.0.1:<port>/` | | 0, once stopped | `curl telnet://127.0.0.1:<port>/` |
| 19 | `tftp` | Serve TFTP | `tftp://127.0.0.1:0/` | | `Listening on tftp://127.0.0.1:<port>/` | | 0, once stopped | `curl tftp://127.0.0.1:<port>/example.txt` |

- `exit-codes` and `security` have no example: their `Examples` section is
  `Nothing for this topic.`. Every other topic and the overview has at least one.
- **Where the texts come from, checked on 2026-09-29:** the `Listening on` line is
  `ListenerStatusLine.Format` (`Surl.Output.UnitLibrary/ListenerStatusLine.cs`); `(1)` and
  `(58)` are `CommandLineRunner.FindListenUrlRefusal` and `FormatMissingCertificate`; `(124)`
  is `DataDirectoryLockOutcome.InUse` (`Surl.Console/DataDirectoryLockOutcome.cs`), which names
  the directory as given; `(2) no URL specified` is `CommandLineParser.NoUrlSpecified`; the
  `try` line is `CommandLineFailure.TryHelpLine`; `expected a proper numerical parameter` is
  `OptionArgumentReader.NotANumber`; the two warnings are
  `AuthenticationComposition.WriteLooseningWarnings` and
  `CommandLineRunner.WriteThrowawayCertificateLines`. Every `surl:` line has the `surl: `
  prefix `CommandLineRunner` writes. `-s` writes neither the `Listening on` line nor a warning
  (ADR-0033 section 1).
- **A serving example** (`ServesUntilStopped`) keeps serving until stopped; its exit code is
  `Ok` (0) once stopped with Ctrl+C or SIGTERM.
- **curl command lines are shown, not their output.** Each is one pinned curl 8.21.0 accepts
  (https://curl.se/docs/manpage.html), chosen to reach the server just started (examples 8
  and 9 use Digest and Basic because a login over `http://` needs Digest unless
  `--allow-plaintext-auth` is given, ADR-0032). What curl then prints depends on what is
  served and is the conformance tests' to pin, against upstream curl only (ADR-0003); a unit
  test cannot run curl without a network.
- **How each shown output is proved** (BL-142): a test in `Surl.Console.UnitTests` runs every
  `AiHelpExamples.All` entry's arguments through `CommandLineRunner.RunAsync` with
  `FakeListenerFactory` (no socket; NFR-001), `<path>` replaced by a new directory under
  `Path.GetTempPath()`, the data-directory lock delegate answering
  `DataDirectoryLockOutcome.InUse(<that path>)` for `DataDirectoryHeldByAnotherSurl`, and a
  serving example cancelled once `FakeListenerFactory.AcceptStarted` completes. It compares the
  written stdout and stderr lines, and the exit code, with the example's, after replacing
  `<port>` with `FakeListenerFactory.BoundPort` and `<path>` with the directory.

### 8. An unknown topic

A topic that is not `all` and names no topic (`--aihelp nosuch`, `--aihelp category`,
`--aihelp http://127.0.0.1:1/`) gets, on stdout, exit `Ok` (0), nothing on stderr:

````markdown
# surl --aihelp: unknown topic

Unknown topic provided, here is a list of all topics:

| Topic | Covers |
| --- | --- |
| `auth` | Accounts and authentication methods |
...one row per topic, in decision 3's order, the overview's topic table exactly...
````

- It mirrors `--help <unknown>` (ADR-0034 decision 3): the same stream, the same exit code,
  curl's line with `topic` for `category`, and the list; in Markdown, with the title every
  `--aihelp` answer starts with, so an agent can tell which answer it got from the first line.
  The topic as given is not echoed, as curl echoes no unknown category.
- **An option-like topic** (`--aihelp --user`, `--aihelp -h`, `--aihelp --`) gets the same
  unknown-topic answer, on stdout, exit 0. Unlike `--help`, `--aihelp` has no per-option page
  and so no separate incorrect-option answer: every option is a row in the tables of each of
  its categories' topics and of `all`, and the topic list is the way back for an agent that
  guessed wrong. One rule, one answer.

### 9. Completeness tests

In `Surl.Cli.UnitTests` (options, topics and exit codes; `Surl.Cli` holds all three):

| Test class (file) | Test method | Fails when | Task |
| --- | --- | --- | --- |
| `AiHelpFactsTests` (`AiHelpFactsTests.cs`) | `EveryOption_HasAnArgumentTypeAndAllowedValues` | an option in `CommandLineOptions.All` has no `ArgumentType`, or an empty `Name` or `AllowedValues` | BL-138 |
| `AiHelpFactsTests` | `LoosensSecurityForTestsOnly_IsExactlyTheFourTestingOptions` | the options in `testing` are not exactly `--allow-anonymous`, `--allow-plaintext-auth`, `--auth` and `--self-signed`, or one lacks an `Explanation` | BL-138 |
| `AiHelpFactsTests` | `ExitCodeGuidance_HasExactlyOneRowPerSurlExitCodeMember` | `ExitCodeGuidanceTable.All` does not hold exactly one row per `Enum.GetValues<SurlExitCode>()` member, each with a non-empty meaning and next step | BL-138 |
| `AiHelpFactsTests` | `EveryProtocolCategoryScheme_HasADefaultPort` | a scheme in a `HelpCategory.Schemes` is not in `SchemeDefaultPorts`, or is claimed by two categories | BL-138 |
| `AiHelpFactsTests` | `FactTexts_HoldNoBacktickPipeOrLineBreak` | any `OptionHelp`, `OptionArgumentType`, `HelpCategory` or `ExitCodeGuidance` text holds a backtick, `\|`, CR or LF | BL-138 |
| `AiHelpTextTests` (`AiHelpTextTests.cs`) | `Answer_EveryOption_AppearsInAllAndInEveryTopicItsCategoriesName` | an option is missing from `all`, or from a topic one of its categories names | BL-139 |
| `AiHelpTextTests` | `Answer_EveryHelpCategory_IsATopic` | a `HelpCategories.All` name is not an `AiHelpTopics.All` topic | BL-139 |
| `AiHelpTextTests` | `Answer_EveryTopic_IsInTheOverviewAndInAll` | a topic is missing from the overview's topic table, or its page is missing from `all` | BL-139 |
| `AiHelpTextTests` | `Answer_ExitCodes_ListsEverySurlExitCodeMember` | a `SurlExitCode` member's number and name are missing from the `exit-codes` page | BL-139 |
| `AiHelpTextTests` | `Answer_EveryTopicPage_HasTheSameSectionsInOrder` | a topic page's `#` and `##` lines are not decision 4's skeleton | BL-139 |
| `AiHelpTextTests` | `Answer_EveryAnswer_HasNoEscapeTabOrTrailingSpaceAndWholeTableRows` | any answer holds U+001B, a TAB or a trailing space, or a table row has a different cell count from its header | BL-139 |
| `AiHelpTextTests` | `Answer_UnknownOrOptionLikeTopic_IsTheUnknownTopicAnswer` | decision 8's answer differs | BL-139 |
| `AiHelpTextTests` | `Answer_Topic_MatchesInAnyCase` | `MQTT` or `All` is not answered as `mqtt` or `all` | BL-139 |
| `AiHelpTextTests` | `Answer_Telnet_IsPinned`, `Answer_ExitCodes_IsPinned` | the full page differs byte for byte | BL-139 (re-pinned by BL-140) |
| `AiHelpTextTests` | `Answer_EveryTopicAndTheOverview_HasItsAboutText` | a topic or the overview still has `Nothing for this topic.` under `About` | BL-140 |
| `AiHelpTextTests` | `Answer_EveryTopicButExitCodesAndSecurity_HasAnExample` | a topic other than `exit-codes` and `security`, or the overview, has no example, or an example names a topic that does not exist | BL-140 |
| `AiHelpTextTests` | `Answer_EveryPage_NamesOnlyOptionsThatExist` | an `--option` named in any prose or example is not in `CommandLineOptions.All` | BL-140 |

In `Surl.Console.UnitTests` (registered schemes and examples; only `Surl.Console` knows what is
registered and what `RunAsync` writes), in `CommandLineRunnerAiHelpTests.cs`:

| Test class | Test method | Fails when | Task |
| --- | --- | --- | --- |
| `CommandLineRunnerAiHelpTests` | `RegisteredSchemes_AreEachClaimedByExactlyOneProtocolTopic` | a scheme of a server `ComposeProtocolServers` registers is claimed by no `AiHelpTopics.All` topic, or by two | BL-142 |
| `CommandLineRunnerAiHelpTests` | `ProtocolTopics_ClaimOnlyRegisteredSchemes` | a topic claims a scheme no registered server serves | BL-142 |
| `CommandLineRunnerAiHelpTests` | `RunAsync_EveryAiHelpExample_WritesWhatTheExampleShows` | an example's stdout lines, stderr lines or exit code differ from what `RunAsync` writes (decision 7) | BL-142 |

### 10. `--manual`'s `SEE ALSO` names `surl --aihelp`

Yes: an agent that reads the manual must learn the agent-facing help exists. BL-141 changes the
section to:

```
SEE ALSO

    surl --help all, surl --help category, surl --aihelp, curl(1),
    https://github.com/StewartScottRogers/Surl
```

and, in the same change, the manual's `EXIT CODES` row for 0 to name `--aihelp` beside help,
the manual and the version, wrapped by ADR-0034 decision 6's rule.

## Alternatives considered

- **JSON output.** Rejected by Stewart: Markdown is what an agent reads and writes, and one
  format keeps one test surface.
- **A skill-file generator.** Rejected by Stewart: `--aihelp` describes surl; turning it into
  a skill is the agent's own work.
- **Markdown as a mode of `--help`** (`--help --markdown`). Rejected: `--help` mirrors curl's
  measured layout and argument rules (ADR-0034), and a second format behind it would change
  how its subject is read.
- **A short name.** Rejected in decision 1.
- **Wrapping at 79 columns.** Rejected in decision 4: a Markdown table row cannot wrap.
- **Demoting the headings in `all`.** Rejected in decision 4: the pages would no longer be byte
  for byte the single-topic pages.
- **A per-option page for `--aihelp --user`.** Rejected in decision 8: the tables already carry
  every option, and one unknown-topic rule is simpler for an agent.
- **Hand-written option tables per topic.** Rejected: they would drift from the option table,
  which is what Stewart's "generated, so the two cannot drift" rules out.
- **The prose in an embedded resource or a Markdown file.** Rejected in decision 6, as
  ADR-0034 decision 6 rejected it for the manual.
- **Marking every `security` option "for tests only".** Rejected in decision 5: untrue of the
  six that are deployment choices.

## Consequences

- **BL-138 (facts):** `OptionArgumentType`, `OptionArgumentReading<T>`, `ArgumentType` on every
  row, `HelpCategory.Schemes`, `ExitCodeGuidance` and `ExitCodeGuidanceTable`, and the
  `AiHelpFactsTests` of decision 9. No `--help` page changes.
- **BL-139 (generator):** `AiHelpTopic`, `AiHelpTopics`, `AiHelpText.Answer` for the overview,
  every topic, `all` and the unknown-topic answer in decision 4's structure, every hand-written
  section `Nothing for this topic.`; its `AiHelpTextTests`.
- **BL-140 (prose and examples):** `AiHelpProse`, `AiHelpExample`,
  `AiHelpExamplePrecondition` and `AiHelpExamples` with decision 7's 19 examples, every
  statement checked against the code.
- **BL-141 (parsing, `--help` row and wiring):** decision 1's shapes and parsing, the
  `--aihelp` row of decision 2 and the re-pinned `--help` pages, `CommandLineRunner` answering
  `ShowAiHelp`, and decision 10's manual changes.
- **BL-142 (registered schemes and examples proved):** the `CommandLineRunnerAiHelpTests` of
  decision 9.
- **BL-143 (documents):** the glossary, the root `README.md`, the product overview and the
  `CLAUDE.md` files.
- A new option gets its `ArgumentType` from its reader and appears in `--aihelp` with no
  further work; a new reader needs its `OptionArgumentType` here first (a new ADR). A new
  protocol server's task adds its category's schemes, `About` text and example (decision 3). A
  new `SurlExitCode` member's ADR adds its guidance row.
- FR-035 names this ADR; FR-008 lists `--aihelp`.
