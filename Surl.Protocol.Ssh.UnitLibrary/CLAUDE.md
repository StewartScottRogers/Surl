# Surl.Protocol.Ssh.UnitLibrary

Phase 2. The SSH server for upstream curl's `scp://` and `sftp://` requests: the transport,
host keys and user authentication as
[ADR-0051](../Documentation/Planning/Decisions/ADR-0051-the-ssh-transport-host-keys-and-user-authentication.md)
decides (amended by
[ADR-0058](../Documentation/Planning/Decisions/ADR-0058-the-ssh-key-exchange-and-host-key-reading-choices-adr-0051-left-open.md),
[ADR-0060](../Documentation/Planning/Decisions/ADR-0060-messages-before-the-clients-kexinit-in-a-server-started-ssh-re-exchange.md)
and
[ADR-0061](../Documentation/Planning/Decisions/ADR-0061-blowfish-cast-128-and-ripemd-160-for-curls-openssl-builds.md)),
and SCP and SFTP as
[ADR-0054](../Documentation/Planning/Decisions/ADR-0054-how-the-ssh-server-answers-upstream-curls-scp-and-sftp-requests.md)
decides.
[ADR-0062](../Documentation/Planning/Decisions/ADR-0062-surl-keeps-ks-canonical-mpint-though-libssh2-1-11-1-on-wincng-fails-1-exchange-in-256.md)
records why the exchange hash keeps K's canonical `mpint`. Terms (host key, algorithm offer,
weak SSH algorithm, strict key exchange, key re-exchange, session channel, SCP command, SFTP
subsystem) are the glossary's, section "SSH, SCP and SFTP".

**URL schemes answered:** `scp`, `sftp` - one server for both.

## What it holds

- **The transport** (`SshProtocolServer`, `SshTransportHandshake`, `SshPacketReader`,
  `SshPacketWriter`): identification lines, `KEXINIT` and negotiation against
  `SshAlgorithmOffer`, strict key exchange, the key exchange methods (`SshCurve25519KeyExchange`,
  `SshEcdhKeyExchange`, `SshFiniteFieldKeyExchange`, `SshGroupExchangeKeyExchange`), `NEWKEYS`,
  packet protection (`SshChaCha20Poly1305Protection`, `SshAesGcmProtection`,
  `SshCipherAndMacProtection` over `SshAesCtr`, `SshCbc`, `SshBlockCbc`, `SshArcfour` and
  `SshHmac`), zlib compression, and key re-exchange (`SshReExchangeLimits`).
- **Host keys** (`SshHostKey`, `SshHostKeySet`, `SshHostKeyFile`): RSA, ECDSA, Ed25519 and DSA
  keys, read from OpenSSH (encrypted ones too), PKCS #8, PKCS #1 and SEC 1 files, each refusal
  typed as an `SshHostKeyRefusal`. Reading the files from disk is `Surl.Console`'s.
- **User authentication** (`SshUserAuthentication`, `SshUserKeySignature`): `none`, `password`,
  `keyboard-interactive` and `publickey`, each credential judged by `ISshAuthenticationPolicy`.
- **The connection protocol** (`SshConnectionProtocol`, `SshSessionChannel`): session channels,
  an `exec` of an SCP command (`SshScpCommand`) handed to `ScpDownloadHandler` (`-f`) or
  `ScpUploadHandler` (`-t`), and the `sftp` subsystem to `SftpSession`, all serving the
  content store through `SshContentChannelHandlers` and `SshContentPath`.
- **The weak algorithms** of ADR-0051 decision 2 and ADR-0061, offered only when
  `SshAlgorithmOffer.Default` is given `allowWeakAlgorithms`, as `Surl.Console` does under
  `--allow-weak-ssh-algorithms`. `SshAlgorithmOffer.Narrowed` narrows an offer's ciphers and MACs
  to the names given, in the order given (`--ssh-ciphers`, `--ssh-macs`, ADR-0066). Host
  certificates (`--hostcert`) are not built here.

## References

`Surl.Protocol.Abstractions.UnitLibrary`, `Surl.Content.UnitLibrary`, and the hand-built
primitive libraries, each in ADR-0002 decision 3's table as a later ADR amended it:

| Library | For | ADR |
| --- | --- | --- |
| `Surl.Cryptography.Curve25519.UnitLibrary` | X25519, for `curve25519-sha256` | ADR-0048 |
| `Surl.Cryptography.Ed25519.UnitLibrary` | `ssh-ed25519` host and user keys | ADR-0048 |
| `Surl.Cryptography.ChaCha20.UnitLibrary`, `Surl.Cryptography.Poly1305.UnitLibrary` | `chacha20-poly1305@openssh.com` | ADR-0048 |
| `Surl.Cryptography.Rc4.UnitLibrary` | `arcfour`, `arcfour128` (weak) | ADR-0051 decision 3 |
| `Surl.Cryptography.BcryptPbkdf.UnitLibrary` | encrypted `openssh-key-v1` host keys | ADR-0051 decision 3 |
| `Surl.Cryptography.Blowfish.UnitLibrary`, `Surl.Cryptography.Cast128.UnitLibrary` | `blowfish-cbc`, `cast128-cbc` (weak) | ADR-0061 |
| `Surl.Cryptography.Ripemd160.UnitLibrary` | `hmac-ripemd160`, `hmac-ripemd160@openssh.com` (weak) | ADR-0061 |

Everything else it needs (SHA-2, HMAC, AES, AES-GCM, ECDH, ECDSA, RSA, `BigInteger`, zlib, and
for the weak algorithms SHA-1, MD5, triple DES and DSA) comes from the base class library. How
a primitive composes into SSH - CBC chaining, key derivation, the packet format - stays here
(ADR-0048 decision 3).

## Rules

Reference nothing beyond the list above without an ADR that adds the library to ADR-0002
decision 3's table. Referencing another protocol server is a build break, and
`Surl.Protocol.Abstractions.UnitTests` fails if one appears.

Never construct a `Socket`, `TcpListener`, `UdpClient`, `SslStream` or `HttpListener`
here. The server receives its transport from the listener seam in
`Surl.Protocol.Abstractions`, so the tests in the matching `.UnitTests` project can drive
it with request bytes measured from pinned upstream curl, with no network.

Expected bytes come from a build pinned in `UpstreamCurlBuilds.json`, measured with
`Record-CurlExchange.ps1`, and never from the Curl port (ADR-0003). Pinned upstream curl's
proof against a live `surl` lives in `Surl.Conformance.UnitTests`
(`UpstreamCurlLogsInToSurlOverSshTests`, `UpstreamCurlOffersSshAlgorithmsTests`,
`UpstreamCurlTransfersFilesWithSurlOverScpTests`, `UpstreamCurlTransfersFilesWithSurlOverSftpTests`).
