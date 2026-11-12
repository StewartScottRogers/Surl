# Surl.Cli.UnitLibrary

Phase 1.

Surl's command line: the option table, argument parsing, the help, the AI help and the
manual, and the mapping from a bad command line to a `SurlExitCode`. `surl [options] <url>`
names what to listen on, the way `curl [options] <url>` names what to fetch. An option
keeps curl's name and meaning wherever a server-side meaning exists (`--cert`, `--key`,
`--cacert`, `-u`, `-v`, `-s`, `-S`, `--trace`, `--trace-ascii`, `--trace-time`); the table
is ADR-0007's, with the rows ADR-0010, ADR-0031, ADR-0032, ADR-0033, ADR-0034 and ADR-0046
add. It references
`Surl.Core.UnitLibrary`, `Surl.Output.UnitLibrary` (for `LogLevel` and `TraceDumpLayout`)
and `Surl.Protocol.Abstractions.UnitLibrary`.

- `CommandLineOptions` is the one option table: every `CommandLineOption` row carries its
  `OptionHelp` (argument name, description, `HelpCategories`, short-list flag, default and,
  for a loosening option, its `Explanation`), so an option cannot be added without its help.
  `CommandLineParser.Parse` reads a whole command line by it, left to right, into a
  `CommandLineParseResult`: `Serve` with a `SurlCommandLine`, `ShowHelp` with its
  `HelpSubject`, `ShowAiHelp` with its `AiHelpTopic`, `ShowManual`, `ShowVersion`, or `Refused` with a `CommandLineFailure`.
  `ListenUrlParser` reads each listen URL; `OptionArgumentReader` reads each argument kind.
  `SchemeDefaultPorts` is upstream curl's port per scheme and stays so; the overloads taking
  `extendedSchemes` also accept each scheme a registered extended server claims at its
  `ExtendedSchemeDefaultPorts` row (ADR-0083 decision 7), consulted only after upstream curl's
  table, and `VersionText.Compose` then adds an `Extended:` line. The `extended` `--aihelp`
  topic sits beside `exit-codes` and `listen-urls`.
- Accounts and loosening options (ADR-0032 section 1): each `-u`/`--user` adds a
  `CommandLineAccount` to `SurlCommandLine.Accounts`, split at the first `:`; a refusal
  of one never echoes its value (`CommandLineOption.ArgumentHoldsSecret`), and a user name
  given twice is refused after the whole line is read. `--user-file` is kept as a path
  (`SurlCommandLine.UserFile`) and read by `Surl.Console`, and so is `--keytab`
  (`SurlCommandLine.KeytabFile`, ADR-0057 decision 1: category `auth`, not negatable). `--allow-anonymous`,
  `--allow-plaintext-auth`, `--auth <methods>` (`GivenAuthenticationMethods`, matched
  case-insensitively, in ADR-0032 section 3's order) and `--self-signed` are parsed here;
  `--self-signed` with `--cert` is refused.
- Proxy configuration (ADR-0088 decision 7 and Amendment 5, BL-1002): `SurlCommandLine.ProxyListenUrls`,
  `ProxyAccounts`, `ProxyUserFile` and `ProxyAllowedOrigins`, with their readers:
  `ListenUrlParser.ParseProxy` (`http` or `https` only, `ProxyDefaultPorts` 1080 and 443, `ListenUrl.IsProxy`
  set; any other scheme `UnsupportedProtocol`), `OptionArgumentReader.ProxyListenUrlText`, `ReadAccount` and
  `ReadPath` reused for `-U` and `--proxy-user-file`, and `ReadProxyAllowedOrigin` / `AllowedOrigin` reading
  `*` or a strict `address[/prefix]` into a `ProxyAllowedOrigin`; their option rows are BL-1004's.
  `--proxy-auth <methods>` (BL-1040, ADR-0088 Amendment 8) reads `--auth`'s words into
  `GivenProxyAuthenticationMethods`, apart from `--auth`; `AcceptedProxyAuthenticationMethods` defaults to
  `digest`, `basic`.
- SSH server options (ADR-0051 decisions 4 to 6): `--hostkey` and `--hostcert` each add a path
  to `HostKeyFiles` and `HostCertificateFiles`; `--authorized-keys <user:file>` adds a
  `CommandLineAuthorizedKeys` to `AuthorizedKeys`, split at the first `:`, a user given twice
  refused after the whole line is read; `--throwaway-hostkey` (`ThrowawayHostKey`) with
  `--hostkey` is refused; `--allow-weak-ssh-algorithms` sets `AllowWeakSshAlgorithms`;
  `--ssh-ciphers` and `--ssh-macs` set `SshCiphers` and `SshMacs` to their comma-separated names,
  kept as given (`OptionArgumentReader.SshAlgorithmNames`, ADR-0066), which `Surl.Console` checks. `--pass`
  without `--cert` is accepted when a `--hostkey` is given. Each is in the `ssh` help category
  (`SSH protocol`, schemes `scp` and `sftp`, ADR-0051 decision 12), with every option the SSH
  server reads. `Surl.Console` reads the files and serves every one of these options, `--hostcert`
  included (BL-222).
- Layer-2 items (ADR-0101 decisions 3, 5 rows 10 to 13, and 6): `ConfigurationItemReader.Read`
  reads one item option argument by its `ConfigurationItemShape` (Argument column, separator,
  `ConfigurationItemValueKind` after it, its own keys and flags as `ConfigurationItemAttributeKind`,
  and a grant's access words) into a `ConfigurationItem`: split at every `;`, the head at the
  first separator, a grant at its last `:`, lists at `,`, then each field percent-decoded.
  Unknown keys and flags are refused, and no refusal holds a value. Members read as
  `ConfiguredMember` (`@name` is a group) and grants as `ConfiguredGrant`. `ConfigurationItemShape.Reading`
  is the `OptionArgumentReading<ConfigurationItem>` an option row takes; its `OptionArgumentType.Item`
  is named by the shape, its allowed values ending in `OptionArgumentType.ItemSyntax`.
  `MaxOccurrences` and `GivenTooOften` are for the parser that counts each option across the
  line and every config file.
- `--group <name>=<member>,...` (category `auth`, ADR-0101 decision 4, BL-646) is the first item
  option: `ConfiguredGroupReader` reads it by its `Shape` into a `ConfiguredGroup` (refusing a
  member twice in one list, `ConfiguredMember.AsWritten` naming it), each adds to
  `SurlCommandLine.Groups`, a group name given twice is refused after the whole line is read, and
  the 10001st `--group` is refused (`GivenTooOften`). Whether members name accounts or groups,
  cycles and the reserved `everyone` are checked at start by `Surl.Composition`'s
  `AccountGroupExpansion`. BL-653, BL-654, BL-663 and BL-666 add the other item options.
- Content items (ADR-0101 decision 4, ADR-0102 decision 2; BL-987): `ConfiguredShareReader`
  (`<name>=<path>[;readonly][;protocols=...]`, schemes checked by
  `ConfiguredMailboxReader.CheckProtocols`, TLS twins not added), `ConfiguredShareAccessReader`
  (`<share>=<member>:<r|rw>,...`, a member twice refused) and `ConfiguredBucketReader`
  (`<name>=<path>`) read into `SurlCommandLine.Shares`, `ShareAccess` and `Buckets`.
  `ConfiguredContentConflicts.Find` holds the whole-line checks (a share, bucket or access share
  twice, a bucket named as a share, an access naming no share). No option row uses them yet:
  BL-989 adds the rows and calls `Find`; BL-988 composes them.
- Messaging items (ADR-0101 decision 4, ADR-0102 decision 3; BL-990): `ConfiguredTopicReader`
  (`<filter>[;retain][;qos=<0|1|2>]`, `qos` exactly `0`, `1` or `2` else `is badly used here`),
  `ConfiguredQueueReader` (`<name>[;durable]`), `ConfiguredTopicMapReader`
  (`<scheme>:<name>=<scheme>:<name>`, each decoded side split at its first `:`, the scheme lower-cased
  and checked by `ConfiguredMailboxReader.IsAnySurlScheme`) and `ConfiguredTopicAccessReader`
  (`<filter>=<member>:<pub|sub|pubsub>,...`, a member twice refused) read into
  `SurlCommandLine.Topics`, `Queues`, `TopicMaps` and `TopicAccess`. `ConfiguredMessagingConflicts.Find`
  holds the whole-line checks (a topic, queue, map source or access filter given twice). No option
  row uses them yet: BL-992 adds the rows and calls `Find`; BL-991 composes them.
- Mail options (ADR-0101 decisions 4, 5 and 7, ADR-0102 decision 1; BL-883), category `mail`:
  `--mail-domain <domain>` (`MailDomainNames.Reading`, kept as written in `MailDomains`),
  `--mailbox <address>:<password>[;protocols=...]` (`ConfiguredMailboxReader.Reading`, the address
  normalised, `ArgumentHoldsSecret`) and `--mailbox-file <file>` (a path in `MailboxFiles`, read at
  start by `Surl.Composition`). Each is counted like `--group`; a `protocols=` scheme no registered
  server claims is refused as read; once the whole line is read `RefuseMailCombination` refuses a
  domain or a mailbox given twice, then a mailbox outside every domain, with none, or equal to a
  `--user` name, each naming the domain or address as written.
- Config files (ADR-0101 decisions 1, 2, 5 rows 1 to 9, and 6; BL-644): `-K`/`--config <file>`
  (category `surl`) reads a file in curl's config-file syntax in place, through the
  `IConfigFileSource` seam the host passes to `CommandLineParser.Parse`'s third overload (the
  overloads without one use `UnreadableConfigFileSource`, so every `-K` there exits 37).
  `ConfigFileText.Lines` splits the bytes into `ConfigFileLine`s (UTF-8, LF or CR LF, blank and `#`
  lines skipped, undashed names, quoted values and their escapes, text after a value and a missing
  quote refused); the parser reads each line's words as if they stood alone on the command line
  (`CommandLineReading.LineWords`), so an option never takes a value from another line. A refusal
  inside a file is prefixed once by `<file>:<line>: `; at most `MaxConfigFileDepth` (5) files are
  open at once, a cycle and a second `-K -` are refused, a file past 16 MiB and a line past 10 MiB
  too. `--url <listen URL>` adds a listen URL exactly as a bare argument does.
- Control endpoint options (ADR-0124 decision 7, BL-815): `--control <listen URL>` is read by
  `ControlEndpointOptionReader` into `SurlCommandLine.ControlListenUrl` (scheme `http` only,
  refused first; then a malformed listen URL as a bare argument's is; then a host other than
  `localhost`, a four-part IPv4 address in `127.0.0.0/8` or `::1` with no zone), the last one
  winning; `--control-token-file <file>` is kept as a path (`ControlTokenFile`). Each without the
  other is refused once the whole line is read. All four refusals are `(2) ...` with no try-help
  line. Both are in the `security` and `testing` categories; the `control` `--aihelp` topic sits
  beside `exit-codes`. Serving the endpoint and reading the token file are BL-816's.
- Unix domain sockets (ADR-0094 decisions 1 and 2, BL-548): `--unix-socket <path>` and
  `--abstract-unix-socket <name>` (category `surl`) set `SurlCommandLine.UnixSocket`, the last
  one winning. Once the whole line is read `UnixSocketCombination` refuses, in order, a second
  listen URL, a UDP scheme (`tftp`, `coap`, `coaps`, `dns`), `--http3`, and `ftp`/`ftps` (until
  BL-605), each `option --<name>: <reason>` with exit 2 and the try-help line, then sets the socket
  on the one listen URL's `ListenUrl.UnixSocket`. Binding it, and the 45 refusals, are
  `Surl.Networking`'s.
- TLS-SRP (ADR-0122 decision 6 and Amendment 2, BL-567), category `tls` (and `auth` for all but
  `--tlsauthtype`): `--tlsauthtype <SRP>` (`OptionArgumentReader.TlsAuthenticationTypeWord`, any case,
  into `TlsAuthenticationType`), `--tlsuser <name>` (`SrpUserName` reading), `--tlspassword <password>`
  (`ArgumentHoldsSecret`), `--srp-verifiers <file>` and `--srp-groups <file>` set the
  `SurlCommandLine` members of those names; `ServesTlsSrp` is true when any is given. Once the whole
  line is read `TlsSrpCombination` refuses, in order, an option without its partner, `--tlsauthtype`
  with no account, no listen URL that uses TLS (implicit TLS or `ftp`, `imap`, `ldap`, `natss`, `pop3`,
  `smtp`, `xmpp`), then TLS-SRP beside `--http3`, `--cacert`, `--tls-max` below 1.2, `--tlsv1.3`,
  `--cert` or `--self-signed`, each `FailedInit` with the try-help line. Reading the files is
  `Surl.Composition`'s.
- Log levels (ADR-0033 section 2): `-s`, `-v`, `--log-level`, `--trace` and `--trace-ascii`
  each set `SurlCommandLine.LogLevel`, the last one given winning, and `-S` (`ShowError`)
  turns `none` into `error` once the whole line is read. `TraceFile`, `TraceLayout`,
  `TraceTime` and `LogFile` hold the rest; opening the files is `Surl.Console`'s.
- Help (ADR-0034): `HelpText.Answer` turns a help subject into a `HelpAnswer` - the short
  list, `all`, `category`, one of `HelpCategories.All`, or an option page - laid out to 79
  columns by `HelpLayout`. `ManualText.Text` is the `--manual` text; `VersionText.Compose`
  the `--version` text.
- AI help (ADR-0046; the glossary's "AI help", "AI help topic", "AI help example",
  "argument type" and "exit-code guidance"): `AiHelpText.Answer` (`AiHelpText.cs`) turns an
  `--aihelp` topic into a `HelpAnswer` in Markdown - the overview, one of
  `AiHelpTopics.All` (`AiHelpTopics.cs`, `AiHelpTopic.cs`: every `HelpCategories.All` row
  with its name, description and `HelpCategory.Schemes`, plus `exit-codes` and
  `listen-urls`, found in any case by `AiHelpTopics.TryFind`), `all`, or the unknown-topic
  answer. Its tables are generated, never hand-written: the option table from
  `CommandLineOptions` and each row's `CommandLineOption.ArgumentType`
  (`OptionArgumentType.cs`, paired with its reader in `OptionArgumentReading.cs`), the
  option's left side shared with `HelpText.LeftSide`; the exit-code table from
  `ExitCodeGuidanceTable.All` (`ExitCodeGuidanceTable.cs`, `ExitCodeGuidance.cs`). The
  hand-written paragraphs are `AiHelpProse`'s source constants (`AiHelpProse.cs`); the
  examples are `AiHelpExamples.All` (`AiHelpExamples.cs`, `AiHelpExample.cs`,
  `AiHelpExamplePrecondition.cs`), public so `Surl.Console.UnitTests` can run them.
  `--aihelp [topic]` is a row of `CommandLineOptions` (`CommandLineOptionKind.AiHelp`, no
  short name), read as `--help` reads its subject into `CommandLineParseResult.AiHelpTopic`
  with the outcome `CommandLineOutcome.ShowAiHelp`, which `Surl.Console` answers.
  `AiHelpFactsTests` and `AiHelpTextTests` in `Surl.Cli.UnitTests` fail when an option, a
  topic or a `SurlExitCode` member is missing from it (root `CLAUDE.md`). Work that changes
  what surl does updates the `AiHelpProse` paragraph and the example that describe it in
  the same change, as it does `ManualText`.

Never touch the console here; `Surl.Console` hands this library its arguments and writers.
Any change to an option's behaviour updates its `OptionHelp` and the `ManualText` section
that describes it in the same change (ADR-0034 decision 6).

`-x`/`--proxy` is read where it is given (`CommandLineParser.ReadProxyListenUrl`) into
`SurlCommandLine.ProxyListenUrls`, not into `ListenUrls`, and is refused beside a Unix socket
(`UnixSocketCombination.RefuseProxy`); the `proxy` help category claims no scheme (ADR-0088 amendment 7).
