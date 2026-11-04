# Surl.Kerberos.TestKdc.UnitLibrary

The hand-built loopback KDC of ADR-0065 decision 1: a **test fixture**, not a surl server. It
serves one realm, `SURL.TEST`, for one user whose password the test gives and the service
principals the test names (`HTTP/web.surl.test`, `smtp/mail.surl.test` and the like), so a
pinned upstream curl's SSPI can get a real Kerberos ticket for `surl --keytab` to accept.

- `KerberosTestKdc.Answer` answers one AS-REQ or TGS-REQ (RFC 4120 sections 3.1 and 3.3): an
  AS-REQ without `PA-ENC-TIMESTAMP` gets `KDC_ERR_PREAUTH_REQUIRED` with `PA-ETYPE-INFO2`
  naming the default salt; tickets are AES (enctypes 17, 18, 19, 20) in the client's order;
  `rc4-hmac` is never chosen. Every refusal is a `KRB-ERROR`, never an exception.
- `KerberosTestKdc.WriteServiceKeytab` writes the service keys as an MIT keytab, which
  `KerberosKeytab.Read` (and so `surl --keytab`) reads.
- `KerberosTestKdcServer` carries it over UDP (`IDatagramFlow`) and TCP (`IConnection`) with
  RFC 4120 section 7.2 framing; requests over 64 KiB are refused `KRB_ERR_FIELD_TOOLONG`,
  datagram answers over 4096 bytes become `KRB_ERR_RESPONSE_TOO_BIG`.

Rules:
- Only test projects and the `Run-KerberosTestKdc.cs` file-based app (BL-267) reference it;
  `Surl.Console` never does, and it has no option and no `--aihelp` topic.
- It references `Surl.Kerberos.UnitLibrary` (whose internals it uses through
  `InternalsVisibleTo`: the enctype profiles and the DER readers) and
  `Surl.Protocol.Abstractions.UnitLibrary` for the transport seam, nothing else. It never
  constructs a socket; the listeners are injected.
- Keys and confounders come from an injected `IKerberosRandomSource`, time from an injected
  `TimeProvider`, so tests fix every byte.
- Simplifications a fixture can afford, each stated where it is made: a TGS-REQ's
  authenticator checksum and time are not checked, no PAC is issued, and there is one
  key version (1).
