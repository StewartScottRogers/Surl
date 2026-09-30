# Surl.Console

The composition root: the `surl` executable (assembly name `surl`), published native
ahead-of-time as a single file. Every object is constructed explicitly here - never
assembly scanning or reflection-based dependency injection, which native AOT forbids.

- `Program.Main` registers Ctrl+C (SIGINT) and SIGTERM to cancel a token and calls
  `Program.RunAsync(args, output, error, cancellationToken)`, the internal entry point the
  in-process conformance tests (`Surl.Conformance.UnitTests`) also call.
- `CommandLineRunner` parses the command line (`Surl.Cli`), answers `--help`, `--aihelp`
  (`CommandLineOutcome.ShowAiHelp`: `AiHelpText.Answer(parsed.AiHelpTopic)` written by
  `WriteHelp` as `--help`'s answer is, exit 0 at every log level; everything `--aihelp`
  says lives in `Surl.Cli`), `--manual` (`ManualText.Text`) and `--version`, refuses a
  start whose `--auth` names `gssapi` without `--keytab` with `surl: (2) --auth gssapi needs
  --keytab` (`KeytabComposition.IsGssapiWithoutKeytab`, ADR-0057 decision 1), then a
  start that gives the `--auth` word `gssapi` or an SSH server option this build does not
  serve yet (`FindUnavailableOption`: `--auth gssapi`, `--hostcert`,
  `--allow-weak-ssh-algorithms`, the first in that order) with
  `surl: (2) --<option> is not available in this build` (`--auth gssapi` for the word) and
  exit 2 before anything else is checked (ADR-0049 section 3, until BL-218 and BL-216 build
  the two mechanisms; ADR-0051 decision 5, `--hostcert` until BL-222 and
  `--allow-weak-ssh-algorithms` until BL-221), checks the
  data directory when `--directory` names one
  (`DataDirectoryProbe`, 37 when it cannot be opened), builds the content store
  (`ComposeContentFileSystem`: a `DiskContentFileSystem` rooted at the data directory's
  full path with `--directory`, a new, empty `InMemoryContentFileSystem` at
  `InMemoryContentFileSystem.RootPath` without it, ADR-0031 decisions 1 and 4), checks
  every scheme against the registered protocol servers, then refuses a TLS-first listen URL
  with no certificate (58) and an `scp` or `sftp` one with neither `--hostkey` nor
  `--throwaway-hostkey` (2, `surl: (2) <url> needs a host key: ...`), and with `--directory` takes the
  data directory's `.surl/lock` (`DataDirectoryLock.Take`, held until serving ends; a
  second surl on the same path gets 124, a `.surl` or lock file that cannot be created 23,
  ADR-0031 decision 7; no lock and no disk access without `--directory`). Before the lock it
  builds the authentication policy (`AuthenticationComposition.Compose`, ADR-0032): the
  `--user-file` and then each `--authorized-keys` file are read through the runner's
  `readStartFile` seam (`File.ReadAllBytes` in `surl`), 37 when one cannot be read and 2
  naming the line when it is malformed (ADR-0051 decision 6); then the `--keytab` file
  (`KeytabComposition.Read`, ADR-0057 decision 1) through the same seam, read by
  `Surl.Kerberos`'s `KerberosKeytab.Read` (37 `Could not read keytab <path>`, 2 `Keytab <path>
  is malformed at byte <offset>` or `holds no key surl can use`), whose keys become a
  `KerberosAcceptor` (one `KerberosReplayCache` per process, the one clock and
  `RandomKerberosRandomSource`) on `AuthenticationSettings.KerberosAcceptor`, `null` without
  `--keytab`, read by no method yet (ADR-0057 decision 6); then the SSH host keys
  (`SshHostKeyComposition.Compose`, ADR-0051 decision 4): each `--hostkey` file read through
  the same seam and parsed by `SshHostKeyFile.Read` (37 unreadable, 2 with the parser's
  refusal or a second key of one type, naming the file), and with `--throwaway-hostkey` an RSA
  3072-bit key made only when an `scp` or `sftp` URL is served; the
  `--user` accounts and then the file's go into one `AccountBook`; each `--auth` word maps to
  its `AuthenticationMethod`, the SASL mechanism words (`digest-md5`, `cram-md5`, `apop`,
  `plain`, `login`, `oauthbearer`, `xoauth2`, `external`, and `ntlm` for both) included, ADR-0049
  section 3 (the default set without `--auth`); and
  `Surl.Authentication`'s `AuthenticationPolicy` with Negotiate, NTLM, Basic, Bearer, Digest
  and AWS Signature Version 4, and `--allow-anonymous` and `--allow-plaintext-auth` in its
  `AuthenticationSettings`, is handed to the HTTP (`http`, `https`), MQTT (`mqtt`,
  `mqtts`), SMTP (`smtp`, `smtps`), IMAP (`imap`, `imaps`) and FTP (`ftp`, `ftps`) servers; `Compose` also returns every account's user
  name, the mail store's owners. Then it builds the protocol servers (today `HttpProtocolServer` for `http` and, through `ImplicitTlsSchemeServer`,
  `https`, `DictProtocolServer` for `dict`, `FtpProtocolServer` for `ftp` and `ftps` (it declares
  both itself; `AUTH TLS` when `--cert` or `--self-signed` is given, ADR-0052 decision 5), given the
  content store and the policy, `GopherProtocolServer` for `gopher` and
  `gophers` (it declares both itself, so no `ImplicitTlsSchemeServer` wraps it),
  `MqttProtocolServer` for `mqtt` and `mqtts` (it too declares both itself), whose
  retained messages are kept in `<data directory>/.surl/mqtt/retained-messages` and loaded
  after the lock and before any listener binds with `--directory` (a file that cannot be
  read or does not parse ends surl with 37), and in memory only without it (ADR-0031
  decision 6),
  `SmtpProtocolServer` for `smtp` and, through `ImplicitTlsSchemeServer`, `smtps`, given the
  policy as both its authentication policies, `STARTTLS` when `--cert` or `--self-signed` is
  given (`ServerTlsComposition.IsCertificateConfigured`, ADR-0053 decision 5) and the one
  `MailboxStore` (`LoadMailStoreAsync`: owners the account names, or the anonymous owner under
  `--allow-anonymous`, one message bounded by `--max-filesize`), kept in
  `<data directory>/.surl/mail` (`ComposeMailStoreFiles`) and loaded after the retained
  messages, before any listener binds, with `--directory` (a store that cannot be loaded ends
  surl with 37 and `surl: (37) Could not read <file>: <reason>`, ADR-0050 decision 7), and in
  memory only without it,
  `ImapProtocolServer` for `imap` and, through `ImplicitTlsSchemeServer`, `imaps`, given the
  policy as both its authentication policies, `STARTTLS` when `--cert` or `--self-signed` is
  given (ADR-0055 decision 11) and the same `MailboxStore` instance as the SMTP server, so mail
  delivered over `smtp` is read over `imap` in the same run,
  `SshProtocolServer` for `scp` and `sftp`, given the host keys, `SshAlgorithmOffer.Default`
  for them, the policy as its `ISshAuthenticationPolicy`, `SshSystemRandomSource` and the
  content store, `TelnetProtocolServer` for `telnet` and `TftpProtocolServer` for `tftp`, over UDP), the
  exchange log of the parsed log level and the serving engine, with the connection limits
  (`ComposeConnectionLimits`) the command line's `--max-connections`,
  `--max-connections-per-address`, `--idle-timeout` and `-m`/`--max-time` give, and the FTP
  data connection opener the runner's `createDataConnectionOpener` seam makes from the TLS settings
  (`Surl.Networking`'s `SocketDataConnectionOpener` in `surl`, ADR-0052 decision 9), and serves. It writes ADR-0007 section 5's
  texts and returns its exit codes.
- `LogStreams` opens the log stream (stderr, or `--log-file`, appended, `-` for stdout)
  and the trace file (truncated, `-` for stdout) once TLS is composed and before any
  listener binds, through the runner's `openLogFile` seam (`LogFile.Open` in `surl`); a
  file that cannot be opened ends surl with 23, and so does a trace file with the
  `--log-file` file's full path, ignoring case (ADR-0037). It builds the exchange log of the
  level: `LevelledExchangeLogFactory` up to `-v`, `TraceExchangeLogFactory` at the trace
  level, to the trace file or, with `--log-level trace` alone, the log stream (ADR-0033).
- `CommandLineRunner` applies the level to what it writes itself: at `-s` every `surl: `
  failure message is hidden but a command-line refusal is not, and the status lines, the
  startup warnings and the throwaway-certificate lines are written from the info level up
  only, so `-s` and `-s -S` hide them.
- `ListenerStartReporter` wraps the listener factory: it writes the status lines once the
  last listener, connection or datagram, has bound, and keeps a bind failure for the
  `(45)` or `(6)` message.
- `ServerTlsComposition` builds the process's `ServerTlsSettings` when a listen URL is
  TLS from the first byte, or can be upgraded (`smtp` and `imap` for `STARTTLS`, `ftp` for `AUTH TLS`) and `--cert` or
  `--self-signed` is given: the `--cert`/`--key` certificate or, with `--self-signed`, a
  throwaway one, the `--cacert` trust anchors and the accepted TLS versions. A bad file ends
  surl with 58, 2 or 77 before any listener binds (ADR-0020). A listen URL TLS from the first byte with neither
  `--cert` nor `--self-signed` ends surl with 58 before any listener binds
  (`ServerTlsComposition.FindListenUrlWithoutCertificate`, ADR-0032 section 10); a start
  with `--self-signed` and neither such a listen URL nor an `smtp`, `imap` or `ftp` one makes no certificate.
- Once the log streams are open, the startup warnings go to the log stream, unstamped:
  `AuthenticationComposition.WriteLooseningWarnings` writes the `--allow-anonymous`,
  `--allow-plaintext-auth` and `--auth` lines, in that order, then
  `KeytabComposition.WriteStartLines` writes `surl: warning: --keytab: skipped the <enctype
  name> key of <principal>` for each keytab entry of an enctype surl does not accept, and
  `surl: warning: --keytab is unused: --auth accepts neither negotiate nor gssapi` when that
  is so, never a key byte, then `CommandLineRunner`
  writes the `--self-signed` line when a throwaway certificate was made, and from the
  verbose level up `* Serving a throwaway certificate, SHA-256 <fingerprint>` (ADR-0032
  section 9, ADR-0033 section 7); then `SshHostKeyComposition.WriteStartLines` writes the
  `--throwaway-hostkey` warning (with the key's `--hostpubsha256` value) when the key was made,
  and from the verbose level up, when an `scp` or `sftp` URL is served,
  `* Serving SSH host key <key type>, --hostpubsha256 <base64> --hostpubmd5 <hex>` per key
  (ADR-0051 decisions 8 and 11).
- `Program.RunAsync` serves through `Surl.Networking`'s `SocketListenerFactory`, created
  with those TLS settings: TCP connection listeners and UDP datagram listeners.
- `CommandLineRunner.ComposeRegisteredSchemes` lists every registered server's schemes, the
  `--version` `Protocols:` line's source. `CommandLineRunnerAiHelpTests`
  (`Surl.Console.UnitTests/CommandLineRunnerAiHelpTests.cs`, ADR-0046 decision 9) is the
  `--aihelp` check only this project can make, because only it knows what is registered:
  `RegisteredSchemes_AreEachClaimedByExactlyOneProtocolTopic` fails when a registered scheme
  has no `AiHelpTopics.All` protocol topic or has two, `ProtocolTopics_ClaimOnlyRegisteredSchemes`
  when a topic names an unregistered scheme, and
  `RunAsync_EveryAiHelpExample_WritesWhatTheExampleShows` when an `AiHelpExamples.All`
  entry's stdout, stderr or exit code differs from what `RunAsync` writes with
  `FakeListenerFactory`. So registering a server in `ComposeProtocolServers` means adding,
  in `Surl.Cli` and in the same change, its `HelpCategories` row with its schemes, its
  `AiHelpProse.TopicAbout` paragraphs and its `AiHelpExamples` entry.

Keep this project thin: parsing belongs in `Surl.Cli`, serving in `Surl.Core`, each
protocol in its own library. Code here is wiring, tested in `Surl.Console.UnitTests`
with a fake listener factory; the real-socket and real-disk paths are
`[TestCategory("Integration")]`.
