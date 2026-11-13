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
- The HTTP and HTTPS proxies (ADR-0088 Amendment 6, BL-1003): `ComposeAsync` appends `SurlCommandLine.ProxyListenUrls`
  to the listen URLs, and `ProxyComposition.RouteProxyListeners` wraps each server answering `http` or `https` in a
  `ProxyRoutingProtocolServer`, which serves a listen URL with `IsProxy` by one `HttpProxyProtocolServer` and any other by
  the wrapped server. The proxy reaches origins through `ComposedProtocolServices.OriginConnector` (a `SocketOriginConnector`
  over `ProxyComposition.ComposeAllowedOriginRule`, or `SurlServerCompositionSeams.CreateOriginConnector`) and judges
  `Proxy-Authorization` by `ComposedProtocolServices.ProxyAuthenticationPolicy`
  (`AuthenticationComposition.ComposeProxyPolicy`, over `-U` and `--proxy-user-file` alone); `--proxy-allow-origin '*'` warns.
- SOCKS (ADR-0121 decision 5, BL-560): one `ProtocolServerRegistrations.Original` entry for `socks4`, `socks4a`,
  `socks5` and `socks5h`, a `SocksProtocolServer` over the same `ComposedProtocolServices.OriginConnector` and
  `ProxyAuthenticationPolicy` as the HTTP proxy (so `-U`, `--proxy-user-file` and `--proxy-allow-origin` apply).
  A SOCKS listen URL, positional or from `--proxy`, is listened on like any other, and
  `ProxyComposition.ComposeAllowedOriginRule` refuses its own end point too. The unserved services and the
  long `ComposeProtocolServers` overload give it an `InMemoryOriginConnector`, since nothing is served through them.
- `SurlServerCompositionSeams.AdditionalProtocolServers` (`AdditionalProtocolServer`: a registration
  and its already-parsed `ListenUrl`s) are composed after the extended servers and served on their
  listen URLs after the command line's: how `Surl.Api` serves a test's own server (BL-813).
- `--http3` (ADR-0082 decision 6, BL-384): `ServerTlsComposition.QuicCertificate` is the served
  certificate as a `QuicServerCertificate` (DER chain and private key), and the start adds an
  `Http3ProtocolServer` for `https` after server TLS, beside the HTTP server's `https`
  registration, so the serving engine binds TCP and UDP on one port; the HTTP server then adds
  `Alt-Svc`. `ListenerStartReporter` counts such a pair as its connection listener alone (one
  status line) and `ComposedServing` writes `* Serving HTTP/3 over QUIC on UDP port <port>` from
  verbose up. With `--cacert` it gives the HTTP/3 server a `QuicClientCertificateTrust` over the
  same anchors, so every QUIC handshake requires a client certificate as TCP does (BL-741,
  ADR-0082 Amendment 5).
- `ExtendedProtocolServers.Compose` builds the extended servers (`Surl.Extended.Protocol.*`: today
  the AMQP 1.0 container for `amqp1` and `amqp1s` (BL-514) - one `Amqp1Container` over the one
  `MessageBus`, `AuthenticationPolicyFor("amqp1")`, an `Amqp1ContainerOptions` of `IdleTimeout` and an
  `Amqp1SecurityNegotiationOptions` of the policy's `AllowAnonymous` and `IsTlsUpgradeAvailable`;
  `amqp1s` is in `TlsSchemes` as `mqtts` is, and `amqp1` is one of `ServerTlsComposition`'s
  upgradable schemes for its TLS header (ADR-0107 decisions 1 and 7) -
  the DNS server for `dns` (BL-1032), two entries - `DnsUdpProtocolServer` and `DnsTcpProtocolServer` -
  so the engine binds UDP and TCP on one port, both over `ComposedProtocolServices.DnsZones` and
  `AllowsZoneTransfer` (`--allow-zone-transfer`, ADR-0092 decision 4),
  the gRPC server for `grpc` and `grpcs`, BL-1093 (one registration over `AuthenticationPolicyFor("grpc")`, stateless; `grpcs` is TLS from the first byte through `TlsSchemes`, ALPN `h2`),
  the Kafka broker for `kafka` and `kafkas`, BL-973 (one registration claiming both schemes; `kafkas` is TLS from the first byte through `TlsSchemes`, ADR-0105 decision 7; every answerer over the one `MessageBus` the MQTT server shares, the policy `AuthenticationPolicyFor("kafka")`, Produce and InitProducerId over one `KafkaProducerStates`, Fetch in the codec `--kafka-fetch-compression` names, BL-974, uncompressed by default), the AMQP 0-9-1 broker for `amqp` and `amqps`, BL-507 (one registration claiming both schemes over the one `MessageBus` the MQTT server shares, `AuthenticationPolicyFor("amqp")`, `--idle-timeout` and the policy's `AllowAnonymous` as `OffersAnonymous`; `amqps` is TLS through `TlsSchemes`), the NATS server for `nats` and `natss`, BL-528, the STOMP broker for `stomp` and `stomps` over the one `MessageBus` (`stomps` is TLS through `TlsSchemes`, BL-533), the Google Cloud Storage server for `gs`, BL-1026 (one registration over the shared content store, object metadata index and `AuthenticationPolicyFor("gs")`; `gs` is TLS from the first byte through `TlsSchemes`, ADR-0110 decision 5, ALPN `http/1.1`, so no `ImplicitTls` entry is needed and a `gs` listen URL without `--cert` or `--self-signed` is `(58)`), the OCI registry server for `oci`, BL-1028 (one registration over the shared content store and `AuthenticationPolicyFor("oci")`; `oci` is TLS from the first byte through `TlsSchemes`, ADR-0115 decision 6, ALPN `http/1.1`, so a `oci` listen URL without `--cert` or `--self-signed` is `(58)`, unless `--oci-plaintext` (BL-971) marks it plain through `OciPlaintextComposition.MarkOciListenUrlsPlain`, as `--s3-plaintext` does for `s3`; `--az-plaintext` (BL-1025) does the same for `az` through `AzPlaintextComposition.MarkAzListenUrlsPlain`), the S3 server for `s3`, BL-421, and the Socket.IO server for `socketio` and `socketios`, BL-455, one server over one `SocketIoSessions`; `socketios` is TLS through `TlsSchemes`) from
  `ExtendedProtocolServers.Registrations`, the same
  `ProtocolServerRegistration` record, each joining with one entry; its `ExtendedProtocolServerServices`
  wraps the start's `ComposedProtocolServices` (the S3 server reads its content store, policy, object
  metadata index and clock); `s3` is TLS from the first byte through `TlsSchemes` (ADR-0108 decision 5),
  so it needs no `ImplicitTls` entry; under `--s3-plaintext` `S3PlaintextComposition.MarkS3ListenUrlsPlain` sets
  each `s3` listen URL's `ListenUrl.IsImplicitTls` to `false` at the start of `ComposeAsync`, and
  `ServerTlsComposition` reads that per-listen-URL answer, so a plain `s3` needs no certificate (ADR-0108 Amendment 5). The NATS server is built over the one `MessageBus` the MQTT
  server shares, `AuthenticationPolicyFor("nats")`, and a `NatsServerOptions` of
  `ComposedProtocolServices.IdleTimeout` (the start sets it to `ComposeConnectionLimits`' idle timeout)
  and the policy's `AllowAnonymous`. `natss` starts in plain text and upgrades (ADR-0113 decision 6),
  so it is not in `TlsSchemes`; `ServerTlsComposition` lists it as requiring a certificate, so a
  `natss` listen URL without `--cert` or `--self-signed` is refused `(58)` like an implicit-TLS one; `ComposeUnservedExtendedServices` wraps
  `ComposeUnservedProtocolServices` for listing schemes. `ExtendedProtocolServers.AreTurnedOn` reads `SURL_EXTENSIONS`
  through an `IEnvironmentVariableReader` (`ProcessEnvironmentVariableReader` in `surl`); `0`
  turns them off (ADR-0083 decisions 6 to 8). `SurlServerComposition.ComposeExtendedProtocolServers`
  never calls the composer while they are off.
- `mcp+stdio:` (ADR-0111 decision 5, BL-979): `ExtendedProtocolServers.Registrations` has an entry for the scheme whose
  `McpStdioListenUrlServer` only claims it, so the parser accepts it (the listen URL has an empty host and port 0;
  `ListenUrlParser` reads it apart). It is never served through the engine: the host calls `McpStdioComposition.IsAsked`,
  `FindRefusal` (another or proxy listen URL beside it, or `--trace`/`--trace-ascii`/`--log-file` to `-`, each `FailedInit`)
  and `ServeAsync`, which serves `McpStdioSession` over the content store on the streams it is given, writing the
  `Listening on mcp+stdio:` line (info up) to the log only. `SURL_EXTENSIONS=0` leaves the scheme unregistered.
- DNS over HTTPS (ADR-0092 Amendment 4, BL-1018): `--dns-transport https` expands a `dns` listen URL to a connection-only one with `IsImplicitTls` and `IsDnsOverHttps` on 443; the `dns` connection registration is a `DnsConnectionRoutingProtocolServer` sending it to `Http2ConnectionServer` (ALPN `h2`) or `Http1ConnectionServer`, both over a `DnsOverHttpsHandler`, and every other listen URL to `DnsTcpProtocolServer`. Refused beside `tcp` or `tls` on one explicit port (2); needs `--cert` or `--self-signed` (58).
- DNS over QUIC (ADR-0092 decision 5, BL-1019): `--dns-transport quic` expands a `dns` listen URL to a datagram-only one with `IsImplicitTls` on 853 (`DnsTransportComposition`; refused beside `udp` on one explicit port and on port 53, exit 2); the `dns` datagram registration is a `DnsDatagramRoutingProtocolServer` sending that listen URL to a `DnsOverQuicProtocolServer` (built on its first flow from `QuicCertificate`) and every other to `DnsUdpProtocolServer`. It needs `--cert` or `--self-signed` (58).
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

- AMQP 0-9-1 exchanges on disk (BL-966): after the extended servers are composed and before any listener binds,
  `ComposeUnderTheLockAsync` calls `LoadAmqp0ExchangesAsync`, which with `--directory` hands each `Amqp0Broker` an
  `Amqp0ExchangeFiles` over `<path>/.surl` (`.surl/amqp0/exchanges`); a malformed file is `(37) Could not read <file>: <reason>`.
  Without `--directory` nothing is loaded. Tests: `SurlServerCompositionTests.Amqp0Exchanges.cs`.

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
  `DeleteFileAsync` (BL-954; Done or Absent, never the fault plan), `SeedFolder`, `AddAccount`, `RemoveAccount`. `ResetAsync`, `TakeSnapshotAsync` and `RestoreAsync` work on
  every store in `ComposedStores.StoreOrder` (content, mail, bus, accounts), or on one `StoreKind`; the
  first refusal stops a whole-instance operation with a `ComposedStoreException`
  (an `InvalidOperationException` whose `Store` names the store, the store's own refusal inner). A
  `ComposedSnapshot` holds one snapshot per store, `ByteCount` together, refused past
  `MaxSnapshotBytes` (`DefaultMaxSnapshotBytes`, 256 MiB) at the store that passed it; another
  instance's snapshot is an `ArgumentException`. With `--directory` the content store (its index is on
  disk), the mail store and the bus refuse reset, snapshot and restore, so a whole-instance reset or
  snapshot stops at `Content`; seeding them is allowed, and accounts stay controllable. Tests:
  `ComposedStoresTests`.
- The control endpoint's target (ADR-0124 decision 7, BL-1022): the internal `ComposedControlTarget` is
  `Surl.Control`'s `IControlTarget` over a start's `Events`, `Faults` and `Stores`, and `ComposedSnapshot` is its
  `IControlSnapshot`. `AddFault` reads one item as `--fault` does (a bad item `BadRequest` with the reader's text, a
  full plan `Conflict`); `ListFaults` writes each rule back in that grammar through `FaultItemText`. Seeding mail
  refuses an invalid address `BadRequest`, no such account or a recipient outside the mail domains `NotFound`, a
  failed delivery `Conflict`; a file path a request may not name is `BadRequest`; a bus refusal maps by
  `RefusalKindOf`; a store on disk refused reset, snapshot or restore is `Conflict`. `ReadEventsAsync` reads its own
  subscription, bytes included, and ends without throwing when cancelled.
  Tests: `ComposedControlTargetTests`, `FaultItemTextTests`.
- Serving the control endpoint (ADR-0124 decision 7, BL-977): with `--control`, `ControlComposition.ReadToken` has read
  the token (`ComposedAuthentication.ControlToken`), and `ComposedServing.ServeAsync` first binds the control listener
  through the same `IListenerFactory`, on its own `ServingEngine` over a `ControlProtocolServer` (a `ControlEndpoint`
  over `ComposedControlTarget`): at most `ControlComposition.MaxConnections` (8) connections of its own, outside
  `--max-connections`, no fault plan, nothing published or logged. Its bound URL is `ControlListenUrl`; the start-up
  warnings then follow, with `ControlComposition.WriteWarning` after the `--fault` one, and
  `Control endpoint listening on <bound URL>` is written after the `Listening on` lines (info up). A bind failure
  ends the start with its exit code (45) before any other line; a stop before it binds ends `Ok` with no line. The
  control engine stops with the instance. Tests: `SurlServerCompositionTests.Control.cs`.

- Identifier sources per instance (ADR-0124 decision 6, BL-997): `ComposedServing` builds a
  `SeededIdentifierRandom` from `SurlCommandLine.RandomSeed` when present (none otherwise) and hands the
  `ServingEngine` `IdentifiersForExchange`, which stamps `ExchangeContext.Identifiers` with
  `ForExchange(exchangeId)` (the cryptographic source unseeded). `IdentifiersForServer(name)` is the
  server-life source for per-server fixes (BL-850); no static state, so instances never share one.
  BL-1001: the start also sets `ComposedProtocolServices.IdentifiersForServer` (the same derivation), which the
  Kafka `KafkaMetadataAnswerer` (`ComposeKafkaMetadataAnswerer`, name `kafka`) and the `NatsServer` (name `nats`) draw
  their cluster ID and `server_id` from, so a seeded instance repeats them. Tests: `ServerLifeIdentifiersCompositionTests`.

The types are `public` because more than one host calls them, but they are not a supported
programming interface and may change in any release.

The ordered start lives here (ADR-0099 decision 2, BL-614); which project runs each step:

| Step | Run by |
| --- | --- |
| Parse the command line, answer help and version, read `SURL_EXTENSIONS` | the host (`Surl.Console`'s `CommandLineRunner`) |
| With `--control`: read the token file (37, 2), after every other start file and before the data-directory lock | `SurlServerComposition.ComposeAsync` through `ControlComposition.ReadToken` |
| `--auth gssapi` without `--keytab` and the `--ssh-ciphers`/`--ssh-macs` refusal (2), the data directory probe (37), the content file system (`ContentWithoutDataDirectory` or a new `InMemoryContentFileSystem` without `--directory`), the TLS-first listen URL without a certificate (58) and the `scp`/`sftp` one without a host key (2), authentication, groups and host keys, the shares and buckets (2, 37) and the content store, the lock (124, 23), the service state (37), the protocol servers, server TLS (58, 2, 77) | `SurlServerComposition.ComposeAsync`, from a `SurlCommandLine` and a `SurlServerCompositionSeams`, returning a `SurlServerStart`: a `SurlStartFailure` (exit code and lines after `surl: `; a start cancelled while loading the service state is `Ok` with no lines) or a `ComposedServing` |
| Open the log streams (`--log-file`, trace file) | the host, which disposes the `ComposedServing` on failure to release the lock |
| With `--control`: bind the control listener first (45, 6 before any other line), then the start-up warnings (the control one after the `--fault` one), the protocol listeners, and the control status line after the `Listening on` lines | `ComposedServing.ServeAsync` |
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
- `ReadableByOthersWarning` - `surl: warning: <file> is readable by other users and may hold
  passwords` (ADR-0101 decision 5, BL-684, BL-780), written by `ComposedServing` through
  `WriteForFilesThatMayHoldPasswords` for each `SurlCommandLine.ConfigFiles` file in the order read
  (never `-`, standard input) and then the `--user-file`, right after the `--fault` warning, from
  info up, when the mode read through `SurlServerCompositionSeams.ReadStartFileUnixMode`
  (`ReadDiskMode` by default: `null` on Windows or when unreadable) grants group or other read.
- `MailboxComposition` - the mail domains and mailboxes (ADR-0102 decisions 1.1, 1.2, 1.4 and 5,
  BL-882): every `SurlCommandLine.Mailboxes` then each `MailboxFiles` record (read through
  `ReadStartFile` with `Surl.Cli`'s `MailboxFileReader`; unreadable `(37) Could not read mailbox
  file <file>`, malformed `(2) Mailbox file <file>, line <n>: ...`), each an `Account` named by its
  address and restricted to its `protocols=` (the mail schemes when absent), and the
  `MailDomainConfiguration` handed to `LoadServiceStateAsync`/`LoadMailStoreAsync`. It is composed
  before `AuthenticationComposition.Compose`, which adds its accounts after the `--user-file` ones
  and refuses one named like a `--user-file` account `(2)`; it rides on `ComposedAuthentication.Mailboxes`,
  and `WriteStartLines` writes the `-v` notes after the groups'. With no `MailDomains` it composes
  nothing and the start is as before. BL-994: the configuration also carries `MailAliases`,
  `MailCatchAlls` and each mailbox's `QuotaBytes`/`Folders`; `ComposeSharedFolders` adds the
  `--shared-folder`s after the groups are expanded (`@group` members become account names, `@everyone`
  is `(2)`); `WriteWarnings` writes the `--mail-catch-all` warning from info up; `DescribeRouteFailure`
  gives a `MailRouteException`'s `(2)` text, which `LoadMailStoreAsync` returns and the start maps to exit 2.
- `AccountGroupExpansion` - every `--group` expanded to account names once the accounts are read
  (ADR-0102 decision 4.1), refusing `everyone` and `@everyone` (4.2), a member naming no account or
  group and a group that includes itself (ADR-0101 decision 5 row 18), each `(2)`; `Expand` turns a
  later option's member list into account names, and `WriteStartLines` writes each group's `-v`
  note after the other start lines. It rides on the start's `ComposedAuthentication`.
- `ContentShareComposition` - every `--share`, `--share-access` and `--bucket` composed into the one
  `ContentStore`'s shares and buckets (ADR-0102 decisions 2, 4 and 5, BL-988), after the groups:
  a `--share-access` member naming nothing `(2)`, then per share a name that is not one path segment
  or is `.surl` `(2) option --share: share <name> is not a valid folder name`, a path that does not
  open through `SurlServerCompositionSeams.CanOpenShareDirectory` (`DataDirectoryProbe.CanOpen` by
  default) `(37) Could not open directory <path> for share <name>`, and a path overlapping
  `--directory` or an earlier share, compared ignoring case, `(2) option --share: share <name> overlaps
  <other>`; then a bucket's name or content path `(2)`. Shares are read through
  `SurlServerCompositionSeams.ShareFileSystem` (a `DiskContentFileSystem` by default); `protocols=`
  gains each TLS twin (`WithTlsTwins`); groups expand to account names, `@everyone` stays its marker,
  and a member granted twice keeps the wider access. `ComposedServing` writes `WriteWarnings`' `@everyone`
  warning after the `--fault` one and `WriteStartLines`' `-v` notes after the mailboxes'.
  Bucket folders are checked at start by `PrepareBucketFolders` (ADR-0102 decision 2.4, BL-1058), after the content
  store is composed: a content path whose first segment names a share, or any path with `--directory`, must be an
  existing folder (`(37) Could not open directory <path> for bucket <name>`); one in the in-memory root is created
  empty through `ContentStore.SeedFolder` (the store's configured state already holds it, so a reset restores it).
- `MessageBusComposition` - every `--topic`, `--queue`, `--topic-map` and `--topic-access` composed into the
  one bus's `MessageBusConfiguration` (ADR-0102 decisions 3, 3.6 and 5, BL-991), after the shares and before the
  lock: a filter that is not canonical, more than 1000 queues, a map scheme `TopicNameLookup` does not know, a
  name not valid on its scheme or aliased twice on one scheme family (a scheme and its TLS twin), and a member
  naming nothing, each `(2)`. Groups expand to account names, `@everyone` becomes `TopicAccessGrant.Everyone`,
  and a member reached twice in a row keeps the union of its rights. `ComposeAsync` hands the configuration
  through `LoadServiceStateAsync` to `LoadMessageBusAsync`, both taking it as an optional last parameter
  (`None` without one). `ComposedServing` writes `WriteWarnings`' `@everyone` warning after the share one and
  `WriteStartLines`' `-v` notes after the shares', both read from `Stores.Bus.Configuration`.
- `KeytabComposition` - the `--keytab` file and its `KerberosAcceptor` (ADR-0057 decision 1),
  with `RandomKerberosRandomSource`; `WriteStartLines`.
- `SshHostKeyComposition` - the `--hostkey`, `--throwaway-hostkey` and `--hostcert` host keys
  (ADR-0051 decision 4); `WriteStartLines`.
- `SshAlgorithmComposition` - the SSH algorithm offer and the `--ssh-ciphers`/`--ssh-macs`
  refusal (ADR-0066).
- `ServerTlsComposition` - the server TLS settings (`--cert`, `--self-signed`, `--cacert`), and
  `DescribeTlsFileFailure`, the exit code and lines for a TLS file that cannot be loaded (ADR-0020).
  Under TLS-SRP (`SurlCommandLine.ServesTlsSrp`, ADR-0122 decisions 4 and 5, BL-567) it builds
  `ServerTlsSettings.ForTlsSrp` instead, over the accounts `TlsSrpAccountsComposition` reads (the
  `--srp-groups` then `--srp-verifiers` files through `ReadStartFile`, then the `--tlsuser` account,
  its salt drawn again while its first byte is zero) and one `RandomNumberGenerator` it owns; a file
  that cannot be read is `TlsSrpAccountsException` `(37)`, a user given twice `(2)`. Each
  `--srp-groups` group outside RFC 5054 Appendix A lands in `SrpGroupIndexesOutsideRfc5054`, and
  `WriteStartLines` notes it once from `-v` up, after the `--self-signed` lines (ADR-0122 Amendment 3). No certificate is
  needed then except for `webtransport` and `coaps`, and `IsTlsUpgradeAvailable` (a certificate, or
  TLS-SRP) is what makes the STARTTLS-style servers offer their upgrade.
- `DataDirectoryProbe`, `DataDirectoryLock` and `DataDirectoryLockOutcome` - the `--directory`
  probe and its `.surl/lock` (ADR-0031 decision 7).

Tests: `Application/Surl.Composition.UnitTests` (`ServerTlsCompositionTests`,
`DataDirectoryLockTests`, `DataDirectoryProbeTests`, `ListenerStartReporterTests`,
`ComposedServingTests` and the start tests in `SurlServerCompositionTests.Start.cs`, which run
over `FakeListenerFactory` and `StartTestSeams` with no network, among them); the loading, the content store and
the start compositions are also exercised through `CommandLineRunner` in
`Application/Surl.Console.UnitTests`, and the coverage measure merges both.
