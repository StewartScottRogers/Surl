# ADR-0020 — How surl composes https

- **Status:** Accepted
- **Date:** 2026-09-29
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-09-29

## Context

ADR-0010 fixed the server-side TLS contract: the serving engine performs the implicit
handshake for a scheme `TlsSchemes.IsImplicitTls` names (BL-065), `Surl.Networking` loads
`--cert`, `--key` and `--cacert` (BL-067) and makes a throwaway certificate when no `--cert`
is given, and failures map to `CertificateProblem` (58), `FailedInit` (2) and
`CaCertificateBadFile` (77) (BL-064). It left to the composition root three things BL-038
had to decide: how `https` reaches the HTTP server, when the TLS files are read, and the
exact texts surl writes for a bad file and for the throwaway certificate.

`HttpProtocolServer.Schemes` lists only `http`, and while BL-038 ran, BL-050 held
`Surl.Protocol.Http.UnitLibrary`.

### Measured against the pinned build

Upstream curl 8.21.0 (x86_64-w64-mingw32, Schannel, pinned in `UpstreamCurlBuilds.json`)
fetching `https://127.0.0.1:<port>/hello.txt` from in-process `surl`, 2026-09-29, pinned
by `UpstreamCurlFetchesFromSurlOverHttpsTests`:

| surl serves | curl arguments | Exit code |
| --- | --- | --- |
| throwaway certificate | `-sS -k` | 0, the file's bytes |
| throwaway certificate | `-sS` | 60 (`CURLE_PEER_FAILED_VERIFICATION`) |
| `--cert`/`--key` signed by a test CA | `-sS --ssl-no-revoke --cacert ca.pem` | 0, the file's bytes |
| `--cert`/`--key` signed by a test CA | `-sS --cacert ca.pem` | 60 (Schannel: revocation status unknown) |
| `--cert`/`--key` signed by a test CA | `-sS --ssl-no-revoke` | 60 |

## Decision

1. **`https` is the HTTP server registered a second time, in `Surl.Console`.**
   `ImplicitTlsSchemeServer(server, "https")` lists `https` as its one scheme and hands
   every exchange to the same `HttpProtocolServer` instance unchanged. The engine has
   already secured the connection, so there is still exactly one HTTP server (ADR-0002).
   The wrapper refuses a scheme that is not TLS from the first byte, so it cannot turn into
   a general alias. Registering `https` here keeps the choice of which schemes surl serves
   in the composition root, and `wss`, `imaps` and the rest follow the same way.
2. **The TLS files are read only when a listen URL is TLS from the first byte**, before any
   listener binds. With only plaintext listen URLs, `--cert`, `--key` and `--cacert` are
   parsed but never opened, as upstream curl opens `--cert` only for a TLS transfer. Once an
   upgrading protocol (FTP, SMTP, IMAP, POP3) is registered, its schemes join this test
   (ADR-0010, section 3).
3. **Texts.** After the `surl: ` prefix (ADR-0007, section 5):
   - `--cert` or `--key` unusable: `(58) Could not load the server certificate: <reason>`,
     exit 58.
   - `--cacert` missing: upstream curl's two lines,
     `The file '<path>' provided to --cacert does not exist` and
     `option --cacert: is badly used here`, exit 2.
   - `--cacert` holds no readable certificate: `(77) Could not load the CA certificates: <reason>`,
     exit 77.

   `<reason>` is the loader's message, which names the file.
4. **The throwaway note.** With `-v`, surl writes `* Serving a throwaway certificate,
   SHA-256 <64 upper-case hex digits>` to stderr once, before the status lines. It belongs to
   no exchange, so it carries no `#<id>` prefix.
5. **Tests trust a test CA with `--cacert` and `--ssl-no-revoke`.** The conformance tests
   generate a CA and a server certificate it signed with `CertificateRequest` at test time.
   `--ssl-no-revoke` is what the Schannel build needs (measured above), and OpenSSL builds
   accept it and ignore it, so one test holds on every platform. The Schannel revocation
   failure is pinned in its own Windows-only test.

## Consequences

- `surl --version` lists `https`.
- `CommandLineRunner` takes a function that creates the listener factory from the TLS
  settings, so the settings are built after the command line is parsed.
- `HttpProtocolServer`'s own `Schemes` comment ("`https` joins it with the TLS contract")
  no longer describes where `https` is registered; BL-083 corrects it.
