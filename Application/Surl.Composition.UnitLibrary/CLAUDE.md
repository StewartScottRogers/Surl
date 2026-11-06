# Surl.Composition

The one place protocol servers are composed (ADR-0099 decision 1). It references every original
protocol server and the horizontal libraries they need; `Surl.Console` references it, and so will
`Surl.Api` and possibly `Surl.Mcp` (BL-612). It references no host, and no protocol server or
horizontal library may reference it (ADR-0099 decision 11, held by
`Foundation/Surl.Protocol.Abstractions.UnitTests/ProtocolIsolationTests.cs`).

- `SurlServerComposition.ComposeProtocolServers` builds every original server `surl` registers,
  in registration order, by iterating the explicit list `ProtocolServerRegistrations.Original`
  (ADR-0124 decision 2, BL-804): one `ProtocolServerRegistration(Schemes, Create)` per server,
  `Create` building it from the start's `ComposedProtocolServices` (content store, mail store,
  message bus, LDAP server, policy, `TimeProvider`, SSH host keys and algorithms, the TLS-upgrade,
  `--ws-echo` and `--http3` flags). `https`, `imaps`, `ldaps`, `pop3s`, `smtps` and `wss` are
  `ProtocolServerRegistration.ImplicitTls` entries, an `ImplicitTlsSchemeServer` over the server an
  earlier entry built (`ComposedProtocolServices.ComposedServerFor`). A new protocol server is one
  entry there, and in the same change gets its `--aihelp` facts in `Surl.Cli` (root `CLAUDE.md`,
  "`surl --aihelp`"). The overload `ComposeProtocolServers(registrations, services)` builds any list.
- Per-scheme accounts (ADR-0102 decision 4.3 and its 2026-11-06 amendment, BL-881): every
  registration with a login builds its server with `ComposedProtocolServices.AuthenticationPolicyFor(<its
  plain scheme>)`, which the start sets to `SchemeAuthenticationPolicies.For` (carried on
  `ComposedAuthentication.SchemePolicies`): one cached policy per scheme over
  `AuthenticationSettings.ForScheme`, so an account whose `protocols=` leaves a scheme out is refused
  there as no such account. Unset, as for listing schemes, every server gets the one policy. The SSH
  server is two entries, `scp` and `sftp`, each a `SingleSchemeServer`; the LDAP server is loaded
  with `For("ldap")`, the HTTP/3 server composed with `For("https")`, the S3 server with
  `AuthenticationPolicyFor("s3")` (`ExtendedProtocolServerServices` forwards both members).
- `SurlServerCompositionSeams.AdditionalProtocolServers` (`AdditionalProtocolServer`: a registration
  and its already-parsed `ListenUrl`s) are composed after the extended servers and served on their
  listen URLs after the command line's: how `Surl.Api` serves a test's own server (BL-813).
- `--http3` (ADR-0082 decision 6, BL-384): `ServerTlsComposition.QuicCertificate` is the served
  certificate as a `QuicServerCertificate` (DER chain and private key), and the start adds an
  `Http3ProtocolServer` for `https` after server TLS, beside the HTTP server's `https`
  registration, so the serving engine binds TCP and UDP on one port; the HTTP server then adds
  `Alt-Svc`. `ListenerStartReporter` counts such a pair as its connection listener alone (one
  status line) and `ComposedServing` writes `* Serving HTTP/3 over QUIC on UDP port <port>` from
  verbose up.
- `ExtendedProtocolServers.Compose` builds the extended servers (`Surl.Extended.Protocol.*`: today
  the DNS server for `dns` (BL-1032), two entries - `DnsUdpProtocolServer` and `DnsTcpProtocolServer` -
  so the engine binds UDP and TCP on one port, both over `ComposedProtocolServices.DnsZones` and
  `AllowsZoneTransfer` (`--allow-zone-transfer`, ADR-0092 decision 4),
  the NATS server for `nats` and `natss`, BL-528, and the S3 server for `s3`, BL-421) from
  `ExtendedProtocolServers.Registrations`, the same
  `ProtocolServerRegistration` record, each joining with one entry; its `ExtendedProtocolServerServices`
  wraps the start's `ComposedProtocolServices` (the S3 server reads its content store, policy, object
  metadata index and clock); `s3` is TLS from the first byte through `TlsSchemes` (ADR-0108 decision 5),
  so it needs no `ImplicitTls` entry. The NATS server is built over the one `MessageBus` the MQTT
  server shares, `AuthenticationPolicyFor("nats")`, and a `NatsServerOptions` of
  `ComposedProtocolServices.IdleTimeout` (the start sets it to `ComposeConnectionLimits`' idle timeout)
  and the policy's `AllowAnonymous`. `natss` starts in plain text and upgrades (ADR-0113 decision 6),
  so it is not in `TlsSchemes`; `ServerTlsComposition` lists it as requiring a certificate, so a
  `natss` listen URL without `--cert` or `--self-signed` is refused `(58)` like an implicit-TLS one; `ComposeUnservedExtendedServices` wraps
  `ComposeUnservedProtocolServices` for listing schemes. `ExtendedProtocolServers.AreTurnedOn` reads `SURL_EXTENSIONS`
  through an `IEnvironmentVariableReader` (`ProcessEnvironmentVariableReader` in `surl`); `0`
  turns them off (ADR-0083 decisions 6 to 8). `SurlServerComposition.ComposeExtendedProtocolServers`
  never calls the composer while they are off.
- `ComposeRegisteredSchemes` and `ComposeExtendedSchemes` list the registered schemes, built over
  `ComposeUnservedProtocolServers` with a policy of no accounts and no HTTP method
  (`ComposePolicyWithoutAccounts`), since nothing is served through them: `--version`'s
  `Protocols:` and `Extended:` lines and the `--aihelp` completeness tests read them.
- `ComposeContentFileSystem` and `ComposeContentStore` build the one content store
  (`DiskContentFileSystem` with `--directory`, a new `InMemoryContentFileSystem` without it,
  ADR-0031); `ComposeConnectionLimits` the serving engine's limits (ADR-0006).
- `LoadServiceStateAsync` loads the `ServiceState` after the data-directory lock: the one
  `MessageBus` (`ComposeMessageBusFiles` over `<path>/.surl`, `LoadMessageBusAsync`, one message
  bounded by `--max-filesize`), which the MQTT server for `mqtt` and `mqtts` keeps its retained
  messages on (BL-635), the one mail store
  (`ComposeMailStoreFiles`, `LoadMailStoreAsync`) and the LDAP server over its directory
  (`ComposeLdapDirectoryFile`, `LoadLdapServerAsync`), then the one `ObjectMetadataIndex` of the served
  root (`ComposeObjectMetadataIndex`, kept in `<path>/.surl/objects/index` with `--directory`,
  `LoadObjectMetadataIndexAsync`), then the DNS zones (`ComposeDnsZoneFolder`, `<path>/.surl/dns/*.zone` with
  `--directory`, none without it; `LoadDnsZonesAsync`, held on `ServiceState.DnsZones`); the first that cannot be loaded returns its
  `(37)` message, which the host writes after `surl: `.

- One event stream and one fault plan per start (ADR-0124 decisions 1 and 4, BL-803):
  `ComposeAsync` creates a `SurlEventStream` and, through `FaultPlanComposition.Compose`, a
  `FaultPlan` holding `SurlCommandLine.Faults` in order (`f1`, `f2` ...), never static, carried as
  the internal `EventStreamAndFaultPlan`. Both go to the content store (`ComposeContentStore`'s
  four-argument overload), the message bus, the mail store and the object metadata index
  (`LoadServiceStateAsync`), the policy (`AuthenticationComposition.Compose`'s `events`/`faults`),
  `ComposedProtocolServices.Events`/`Faults` and so `ExtendedProtocolServerServices`, and the
  `ServingEngine`, which hands them to every server through `ExchangeContext`.
  `ComposedServing.Events` (subscribe there) and `ComposedServing.Faults` expose them to the host.
  `ComposeUnservedProtocolServices` keeps the null objects (`DiscardingSurlEventPublisher`,
  `EmptyFaultPlan`). With any rule, `ServeAsync` writes `FaultPlanComposition.WriteWarning`'s
  `surl: warning: --fault: <n> fault rule(s) make this server misbehave on purpose` once, from info
  up, right after the other loosening warnings.

- Store control per instance (ADR-0124 decision 3, BL-805): `ComposedServing.Stores` is a
  `ComposedStores` over the start's content store and object metadata index, mail store, message bus
  and account book (the policy's `Settings.Accounts`, which `AuthenticationComposition.ComposeSettings`'
  three-argument overload builds over the instance's stream, so account changes are published too).
  Seeds go through each store's own seed path: `SeedMailAsync` and `SeedPublishAsync`/`SeedEnqueueAsync`
  then save the store (a bus save that fails throws `IOException` with its message), `SeedFileAsync`,
  `SeedFolder`, `AddAccount`, `RemoveAccount`. `ResetAsync`, `TakeSnapshotAsync` and `RestoreAsync` work on
  every store in `ComposedStores.StoreOrder` (content, mail, bus, accounts), or on one `StoreKind`; the
  first refusal stops a whole-instance operation with a `ComposedStoreException`
  (an `InvalidOperationException` whose `Store` names the store, the store's own refusal inner). A
  `ComposedSnapshot` holds one snapshot per store, `ByteCount` together, refused past
  `MaxSnapshotBytes` (`DefaultMaxSnapshotBytes`, 256 MiB) at the store that passed it; another
  instance's snapshot is an `ArgumentException`. With `--directory` the content store (its index is on
  disk), the mail store and the bus refuse reset, snapshot and restore, so a whole-instance reset or
  snapshot stops at `Content`; seeding them is allowed, and accounts stay controllable. Tests:
  `ComposedStoresTests`.

The types are `public` because more than one host calls them, but they are not a supported
programming interface and may change in any release.

The ordered start lives here (ADR-0099 decision 2, BL-614); which project runs each step:

| Step | Run by |
| --- | --- |
| Parse the command line, answer help and version, read `SURL_EXTENSIONS` | the host (`Surl.Console`'s `CommandLineRunner`) |
| `--auth gssapi` without `--keytab` and the `--ssh-ciphers`/`--ssh-macs` refusal (2), the data directory probe (37), the content store (`ContentWithoutDataDirectory` or a new `InMemoryContentFileSystem` without `--directory`), the TLS-first listen URL without a certificate (58) and the `scp`/`sftp` one without a host key (2), authentication and host keys, the lock (124, 23), the service state (37), the protocol servers, server TLS (58, 2, 77) | `SurlServerComposition.ComposeAsync`, from a `SurlCommandLine` and a `SurlServerCompositionSeams`, returning a `SurlServerStart`: a `SurlStartFailure` (exit code and lines after `surl: `; a start cancelled while loading the service state is `Ok` with no lines) or a `ComposedServing` |
| Open the log streams (`--log-file`, trace file) | the host, which disposes the `ComposedServing` on failure to release the lock |
| Start-up warnings and notes, the status lines, the `ServingEngine`, the bound listen URLs, bind failures (45, 6) and `(125)` | `ComposedServing.ServeAsync`, given `SurlServingOutputs`; `ListenerStartReporter` calls `AllListenersBound` once with every bound `ListenUrl`, never after a bind failure; it returns a `SurlServingOutcome` |
| Write each `surl: ` line and return the exit code | the host |

Disposing a `ComposedServing` releases the TLS certificates and then the data-directory lock; a
start refused or failed after the lock was taken releases it before `ComposeAsync` returns or
throws. `FormatBindFailure` and `FormatMissingCertificate` format the `(45)`/`(6)` and `(58)`
texts.

The compositions that read and check what a host is given at start live here too (ADR-0099
decision 1, BL-613); each returns a refusal as a `SurlExitCode` and the text after `surl: `
rather than writing it, and the warning writers take the log `TextWriter` they write to:

- `AuthenticationComposition` - the accounts (`--user`, `--user-file`), `--authorized-keys`, the
  `--auth` methods and the `AuthenticationPolicy` (ADR-0032); `WriteLooseningWarnings`.
- `MailboxComposition` - the mail domains and mailboxes (ADR-0102 decisions 1.1, 1.2, 1.4 and 5,
  BL-882): every `SurlCommandLine.Mailboxes` then each `MailboxFiles` record (read through
  `ReadStartFile` with `Surl.Cli`'s `MailboxFileReader`; unreadable `(37) Could not read mailbox
  file <file>`, malformed `(2) Mailbox file <file>, line <n>: ...`), each an `Account` named by its
  address and restricted to its `protocols=` (the mail schemes when absent), and the
  `MailDomainConfiguration` handed to `LoadServiceStateAsync`/`LoadMailStoreAsync`. It is composed
  before `AuthenticationComposition.Compose`, which adds its accounts after the `--user-file` ones
  and refuses one named like a `--user-file` account `(2)`; it rides on `ComposedAuthentication.Mailboxes`,
  and `WriteStartLines` writes the `-v` notes after the groups'. With no `MailDomains` it composes
  nothing and the start is as before.
- `AccountGroupExpansion` - every `--group` expanded to account names once the accounts are read
  (ADR-0102 decision 4.1), refusing `everyone` and `@everyone` (4.2), a member naming no account or
  group and a group that includes itself (ADR-0101 decision 5 row 18), each `(2)`; `Expand` turns a
  later option's member list into account names, and `WriteStartLines` writes each group's `-v`
  note after the other start lines. It rides on the start's `ComposedAuthentication`.
- `KeytabComposition` - the `--keytab` file and its `KerberosAcceptor` (ADR-0057 decision 1),
  with `RandomKerberosRandomSource`; `WriteStartLines`.
- `SshHostKeyComposition` - the `--hostkey`, `--throwaway-hostkey` and `--hostcert` host keys
  (ADR-0051 decision 4); `WriteStartLines`.
- `SshAlgorithmComposition` - the SSH algorithm offer and the `--ssh-ciphers`/`--ssh-macs`
  refusal (ADR-0066).
- `ServerTlsComposition` - the server TLS settings (`--cert`, `--self-signed`, `--cacert`), and
  `DescribeTlsFileFailure`, the exit code and lines for a TLS file that cannot be loaded (ADR-0020).
- `DataDirectoryProbe`, `DataDirectoryLock` and `DataDirectoryLockOutcome` - the `--directory`
  probe and its `.surl/lock` (ADR-0031 decision 7).

Tests: `Application/Surl.Composition.UnitTests` (`ServerTlsCompositionTests`,
`DataDirectoryLockTests`, `DataDirectoryProbeTests`, `ListenerStartReporterTests`,
`ComposedServingTests` and the start tests in `SurlServerCompositionTests.Start.cs`, which run
over `FakeListenerFactory` and `StartTestSeams` with no network, among them); the loading, the content store and
the start compositions are also exercised through `CommandLineRunner` in
`Application/Surl.Console.UnitTests`, and the coverage measure merges both.
