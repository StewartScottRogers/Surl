# Surl.Tls13Stream.UnitLibrary

`Tls13ServerStream.AcceptAsync` runs the server side of TLS 1.3 over any `Stream` and returns a
`Stream` of the application data (ADR-0157 decision 2). It drives `Surl.Tls13`'s
`TlsServerHandshake` and its record layer (`TlsRecordProtector`, `TlsRecordUnprotector`); it
references only `Surl.Tls13.UnitLibrary`.

- `Tls13Connection` is the record layer: plaintext records until traffic secrets are installed,
  protected after, writes chunked to 2^14 bytes under one lock.
- `Tls13ServerHandshakeRunner` feeds records to the handshake, writes its output (ServerHello in
  plaintext, the rest under the handshake keys), sends one compatibility `change_cipher_spec`
  when the ClientHello carried a legacy session id, and switches to the application keys once the
  client's Finished is accepted.
- `alreadyRead` replays bytes the caller read before handing the transport over, starting at a
  record header.
- After the handshake: `close_notify` ends the stream and is answered; `KeyUpdate` is honoured
  (and answered when it asks); `user_canceled` is ignored; any other alert is
  `Tls13PeerAlertException`; a faulty record sends its fatal alert and throws
  `TlsAlertException`; a plaintext record is refused. No tickets, no 0-RTT.
- Failures are reported by awaiting the failed task again after the alert is written
  (`WriteFatalAlertOnFailureAsync`), not by `throw;`, so the coverage collector sees the rethrow.
- Client certificates (BL-1466): options with `TrustsClientCertificateChain` set make
  `AcceptAsync` request and verify one (the handshake is `Surl.Tls13`'s, BL-600); the verified DER
  chain is `ClientCertificateChain`. A missing, untrusted or wrongly signed certificate sends
  `certificate_required`, `bad_certificate` or `decrypt_error` under the handshake keys and throws.
  `Surl.Networking` does not yet set the delegate (BL-1467).
