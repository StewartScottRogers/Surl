# Surl.Networking.UnitLibrary

Phase 1.

The production transports behind the seams in `Surl.Protocol.Abstractions`: accepting TCP
connections and UDP datagrams on the addresses a listen URL names; server-side TLS through
`SslStream` (certificates and keys, client-certificate verification for upstream curl's
`--cert`, ALPN); and the proxy servers upstream curl's proxy options talk to - HTTP
`CONNECT`, HTTPS proxy, SOCKS4, SOCKS4a, SOCKS5 and SOCKS5h.

This is the only project that constructs `Socket`, `TcpListener`, `UdpClient` or
`SslStream`. Keep that code as thin as it can be, so that everything above it stays
testable without a network. It references `Surl.Protocol.Abstractions.UnitLibrary` and no
protocol server.
