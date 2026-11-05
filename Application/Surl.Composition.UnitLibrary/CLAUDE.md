# Surl.Composition

The one place protocol servers are composed (ADR-0099 decision 1). It references every original
protocol server and the horizontal libraries they need; `Surl.Console` references it, and so will
`Surl.Api` and possibly `Surl.Mcp` (BL-612). It references no host, and no protocol server or
horizontal library may reference it (ADR-0099 decision 11, held by
`Foundation/Surl.Protocol.Abstractions.UnitTests/ProtocolIsolationTests.cs`).

- `SurlServerComposition.ComposeProtocolServers` builds every original server `surl` registers,
  in registration order, with an `ImplicitTlsSchemeServer` second registration for `https`,
  `imaps`, `ldaps`, `pop3s`, `smtps` and `wss`. A new protocol server is registered here, and in
  the same change gets its `--aihelp` facts in `Surl.Cli` (root `CLAUDE.md`, "`surl --aihelp`").
- `ExtendedProtocolServers.Compose` builds the extended servers (`Surl.Extended.Protocol.*`, none
  yet), each joining with one line; `ExtendedProtocolServers.AreTurnedOn` reads `SURL_EXTENSIONS`
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
- `LoadServiceStateAsync` loads the `ServiceState` after the data-directory lock: the MQTT
  retained messages (`ComposeRetainedMessageFile`, `LoadRetainedMessagesAsync`), the one mail store
  (`ComposeMailStoreFiles`, `LoadMailStoreAsync`) and the LDAP server over its directory
  (`ComposeLdapDirectoryFile`, `LoadLdapServerAsync`); the first that cannot be loaded returns its
  `(37)` message, which the host writes after `surl: `.

The types are `public` because more than one host calls them, but they are not a supported
programming interface and may change in any release. The start sequence still lives in
`Surl.Console` until BL-614 moves it.

The compositions that read and check what a host is given at start live here too (ADR-0099
decision 1, BL-613); each returns a refusal as a `SurlExitCode` and the text after `surl: `
rather than writing it, and the warning writers take the log `TextWriter` they write to:

- `AuthenticationComposition` - the accounts (`--user`, `--user-file`), `--authorized-keys`, the
  `--auth` methods and the `AuthenticationPolicy` (ADR-0032); `WriteLooseningWarnings`.
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
`DataDirectoryLockTests`, `DataDirectoryProbeTests` among them); the loading, the content store and
the start compositions are also exercised through `CommandLineRunner` in
`Application/Surl.Console.UnitTests`, and the coverage measure merges both.
