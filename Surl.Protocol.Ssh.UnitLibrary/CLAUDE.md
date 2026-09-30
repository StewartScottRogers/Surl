# Surl.Protocol.Ssh.UnitLibrary

Phase 2.

The SSH server with its SCP and SFTP subsystems: key exchange, host keys, password and
public-key user authentication, and the file operations upstream curl's libssh2 build
performs, over the hand-built primitives ADR-0048 places in four libraries:
`Surl.Cryptography.Curve25519.UnitLibrary` (X25519), `Surl.Cryptography.Ed25519.UnitLibrary`,
`Surl.Cryptography.ChaCha20.UnitLibrary` and `Surl.Cryptography.Poly1305.UnitLibrary`, and ADR-0051's
`Surl.Cryptography.Rc4.UnitLibrary` for the `arcfour` ciphers offered with `--allow-weak-ssh-algorithms`.
Everything else it needs (SHA-2, HMAC, AES, AES-GCM, ECDH, ECDSA, RSA, `BigInteger`, and for the weak algorithms SHA-1, MD5, triple DES and DSA) comes
from the base class library.

**URL schemes answered:** `scp`, `sftp`

This library references `Surl.Protocol.Abstractions.UnitLibrary`, and may also reference
the horizontal libraries in ADR-0002 decision 3's table as later ADRs amend it - among
them the four ADR-0048 primitive libraries above - and nothing else. Referencing another protocol server is a
build break, and `Surl.Protocol.Abstractions.UnitTests` fails if one appears.

Never construct a `Socket`, `TcpListener`, `UdpClient`, `SslStream` or `HttpListener`
here. The server receives its transport from the listener seam in
`Surl.Protocol.Abstractions`, so the tests in the matching `.UnitTests` project can drive
it with request bytes measured from pinned upstream curl, with no network.

Expected bytes come from a build pinned in `UpstreamCurlBuilds.json`, measured with
`Record-CurlExchange.ps1`, and never from the Curl port (ADR-0003).
