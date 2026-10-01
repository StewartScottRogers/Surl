# Surl.Protocol.Smb.UnitLibrary

Phase 5, built. How the server answers is decided in ADR-0073; what it answers, and what pinned
upstream curl has proven against it, is in `Documentation/Product/Product-Overview.md`, "Built
for Phase 5: LDAP, SMB and RTSP", and its terms are in `Documentation/Wiki/Glossary.md`, section
"LDAP, SMB and RTSP".

The SMB server: SMB version 1 (`NT LM 0.12`, no extended security), the only dialect upstream
curl speaks, over NetBIOS session framing, with curl's NTLMv1 session setup as its login,
serving and receiving files through the content store.

**URL schemes answered:** `smb`, `smbs`

What exists (BL-290, BL-296 to BL-298, ADR-0073 decisions 1 to 8):

- `SmbProtocolServer` - the public `IConnectionProtocolServer`, claiming `smb` and `smbs` itself
  (`smbs` arrives already secured by the engine, so `Surl.Console` registers it once, with no
  `ImplicitTlsSchemeServer`). Takes the `ContentStore`, the `ISmbAuthenticationPolicy` and an
  optional `ISmbChallengeSource` (`SmbSystemChallengeSource`, the operating system's generator,
  when none is given), and runs one `SmbExchange` per connection.
- `SmbExchange` - one connection from its first frame to its close: reads each NetBIOS frame with
  `SmbFrameReader`, has the session answer it, keeps answering every request (curl does not
  notice the server close while it waits), and applies the head timeout, `--max-message` and a
  limit told from shutdown (ADR-0059).
- `SmbSession` - the connection's one session: the negotiate (`SmbNegotiateResponse`), the
  session setup checked through `ISmbAuthenticationPolicy.CheckSmbNtlmV1LoginAsync` (refused
  `ERRSRV/ERRbadpw` and closed), the trees (`MaxTrees`, 16; a share is a top-level directory of
  the content store), the open files (`SmbOpenFile`, `MaxOpenFiles`, 16), reads of at most
  `MaxReadBytes` (61440) and uploads through `ContentStore.OpenUploadAsync` under
  `--allow-uploads`, committed by the close and discarded when the tree or the connection ends.
- The codec: `SmbRequestDecoder` decodes the eight requests curl sends (`SmbNegotiateRequest`,
  `SmbSessionSetupRequest`, `SmbTreeConnectRequest`, `SmbNtCreateRequest`, `SmbReadRequest`,
  `SmbWriteRequest`, `SmbCloseRequest`, `SmbTreeDisconnectRequest`), reporting a malformed one as
  an `SmbRequestFault`; `SmbResponseEncoder` writes their responses and every refusal
  (`EncodeError`) with a DOS-class `SmbStatus`; `SmbCommand` names the command codes.

What this library does not hold: the NTLMv1 check. The server hands the user, domain, challenge
and responses to `ISmbAuthenticationPolicy` in `Surl.Protocol.Abstractions`; `Surl.Authentication`
implements it with `NtlmV1Calculation` over `Surl.Cryptography`'s hand-built `Des` and `Md4`, and
accepts only when `--auth` names `ntlmv1`. The challenge source is this library's own
`ISmbChallengeSource`, not `Surl.Authentication`'s NTLM one (ADR-0002 decision 3).

`UpstreamCurlTransfersFilesWithSurlOverSmbTests` in `Surl.Conformance.UnitTests` proves the server
against ADR-0030's static-curl Windows build (ADR-0073 decision 11). `Surl.Protocol.Smb.UnitTests`
replays the requests that build sent (`Surl.Protocol.Smb.UnitTests/Fixtures/README.md`).

This library references `Surl.Protocol.Abstractions.UnitLibrary` and `Surl.Content.UnitLibrary`,
one of the horizontal libraries in ADR-0002 decision 3's table, as later ADRs amend it - nothing
else. Referencing another protocol server is a build break, and
`Surl.Protocol.Abstractions.UnitTests` fails if one appears.

Never construct a `Socket`, `TcpListener`, `UdpClient`, `SslStream` or `HttpListener`
here. The server receives its transport from the listener seam in
`Surl.Protocol.Abstractions`, so the tests in the matching `.UnitTests` project can drive
it with request bytes measured from pinned upstream curl, with no network.

Expected bytes come from a build pinned in `UpstreamCurlBuilds.json`, measured with
`Record-CurlExchange.ps1`, and never from the Curl port (ADR-0003).
